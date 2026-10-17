"""
Memory Spire: every mind that ever lived here, held in a double helix of light around a needle 600 m tall,
on a ringed base, with archive vaults stacked up the needle. Z is up (the game spins it about this axis).
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/memory/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Memory")
M["Mind"] = mat("Mind", (1.0, 0.8, 1.0), emit=(1.0, 0.75, 1.0), strength=10)
M["Mind2"] = mat("Mind2", (0.7, 0.8, 1.0), emit=(0.6, 0.75, 1.0), strength=10)

H = 300.0
bm = bmesh.new()
revolve(bm, [(0, -H - 40), (6, -H - 30), (14, -H), (10, -H + 40), (8, H - 60), (5, H - 20), (1.5, H + 30), (0, H + 50)], 24,
        lambda i, s: 1 if i in (0, 6) else 0, closed=False, toward=lambda c: (radial(c) + Vector((0, 0, 0.05))).normalized())
# archive vaults: chamfered drums stacked up the needle
for k in range(9):
    z = -H + 70 + k * 60
    n0 = set(bm.verts)
    c = solid_ring(bm, 8, 20 - k * 0.8, -6, 6, 24, mats=(0, 1, 0, 0))
    bmesh.ops.translate(bm, vec=Vector((0, 0, z)), verts=since(bm, n0))
    window_rows(bm, cyl_surface(20 - k * 0.8 + 0.03, z - 2, z + 2), 1, 36, size=(1.5, 0.8), mi=2, seed=k, lit=0.8)
# the base: three stepped rings with buttress arms
for k, (r, z) in enumerate(((60, -H - 6), (44, -H + 8), (30, -H + 20))):
    n0 = set(bm.verts)
    solid_ring(bm, r - 10, r, -4, 4, 48, mats=(0, 1, 0, 0))
    bmesh.ops.translate(bm, vec=Vector((0, 0, z)), verts=since(bm, n0))
for k in range(6):
    a = k * math.pi / 3
    _rod(bm, Rz(a) @ Vector((58, 0, -H - 6)), Vector((0, 0, -H + 90)), 2.2, 1)
detail(bm, disc_surface(52, 58, -H - 2, up=True), 3, scale=1.0, mats=(0, 1), window=2, window_frac=0.1)
antennas(bm, (0, 0, H + 50), (0, 0, 1), 4, 40, 9, mi=1, light=3)
obj("Spire", bm, ["Hull", "HullDark", "Window", "NavRed"], uv="cyl")

bm = bmesh.new()
# the helix: two strands of glowing beads, with rungs every few turns
N = 160
for strand in range(2):
    for i in range(N):
        u = i / N; a = u * 2 * math.pi * 5 + strand * math.pi
        r = 44 - 20 * u
        sphere(bm, 3.0 - 1.2 * u, (math.cos(a) * r, math.sin(a) * r, -H + 40 + u * (2 * H - 60)), mi=strand, seg=8)
for i in range(0, N, 8):
    u = i / N; a = u * 2 * math.pi * 5
    r = 44 - 20 * u; z = -H + 40 + u * (2 * H - 60)
    _rod(bm, Vector((math.cos(a) * r, math.sin(a) * r, z)), Vector((-math.cos(a) * r, -math.sin(a) * r, z)), 0.5, 2)
obj("Helix", bm, ["Mind", "Mind2", "HullDark"])


# ================================================================ v2 secondary-scale pass (asset overhaul, docs/ASSET_REFERENCES.md)
AM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar", "Rock"]
A_HULL, A_DARK, A_LIGHT, A_ACC, A_HAZ, A_WIN, A_WCOOL, A_STROBE, A_RAD, A_GLOW, A_RED, A_SOLAR, A_ROCK = range(len(AM))
M["Rock"] = mat("Rock", (0.32, 0.27, 0.22), 0.0, 0.9)
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
bm = bmesh.new()

# a base station round the foot of the spire, data pylons standing in a ring, a pilgrim ship docked
for k in range(8):
    a = k * math.pi / 4
    d = Vector((math.cos(a), math.sin(a), 0))
    module(bm, d * 80 + Z_ * (-H - 6), Vector((-d.y, d.x, 0)), Z_, 34, 14, 12, mats=(A_HULL if k % 2 else A_LIGHT, A_DARK, A_WIN), seed=120 + k)
    p = d * 112 + Z_ * (-H)
    loft(bm, [(0, 4, 4, 0.4), (90, 2.4, 2.4, 0.4), (110, 0.8, 0.8, 0.4)], UPX, p, A_DARK)
    for j in range(5): sphere(bm, 1.2, p + Z_ * (16 + j * 18) + d * 2.6, mi=A_GLOW if j % 2 else A_WCOOL, seg=6)
    strobe(bm, p + Z_ * 111, 0.9, A_STROBE)
    if k % 2 == 0: radiator_bank(bm, d * 80 + Z_ * (-H - 16), -Z_, Vector((-d.y, d.x, 0)), 6, 16, 8, 4, mi_fin=A_RAD, mi_man=A_DARK)
dock_arm(bm, Vector((60, 0, -H + 10)), Vector((1, 0, 0.3)), 50, 40, mats=(A_LIGHT, A_DARK, A_GLOW, A_WIN), seed=131)
obj("Base", bm, AM)

result = export(col, root, "memory")
