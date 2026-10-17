"""
Laser Sail Array v2 (asset overhaul, docs/ASSET_REFERENCES.md). A phased mosaic of hexagonal mirror tiles, each in
its own rimmed mount, on a curved lattice 300 m across facing -Y (the game's +Z, the procedural beam's direction),
the laser fired through a glowing aperture at its centre. Behind it at +Y: a truss spine to the laser plant
(capacitor banks, power modules, radiators), magazines of folded lightsails waiting to be thrown, a targeting dish
and strobes. Z is up.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/laser/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Laser")
M["Mirror"] = mat("Mirror", (0.55, 0.78, 0.74), 0.75, 0.22)
MATS = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Mirror", "Plasma", "Radiator", "NavRed", "Chrome"]
HULL, DARK, LIGHT, ACC, HAZ, WIN, WCOOL, STROBE, MIRROR, PLASMA, RADI, RED, CHROME = range(len(MATS))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
def U(b): return b.loops.layers.uv.verify()
RA = 150.0              # array radius
CURVE = 900.0           # radius of curvature of the mosaic (concave toward -Y)

def onArray(x, z):
    """A point on the curved mosaic: concave toward -Y, so its edges lean forward to focus the beam."""
    return Vector((x, math.sqrt(max(CURVE * CURVE - x * x - z * z, 1)) - CURVE, z)), -Y_

# ---------------------------------------------------------------- the mirror mosaic
bm = bmesh.new()
S = 15.0                # hex tile spacing
for i in range(-12, 13):
    for j in range(-12, 13):
        x = S * (i + 0.5 * (j % 2)); z = S * 0.866 * j
        r = math.hypot(x, z)
        # a hexagonal outline (like a segmented telescope primary), not a disc
        ang = math.atan2(z, x) % (math.pi / 3) - math.pi / 6
        if r * math.cos(ang) > RA * 0.9 or r < 16: continue
        p, n = onArray(x, z)
        # rim (mount), then the mirror face slightly proud of it
        fast_prism(bm, ellipse(S * 1.12, S * 1.12, 6), 1.6, p, Matrix((X_, Z_, -Y_)).transposed(), DARK, U(bm), top=(0.96, 0.96, 0, 0), base_cap=True)
        # each segment is steered a little differently, so the mosaic catches light tile by tile
        tr = random.Random(i * 131 + j)
        tilt = Matrix.Rotation(tr.uniform(-0.05, 0.05), 3, "X") @ Matrix.Rotation(tr.uniform(-0.05, 0.05), 3, "Z")
        fast_prism(bm, ellipse(S * 0.94, S * 0.94, 6), 0.4, p - Y_ * 1.6, tilt @ Matrix((X_, Z_, -Y_)).transposed(), MIRROR, U(bm), top=(1, 1, 0, 0))
        if (i * 7 + j * 3) % 11 == 0:
            fast_box(bm, (1.2, 1.2, 0.6), p - Y_ * 2.2 + X_ * S * 0.4, Matrix((X_, Z_, -Y_)).transposed(), WCOOL, U(bm), bottom=True)
# the black backplane behind the tiles, so the gaps read dark
for k in range(6):
    a0, a1 = k * math.pi / 3, (k + 1) * math.pi / 3
    q = [Vector((0, 0, 0)), Vector((math.cos(a0) * RA * 1.04, 0, math.sin(a0) * RA * 1.04)), Vector((math.cos(a1) * RA * 1.04, 0, math.sin(a1) * RA * 1.04))]
    q = [Vector((v.x, onArray(v.x, v.z)[0].y + 7.0, v.z)) for v in q]
    f = bm.faces.new([bm.verts.new(v) for v in q]); f.material_index = DARK
    f2 = bm.faces.new([bm.verts.new(v + Y_ * 0.4) for v in reversed(q)]); f2.material_index = DARK
obj("Mosaic", bm, MATS)

# ---------------------------------------------------------------- the backing lattice, the rim and the aperture
bm = bmesh.new()
for k in range(12):                                   # radial lattice ribs behind the mosaic
    a = k * math.pi / 6
    pts = []
    for s in range(7):
        rr = 18 + (RA - 18) * s / 6
        p, n = onArray(math.cos(a) * rr, math.sin(a) * rr)
        pts.append(p + Y_ * 6)
    for pa, pb in zip(pts, pts[1:]): truss(bm, pa, pb, w=4.5, mi=DARK, rod=0.35)
for rr in (60, 110, RA + 6):                          # hoop chords
    n0 = set(bm.verts)
    p, n = onArray(rr, 0)
    solid_ring(bm, rr - 2.2, rr + 2.2, -2, 2, 72, mats=(DARK, HULL, ACC if rr > RA else HULL, HULL))
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0, p.y + 7, 0)) @ Rx(math.pi / 2), verts=since(bm, n0))
for k in range(16):                                   # armoured rim segments and strobes
    a = k * math.pi / 8
    d = Vector((math.cos(a), 0, math.sin(a)))
    p, n = onArray(d.x * (RA + 10), d.z * (RA + 10))
    loft(bm, [(-9, 4, 3, 0.4), (9, 4, 3, 0.4)], basis(Vector((-d.z, 0, d.x)), Y_), p + Y_ * 4, HULL if k % 2 else LIGHT)
    strobe(bm, p + d * 6, 1.1, RED if k % 4 == 0 else STROBE)
cyl(bm, 14, 14, 10, Vector((0, 0, 0)), Rx(math.pi / 2), seg=24, mi=HULL)            # the aperture collar
cyl(bm, 9, 9, 0.6, Vector((0, -5.4, 0)), Rx(math.pi / 2), seg=24, mi=PLASMA)
for e in range(3): band(bm, Vector((0, 4 + e * 6, 0)), Y_, 14, 1.4, 1.2, HAZ if e == 1 else DARK, 24)
obj("Frame", bm, MATS)

# ---------------------------------------------------------------- the laser plant behind (+Y)
bm = bmesh.new()
truss(bm, Vector((0, 10, 0)), Vector((0, 150, 0)), w=12, mi=DARK, rod=0.7)
module(bm, Vector((0, 170, 0)), Y_, Z_, 50, 36, 28, mats=(HULL, DARK, WIN), seed=11, detail_scale=1.0)
module(bm, Vector((0, 200, 16)), Y_, Z_, 30, 22, 16, mats=(LIGHT, DARK, WIN), seed=12)
for k in range(6):                                    # capacitor banks: rows of drums along the spine
    a = k * math.pi / 3
    d = Vector((math.cos(a), 0, math.sin(a)))
    for j in range(5):
        cyl(bm, 3.2, 3.2, 12, Vector((0, 40 + j * 20, 0)) + d * 12, Rx(math.pi / 2), seg=12, mi=ACC if (j + k) % 4 == 0 else LIGHT)
        band(bm, Vector((0, 40 + j * 20, 0)) + d * 12, Y_, 3.2, 1.0, 0.5, DARK, 12)
for sx in (1, -1):
    radiator_bank(bm, Vector((sx * 20, 175, 0)), Vector((sx, 0, 0)), Y_, 12, 50, 14, 3.6, mi_fin=RADI, mi_man=DARK)
# sail magazines: racks of folded sails, and a targeting dish
for sz in (1, -1):
    for j in range(8):
        cyl(bm, 7, 7, 1.0, Vector((26, 90 + j * 3.2, sz * 22)), Rx(math.pi / 2), seg=16, mi=CHROME if j % 2 else LIGHT)
    loft(bm, [(80, 2, 9, 0.3), (120, 2, 9, 0.3)], basis(Y_), Vector((26 - 8, 0, sz * 22)), DARK)
dish(bm, Vector((-24, 196, 24)), Vector((0, -1, 0.5)), 10, mi_dish=LIGHT, mi_frame=DARK)
antennas(bm, Vector((0, 200, 25)), Z_, 5, 18, 21, mi=DARK, light=RED)
obj("Plant", bm, MATS)
lamp("L_Ap", (0, -8, 0), 1e5, (0.5, 1.0, 0.8), 8)
result = export(col, root, "laser")
