"""
Nicoll-Dyson Beam v2 (asset overhaul, docs/ASSET_REFERENCES.md). A focusing station: an enormous primary mirror of
twelve curved petals with gaps between them, held in an armoured segmented rim; a secondary mirror on six spars out
in front at the focus; the beam barrel (a collimator of stacked rings) through the centre with the emitter throat
glowing white-gold; behind, a control citadel, radiator wings and power trunks from the swarm. Faces -Y (the game's
+Z, the procedural beam's direction). ~420 m. Z is up.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/beam/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Beam")
M["Mirror"] = mat("Mirror", (0.55, 0.78, 0.74), 0.75, 0.22)
M["GoldMirror"] = mat("GoldMirror", (1.0, 0.68, 0.22), 0.85, 0.2)
M["Emit"] = mat("Emit", (1, 0.9, 0.7), emit=(1, 0.85, 0.6), strength=10)
MATS = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "GoldMirror", "Emit", "Radiator", "NavRed"]
HULL, DARK, LIGHT, ACC, HAZ, WIN, WCOOL, STROBE, GOLD, EMIT, RADI, RED = range(len(MATS))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
def U(b): return b.loops.layers.uv.verify()
R = 200.0
F = 260.0                                   # focal length

def mirror_pt(r, a):
    """A point on the paraboloid (vertex at origin, opening toward -Y)."""
    return Vector((math.cos(a) * r, -r * r / (4 * F), math.sin(a) * r))

# ---------------------------------------------------------------- twelve petals with gaps, each with a backing frame
bm = bmesh.new()
P = 12
for k in range(P):
    a0, a1 = k * 2 * math.pi / P + 0.02, (k + 1) * 2 * math.pi / P - 0.02
    rings = []
    for i in range(9):
        r = 30 + (R - 30) * i / 8
        rings.append([bm.verts.new(mirror_pt(r, a0 + (a1 - a0) * j / 6)) for j in range(7)])
    for i in range(8):
        for j in range(6):
            # each tile shrunk toward its own centre so thin dark seams show between tiles
            q = [rings[i][j].co, rings[i + 1][j].co, rings[i + 1][j + 1].co, rings[i][j + 1].co]
            c = sum(q, Vector()) / 4
            f = bm.faces.new([bm.verts.new(c + (v - c) * 0.94) for v in q])
            f.material_index = GOLD
            b = bm.faces.new([bm.verts.new(v + Y_ * 1.0) for v in reversed(q)])
            b.material_index = DARK
    for row in rings:
        for v in row: bm.verts.remove(v)
    # the petal's back: ribs along its spine and edges, and a spar to the rim
    am = (a0 + a1) / 2
    for a in (a0, am, a1):
        pts = [mirror_pt(30 + (R - 30) * i / 8, a) + Y_ * 3 for i in range(9)]
        for pa, pb in zip(pts, pts[1:]): _rod(bm, pa, pb, 1.4 if a == am else 0.9, DARK)
bm.normal_update()
flip = [f for f in bm.faces if f.material_index == GOLD and f.normal.y > 0]
bmesh.ops.reverse_faces(bm, faces=flip)
obj("Petals", bm, MATS)

# ---------------------------------------------------------------- the armoured rim, the secondary on its spars, the barrel
bm = bmesh.new()
yr = mirror_pt(R, 0).y
n0 = set(bm.verts)
solid_ring(bm, R + 2, R + 14, -6, 6, 96, mats=(DARK, HULL, HULL, LIGHT))
bmesh.ops.transform(bm, matrix=Matrix.Translation((0, yr, 0)) @ Rx(math.pi / 2), verts=since(bm, n0))
for k in range(24):                                   # rim armour blocks with accent plates and strobes
    a = k * 2 * math.pi / 24
    d = Vector((math.cos(a), 0, math.sin(a)))
    t = Vector((-d.z, 0, d.x))
    loft(bm, [(-18, 5, 10, 0.4), (18, 5, 10, 0.4)], basis(t, d), d * (R + 16) + Y_ * yr, ACC if k % 6 == 0 else HULL)
    if k % 3 == 0: strobe(bm, d * (R + 23) + Y_ * yr, 1.4, RED if k % 6 == 0 else STROBE)
# six spars to the secondary mirror at the focus
fy = -F * 0.82
for k in range(6):
    a = k * math.pi / 3 + math.pi / 6
    d = Vector((math.cos(a), 0, math.sin(a)))
    truss(bm, d * (R + 8) + Y_ * yr, d * 30 + Y_ * fy, w=5, mi=DARK, rod=0.45)
n0 = set(bm.verts)
f = revolve(bm, [(0.5, -6), (30, 2), (34, 6)], 32, lambda i, s: GOLD, closed=False)
bmesh.ops.transform(bm, matrix=Matrix.Translation((0, fy, 0)) @ Rx(-math.pi / 2), verts=since(bm, n0))
loft(bm, [(-10, 34, 34, 0.3), (0, 36, 36, 0.3), (8, 26, 26, 0.3)], basis(-Y_), Vector((0, fy - 2, 0)), HULL)
# the barrel: a collimator of stacked rings through the centre, the emitter throat glowing
for j in range(14):
    y = 40 - j * 9
    band(bm, Vector((0, y, 0)), Y_, 20 - j * 0.4, 4, 3, HAZ if j % 5 == 0 else (DARK if j % 2 else HULL), 24)
cyl(bm, 19, 19, 130, Vector((0, -20, 0)), Rx(math.pi / 2), seg=24, mi=DARK, caps=False)
cyl(bm, 13, 13, 0.6, Vector((0, -86, 0)), Rx(math.pi / 2), seg=24, mi=EMIT)
obj("Rim", bm, MATS)

# ---------------------------------------------------------------- the citadel behind, radiator wings, power trunks
bm = bmesh.new()
loft(bm, [(40, 30, 30, 0.35), (70, 46, 40, 0.35), (130, 40, 34, 0.35), (160, 18, 16, 0.4)], basis(Y_), Vector((0, 0, 0)), HULL)
detail(bm, plane_surface(Vector((-38, 70, 38)), Vector((0, 60, 0)), Vector((76, 0, 0))), 31, scale=1.5, mats=(HULL, DARK), window=WIN, window_frac=0.07)
for sx in (1, -1):
    window_rows(bm, plane_surface(Vector((sx * 46.1, 75, -20)), Vector((0, 50, 0)) * sx, Vector((0, 0, 40))), 30, 6, size=(1.4, 0.7), mi=WIN, seed=33 + sx, lit=0.75)
    radiator_bank(bm, Vector((sx * 46, 110, 0)), Vector((sx, 0.2, 0)), Y_, 14, 90, 22, 4.0, mi_fin=RADI, mi_man=DARK)
    pipe_run(bm, [Vector((sx * 30, 150, -30)), Vector((sx * 30, 200, -60)), Vector((sx * 60, 260, -80))], 2.2, n=3, mi=DARK, clamp_mi=HAZ)
dish(bm, Vector((0, 150, 40)), Vector((0.3, 0.2, 1)), 12, mi_dish=LIGHT, mi_frame=DARK)
antennas(bm, Vector((20, 140, 38)), Z_, 6, 26, 37, mi=DARK, light=RED)
obj("Citadel", bm, MATS)
lamp("L_Throat", (0, -92, 0), 2e5, (1.0, 0.85, 0.6), 12)
result = export(col, root, "beam")
