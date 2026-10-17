"""Preview render for the station kit. Before exec, set:
    OUT  = "outpost/hero"                      # path under assets/stations/renders/, no extension
    CAM  = ((x, y, z), (tx, ty, tz))          # camera position and target
    LENS = 35                                  # optional
    UP   = (0, 0, 1)                           # optional camera up (e.g. toward a ring's hub)
    RES  = (1600, 900)                         # optional
A hard key light, a cool rim light from behind, a starfield and a faint planet glow fill."""
import bpy, math, os
from mathutils import Vector, Matrix

sc = bpy.context.scene
g = globals()
OUT_ = g.get("OUT", "preview"); CAM_ = g.get("CAM", ((150, -120, 70), (0, 0, 0)))
LENS_ = g.get("LENS", 35); UP_ = Vector(g.get("UP", (0, 0, 1))); RES_ = g.get("RES", (1600, 900))

w = sc.world or bpy.data.worlds.new("World"); sc.world = w
w.use_nodes = True
nt = w.node_tree
bg = nt.nodes.get("Background")
if not nt.nodes.get("Stars"):
    vor = nt.nodes.new("ShaderNodeTexVoronoi"); vor.name = "Stars"
    vor.inputs["Scale"].default_value = 420.0
    ramp = nt.nodes.new("ShaderNodeValToRGB"); ramp.name = "StarRamp"
    ramp.color_ramp.elements[0].position = 0.0; ramp.color_ramp.elements[0].color = (1.6, 1.6, 1.7, 1)
    ramp.color_ramp.elements[1].position = 0.045; ramp.color_ramp.elements[1].color = (0.003, 0.0035, 0.006, 1)
    coord = nt.nodes.new("ShaderNodeTexCoord")
    nt.links.new(coord.outputs["Generated"], vor.inputs["Vector"])
    nt.links.new(vor.outputs["Distance"], ramp.inputs["Fac"])
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
bg.inputs[1].default_value = 1.0

def light(name, energy, color, rot):
    o = bpy.data.objects.get(name)
    if not o:
        o = bpy.data.objects.new(name, bpy.data.lights.new(name, "SUN"))
        sc.collection.objects.link(o)
    o.data.energy = energy; o.data.color = color; o.data.angle = math.radians(0.6)
    o.rotation_euler = rot
    return o
light("Sun", 4.5, (1.0, 0.96, 0.9), (math.radians(55), 0, math.radians(35)))
light("RenderRim", 2.0, (0.55, 0.7, 1.0), (math.radians(-60), 0, math.radians(200)))
light("Earthshine", 0.4, (0.4, 0.6, 1.0), (math.radians(170), 0, 0))
for n in ("Light",):
    o = bpy.data.objects.get(n)
    if o: o.hide_render = True

cam = bpy.data.objects.get("Camera")
frm, to = Vector(CAM_[0]), Vector(CAM_[1])
_d = (to - frm).length                       # clip range follows the camera distance, so 20 km hulls aren't clipped
cam.data.lens = LENS_; cam.data.clip_start = max(0.05, _d / 2000); cam.data.clip_end = max(20000, _d * 4)
fwd = (to - frm).normalized()
right = fwd.cross(UP_).normalized()
up = right.cross(fwd)
cam.location = frm
cam.rotation_euler = Matrix((right, up, -fwd)).transposed().to_euler()
sc.camera = cam

sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = RES_
sc.render.resolution_percentage = 100
try: sc.eevee.use_raytracing = True
except Exception: pass
sc.view_settings.view_transform = "AgX"
sc.view_settings.exposure = g.get("EXPOSURE", 0.0)
try: sc.view_settings.look = "AgX - Medium High Contrast"
except Exception: pass
out = os.path.join(r"C:\Users\diamo\Desktop\starforge\assets\stations\renders", OUT_ + ".png")
os.makedirs(os.path.dirname(out), exist_ok=True)
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
result = {"out": out}
