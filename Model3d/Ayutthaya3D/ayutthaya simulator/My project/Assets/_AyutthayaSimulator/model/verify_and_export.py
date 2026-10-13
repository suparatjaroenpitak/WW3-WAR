import bpy

obs = [o for o in bpy.data.objects if o.name.startswith("RH_")]
cols = sorted(set(c.name for o in obs for c in o.users_collection))
print("RH_OBJS:", len(obs))
print("COLS:", cols)
xs = [o.location.x for o in obs]
ys = [o.location.y for o in obs]
zs = [o.location.z for o in obs]
print("BOUNDS x", round(min(xs), 2), round(max(xs), 2),
      "y", round(min(ys), 2), round(max(ys), 2),
      "z", round(min(zs), 2), round(max(zs), 2))
mats = sorted(set(mm.name for o in obs for mm in o.data.materials)) if obs else []
print("MATS:", len(mats), mats[:16])

try:
    for o in bpy.data.objects:
        o.select_set(False)
    for o in obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    bpy.ops.export_scene.fbx(
        filepath="__FBX__",
        use_selection=True,
        apply_unit_scale=True,
        bake_space_transform=True,
    )
    print("EXPORT_OK: __FBX__")
except Exception as e:
    print("EXPORT_FAIL:", type(e).__name__, str(e)[:300])
