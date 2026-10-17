"""
Hero renders framed automatically. Before exec set:
    HERO = [("Collection", "slug", (dx, dy, dz)), ...]     # view direction from the structure toward the camera
Renders assets/stations/renders/<slug>/hero_v2.png (or HERO_NAME) with the whole structure in frame.
"""
from mathutils import Vector
for _col, _slug, _dir in HERO:
    show(_col)
    c = bpy.data.collections[_col]
    pts = [o.matrix_world @ Vector(k) for o in c.all_objects if o.type == "MESH" for k in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    mid = (lo + hi) / 2
    rad = (hi - lo).length / 2
    d = Vector(_dir).normalized()
    OUT = f"{_slug}/{globals().get('HERO_NAME', 'hero_v2')}"
    CAM = (tuple(mid + d * rad * globals().get("HERO_DIST", 2.25)), tuple(mid))
    LENS = 35; RES = globals().get("HERO_RES", (1600, 900)); EXPOSURE = 0.6
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/render.py").read())
result = {"rendered": [h[1] for h in HERO]}
