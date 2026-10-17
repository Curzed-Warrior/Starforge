"""
Starforge interior prop kit: furniture and fittings at real size, built once and instanced by the furnished station
builds (assets/stations/outpost/interior.py places them from the blueprint's furniture list).

Run inside Blender:  exec(open(r"C:/Users/diamo/Desktop/starforge/assets/props/props.py").read())
-> collection "Props" with one object per kind, named P_<kind>.

Authoring frame (every prop): X = width, Y = depth, Z = up; origin on the floor at the centre of the footprint;
the FRONT faces -Y (the side you use), the back (+Y) goes against the wall. NOMINAL[kind] = (W, D, H).
The builds scale X/Y to the footprint in the plan, so keep details proportional rather than counted.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

NOMINAL = {
    "bed": (2.0, 0.9, 0.95), "bunk": (2.0, 0.9, 1.9), "medbed": (2.0, 0.9, 1.3), "wardrobe": (0.67, 0.6, 2.1),
    "desk": (1.2, 0.52, 1.6), "chair": (0.5, 0.5, 0.95), "stool": (0.4, 0.4, 0.47), "table": (1.2, 0.8, 0.76),
    "coffee_table": (1.0, 0.6, 0.4), "sofa": (2.0, 0.85, 0.85), "bench": (3.2, 0.5, 0.45), "wc": (0.4, 0.73, 0.8),
    "basin": (0.5, 0.42, 1.75), "shower": (1.15, 1.45, 2.1), "hydro_rack": (4.8, 0.9, 2.5), "storage_rack": (2.6, 0.6, 2.2),
    "locker": (1.25, 0.65, 2.0), "cabinet": (1.0, 0.6, 1.8), "console": (1.0, 0.6, 1.25), "machine": (1.5, 0.9, 1.6),
    "counter": (2.0, 0.65, 1.05), "treadmill": (0.9, 2.0, 1.4),
}

# ------------------------------------------------------------------ materials
def _mat(name, col, metal=0.0, rough=0.5, emit=None, strength=0.0, alpha=1.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*col, 1); b.inputs["Metallic"].default_value = metal; b.inputs["Roughness"].default_value = rough
    b.inputs["Emission Strength"].default_value = 0.0
    if emit: b.inputs["Emission Color"].default_value = (*emit, 1); b.inputs["Emission Strength"].default_value = strength
    if alpha < 1: b.inputs["Alpha"].default_value = alpha; m.surface_render_method = "BLENDED"
    m.use_backface_culling = False
    return m

MATS = {
    "frame": _mat("PR_Frame", (0.11, 0.12, 0.14), 0.8, 0.35), "panel": _mat("PR_Panel", (0.62, 0.64, 0.67), 0.15, 0.4),
    "white": _mat("PR_White", (0.86, 0.87, 0.88), 0.0, 0.3), "fabric": _mat("PR_Fabric", (0.16, 0.24, 0.36), 0.0, 0.92),
    "warm": _mat("PR_FabricWarm", (0.5, 0.36, 0.24), 0.0, 0.9), "linen": _mat("PR_Linen", (0.9, 0.9, 0.88), 0.0, 0.85),
    "ceramic": _mat("PR_Ceramic", (0.95, 0.95, 0.96), 0.0, 0.12), "chrome": _mat("PR_Chrome", (0.9, 0.9, 0.92), 1.0, 0.12),
    "glass": _mat("PR_Glass", (0.75, 0.85, 0.9), 0.0, 0.05, alpha=0.22), "screen": _mat("PR_Screen", (0.02, 0.05, 0.08), 0.0, 0.15, (0.2, 0.65, 1.0), 2.5),
    "led": _mat("PR_Led", (1.0, 0.85, 0.65), 0.0, 0.5, (1.0, 0.8, 0.55), 4.0), "top": _mat("PR_Worktop", (0.22, 0.24, 0.27), 0.3, 0.22),
    "rubber": _mat("PR_Rubber", (0.03, 0.03, 0.035), 0.0, 0.9), "plant": _mat("PR_Plant", (0.12, 0.42, 0.1), 0.0, 0.7),
    "soil": _mat("PR_Soil", (0.13, 0.08, 0.05), 0.0, 0.95), "grow": _mat("PR_Grow", (1.0, 0.4, 0.9), 0.0, 0.5, (1.0, 0.3, 0.85), 5.0),
    "amber": _mat("PR_Amber", (0.95, 0.5, 0.06), 0.2, 0.4, (1.0, 0.45, 0.05), 1.5), "crate": _mat("PR_Crate", (0.32, 0.36, 0.3), 0.2, 0.6),
    "mirror": _mat("PR_Mirror", (0.95, 0.95, 0.95), 1.0, 0.03),
}
ORDER = list(MATS)

# ------------------------------------------------------------------ mesh helpers
class B:
    """A bmesh being built; parts are tagged with a material by name."""
    def __init__(self): self.bm = bmesh.new()

    def _tag(self, verts, m):
        for f in {f for v in verts for f in v.link_faces}: f.material_index = ORDER.index(m)

    def box(self, x0, y0, z0, x1, y1, z1, m="panel"):
        vs = bmesh.ops.create_cube(self.bm, size=1.0)["verts"]
        bmesh.ops.scale(self.bm, vec=Vector((abs(x1 - x0), abs(y1 - y0), abs(z1 - z0))), verts=vs)
        bmesh.ops.translate(self.bm, vec=Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)), verts=vs)
        self._tag(vs, m); return vs

    def cyl(self, cx, cy, z0, z1, r, m="frame", seg=16, r2=None, rx=1.0, ry=1.0):
        vs = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=z1 - z0)["verts"]
        bmesh.ops.scale(self.bm, vec=Vector((rx, ry, 1)), verts=vs)
        bmesh.ops.translate(self.bm, vec=Vector((cx, cy, (z0 + z1) / 2)), verts=vs)
        self._tag(vs, m); return vs

    def rod(self, a, b, r, m="chrome", seg=8):
        a, b = Vector(a), Vector(b); d = b - a
        vs = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r, depth=d.length)["verts"]
        rot = d.normalized().to_track_quat("Z", "Y").to_matrix().to_4x4()
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((a + b) / 2) @ rot, verts=vs)
        self._tag(vs, m); return vs

    def ball(self, c, r, m="plant", seg=8, sz=1.0):
        vs = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=max(4, seg // 2), radius=r)["verts"]
        bmesh.ops.scale(self.bm, vec=Vector((1, 1, sz)), verts=vs)
        bmesh.ops.translate(self.bm, vec=Vector(c), verts=vs)
        self._tag(vs, m); return vs

    def wedge(self, x0, x1, y0, z0, y1, z1, t, m="panel"):
        """A slab from (y0, z0) to (y1, z1) in the YZ plane, thickness t, spanning x0..x1 (backrests, sloped consoles)."""
        p = Vector((0, y1 - y0, z1 - z0)); n = Vector((0, -p.z, p.y)).normalized() * t
        pts = [(y0, z0), (y1, z1), (y1 + n.y, z1 + n.z), (y0 + n.y, z0 + n.z)]
        vs = [self.bm.verts.new((x, y, z)) for x in (x0, x1) for y, z in pts]
        F = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        for f in F: self.bm.faces.new([vs[i] for i in f])
        self._tag(vs, m); return vs


def finish(name, b, bevel=0.008):
    bm = b.bm
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    me = bpy.data.meshes.new(name)
    for k in ORDER: me.materials.append(MATS[k])      # slots first: clearing them later resets every face to slot 0
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    o = bpy.data.objects.get(name)
    if o is None:
        o = bpy.data.objects.new(name, me); COL.objects.link(o)
    else:
        old = o.data; o.data = me
        if old.users == 0: bpy.data.meshes.remove(old)
    for mod in list(o.modifiers): o.modifiers.remove(mod)
    if bevel:
        bv = o.modifiers.new("bevel", "BEVEL"); bv.width = bevel; bv.segments = 2; bv.limit_method = "ANGLE"; bv.harden_normals = True
        o.modifiers.new("wn", "WEIGHTED_NORMAL").keep_sharp = True
        dg = bpy.context.evaluated_depsgraph_get()
        me2 = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
        o.modifiers.clear(); old = o.data; o.data = me2; bpy.data.meshes.remove(old); me2.name = name
    return o

# ------------------------------------------------------------------ the props (front = -Y)
def bed(b, bunk=False):
    W, D = 2.0, 0.9
    b.box(-1.0, -0.45, 0.04, 1.0, 0.45, 0.3, "panel")                      # base with drawers
    b.box(-0.97, -0.45, 0.0, 0.97, 0.42, 0.04, "rubber")                    # kick recess
    for x in (-0.95, 0.03):
        b.box(x, -0.452, 0.07, x + 0.92, -0.44, 0.27, "white")              # drawer fronts
        b.box(x + 0.36, -0.465, 0.17, x + 0.56, -0.452, 0.19, "chrome")     # handles
    b.box(-0.98, -0.43, 0.3, 0.98, 0.43, 0.47, "linen")                     # mattress
    b.box(-0.35, -0.445, 0.38, 0.99, 0.445, 0.5, "fabric")                  # blanket over the foot two-thirds
    b.box(-0.42, -0.445, 0.46, -0.32, 0.445, 0.51, "linen")                 # turned-down sheet
    b.box(-0.95, -0.36, 0.47, -0.55, 0.36, 0.58, "white")                   # pillow
    b.box(-1.0, -0.45, 0.0, -0.94, 0.45, 0.95 if not bunk else 0.3, "frame")  # headboard
    if not bunk:
        b.box(-0.94, -0.4, 0.82, -0.91, 0.4, 0.86, "led")                   # reading light strip
        return
    # bunk: an upper berth on four posts, guard rail, ladder at the foot
    for x in (-0.98, 0.98):
        for y in (-0.43, 0.43): b.box(x - 0.025, y - 0.025, 0.0, x + 0.025, y + 0.025, 1.9, "frame")
    b.box(-1.0, -0.45, 1.25, 1.0, 0.45, 1.35, "panel")
    b.box(-0.98, -0.43, 1.35, 0.98, 0.43, 1.5, "linen")
    b.box(-0.35, -0.445, 1.42, 0.99, 0.445, 1.53, "warm")
    b.box(-0.95, -0.36, 1.5, -0.55, 0.36, 1.6, "white")
    b.box(-0.6, -0.47, 1.62, 0.7, -0.44, 1.66, "frame")                      # guard rail
    for z in (0.45, 0.75, 1.05): b.box(0.8, -0.5, z - 0.015, 1.0, -0.47, z + 0.015, "chrome")
    for x in (0.8, 1.0): b.box(x - 0.015, -0.5, 0.0, x + 0.015, -0.47, 1.6, "chrome")
    b.box(-0.94, -0.4, 1.18, -0.91, 0.4, 1.22, "led")                       # under-berth light for the lower bunk


def medbed(b):
    for x in (-0.85, 0.85):
        for y in (-0.35, 0.35): b.cyl(x, y, 0.0, 0.1, 0.05, "rubber", 12)
    b.box(-0.9, -0.3, 0.1, 0.9, 0.3, 0.16, "frame"); b.box(-0.15, -0.15, 0.16, 0.15, 0.15, 0.5, "chrome")
    b.box(-1.0, -0.45, 0.5, 1.0, 0.45, 0.58, "panel")
    b.box(-0.2, -0.43, 0.58, 0.98, 0.43, 0.72, "linen")
    # raised back rest at the head (-X)
    vs = b.box(-0.98, -0.43, 0.58, -0.2, 0.43, 0.72, "linen")
    bmesh.ops.rotate(b.bm, verts=vs, cent=Vector((-0.2, 0, 0.6)), matrix=Matrix.Rotation(math.radians(35), 3, "Y"))
    b.box(-0.7, -0.33, 0.95, -0.4, 0.33, 1.06, "white")                      # pillow
    for y in (-0.47, 0.47):
        b.rod((-0.3, y, 0.85), (0.6, y, 0.85), 0.015); b.rod((-0.3, y, 0.6), (-0.3, y, 0.85), 0.015); b.rod((0.6, y, 0.6), (0.6, y, 0.85), 0.015)
    b.rod((-1.0, 0.4, 0.58), (-1.0, 0.4, 1.3), 0.02, "frame")                 # monitor arm
    b.box(-1.15, 0.2, 1.05, -0.85, 0.24, 1.3, "frame"); b.box(-1.13, 0.195, 1.07, -0.87, 0.2, 1.28, "screen")


def wardrobe(b):
    b.box(-0.335, -0.3, 0.06, 0.335, 0.3, 2.1, "panel")
    b.box(-0.32, -0.28, 0.0, 0.32, 0.28, 0.06, "rubber")
    for x0, x1 in ((-0.33, -0.004), (0.004, 0.33)):
        b.box(x0, -0.31, 0.1, x1, -0.3, 2.05, "white")                        # doors
    for x in (-0.03, 0.03): b.box(x - 0.008, -0.33, 0.9, x + 0.008, -0.31, 1.3, "chrome")
    b.box(-0.33, -0.312, 1.9, 0.33, -0.305, 1.93, "amber")                     # name strip


def desk(b):
    b.box(-0.6, -0.26, 0.7, 0.6, 0.26, 0.74, "top")                           # top
    b.box(-0.6, -0.27, 0.7, 0.6, -0.26, 0.74, "frame")
    b.box(0.2, -0.24, 0.0, 0.58, 0.24, 0.7, "panel")                          # drawer pedestal
    for z in (0.08, 0.32, 0.52): b.box(0.22, -0.245, z, 0.56, -0.24, z + 0.16, "white")
    b.box(-0.6, 0.24, 0.74, 0.6, 0.26, 1.6, "panel")                          # wall panel
    b.box(-0.6, 0.0, 1.32, 0.6, 0.26, 1.35, "top")                            # shelf
    b.box(-0.55, 0.05, 1.3, 0.55, 0.08, 1.32, "led")                          # task light under the shelf
    b.box(-0.35, 0.1, 0.74, 0.05, 0.14, 1.02, "frame"); b.box(-0.34, 0.096, 0.76, 0.04, 0.1, 1.0, "screen")
    b.box(-0.25, -0.15, 0.74, 0.1, 0.0, 0.755, "frame")                       # keyboard


def chair(b):
    for k in range(5):
        a = k * 2 * math.pi / 5
        b.rod((0, 0, 0.06), (0.24 * math.cos(a), 0.24 * math.sin(a), 0.04), 0.018, "frame")
        b.cyl(0.24 * math.cos(a), 0.24 * math.sin(a), 0.0, 0.04, 0.025, "rubber", 8)
    b.cyl(0, 0, 0.06, 0.42, 0.025, "chrome", 12)
    b.box(-0.23, -0.22, 0.42, 0.23, 0.22, 0.5, "fabric")
    b.wedge(-0.21, 0.21, 0.2, 0.52, 0.24, 0.95, 0.06, "fabric")                # backrest at +Y
    b.rod((0, 0.22, 0.46), (0, 0.23, 0.6), 0.015, "frame")


def stool(b):
    b.cyl(0, 0, 0.0, 0.02, 0.17, "frame", 20); b.cyl(0, 0, 0.02, 0.42, 0.03, "chrome", 12)
    b.cyl(0, 0, 0.42, 0.47, 0.19, "warm", 20)


def table(b, W=1.2, D=0.8, H=0.76):
    b.box(-W / 2, -D / 2, H - 0.04, W / 2, D / 2, H, "top")
    b.box(-W / 2 + 0.01, -D / 2 + 0.01, H - 0.06, W / 2 - 0.01, D / 2 - 0.01, H - 0.04, "frame")
    b.cyl(0, 0, 0.03, H - 0.06, 0.05, "chrome", 16)
    b.box(-W * 0.32, -0.04, 0.0, W * 0.32, 0.04, 0.03, "frame"); b.box(-0.04, -D * 0.35, 0.0, 0.04, D * 0.35, 0.03, "frame")


def sofa(b):
    b.box(-1.0, -0.42, 0.0, 1.0, 0.42, 0.08, "rubber"); b.box(-1.0, -0.42, 0.08, 1.0, 0.42, 0.25, "frame")
    for i in range(3):
        x0 = -0.88 + i * 0.59
        b.box(x0, -0.4, 0.25, x0 + 0.57, 0.2, 0.42, "warm")                    # seat cushions
        b.wedge(x0, x0 + 0.57, 0.22, 0.4, 0.33, 0.85, 0.16, "warm")            # back cushions
    for x in (-1.0, 0.89): b.box(x, -0.42, 0.08, x + 0.11, 0.42, 0.6, "warm")   # arms


def bench(b, W=3.2):
    for i in range(5): b.box(-W / 2, -0.25 + i * 0.1, 0.42, W / 2, -0.17 + i * 0.1, 0.45, "warm")
    for x in (-W / 2 + 0.2, 0.0, W / 2 - 0.2): b.box(x - 0.04, -0.22, 0.0, x + 0.04, 0.22, 0.42, "frame")


def wc(b):
    b.box(-0.2, 0.18, 0.0, 0.2, 0.365, 1.0, "panel")                          # cistern panel at the back
    b.box(-0.07, 0.175, 0.85, 0.07, 0.18, 0.92, "chrome")                     # flush plate
    b.cyl(0, -0.08, 0.15, 0.4, 0.19, "ceramic", 24, r2=0.21, ry=1.35)          # bowl, wall-hung
    b.cyl(0, -0.08, 0.4, 0.43, 0.2, "white", 24, ry=1.35)                      # seat
    b.box(-0.17, 0.12, 0.25, 0.17, 0.18, 0.4, "ceramic")


def basin(b):
    b.box(-0.25, -0.21, 0.82, 0.25, 0.21, 0.88, "ceramic")                    # vanity slab
    b.cyl(0, -0.03, 0.83, 0.881, 0.15, "chrome", 24, ry=0.75)                  # bowl recess
    b.box(-0.24, -0.2, 0.55, 0.24, 0.2, 0.82, "panel")                        # under cabinet (wall-hung)
    b.rod((0, 0.17, 0.88), (0, 0.17, 1.0), 0.012); b.rod((0, 0.17, 1.0), (0, 0.08, 1.0), 0.012)   # tap
    b.box(-0.24, 0.2, 1.05, 0.24, 0.21, 1.7, "mirror"); b.box(-0.24, 0.195, 1.7, 0.24, 0.21, 1.73, "led")


def shower(b):
    W, D = 1.15, 1.45
    b.box(-W / 2, -D / 2, 0.0, W / 2, D / 2, 0.05, "ceramic")                 # tray
    b.box(-0.06, -0.06, 0.05, 0.06, 0.06, 0.051, "chrome")                    # drain
    b.box(-W / 2, -D / 2, 0.05, W / 2, -D / 2 + 0.01, 2.0, "glass")           # glass front
    for x in (-W / 2, W / 2): b.box(x - 0.015, -D / 2 - 0.01, 0.05, x + 0.015, -D / 2 + 0.02, 2.0, "chrome")
    b.box(-W / 2, -D / 2 - 0.01, 1.98, W / 2, -D / 2 + 0.02, 2.0, "chrome")
    b.rod((0, D / 2, 1.0), (0, D / 2 - 0.02, 2.0), 0.012); b.cyl(0, D / 2 - 0.15, 1.95, 1.97, 0.1, "chrome", 20)   # riser and head
    b.rod((0, D / 2, 1.95), (0, D / 2 - 0.15, 1.96), 0.01)
    b.box(-0.2, D / 2 - 0.08, 1.0, 0.2, D / 2, 1.08, "frame")                  # shelf


def hydro_rack(b, W=4.8):
    rnd = random.Random(3)
    posts = [-W / 2, -W / 6, W / 6, W / 2]
    for x in posts:
        for y in (-0.42, 0.42): b.box(x - 0.025, y - 0.025, 0.0, x + 0.025, y + 0.025, 2.5, "frame")
    for z in (0.35, 0.9, 1.45, 2.0):
        b.box(-W / 2, -0.44, z, W / 2, 0.44, z + 0.06, "white")                 # tray
        b.box(-W / 2 + 0.03, -0.41, z + 0.06, W / 2 - 0.03, 0.41, z + 0.09, "soil")
        for x in [(-W / 2 + 0.2) + i * 0.42 for i in range(int((W - 0.4) / 0.42) + 1)]:
            for y in (-0.2, 0.2): b.ball((x + rnd.uniform(-0.04, 0.04), y, z + 0.17), rnd.uniform(0.09, 0.14), "plant", 6, sz=0.8)
        for y in (-0.22, 0.22): b.box(-W / 2 + 0.05, y - 0.03, z + 0.5, W / 2 - 0.05, y + 0.03, z + 0.52, "grow")   # grow lights
    b.box(-W / 2, -0.44, 2.45, W / 2, 0.44, 2.5, "frame")


def storage_rack(b, W=2.6):
    rnd = random.Random(5)
    for x in (-W / 2, 0.0, W / 2):
        for y in (-0.28, 0.28): b.box(x - 0.02, y - 0.02, 0.0, x + 0.02, y + 0.02, 2.2, "frame")
    for z in (0.1, 0.55, 1.0, 1.45, 1.9):
        b.box(-W / 2, -0.3, z, W / 2, 0.3, z + 0.03, "panel")
        x = -W / 2 + 0.05
        while x < W / 2 - 0.3 and z < 1.85:
            w, h = rnd.uniform(0.25, 0.55), rnd.uniform(0.15, 0.38)
            if x + w > W / 2 - 0.05: break
            if rnd.random() < 0.85: b.box(x, -0.25, z + 0.03, x + w, 0.25, z + 0.03 + h, "crate" if rnd.random() < 0.6 else "white")
            x += w + rnd.uniform(0.02, 0.08)


def locker(b):
    for x0 in (-0.62, 0.01):
        b.box(x0, -0.32, 0.0, x0 + 0.61, 0.32, 2.0, "panel")
        b.box(x0 + 0.03, -0.33, 0.1, x0 + 0.58, -0.32, 1.95, "frame")
        b.box(x0 + 0.08, -0.335, 0.6, x0 + 0.53, -0.33, 1.7, "glass")          # window onto the suit
        b.box(x0 + 0.03, -0.335, 1.85, x0 + 0.58, -0.33, 1.88, "amber")
        b.box(x0 + 0.17, -0.2, 0.55, x0 + 0.44, 0.15, 1.6, "white")            # the suit inside
        b.ball((x0 + 0.305, -0.03, 1.72), 0.14, "white", 10)


def cabinet(b):
    b.box(-0.5, -0.3, 0.05, 0.5, 0.3, 1.8, "panel"); b.box(-0.48, -0.28, 0.0, 0.48, 0.28, 0.05, "rubber")
    b.box(-0.45, -0.305, 1.25, 0.05, -0.3, 1.6, "frame"); b.box(-0.43, -0.31, 1.27, 0.03, -0.305, 1.58, "screen")
    for i in range(6): b.box(0.12, -0.305, 0.25 + i * 0.12, 0.45, -0.3, 0.29 + i * 0.12, "frame")   # vents
    for i in range(3): b.cyl(0.18 + i * 0.1, -0.31, 1.45, 1.47, 0.015, "amber" if i else "led", 8)
    b.box(-0.45, -0.305, 0.25, 0.05, -0.3, 1.1, "white")                      # service door
    b.rod((-0.3, 0.1, 1.8), (-0.3, 0.1, 2.05), 0.04, "chrome"); b.rod((0.3, 0.1, 1.8), (0.3, 0.1, 2.05), 0.04, "chrome")


def console(b):
    b.box(-0.5, -0.1, 0.0, 0.5, 0.3, 0.75, "panel")
    b.wedge(-0.5, 0.5, -0.3, 0.75, 0.1, 0.95, 0.04, "frame")                    # sloped desk
    b.box(-0.48, 0.15, 0.95, 0.48, 0.2, 1.25, "frame"); b.box(-0.46, 0.145, 0.97, 0.46, 0.15, 1.23, "screen")
    b.box(-0.5, -0.3, 0.0, 0.5, -0.1, 0.05, "rubber")


def machine(b):
    b.box(-0.75, -0.45, 0.0, 0.75, 0.45, 0.85, "panel"); b.box(-0.73, -0.43, 0.0, 0.73, 0.43, 0.06, "rubber")
    b.box(-0.7, -0.4, 0.85, 0.7, 0.4, 1.55, "frame")                          # enclosure
    b.box(-0.5, -0.405, 0.95, 0.4, -0.4, 1.45, "glass")
    b.box(0.45, -0.41, 0.95, 0.68, -0.4, 1.35, "frame"); b.box(0.47, -0.415, 1.1, 0.66, -0.41, 1.3, "screen")
    b.box(-0.7, -0.41, 0.8, 0.7, -0.4, 0.85, "amber")
    b.cyl(-0.05, 0.0, 1.3, 1.55, 0.12, "chrome", 16)                           # spindle head


def counter(b, W=2.0):
    b.box(-W / 2, -0.3, 0.1, W / 2, 0.32, 0.88, "white"); b.box(-W / 2 + 0.02, -0.28, 0.0, W / 2 - 0.02, 0.3, 0.1, "rubber")
    n = max(2, round(W / 0.6))
    for i in range(1, n): x = -W / 2 + W * i / n; b.box(x - 0.003, -0.305, 0.12, x + 0.003, -0.3, 0.86, "frame")
    for i in range(n): x = -W / 2 + W * (i + 0.5) / n; b.box(x - 0.08, -0.32, 0.74, x + 0.08, -0.3, 0.76, "chrome")
    b.box(-W / 2, -0.33, 0.88, W / 2, 0.33, 0.92, "top")
    b.box(-W / 2, 0.3, 0.92, W / 2, 0.33, 1.05, "top"); b.box(-W / 2, 0.29, 1.03, W / 2, 0.3, 1.05, "led")


def treadmill(b):
    b.box(-0.4, -0.95, 0.0, 0.4, 0.95, 0.12, "frame"); b.box(-0.3, -0.85, 0.12, 0.3, 0.95, 0.14, "rubber")
    for x in (-0.4, 0.4):
        b.box(x - 0.03, -0.95, 0.0, x + 0.03, -0.85, 1.3, "frame"); b.rod((x, -0.9, 1.0), (x, -0.4, 1.0), 0.018)
    b.box(-0.4, -1.0, 1.15, 0.4, -0.85, 1.4, "frame"); b.box(-0.35, -0.86, 1.18, 0.35, -0.85, 1.37, "screen")


BUILD = {"bed": bed, "bunk": lambda b: bed(b, True), "medbed": medbed, "wardrobe": wardrobe, "desk": desk, "chair": chair,
         "stool": stool, "table": table, "coffee_table": lambda b: table(b, 1.0, 0.6, 0.4), "sofa": sofa, "bench": bench,
         "wc": wc, "basin": basin, "shower": shower, "hydro_rack": hydro_rack, "storage_rack": storage_rack, "locker": locker,
         "cabinet": cabinet, "console": console, "machine": machine, "counter": counter, "treadmill": treadmill}


def build_all(only=None):
    global COL
    COL = bpy.data.collections.get("Props")
    if not COL:
        COL = bpy.data.collections.new("Props"); bpy.context.scene.collection.children.link(COL)
    out = {}
    x = 0.0
    for k, fn in BUILD.items():
        if only and k not in only: continue
        b = B(); fn(b)
        o = finish("P_" + k, b, bevel=0.006 if k in ("chair", "stool", "wc", "basin", "hydro_rack", "storage_rack") else 0.012)
        W, D, H = NOMINAL[k]
        x += W / 2; o.location = (x, 200.0, 0.0); x += W / 2 + 0.6          # a lineup away from the station
        out[k] = len(o.data.polygons)
    return out


result = build_all(globals().get("ONLY"))
