"""Build a higher-detail, explicitly estimated building pass for one GIS tile.

Run in a separate Blender process. The source demo is opened read-only and all
new deliverables go to a new versioned tile directory.
"""
import argparse
import hashlib
import json
import math
import random
import sys
from collections import Counter
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


MATERIALS = {
    "wall_cream": ((0.78, 0.72, 0.59), 0.82, 0.0),
    "wall_ivory": ((0.83, 0.80, 0.70), 0.84, 0.0),
    "wall_sand": ((0.72, 0.62, 0.47), 0.84, 0.0),
    "wall_sage": ((0.56, 0.65, 0.54), 0.85, 0.0),
    "wall_blue": ((0.55, 0.64, 0.68), 0.82, 0.0),
    "wall_pale": ((0.78, 0.73, 0.68), 0.85, 0.0),
    "wall_gable": ((0.72, 0.65, 0.52), 0.86, 0.0),
    "trim_light": ((0.88, 0.82, 0.68), 0.72, 0.0),
    "trim_dark": ((0.25, 0.19, 0.14), 0.7, 0.0),
    "concrete": ((0.43, 0.43, 0.39), 0.9, 0.0),
    "wood": ((0.28, 0.15, 0.075), 0.65, 0.0),
    "door_teal": ((0.10, 0.25, 0.26), 0.5, 0.0),
    "glass_blue": ((0.12, 0.28, 0.32), 0.2, 0.08),
    "glass_green": ((0.15, 0.30, 0.26), 0.24, 0.05),
    "roof_terra_0": ((0.40, 0.13, 0.065), 0.83, 0.0),
    "roof_terra_1": ((0.48, 0.18, 0.09), 0.82, 0.0),
    "roof_terra_2": ((0.34, 0.105, 0.055), 0.86, 0.0),
    "roof_terra_3": ((0.44, 0.15, 0.07), 0.84, 0.0),
    "roof_ridge": ((0.30, 0.085, 0.045), 0.78, 0.0),
    "roof_metal_0": ((0.24, 0.31, 0.32), 0.48, 0.22),
    "roof_metal_1": ((0.34, 0.39, 0.38), 0.5, 0.18),
    "roof_metal_2": ((0.20, 0.26, 0.28), 0.45, 0.24),
    "roof_flat": ((0.37, 0.38, 0.35), 0.9, 0.0),
    "awning": ((0.48, 0.22, 0.12), 0.7, 0.0),
    "hvac": ((0.55, 0.57, 0.53), 0.66, 0.12),
}

WALL_PALETTE = ["wall_cream", "wall_ivory", "wall_sand", "wall_sage", "wall_blue", "wall_pale"]
TERRA = ["roof_terra_0", "roof_terra_1", "roof_terra_2", "roof_terra_3"]
METAL = ["roof_metal_0", "roof_metal_1", "roof_metal_2"]


def seeded_rng(osm_id):
    value = int(hashlib.sha256(str(osm_id).encode("utf-8")).hexdigest()[:8], 16)
    return random.Random(value)


def make_material(name, color, roughness, metallic):
    mat = bpy.data.materials.new("AYU2_" + name)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    mat.diffuse_color = (*color, 1.0)
    return mat


class MeshBuilder:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.material_ids = []

    def face(self, points, material_id):
        if len(points) < 3:
            return
        vs = [Vector(p) for p in points]
        if (vs[1] - vs[0]).cross(vs[2] - vs[0]).length < 1e-7:
            return
        start = len(self.vertices)
        self.vertices.extend([tuple(v) for v in vs])
        self.faces.append(tuple(range(start, start + len(vs))))
        self.material_ids.append(material_id)

    def oriented_box(self, center, axis_u, axis_v, width, depth, height, material_id):
        c = Vector(center)
        u = Vector((axis_u[0], axis_u[1], 0.0)).normalized()
        v = Vector((axis_v[0], axis_v[1], 0.0)).normalized()
        z = Vector((0.0, 0.0, 1.0))
        corners = []
        for sz in (-1, 1):
            for sv in (-1, 1):
                for su in (-1, 1):
                    corners.append(c + u * (su * width * 0.5) + v * (sv * depth * 0.5) + z * (sz * height * 0.5))
        # order: bottom four, top four
        self.face([corners[i] for i in (0, 1, 3, 2)], material_id)
        self.face([corners[i] for i in (4, 6, 7, 5)], material_id)
        self.face([corners[i] for i in (0, 4, 5, 1)], material_id)
        self.face([corners[i] for i in (1, 5, 7, 3)], material_id)
        self.face([corners[i] for i in (3, 7, 6, 2)], material_id)
        self.face([corners[i] for i in (2, 6, 4, 0)], material_id)

    def wall_box(self, center, tangent, outward, width, height, depth, material_id):
        self.oriented_box(center, tangent, outward, width, depth, height, material_id)


def polygon_area(points):
    return abs(sum(points[i][0] * points[(i + 1) % len(points)][1] - points[(i + 1) % len(points)][0] * points[i][1]
                   for i in range(len(points))) * 0.5)


def parse_height(tags):
    raw = tags.get("height") or tags.get("building:height")
    if not raw:
        return None
    text = str(raw).strip().lower().replace(",", ".")
    try:
        if "ft" in text or "feet" in text or "'" in text:
            return float("".join(c for c in text if c.isdigit() or c in ".-")) * 0.3048
        return float("".join(c for c in text if c.isdigit() or c in ".-"))
    except ValueError:
        return None


def estimate_profile(spec, ring, rng):
    tags = spec["metadata"].get("tags", {})
    kind = str(tags.get("building", "yes")).lower()
    area = polygon_area(ring)
    explicit_levels = tags.get("building:levels") or tags.get("levels")
    explicit_height = parse_height(tags)
    shop = bool(tags.get("shop")) or kind in ("commercial", "retail", "office")
    hotel = bool(tags.get("tourism")) or kind in ("hotel", "apartments")
    industrial = kind in ("industrial", "warehouse", "factory")
    open_roof = kind == "roof"
    home = kind in ("detached", "house", "residential", "semidetached_house", "bungalow", "terrace")

    if explicit_levels:
        try:
            floors = max(1, min(5, int(float(str(explicit_levels).split(";")[0]))))
            height_source = "OSM building:levels"
        except ValueError:
            floors = 1
            height_source = "estimated from OSM type and footprint"
    elif explicit_height:
        floors = max(1, min(5, round(explicit_height / 3.1)) )
        height_source = "OSM height"
    else:
        height_source = "procedural estimate; OSM has no height/levels"
        if open_roof:
            floors = 0
        elif hotel or shop:
            floors = 3 if area > 180 and rng.random() < 0.55 else 2
        elif industrial:
            floors = 1
        elif home:
            floors = 2 if rng.random() < 0.22 and area > 55 else 1
        else:
            floors = 3 if area > 260 else (2 if area > 95 or rng.random() < 0.18 else 1)

    if explicit_height and not explicit_levels:
        wall_height = max(2.7, explicit_height - 1.3)
    elif industrial:
        wall_height = max(4.3, floors * 3.1)
    else:
        wall_height = max(2.7, floors * (3.15 if floors <= 2 else 3.0))

    if open_roof:
        roof_style = "open_gable_canopy"
        wall_height = 2.8
    elif industrial:
        roof_style = "corrugated_gable"
    elif shop or hotel:
        roof_style = "flat_parapet" if rng.random() < 0.74 else "terracotta_gable"
    elif home:
        roof_style = "terracotta_gable"
    else:
        roof_style = "terracotta_gable" if rng.random() < 0.66 else "flat_parapet"

    if explicit_height or explicit_levels:
        height_status = "OSM tagged measurement/levels; simplified geometry"
    else:
        height_status = "estimated by OSM building type, footprint and seeded variation"

    return {
        "tags": tags,
        "kind": kind,
        "area_m2": area,
        "floors": floors,
        "wall_height_m": wall_height,
        "roof_style": roof_style,
        "height_source": height_source,
        "height_status": height_status,
        "storefront": shop,
        "open_roof": open_roof,
        "industrial": industrial,
        "home": home,
    }


def make_window(builder, center, tangent, outward, width, height, ids, balcony=False):
    c = Vector(center)
    u = Vector((tangent[0], tangent[1], 0.0)).normalized()
    n = Vector((outward[0], outward[1], 0.0)).normalized()
    builder.wall_box(c + n * 0.035, u, n, width + 0.18, height + 0.18, 0.075, ids["trim_dark"])
    builder.wall_box(c + n * 0.085, u, n, width - 0.14, height - 0.14, 0.025, ids["glass"])
    frame = ids["trim_light"]
    bar = 0.075
    builder.wall_box(c + n * 0.105 + u * (-(width * 0.5 - bar * 0.5)), u, n, bar, height, 0.055, frame)
    builder.wall_box(c + n * 0.105 + u * ((width * 0.5 - bar * 0.5)), u, n, bar, height, 0.055, frame)
    builder.wall_box(c + n * 0.105 + Vector((0, 0, height * 0.5 - bar * 0.5)), u, n, width, bar, 0.055, frame)
    builder.wall_box(c + n * 0.105 - Vector((0, 0, height * 0.5 - bar * 0.5)), u, n, width, bar, 0.055, frame)
    # A simple centre mullion adds depth without pretending to be a photo texture.
    builder.wall_box(c + n * 0.11, u, n, 0.045, height - 0.12, 0.045, frame)
    builder.wall_box(c + n * 0.12 - Vector((0, 0, height * 0.08)), u, n, width - 0.12, 0.04, 0.04, frame)
    sill = c - Vector((0, 0, height * 0.5 + 0.05)) + n * 0.16
    builder.wall_box(sill, u, n, width + 0.25, 0.10, 0.28, ids["trim_light"])
    return c


def add_door(builder, center, tangent, outward, ids, storefront):
    c = Vector(center)
    u = Vector((tangent[0], tangent[1], 0.0)).normalized()
    n = Vector((outward[0], outward[1], 0.0)).normalized()
    w, h = (1.25, 2.15) if storefront else (0.96, 2.05)
    builder.wall_box(c + n * 0.045, u, n, w + 0.18, h + 0.10, 0.09, ids["trim_light"])
    builder.wall_box(c + n * 0.10, u, n, w, h, 0.055, ids["wood"] if not storefront else ids["door_teal"])
    builder.wall_box(c + u * (w * 0.27) + n * 0.14, u, n, 0.045, h * 0.76, 0.035, ids["trim_dark"])
    knob = c + u * (w * 0.36) + n * 0.17 - Vector((0, 0, 0.05))
    builder.wall_box(knob, u, n, 0.045, 0.055, 0.045, ids["trim_light"])
    if storefront:
        canopy = c + Vector((0, 0, h * 0.64)) + n * 0.38
        builder.wall_box(canopy, u, n, w + 0.6, 0.10, 0.72, ids["awning"])


def add_balcony(builder, center, tangent, outward, width, ids):
    c = Vector(center)
    u = Vector((tangent[0], tangent[1], 0.0)).normalized()
    n = Vector((outward[0], outward[1], 0.0)).normalized()
    slab = c + n * 0.30 - Vector((0, 0, 0.12))
    builder.wall_box(slab, u, n, width, 0.18, 0.62, ids["concrete"])
    rail_z = c.z + 0.40
    rail_c = c + n * 0.60 + Vector((0, 0, 0.06))
    builder.wall_box(rail_c, u, n, width, 0.08, 0.08, ids["trim_dark"])
    for side in (-1, 1):
        post = rail_c + u * (side * width * 0.48) - Vector((0, 0, 0.42))
        builder.wall_box(post, u, n, 0.075, 0.86, 0.075, ids["trim_dark"])
    for i in range(1, 5):
        post = rail_c + u * ((i / 5.0 - 0.5) * width) - Vector((0, 0, 0.42))
        builder.wall_box(post, u, n, 0.045, 0.78, 0.045, ids["trim_dark"])


def make_roof_box(builder, spec, bottom_ring, profile, rng, ids):
    style = profile["roof_style"]
    if style == "flat_parapet":
        add_flat_roof(builder, spec, bottom_ring, profile, ids, rng)
        return style

    # A conservative oriented roof envelope. Complex or clipped outlines get a
    # flat/parapet roof so a rectangular procedural cap does not invent a wing.
    edges = []
    for i, p in enumerate(bottom_ring):
        q = bottom_ring[(i + 1) % len(bottom_ring)]
        dx, dy = q[0] - p[0], q[1] - p[1]
        length = math.hypot(dx, dy)
        if length > 0.2:
            edges.append((length, (dx / length, dy / length)))
    if not edges:
        add_flat_roof(builder, spec, bottom_ring, profile, ids, rng)
        return "flat_parapet_fallback"
    _, u = max(edges, key=lambda x: x[0])
    v = (-u[1], u[0])
    pu = [p[0] * u[0] + p[1] * u[1] for p in bottom_ring]
    pv = [p[0] * v[0] + p[1] * v[1] for p in bottom_ring]
    u0, u1, v0, v1 = min(pu), max(pu), min(pv), max(pv)
    footprint_area = max(profile["area_m2"], 0.01)
    obb_ratio = ((u1 - u0) * (v1 - v0)) / footprint_area
    if (u1 - u0) < 2.0 or (v1 - v0) < 1.5 or obb_ratio > 1.42:
        add_flat_roof(builder, spec, bottom_ring, profile, ids, rng)
        return "flat_parapet_fallback_irregular"

    clipped = bool(spec["metadata"].get("clipped_at_tile_boundary"))
    overhang = 0.02 if clipped else min(0.20, (v1 - v0) * 0.045)
    u0 -= 0.02 if clipped else 0.10
    u1 += 0.02 if clipped else 0.10
    v0 -= overhang
    v1 += overhang
    vm = (v0 + v1) * 0.5

    def xy(a, b):
        return (u[0] * a + v[0] * b, u[1] * a + v[1] * b)

    def ground_at(x, y):
        return min(bottom_ring, key=lambda p: (p[0] - x) ** 2 + (p[1] - y) ** 2)[2]

    eave = {}
    for a, b in ((u0, v0), (u1, v0), (u1, v1), (u0, v1)):
        x, y = xy(a, b)
        eave[(a, b)] = (x, y, ground_at(x, y) + profile["wall_height_m"])
    ridge_rise = max(0.85, min(2.0, (v1 - v0) * (0.35 if profile["home"] else 0.25)))
    r0xy, r1xy = xy(u0, vm), xy(u1, vm)
    ridge0 = (r0xy[0], r0xy[1], (eave[(u0, v0)][2] + eave[(u0, v1)][2]) * 0.5 + ridge_rise)
    ridge1 = (r1xy[0], r1xy[1], (eave[(u1, v0)][2] + eave[(u1, v1)][2]) * 0.5 + ridge_rise)

    low0, low1 = eave[(u0, v0)], eave[(u1, v0)]
    high0, high1 = eave[(u0, v1)], eave[(u1, v1)]
    tile_names = TERRA if style == "terracotta_gable" else METAL
    roof_base = ids[tile_names[-1]]
    builder.face([low0, low1, ridge1, ridge0], roof_base)
    builder.face([high1, high0, ridge0, ridge1], roof_base)
    wall_id = ids[rng.choice(WALL_PALETTE)]
    builder.face([low0, high0, ridge0], wall_id)
    builder.face([high1, low1, ridge1], wall_id)

    length = u1 - u0
    slope = math.hypot((v1 - v0) * 0.5, ridge_rise)
    nu = max(2, min(18, int(math.ceil(length / 1.1))))
    nv = max(2, min(9, int(math.ceil(slope / 0.62))))
    for side, e0, e1, r0, r1 in ((0, low0, low1, ridge0, ridge1), (1, high1, high0, ridge1, ridge0)):
        for row in range(nv):
            ta, tb = row / nv + 0.018, (row + 1) / nv - 0.018
            for col in range(nu):
                ua = u0 + length * col / nu + 0.018
                ub = u0 + length * (col + 1) / nu - 0.018
                if ub - ua < 0.03 or tb - ta < 0.03:
                    continue
                def point(t, a):
                    # Interpolate across the roof from eave to ridge, with a
                    # small lift so the tile courses read in the close-up.
                    lo = Vector(e0) * (1 - a) + Vector(e1) * a
                    hi = Vector(r0) * (1 - a) + Vector(r1) * a
                    p = lo * (1 - t) + hi * t
                    p.z += 0.012
                    return tuple(p)
                a0, a1 = (ua - u0) / length, (ub - u0) / length
                material = ids[tile_names[(row + col + side) % len(tile_names)]]
                builder.face([point(ta, a0), point(ta, a1), point(tb, a1), point(tb, a0)], material)

    trim_id = ids["roof_ridge"] if style == "terracotta_gable" else ids["trim_dark"]
    ridge_mid = ((ridge0[0] + ridge1[0]) * 0.5, (ridge0[1] + ridge1[1]) * 0.5, (ridge0[2] + ridge1[2]) * 0.5 + 0.04)
    builder.oriented_box(ridge_mid, u, v, length, 0.13, 0.12, trim_id)
    for side_b in (v0, v1):
        p0, p1 = xy(u0, side_b), xy(u1, side_b)
        mid = ((p0[0] + p1[0]) * 0.5, (p0[1] + p1[1]) * 0.5,
               (ground_at(*p0) + ground_at(*p1)) * 0.5 + profile["wall_height_m"] - 0.03)
        builder.oriented_box(mid, u, v, length, 0.11, 0.12, ids["trim_dark"])

    if profile["open_roof"]:
        for p in bottom_ring:
            builder.oriented_box((p[0], p[1], p[2] + profile["wall_height_m"] * 0.5), u, v, 0.20, 0.20,
                                 profile["wall_height_m"], ids["wood"])
    return style


def add_flat_roof(builder, spec, bottom_ring, profile, ids, rng):
    wall_count = int(spec.get("wall_faces", 0))
    verts = spec["vertices"]
    ground = sum(p[2] for p in bottom_ring) / len(bottom_ring)
    deck_z = ground + profile["wall_height_m"] + 0.08
    top_faces = []
    for face in spec["faces"][wall_count:]:
        if sum(verts[i][2] for i in face) / len(face) > ground + 0.5:
            top_faces.append(face)
    if top_faces:
        for face in top_faces:
            builder.face([(verts[i][0], verts[i][1], deck_z) for i in face], ids["roof_flat"])
    else:
        builder.face([(p[0], p[1], deck_z) for p in bottom_ring], ids["roof_flat"])

    # Low parapet, cap and occasional rooftop plant: details are visual estimates.
    for i, p0 in enumerate(bottom_ring):
        p1 = bottom_ring[(i + 1) % len(bottom_ring)]
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        length = math.hypot(dx, dy)
        if length < 0.5:
            continue
        tangent = (dx / length, dy / length)
        outward = (tangent[1], -tangent[0])
        center = ((p0[0] + p1[0]) * 0.5, (p0[1] + p1[1]) * 0.5, deck_z + 0.30)
        builder.wall_box(center, tangent, outward, length, 0.62, 0.14, ids["concrete"])
        cap = (center[0], center[1], deck_z + 0.63)
        builder.wall_box(cap, tangent, outward, length, 0.08, 0.20, ids["trim_light"])

    if profile["area_m2"] > 55:
        u = (1.0, 0.0)
        v = (0.0, 1.0)
        cx = sum(p[0] for p in bottom_ring) / len(bottom_ring)
        cy = sum(p[1] for p in bottom_ring) / len(bottom_ring)
        count = 1 + (1 if profile["area_m2"] > 150 and rng.random() < 0.5 else 0)
        for i in range(count):
            x = cx + (i - (count - 1) / 2) * 1.7
            z = deck_z + 0.72
            builder.oriented_box((x, cy, z), u, v, 1.05, 0.82, 0.62, ids["hvac"])
            builder.oriented_box((x, cy - 0.44, z + 0.02), u, v, 1.12, 0.04, 0.08, ids["trim_dark"])


def uv_project(mesh):
    layer = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        n = poly.normal
        for li in poly.loop_indices:
            v = mesh.vertices[mesh.loops[li].vertex_index].co
            if abs(n.z) >= abs(n.x) and abs(n.z) >= abs(n.y):
                uv = (v.x * 0.65, v.y * 0.65)
            elif abs(n.x) >= abs(n.y):
                uv = (v.y * 0.65, v.z * 0.65)
            else:
                uv = (v.x * 0.65, v.z * 0.65)
            layer.data[li].uv = uv


def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--source", default="tiles/AYU_DEMO_E0_N0/demo.blend")
    parser.add_argument("--output", default="tiles/AYU_DEMO_E0_N0_REALISTIC_V2")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    root = Path(args.root).resolve()
    source_blend = root / args.source
    output = root / args.output
    if output.exists() and any(output.iterdir()):
        raise RuntimeError(f"Refusing to overwrite existing output directory: {output}")
    output.mkdir(parents=True, exist_ok=True)
    (root / "previews").mkdir(exist_ok=True)

    recipe = json.loads((root / "data/processed/mesh_recipe.json").read_text(encoding="utf-8"))
    tile_meta = json.loads((root / "tiles/AYU_DEMO_E0_N0/tile.json").read_text(encoding="utf-8"))
    bpy.ops.wm.open_mainfile(filepath=str(source_blend))
    scene = bpy.context.scene
    scene.render.threads_mode = "FIXED"
    scene.render.threads = 2
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    old_buildings = bpy.data.collections.get("Buildings")
    if old_buildings is None:
        raise RuntimeError("Source BLEND is missing its Buildings collection")
    collision_col = bpy.data.collections.get("Buildings_Collision_Estimated")
    if collision_col is None:
        collision_col = bpy.data.collections.new("Buildings_Collision_Estimated")
        scene.collection.children.link(collision_col)
    detailed_col = bpy.data.collections.get("Buildings_Procedural_Detail_V2")
    if detailed_col is None:
        detailed_col = bpy.data.collections.new("Buildings_Procedural_Detail_V2")
        scene.collection.children.link(detailed_col)

    # Keep the previous simple GIS prisms as hidden, explicitly estimated
    # collision candidates. They remain in the BLEND but are not sent to GLB.
    for obj in list(old_buildings.objects):
        old_buildings.objects.unlink(obj)
        collision_col.objects.link(obj)
        obj.name = obj.name + "_collision_estimated"
        obj.hide_render = True
        obj.hide_set(True)
        obj.display_type = "WIRE"
        obj["role"] = "coarse collision candidate; estimated source height"
        obj["source"] = "OSM footprint; original massing from Phase 1-4"

    mats = {name: make_material(name, *values) for name, values in MATERIALS.items()}
    mat_names = list(MATERIALS)
    mat_ids = {name: i for i, name in enumerate(mat_names)}
    new_objects = []
    style_counts = Counter()
    category_counts = Counter()
    window_count = 0
    door_count = 0
    balcony_count = 0
    estimate_count = 0
    source_height_count = 0
    detail_stats = []

    for spec in (m for m in recipe["meshes"] if m["category"] == "buildings"):
        metadata = spec["metadata"]
        osm_id = metadata.get("osm_id", spec["name"])
        rng = seeded_rng(osm_id)
        wall_count = int(spec.get("wall_faces", 0))
        if wall_count < 3:
            raise RuntimeError(f"Building lacks ordered wall faces: {spec['name']}")
        src_vertices = spec["vertices"]
        wall_faces = spec["faces"][:wall_count]
        bottom_ring = [tuple(src_vertices[face[0]]) for face in wall_faces]
        profile = estimate_profile(spec, [(p[0], p[1]) for p in bottom_ring], rng)
        wall_mat = rng.choice(WALL_PALETTE)
        builder = MeshBuilder()
        tags = profile["tags"]

        # Solid GIS footprint extrusion. Only the elevation is procedural when
        # OSM has no height/levels; XY follows the downloaded footprint exactly.
        if not profile["open_roof"]:
            for face in wall_faces:
                b0, b1 = Vector(src_vertices[face[0]]), Vector(src_vertices[face[1]])
                top0 = Vector((b0.x, b0.y, b0.z + profile["wall_height_m"]))
                top1 = Vector((b1.x, b1.y, b1.z + profile["wall_height_m"]))
                builder.face([b0, b1, top1, top0], mat_ids[wall_mat])
                # Durable raised plinth, common on low-rise urban buildings.
                p0 = b0 + Vector((0, 0, min(0.34, profile["wall_height_m"] * 0.13)))
                p1 = b1 + Vector((0, 0, min(0.34, profile["wall_height_m"] * 0.13)))
                builder.face([b0, b1, p1, p0], mat_ids["concrete"])

        # Facade windows, sills, mullions and one street-facing door. All are
        # surface-mounted low-poly details, not claims of surveyed interiors.
        edges = []
        signed_area = sum(bottom_ring[i][0] * bottom_ring[(i + 1) % len(bottom_ring)][1] -
                         bottom_ring[(i + 1) % len(bottom_ring)][0] * bottom_ring[i][1]
                         for i in range(len(bottom_ring))) * 0.5
        for i, face in enumerate(wall_faces):
            b0 = Vector(src_vertices[face[0]])
            b1 = Vector(src_vertices[face[1]])
            d = b1 - b0
            length = math.hypot(d.x, d.y)
            if length < 1.0:
                continue
            tangent = (d.x / length, d.y / length)
            outward = (tangent[1], -tangent[0]) if signed_area > 0 else (-tangent[1], tangent[0])
            edges.append((length, i, b0, b1, tangent, outward))

        front_idx = max(edges, key=lambda item: item[0])[1] if edges else -1
        open_roof = profile["open_roof"]
        if not open_roof:
            for length, edge_idx, b0, b1, tangent, outward in edges:
                base_z = (b0.z + b1.z) * 0.5
                win_count_edge = max(1, min(6, int((length - 0.9) / (2.7 if profile["storefront"] else 3.2))))
                win_w = min(1.22 if profile["storefront"] else 1.05, length / (win_count_edge + 1) * 0.62)
                win_h = 1.28 if profile["floors"] >= 2 else 1.16
                floor_h = profile["wall_height_m"] / max(profile["floors"], 1)
                for floor in range(max(1, profile["floors"])):
                    zc = base_z + floor * floor_h + min(floor_h * 0.56, 1.65)
                    if zc + win_h * 0.5 > base_z + profile["wall_height_m"] - 0.22:
                        continue
                    for j in range(win_count_edge):
                        frac = (j + 1) / (win_count_edge + 1)
                        if edge_idx == front_idx and floor == 0 and abs(frac - 0.5) < 0.19:
                            continue
                        center = (b0.x + (b1.x - b0.x) * frac, b0.y + (b1.y - b0.y) * frac, zc)
                        make_window(builder, center, tangent, outward, win_w, win_h,
                                    {**mat_ids, "glass": mat_ids[rng.choice(("glass_blue", "glass_green"))]},
                                    balcony=False)
                        window_count += 1
                        if profile["storefront"] and floor == 0 and rng.random() < 0.26:
                            awn = Vector(center) + Vector((outward[0], outward[1], 0.0)) * 0.42 + Vector((0, 0, 0.72))
                            builder.wall_box(awn, tangent, outward, win_w + 0.4, 0.10, 0.72, mat_ids["awning"])

                if edge_idx == front_idx:
                    door_center = (b0.x + d.x * 0.5, b0.y + d.y * 0.5, base_z + 1.10)
                    add_door(builder, door_center, tangent, outward, mat_ids, profile["storefront"])
                    door_count += 1
                    if profile["storefront"] and profile["floors"] >= 2 and length > 5 and rng.random() < 0.42:
                        balcony_center = (b0.x + d.x * 0.5, b0.y + d.y * 0.5,
                                          base_z + min(profile["wall_height_m"] - 1.0, floor_h + 1.8))
                        add_balcony(builder, balcony_center, tangent, outward, min(2.1, length * 0.48), mat_ids)
                        balcony_count += 1

        actual_roof = make_roof_box(builder, spec, bottom_ring, profile, rng, mat_ids)
        style_counts[actual_roof] += 1
        category_counts["commercial" if profile["storefront"] else ("industrial" if profile["industrial"] else
                                                                      ("residential" if profile["home"] else "general"))] += 1
        estimate_count += profile["height_source"].startswith("procedural")
        source_height_count += not profile["height_source"].startswith("procedural")

        mesh = bpy.data.meshes.new(spec["name"] + "_realistic_v2_mesh")
        mesh.from_pydata(builder.vertices, [], builder.faces)
        mesh.update()
        if mesh.validate(verbose=False):
            raise RuntimeError("Blender repaired degenerate generated geometry: " + spec["name"])
        for name in mat_names:
            mesh.materials.append(mats[name])
        for poly, material_id in zip(mesh.polygons, builder.material_ids):
            poly.material_index = material_id
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        uv_project(mesh)
        obj = bpy.data.objects.new(spec["name"] + "_realistic_v2", mesh)
        detailed_col.objects.link(obj)
        obj["category"] = "buildings"
        obj["osm_id"] = osm_id
        obj["source_url"] = metadata.get("source_url", "")
        obj["source_license"] = metadata.get("license", "ODbL-1.0")
        obj["building_type_osm"] = profile["kind"]
        obj["building_area_m2"] = round(profile["area_m2"], 2)
        obj["estimated_floors"] = profile["floors"]
        obj["wall_height_m"] = round(profile["wall_height_m"], 2)
        obj["height_status"] = profile["height_status"]
        obj["height_source"] = profile["height_source"]
        obj["roof_style"] = actual_roof
        obj["facade_status"] = "procedural low-poly facade; no photographic reconstruction"
        obj["roof_status"] = "procedural roof form and tile/corrugation detail; not individually surveyed"
        obj["collision_candidate"] = "see hidden Buildings_Collision_Estimated collection; not exported to GLB"
        obj["clipped_at_tile_boundary"] = bool(metadata.get("clipped_at_tile_boundary"))
        obj["visual_confidence"] = "low-to-medium; footprint GIS-derived, architecture estimated"
        new_objects.append(obj)
        detail_stats.append({
            "osm_id": osm_id,
            "object": obj.name,
            "osm_building_type": profile["kind"],
            "roof_style": actual_roof,
            "floors_estimated": profile["floors"],
            "wall_height_estimated_m": round(profile["wall_height_m"], 2),
            "height_source": profile["height_source"],
            "clipped_at_tile_boundary": bool(metadata.get("clipped_at_tile_boundary")),
            "vertices": len(mesh.vertices),
            "polygons": len(mesh.polygons),
            "uv_maps": len(mesh.uv_layers),
        })

    # Keep a coarse hidden collision group for engine artists, but export only
    # visible GIS meshes and the detailed building render meshes.
    collision_col.hide_render = True
    collision_col.hide_viewport = True
    scene.name = "Ayutthaya_Demo_500m_Realistic_Buildings_V2"
    scene["building_visual_version"] = "procedural_facades_v2"
    scene["building_visual_note"] = "OSM footprint-derived; facades, roof styles and heights are procedural estimates"
    scene["source_attribution"] = "© OpenStreetMap contributors (ODbL); Copernicus GLO-30 DSM. See ATTRIBUTION.md"

    collision_pointers = {o.as_pointer() for o in collision_col.objects}
    export_objects = [o for o in scene.objects if o.type == "MESH" and o.as_pointer() not in collision_pointers]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in export_objects:
        obj.hide_set(False)
        obj.select_set(True)
    if export_objects:
        bpy.context.view_layer.objects.active = export_objects[0]

    glb_path = output / "demo.glb"
    bpy.ops.export_scene.gltf(filepath=str(glb_path), export_format="GLB", use_selection=True,
                              export_extras=True, export_yup=True, export_texcoords=True, export_normals=True)

    # Aerial preview camera for reference comparison.
    size = float(tile_meta["tile_size_m"])
    cam_data = bpy.data.cameras.new("Realistic_Buildings_Close_View")
    close_cam = bpy.data.objects.new("Realistic_Buildings_Close_View", cam_data)
    scene.collection.objects.link(close_cam)
    cx, cy = size * 0.5, size * 0.5
    close_cam.location = (cx + 145, cy - 175, 150)
    aim(close_cam, (cx, cy, 3.0))
    cam_data.type = "PERSP"
    cam_data.lens = 42
    cam_data.clip_end = 3000
    scene.camera = close_cam
    engine_ids = {item.identifier for item in scene.render.bl_rna.properties["engine"].enum_items}
    if "BLENDER_EEVEE_NEXT" in engine_ids:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1440
    scene.render.resolution_y = 960
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.threads_mode = "FIXED"
    scene.render.threads = 2
    scene.render.filepath = str(root / "previews/ayutthaya_buildings_closeup_v2.png")
    bpy.ops.render.render(write_still=True)
    close_preview = root / "previews/ayutthaya_buildings_closeup_v2.png"

    # Save a full-tile camera as a second preview; leave the close view active in BLEND.
    survey_cam = bpy.data.objects.get("SurveyCamera")
    full_preview = root / "previews/ayutthaya_realistic_v2.png"
    if survey_cam:
        scene.camera = survey_cam
        scene.render.resolution_x = 1280
        scene.render.resolution_y = 1024
        scene.render.filepath = str(full_preview)
        bpy.ops.render.render(write_still=True)
        scene.camera = close_cam

    out_blend = output / "demo.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(out_blend))

    all_meshes = [o for o in export_objects if o.type == "MESH"]
    triangle_count = sum(sum(len(poly.vertices) - 2 for poly in o.data.polygons) for o in all_meshes)
    report = {
        "status": "passed",
        "version": "realistic_buildings_v2",
        "source_tile": "AYU_DEMO_E0_N0",
        "tile_id": output.name,
        "tile_size_m": tile_meta["tile_size_m"],
        "coordinate_system": "WGS84 source; UTM 47N; Blender local east/north/up metres",
        "building_count": len(new_objects),
        "original_collision_candidates_hidden_in_blend": len(collision_col.objects),
        "exported_mesh_objects": len(all_meshes),
        "triangles": triangle_count,
        "windows": window_count,
        "doors": door_count,
        "balconies": balcony_count,
        "source_tagged_heights_or_levels": source_height_count,
        "procedurally_estimated_heights": estimate_count,
        "roof_styles": dict(style_counts),
        "building_use_classes": dict(category_counts),
        "external_textures": 0,
        "materials": sorted({slot.material.name for obj in new_objects for slot in obj.material_slots}),
        "source_license": "OpenStreetMap ODbL-1.0; attribution retained in object metadata and ATTRIBUTION.md",
        "visual_provenance": "Footprints and OSM tags are real-source; heights, facades, windows, doors, balconies and roofs are procedural estimates; no photo textures or historical reconstruction.",
        "limitations": [
            "Only one 500x500m GIS tile; not the whole Ayutthaya island or province.",
            "All 513 OSM buildings lack height/levels; heights and architectural details are estimates.",
            "No historical site has been reconstructed from this generic facade pass.",
            "Buildings_Collision_Estimated is hidden and excluded from GLB; engine colliders/LOD/streaming are not configured.",
            "Raster terrain remains Copernicus GLO-30 DSM, not bare-earth DTM."
        ],
        "assets": {
            "blend": str(out_blend.relative_to(root)).replace("\\", "/"),
            "glb": str(glb_path.relative_to(root)).replace("\\", "/"),
            "close_preview": str(close_preview.relative_to(root)).replace("\\", "/"),
            "full_preview": str(full_preview.relative_to(root)).replace("\\", "/") if full_preview.exists() else None,
        },
        "per_building": detail_stats,
    }
    (output / "building_realism_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    new_tile = dict(tile_meta)
    new_tile["tile_id"] = output.name
    new_tile["parent_tile_id"] = "AYU_DEMO_E0_N0"
    new_tile["building_visual_version"] = "procedural_facades_v2"
    new_tile["building_visual_status"] = "GIS footprints retained; architectural height and visible facade/roof details procedurally estimated"
    new_tile["building_detail_report"] = "building_realism_report.json"
    (output / "tile.json").write_text(json.dumps(new_tile, ensure_ascii=False, indent=2), encoding="utf-8")
    print("AYUTTHAYA_REALISTIC_V2_COMPLETE", json.dumps({k: report[k] for k in
          ("building_count", "exported_mesh_objects", "triangles", "windows", "doors", "balconies", "roof_styles", "assets")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
