"""
Coordination Center v2 (asset overhaul, docs/ASSET_REFERENCES.md). Where a hive-mind's consensus is kept: an armoured
sphere banded with heavy collars and a glowing eye, a central spire with a dish crown, ten command arms reaching out in
every direction, each a truss-braced boom ending in a pod cluster with windows and a relay dish; a tilted halo ring of
synapse relays ("halo", pulsed by the game). Z is up.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/coord/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Coord")
MATS = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Glow", "NavRed"]
HULL, DARK, LIGHT, ACC, HAZ, WIN, WCOOL, STROBE, GLOW, RED = range(len(MATS))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
def U(b): return b.loops.layers.uv.verify()

bm = bmesh.new()
sphere(bm, 40, (0, 0, 0), mi=HULL, seg=40)
for k in range(5):
    band(bm, Vector((0, 0, 0)), Rz(k * math.pi / 5) @ Y_, 40, 4, 3, DARK if k % 2 else LIGHT, 64)
for j, z in enumerate((-30, -18, 18, 30)):
    band(bm, Vector((0, 0, z)), Z_, math.sqrt(40 * 40 - z * z), 2.4, 2.0, ACC if j in (0, 3) else DARK, 48)
greeble(bm, cyl_surface(40, -26, 26), 0.05, 4, size=(1.2, 4), height=(0.5, 2), mats=(HULL, DARK), window=WIN, glow=GLOW, window_frac=0.08, glow_frac=0.03)
# the eye
n0 = set(bm.verts)
revolve(bm, [(0, 41), (14, 38.5), (17, 36)], 32, lambda i, s: GLOW, closed=False, toward=lambda c: Vector((0, 0, 1)))
bmesh.ops.transform(bm, matrix=Rx(-math.pi / 2), verts=since(bm, n0))
band(bm, Vector((0, -36, 0)), Y_, 18, 3, 2.5, DARK, 32)
# the central spire and its dish crown
loft(bm, [(36, 10, 10, 0.4), (70, 6, 6, 0.4), (76, 10, 10, 0.4), (88, 10, 10, 0.4), (94, 3, 3, 0.4), (130, 1, 1, 0.4)], UPX, Vector((0, 0, 0)), HULL)
window_rows(bm, plane_surface(Vector((10.05, -8, 76)), Vector((0, 16, 0)), Vector((0, 0, 12))), 3, 8, size=(1.0, 0.5), mi=WIN, seed=5, lit=0.9)
dish(bm, Vector((0, 0, 98)), Vector((0.3, -0.4, 1)), 9, mi_dish=LIGHT, mi_frame=DARK)
loft(bm, [(-36, 10, 10, 0.4), (-60, 6, 6, 0.4), (-90, 1.5, 1.5, 0.4)], UPX, Vector((0, 0, 0)), DARK)
strobe(bm, Vector((0, 0, 131)), 1.2, RED); strobe(bm, Vector((0, 0, -91)), 1.2, RED)
# ten command arms ending in pod clusters
dirs = [Vector((math.cos(a), math.sin(a), z)).normalized() for a, z in [(k * 2 * math.pi / 10 + (0.3 if k % 2 else 0), 0.42 if k % 2 else -0.3) for k in range(10)]]
for i, d in enumerate(dirs):
    rot = basis(d)
    L = 95 + (i % 3) * 14                              # arms of different lengths, not a perfect star
    loft(bm, [(36, 8, 8, 0.4), (48, 5, 5, 0.4), (L - 10, 4, 4, 0.4)], rot, Vector((0, 0, 0)), DARK)
    truss(bm, d * 44 + rot.col[2] * 6, d * (L - 12) + rot.col[2] * 4, w=3, mi=DARK, rod=0.25)
    module(bm, d * (L + 6), d, Z_ if abs(d.z) < 0.9 else X_, 26, 12, 10, mats=(LIGHT if i % 3 else HULL, DARK, WIN), seed=30 + i, detail_scale=0.5)
    module(bm, d * (L + 2) + rot.col[1] * 10, d, Z_ if abs(d.z) < 0.9 else X_, 14, 7, 7, mats=(HULL, DARK, WIN), seed=60 + i)
    dish(bm, d * (L + 22), d, 5, mi_dish=LIGHT, mi_frame=DARK)
    strobe(bm, d * (L + 20) + rot.col[2] * 7, 0.8, STROBE)
obj("Core", bm, MATS)

bm = bmesh.new()
solid_ring(bm, 150, 156, -2.5, 2.5, 128, mats=(DARK, DARK, HULL, DARK))
for k in range(48):
    a = k * 2 * math.pi / 48
    sphere(bm, 1.8, Rz(a) @ Vector((156.5, 0, 0)), mi=GLOW, seg=8)
    if k % 6 == 0:
        loft(bm, [(-6, 4, 4, 0.4), (6, 4, 4, 0.4)], Rz(a + math.pi / 2).to_3x3(), Rz(a) @ Vector((153, 0, 0)), HULL)
bmesh.ops.transform(bm, matrix=Rx(0.4), verts=bm.verts[:])
obj("halo", bm, MATS)
lamp("L_Eye", (0, -60, 0), 3e5, FIRE, 8)
result = export(col, root, "coord")
