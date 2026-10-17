"""Render each hull class on its own (three-quarter front, from above), after build.py has run in this session."""
import bpy
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read()) if "show" not in globals() else None
ANG = globals().get("ANG", ((1.7, -1.9, 0.85), (0.0, 0.1, 0.0)))
for k in range(9):
    show(f"Ship{k}")
    c = bpy.data.collections[f"Ship{k}"]
    rt = next(o for o in c.objects if o.parent is None)
    rt.location = (0, 0, 0); rt.scale = (1, 1, 1); rt.rotation_euler = (0, 0, 0)
    OUT = f"ships/class{k}"; CAM = ANG; LENS = 40; RES = (900, 600); EXPOSURE = 0.6
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/render.py").read())
