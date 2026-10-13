"""Build the two-story reference house (runs INSIDE Blender via MCP execute_code).

Reference: cream 2-storey block, brown plinth, red-brown gable roof with
white eaves + orange corbels, 5 window bays/floor with brown frames and
white louvered shutters, exterior side staircase with balustrade,
ventilation grilles, tiled entrance pad.
Units: meters. Re-runnable (clears previous RH_ objects first).
"""
import bpy
import math

PRE = "RH_"
COL = "Ref_House"
W, D = 12.0, 7.0          # footprint
FLOOR_H = 3.0             # per-floor height

# ---------------------------------------------------------------- materials
def get_mat(name, rgb, rough=0.85, metallic=0.0):
    m = bpy.data.materials.get(PRE + name)
    if m is None:
        m = bpy.data.materials.new(PRE + name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
            if "Roughness" in bsdf.inputs:
                bsdf.inputs["Roughness"].default_value = rough
            if "Metallic" in bsdf.inputs:
                bsdf.inputs["Metallic"].default_value = metallic
    return m


M_WALL = get_mat("WallCream", (0.93, 0.87, 0.74), 0.9)
M_BASE = get_mat("PlinthBrown", (0.36, 0.26, 0.17), 0.95)
M_ROOF = get_mat("RoofRed", (0.52, 0.23, 0.13), 0.8)
M_WHITE = get_mat("TrimWhite", (0.94, 0.94, 0.92), 0.7)
M_FRAME = get_mat("FrameWood", (0.40, 0.21, 0.10), 0.7)
M_GLASS = get_mat("GlassDark", (0.09, 0.11, 0.14), 0.25, 0.6)
M_ORANGE = get_mat("CorbelOrange", (0.88, 0.52, 0.14), 0.75)
M_CONC = get_mat("Concrete", (0.62, 0.61, 0.58), 0.95)
M_DOOR = get_mat("DoorBrown", (0.32, 0.18, 0.09), 0.65)
M_TILE = get_mat("TileOrange", (0.80, 0.42, 0.16), 0.9)
M_TILE2 = get_mat("TileGrey", (0.45, 0.45, 0.46), 0.9)
M_GROUND = get_mat("Ground", (0.55, 0.55, 0.55), 1.0)

# ---------------------------------------------------------------- collection
for o in list(bpy.data.objects):
    if o.name.startswith(PRE):
        bpy.data.objects.remove(o, do_unlink=True)
for c in list(bpy.data.collections):
    if c.name == COL and not c.objects:
        bpy.data.collections.remove(c)
col = bpy.data.collections.get(COL)
if col is None:
    col = bpy.data.collections.new(COL)
    bpy.context.scene.collection.children.link(col)

CUBE_VERTS = [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
              (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]
CUBE_FACES = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1),
              (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]


def box(name, loc, dims, material, rot=None):
    dx, dy, dz = dims[0] / 2.0, dims[1] / 2.0, dims[2] / 2.0
    verts = [(x * dx, y * dy, z * dz) for (x, y, z) in
             [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
              (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]]
    me = bpy.data.meshes.new(PRE + name + "_mesh")
    me.from_pydata(verts, [], CUBE_FACES)
    me.update()
    ob = bpy.data.objects.new(PRE + name, me)
    ob.location = loc
    if rot:
        ob.rotation_euler = rot
    if material is not None:
        ob.data.materials.append(material)
    col.objects.link(ob)
    return ob


def put(face, u, v, z, dims_uv, material):
    """Place a box on a wall face. face in +y/-y/+x/-x; u = along wall."""
    du, dv, dz = dims_uv
    if face == "+y":
        box("p", (u, D / 2 + v, z), (du, dv, dz), material)
    elif face == "-y":
        box("p", (u, -D / 2 - v, z), (du, dv, dz), material)
    elif face == "+x":
        box("p", (W / 2 + v, u, z), (dv, du, dz), material)
    else:
        box("p", (-W / 2 - v, u, z), (dv, du, dz), material)


def add_window(u, zc, face, shutters=True):
    put(face, u, 0.02, zc, (1.2, 0.10, 1.6), M_GLASS)          # glass
    put(face, u - 0.66, 0.05, zc, (0.12, 0.14, 1.72), M_FRAME)  # frame L/R
    put(face, u + 0.66, 0.05, zc, (0.12, 0.14, 1.72), M_FRAME)
    put(face, u, 0.05, zc + 0.86, (1.44, 0.14, 0.12), M_FRAME)  # frame T/B
    put(face, u, 0.05, zc - 0.86, (1.44, 0.14, 0.12), M_FRAME)
    put(face, u, 0.04, zc, (0.07, 0.10, 1.6), M_FRAME)          # mullions
    put(face, u, 0.04, zc, (1.2, 0.10, 0.07), M_FRAME)
    put(face, u, 0.10, zc - 1.0, (1.56, 0.24, 0.12), M_WHITE)   # sill
    if shutters:
        for s in (-1, 1):
            su = u + s * 1.03
            put(face, su, 0.06, zc, (0.62, 0.07, 1.66), M_WHITE)
            for k in range(3):
                put(face, su, 0.115, zc - 0.55 + k * 0.55,
                    (0.56, 0.04, 0.12), M_WHITE)


def add_door(u, zc, face):
    put(face, u - 0.68, 0.05, zc, (0.14, 0.16, 2.5), M_FRAME)
    put(face, u + 0.68, 0.05, zc, (0.14, 0.16, 2.5), M_FRAME)
    put(face, u, 0.05, zc + 1.28, (1.5, 0.16, 0.14), M_FRAME)
    put(face, u - 0.30, 0.03, zc, (0.58, 0.10, 2.3), M_DOOR)
    put(face, u + 0.30, 0.03, zc, (0.58, 0.10, 2.3), M_DOOR)


# ---------------------------------------------------------------- shell
box("plinth", (0, 0, 0.3), (W + 0.4, D + 0.4, 0.6), M_BASE)
box("floor1", (0, 0, 1.5), (W, D, 3.0), M_WALL)
box("band_mid", (0, 0, 3.0), (W + 0.3, D + 0.3, 0.25), M_WHITE)
box("floor2", (0, 0, 4.5), (W, D, 3.0), M_WALL)
box("band_top", (0, 0, 6.0), (W + 0.3, D + 0.3, 0.25), M_WHITE)

# ---------------------------------------------------------------- roof (gable, ridge along X)
RL, RS, RH = W + 1.6, D + 1.6, 2.2
zb = 6.0
A = (-RL / 2, -RS / 2, zb); B = (RL / 2, -RS / 2, zb)
C = (RL / 2, RS / 2, zb); Dd = (-RL / 2, RS / 2, zb)
E = (-RL / 2, 0, zb + RH); F = (RL / 2, 0, zb + RH)
me = bpy.data.meshes.new(PRE + "roof_mesh")
me.from_pydata([A, B, C, Dd, E, F], [],
               [(0, 1, 5, 4), (3, 4, 5, 2), (0, 4, 3), (1, 2, 5),
                (0, 3, 2, 1)])
me.update()
roof = bpy.data.objects.new(PRE + "roof", me)
roof.data.materials.append(M_ROOF)
col.objects.link(roof)

# gable infill triangles (wall color)
for sx in (-1, 1):
    x = sx * W / 2
    gm = bpy.data.meshes.new(PRE + "gable_mesh")
    gm.from_pydata([(x, -D / 2, 6.0), (x, D / 2, 6.0), (x, 0, 6.0 + RH)],
                   [], [(0, 1, 2)])
    gm.update()
    go = bpy.data.objects.new(PRE + "gable", gm)
    go.data.materials.append(M_WALL)
    col.objects.link(go)

box("ridge", (0, 0, zb + RH + 0.05), (RL, 0.34, 0.16), M_ROOF)
for s in (-1, 1):  # eave fascia boards + corbels
    box("fascia", (0, s * (RS / 2 - 0.05), zb - 0.05), (RL, 0.12, 0.34), M_WHITE)
    x = -5.5
    while x <= 5.51:
        box("corbel", (x, s * (D / 2 + 0.15), zb - 0.18), (0.2, 0.25, 0.36), M_ORANGE)
        x += 1.0

# ---------------------------------------------------------------- windows & doors
BAYS = [-4.8, -2.4, 0.0, 2.4, 4.8]
for x in BAYS:                       # front floor2: 5 windows
    add_window(x, 4.9, "+y", True)
for x in BAYS:                       # front floor1: door center + 4 windows
    if abs(x) < 0.01:
        add_door(x, 1.15, "+y")
    else:
        add_window(x, 1.9, "+y", True)
for x in [-4.5, -1.5, 1.5, 4.5]:      # back: 4 + 4, no shutters
    add_window(x, 1.9, "-y", False)
    add_window(x, 4.9, "-y", False)
for zc in (1.9, 4.9):                # sides: 2 per floor, simple
    for uu in (-1.75, 1.75):
        add_window(uu, zc, "+x", False)
        add_window(uu, zc, "-x", False)
add_door(-2.0, 4.15, "-x")            # side door to stair landing (floor2)

# porch + steps + tiled pad (front door)
box("porch", (0, D / 2 + 0.7, 0.12), (3.2, 1.6, 0.24), M_CONC)
box("step1", (0, D / 2 + 1.7, 0.08), (2.6, 0.5, 0.16), M_CONC)
box("canopy", (0, D / 2 + 0.3, 2.55), (2.0, 0.7, 0.12), M_WHITE)
box("pad", (0, D / 2 + 3.2, 0.03), (4.4, 3.4, 0.06), M_TILE)
box("pad_edge1", (0, D / 2 + 1.55, 0.035), (4.4, 0.25, 0.07), M_TILE2)
box("pad_edge2", (0, D / 2 + 4.85, 0.035), (4.4, 0.25, 0.07), M_TILE2)

# ventilation grilles on plinth
for vx in (-4.5, -3.0, 3.0, 4.5):
    put("+y", vx, 0.21, 0.3, (0.62, 0.05, 0.42), M_WHITE)
    put("+y", vx, 0.24, 0.3, (0.5, 0.06, 0.3), M_GLASS)

# ---------------------------------------------------------------- side staircase (x = -W/2 side, front -> back, up to z=3)
ST_X = -W / 2 - 0.8
for i in range(10):
    yc = 4.2 - 0.6 * i
    zt = 0.3 * (i + 1)
    box("stair", (ST_X, yc, zt - 0.09), (1.4, 0.62, 0.18), M_CONC)
    box("baluster", (ST_X - 0.62, yc, zt + 0.47), (0.09, 0.09, 0.95), M_CONC)
box("stringer", (ST_X, 1.5, 1.5), (1.3, 7.2, 0.25), M_CONC,
    rot=(math.radians(-22.3), 0, 0))
box("handrail", (ST_X - 0.62, 1.5, 2.475 + 0.45), (0.12, 6.9, 0.14), M_WHITE,
    rot=(math.radians(-26.2), 0, 0))
box("landing", (ST_X, -2.0, 2.9), (1.5, 1.6, 0.2), M_CONC)
box("newel_b", (ST_X - 0.62, 4.6, 0.57), (0.16, 0.16, 1.15), M_CONC)
box("newel_t", (ST_X - 0.62, -1.6, 3.5), (0.16, 0.16, 1.15), M_CONC)

# ---------------------------------------------------------------- ground
box("ground", (0, 0, -0.06), (60, 60, 0.1), M_GROUND)

n = sum(1 for o in bpy.data.objects if o.name.startswith(PRE))
print("RH_DONE objects=", n)
