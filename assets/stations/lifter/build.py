"""
Star Lifter: magnetic funnels over both poles of a star, drawing its plasma up into collector stations.
Star radius = 100 (the game scales to the real star). At each pole, 420 above the surface: a great collector
ring with field coils, a funnel of lattice vanes narrowing toward the star, and a refinery drum at the top.
The plasma jets themselves stay procedural. Z is up (the star's axis).
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/lifter/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Lifter")

for sgn in (1, -1):
    bm = bmesh.new()
    Zc = sgn * 420.0
    n0 = set(bm.verts)
    c = solid_ring(bm, 60, 84, -7, 7, 96, mats=(0, 1, 0, 0))
    detail(bm, cyl_surface(84, -7 + c + 0.2, 7 - c - 0.2), 1, scale=3.4, mats=(0, 1), window=2, glow=3, window_frac=0.04, glow_frac=0.05)
    for s2 in (-1, 1):
        detail(bm, disc_surface(60 + c + 0.2, 84 - c - 0.2, s2 * 7, up=s2 > 0), 2 + s2, scale=3.4, mats=(0, 1), glow=3, glow_frac=0.04)
    # field coils: twelve chunky toroidal magnets wrapped round the ring
    for k in range(12):
        a = k * math.pi / 6
        m0 = set(bm.verts)
        solid_ring(bm, 14, 19, -3, 3, 12, mats=(1, 1, 1, 1))
        bmesh.ops.transform(bm, matrix=Rz(a) @ Matrix.Translation((72, 0, 0)) @ Rx(math.pi / 2), verts=since(bm, m0))
        sphere(bm, 2.2, Rz(a) @ Vector((72, 0, 0)) + Vector((0, 0, -9 * sgn)), mi=3, seg=8)
    # the funnel: vanes sweeping down toward the star, a glowing throat at the bottom
    for k in range(16):
        a = k * math.pi / 8 + math.pi / 16
        p0 = Rz(a) @ Vector((64, 0, -6 * sgn)); p1 = Rz(a) @ Vector((22, 0, -120 * sgn))
        truss(bm, p0, p1, w=4, mi=1, rod=0.4)
    m0 = set(bm.verts)
    solid_ring(bm, 18, 26, -4, 4, 32, mats=(1, 1, 1, 1))
    bmesh.ops.translate(bm, vec=Vector((0, 0, -122 * sgn)), verts=since(bm, m0))
    m0 = set(bm.verts)
    solid_ring(bm, 17.4, 18, -1.5, 1.5, 32, mats=(3, 3, 3, 3), bevel=0)      # the glowing throat
    bmesh.ops.translate(bm, vec=Vector((0, 0, -122 * sgn)), verts=since(bm, m0))
    # the refinery drum above the ring, with docks and antennas
    revolve(bm, [(0, 10 * sgn), (30, 10 * sgn), (36, 22 * sgn), (36, 60 * sgn), (24, 74 * sgn), (0, 74 * sgn)], 32,
            lambda i, s: 1 if i in (1, 4) else 0, closed=False, toward=lambda c, sgn=sgn: (radial(c) + Vector((0, 0, sgn * 0.6))).normalized())
    detail(bm, cyl_surface(36, min(22 * sgn, 60 * sgn) + 0.5, max(22 * sgn, 60 * sgn) - 0.5), 9 + sgn, scale=1.8, mats=(0, 1), window=2, window_frac=0.08)
    for k in range(4):
        a = k * math.pi / 2
        loft(bm, [(4, 6, 6, 0.4), (60, 4, 4, 0.4)], Rz(a).to_3x3(), Vector((0, 0, 30 * sgn)), 1)
    for k in range(6):
        a = k * math.pi / 3
        _rod(bm, Rz(a) @ Vector((34, 0, 14 * sgn)), Rz(a) @ Vector((62, 0, 6 * sgn)), 1.5, 1)
    antennas(bm, (0, 0, 74 * sgn), (0, 0, sgn), 5, 30, 40 + sgn, mi=1, light=4)
    bmesh.ops.translate(bm, vec=Vector((0, 0, Zc)), verts=bm.verts[:])
    obj("Pole" + ("N" if sgn > 0 else "S"), bm, ["Hull", "HullDark", "Window", "Glow", "NavRed"])


# ================================================================ v2 secondary-scale pass (asset overhaul, docs/ASSET_REFERENCES.md)
AM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar", "Rock"]
A_HULL, A_DARK, A_LIGHT, A_ACC, A_HAZ, A_WIN, A_WCOOL, A_STROBE, A_RAD, A_GLOW, A_RED, A_SOLAR, A_ROCK = range(len(AM))
M["Rock"] = mat("Rock", (0.32, 0.27, 0.22), 0.0, 0.9)
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
bm = bmesh.new()

# collector stations at each pole: modules round the refinery drum, tanks, radiators, a tender, coil rings down the funnel
for sgn in (1, -1):
    Zc = sgn * 420.0
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        d = Vector((math.cos(a), math.sin(a), 0))
        module(bm, d * 66 + Z_ * (Zc + sgn * 40), Vector((-d.y, d.x, 0)), Z_, 40, 16, 16, mats=(A_LIGHT if k % 2 else A_HULL, A_DARK, A_WIN), seed=80 + k + sgn)
        tank_cage(bm, d * 100 + Z_ * (Zc + sgn * 24), 10, axis=Z_, capsule=1.2, mi_tank=A_DARK if k % 2 else A_HULL, mi_cage=A_DARK)
        radiator_bank(bm, d * 74 + Z_ * (Zc + sgn * 58), d, Vector((-d.y, d.x, 0)), 10, 34, 12, 5, mi_fin=A_RAD, mi_man=A_DARK)
        strobe(bm, d * 132 + Z_ * (Zc + sgn * 58), 1.4, A_RED)
    for j in range(5):
        r = 62 - j * 9
        band(bm, Vector((0, 0, Zc - sgn * (16 + j * 22))), Z_, r, 2.4, 2.0, A_GLOW if j % 2 == 0 else A_DARK, 48)
    dock_arm(bm, Vector((0, -36, Zc + sgn * 60)), Vector((0, -1, sgn * 0.6)), 40, 40, mats=(A_LIGHT, A_DARK, A_GLOW, A_WIN), seed=90 + sgn)
obj("Collectors", bm, AM)

result = export(col, root, "lifter")
