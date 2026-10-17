"""
Stable Wormhole v2 (asset overhaul, docs/ASSET_REFERENCES.md). After jump-gate references: a dark throat held inside
a heavy segmented stabiliser ring: twelve armoured clamp blocks with blue-glowing projector throats and seams, four
great anchor pylons reaching out with containment spheres in caged rings, monitoring stations and a research station
clamped on, traffic beacons, radiators and strobes. The procedural Einstein ring and lensing sit in front.
The throat faces -Y (the game's +Z). Z is up.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/wormhole/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Wormhole")
M["Void"] = mat("Void", (0, 0, 0), 0.0, 1.0)
M["Exotic"] = mat("Exotic", (0.6, 0.7, 1.0), emit=(0.55, 0.65, 1.0), strength=10)
MATS = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Exotic", "Radiator", "NavRed", "Void"]
HULL, DARK, LIGHT, ACC, HAZ, WIN, WCOOL, STROBE, EXO, RADI, RED, VOID = range(len(MATS))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
def U(b): return b.loops.layers.uv.verify()
FACE = Rx(math.pi / 2)      # build facing +Z, then turn to face -Y (the game's +Z)
TR = 100.0

bm = bmesh.new()
revolve(bm, [(0, -2), (TR, 0), (0, 2)], 64, lambda i, s: 0, closed=False, toward=lambda c: Vector((0, 0, 1 if c.z > 0 else -1)))
obj("Throat", bm, ["Void"])

bm = bmesh.new()
for k in range(12):
    a0 = k * 2 * math.pi / 12
    am = a0 + math.pi / 12
    d = Vector((math.cos(am), math.sin(am), 0)); t = Vector((-d.y, d.x, 0))
    # a ring segment, and the clamp block over its joint
    solid_ring(bm, TR + 6, TR + 28, -16, 16, 8, mats=(DARK, DARK, DARK, HULL), a0=a0 + 0.02, a1=a0 + 2 * math.pi / 12 - 0.02, bevel=3)
    dj = Vector((math.cos(a0), math.sin(a0), 0)); tj = Vector((-dj.y, dj.x, 0))
    fast_prism(bm, octo(22, 48, 7), 46, dj * (TR + 18) - Z_ * 23, Matrix((tj, dj, Z_)).transposed(), DARK if k % 2 else HULL, U(bm), top=(0.8, 0.85, 0, 0), base_cap=True)
    loft(bm, [(TR + 2, 6, 10, 0.4), (TR - 6, 4, 7, 0.4)], Rz(a0).to_3x3(), Vector((0, 0, 0)), DARK)
    cyl(bm, 3.6, 3.6, 0.5, dj * (TR - 6.3), Rz(a0) @ Ry(math.pi / 2), seg=12, mi=EXO)           # the projector throat
    fast_box(bm, (2 * math.pi * (TR + 16) / 12 * 0.8, 1.0, 0.4), d * (TR + 16) + Z_ * 16.1, Matrix((t, d, Z_)).transposed(), EXO, U(bm), bottom=True)   # glowing seam
    fast_box(bm, (2 * math.pi * (TR + 16) / 12 * 0.8, 1.0, 0.4), d * (TR + 16) - Z_ * 16.1, Matrix((t, -d, -Z_)).transposed(), EXO, U(bm), bottom=True)
    fast_box(bm, (14, 3.0, 0.4), dj * (TR + 18) + Z_ * 23.2, Matrix((tj, dj, Z_)).transposed(), HAZ, U(bm), bottom=True)
    strobe(bm, dj * (TR + 36) + Z_ * 14, 1.0, RED if k % 3 == 0 else STROBE)
detail(bm, cyl_surface(TR + 28, -12, 12), 2, scale=2.0, mats=(DARK, HULL), window=WIN, window_frac=0.09)
solid_ring(bm, TR + 5.5, TR + 6, -3, 3, 96, mats=(EXO,) * 4, bevel=0.0)                 # the throat's rim glows
detail(bm, disc_surface(TR + 9, TR + 25, 16, up=True), 3, scale=1.8, mats=(HULL, DARK), window=WCOOL, window_frac=0.05)
# four anchor pylons with containment spheres in caged rings
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    d = Vector((math.cos(a), math.sin(a), 0))
    loft(bm, [(TR + 24, 9, 12, 0.4), (TR + 60, 6, 8, 0.4), (TR + 90, 4, 5, 0.4)], Rz(a).to_3x3(), Vector((0, 0, 0)), HULL)
    truss(bm, d * (TR + 26) + Z_ * 12, d * (TR + 86) + Z_ * 4, w=3, mi=DARK, rod=0.3)
    tank_cage(bm, d * (TR + 100), 9, axis=Z_, mi_tank=EXO, mi_cage=DARK, ribs=5)
    radiator_bank(bm, d * (TR + 55) - Z_ * 8, -Z_, Vector((-d.y, d.x, 0)), 6, 18, 8, 3, mi_fin=RADI, mi_man=DARK)
# a research station clamped to the ring, and two monitoring pods
module(bm, Rz(0.6 + math.pi / 2) @ Vector((TR + 46, 0, 0)), Rz(0.6) @ X_, Z_, 60, 22, 18, mats=(HULL, DARK, WIN), seed=11, detail_scale=1.2)
dish(bm, Rz(0.6 + math.pi / 2) @ Vector((TR + 44, 0, 14)), Vector((0.2, 0.4, 1)), 7, mi_dish=LIGHT, mi_frame=DARK)
for k, a in enumerate((2.6, 4.4)):
    module(bm, Rz(a) @ Vector((TR + 36, 0, 0)), Rz(a) @ Y_, Z_, 20, 10, 10, mats=(HULL, DARK, WIN), seed=21 + k)
obj("Stabiliser", bm, MATS)

for o in col.objects:
    if o.type == "MESH":
        o.data.transform(FACE)
lamp("L_Throat", (0, -10, 0), 1e5, (0.55, 0.65, 1.0), 20)
result = export(col, root, "wormhole")
