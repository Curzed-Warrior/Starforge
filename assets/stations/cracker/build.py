"""
Arc Furnace (inspired by Stellaris' arc furnace, not a copy): built around a world that is being taken apart.
Unit planet radius is 100 (the game scales it to the mined world). An equatorial ring carrying four harvester
claws that reach down into the crust and glow, four meridian arches meeting in a crown over the north pole,
collector spikes, and a south-pole drill. Z is up. The planet itself is not part of the asset.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/cracker/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Cracker")

PR = 100.0
bm = bmesh.new()
c = solid_ring(bm, 118, 128, -5, 5, 128, mats=(1, 1, 0, 0))
detail(bm, cyl_surface(128, -5 + c + 0.1, 5 - c - 0.1), 1, scale=1.3, mats=(0, 1), window=2, glow=3, window_frac=0.05, glow_frac=0.06)
for sz in (-1, 1):
    detail(bm, disc_surface(118 + c + 0.1, 128 - c - 0.1, sz * 5, up=sz > 0), 2 + sz, scale=1.3, mats=(0, 1), glow=3, glow_frac=0.05)
# a glowing seam on the inner face where the ring's collectors drink the heat
solid_ring(bm, 117.6, 118.0, -1.2, 1.2, 128, mats=(3, 3, 3, 3), bevel=0)
obj("Ring", bm, ["Hull", "HullDark", "Window", "Glow"])

bm = bmesh.new()
# four harvester claws: heavy lofted blocks on the ring, a jaw reaching down to the surface, glowing at the bite
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    d = Vector((math.cos(a), math.sin(a), 0))
    rot = Matrix((-d, Vector((-d.y, d.x, 0)), Vector((0, 0, 1)))).transposed()     # local +X points at the planet
    loft(bm, [(-140, 9, 12, 0.4), (-126, 14, 16, 0.4), (-112, 13, 14, 0.4), (-104, 7, 9, 0.4)], rot, Vector((0, 0, 0)), 0)
    for sz in (-1, 1):
        loft(bm, [(-108, 3, 3, 0.4), (-99, 2.4, 2.4, 0.4), (-96, 1.4, 1.4, 0.4)], rot, Vector((0, 0, sz * 6)), 1)
    sphere(bm, 4.5, d * 101, mi=2, seg=12)
    greeble(bm, plane_surface(d * 140 + Vector((0, 0, 16)) + Vector((-d.y, d.x, 0)) * -12, -d * 26, Vector((-d.y, d.x, 0)) * 24), 0.25, 10 + k, size=(0.8, 3), height=(0.4, 1.6), mats=(0, 1), glow=2, glow_frac=0.08)
    antennas(bm, d * 132 + Vector((0, 0, 16)), (0, 0, 1), 3, 14, 30 + k, mi=1, light=3)
obj("Claws", bm, ["Hull", "HullDark", "Glow", "NavRed"])

bm = bmesh.new()
# meridian arches over the north pole, meeting in a crown; a drill at the south pole
for k in range(4):
    a = k * math.pi / 2
    pts = [Vector((math.cos(a) * 123 * math.cos(t), math.sin(a) * 123 * math.cos(t), 123 * math.sin(t))) for t in [i * (math.pi / 2 - 0.12) / 12 for i in range(13)]]
    for p0, p1 in zip(pts, pts[1:]):
        _rod(bm, p0, p1, 2.6, 0)
    for p in pts[2::3]:
        sphere(bm, 1.2, p * 1.03, mi=2, seg=6)
    for p in pts[3::4]:
        loft(bm, [(-7, 4, 4, 0.45), (-3, 7, 7, 0.45), (3, 7, 7, 0.45), (7, 4, 4, 0.45)], basis(p), p, 0)
        band(bm, p, p, 5.5, 1.2, 1.4, 2, 16)
n0 = set(bm.verts)
solid_ring(bm, 12, 22, 118, 126, 32, mats=(0, 1, 0, 0))
cyl(bm, 6, 2, 30, (0, 0, 138), seg=12, mi=1)
antennas(bm, (0, 0, 153), (0, 0, 1), 4, 20, 9, mi=1, light=3)
loft(bm, [(-150, 3, 3, 0.4), (-124, 10, 10, 0.4), (-108, 7, 7, 0.4), (-101, 2, 2, 0.4)], UPX, Vector((0, 0, 0)), 0)
sphere(bm, 3.5, (0, 0, -101), mi=2, seg=10)
obj("Crown", bm, ["Hull", "HullDark", "Glow", "NavRed"])


# ================================================================ v2 secondary-scale pass (asset overhaul, docs/ASSET_REFERENCES.md)
AM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar", "Rock"]
A_HULL, A_DARK, A_LIGHT, A_ACC, A_HAZ, A_WIN, A_WCOOL, A_STROBE, A_RAD, A_GLOW, A_RED, A_SOLAR, A_ROCK = range(len(AM))
M["Rock"] = mat("Rock", (0.32, 0.27, 0.22), 0.0, 0.9)
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
bm = bmesh.new()

# mining rigs on each claw, conveyors running up the arches to an orbital refinery above the crown
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    d = Vector((math.cos(a), math.sin(a), 0)); side = Vector((-d.y, d.x, 0))
    module(bm, d * 140 + Z_ * 26, side, Z_, 26, 12, 10, mats=(A_HULL, A_DARK, A_WIN), seed=140 + k)
    tank_cage(bm, d * 146 - Z_ * 18, 7, axis=side, capsule=1.0, mi_tank=A_DARK if k % 2 else A_LIGHT, mi_cage=A_DARK)
    pts = [d * 150 + Z_ * 20] + [Vector((math.cos(a) * 128 * math.cos(t), math.sin(a) * 128 * math.cos(t), 128 * math.sin(t))) for t in (0.35, 0.8, 1.2, 1.45)] + [Z_ * 170]
    pipe_run(bm, pts, 1.6, n=2, mi=A_DARK, clamp_mi=A_HAZ)
module(bm, Z_ * 190, X_, Z_, 70, 40, 26, mats=(A_LIGHT, A_DARK, A_WIN), seed=150, detail_scale=1.2)
module(bm, Z_ * 214 + X_ * 10, Y_, Z_, 40, 22, 18, mats=(A_HULL, A_DARK, A_WIN), seed=151)
for sx in (1, -1):
    radiator_bank(bm, Z_ * 190 + X_ * sx * 36, X_ * sx, Y_, 10, 40, 14, 4.4, mi_fin=A_RAD, mi_man=A_DARK)
    strobe(bm, Z_ * 190 + X_ * sx * 78, 1.2, A_RED)
dish(bm, Z_ * 226 - X_ * 16, Vector((0.3, 0.3, 1)), 8, mi_dish=A_LIGHT, mi_frame=A_DARK)
obj("Refinery", bm, AM)

result = export(col, root, "cracker")
