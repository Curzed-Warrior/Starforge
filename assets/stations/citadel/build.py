"""
Starforge Citadel: the final starbase tier. A disc some 700 m across: an armoured outer ring, two terraces
stepping up toward a central hub, open gaps between them crossed by radial bridges, a tall command spire with
two lesser spires, docking pylons reaching out from the rim, and a long keel beneath with an antenna farm.
Exterior asset. Run inside Blender:
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/citadel/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Citadel")

def tiered_ring(name, rin, rout, z0, z1, steps, seed, scale, lip=True, windows=2):
    """A heavy ring: hull, kitbash on every face, window bands, and a bevelled lip round the top edge."""
    bm = bmesh.new()
    c = solid_ring(bm, rin, rout, z0, z1, steps, mats=(0, 1, 0, 0))
    if lip:
        # a raked fairing along the outer face's upper edge
        solid_ring(bm, rout - c, rout + 2.5, z1 - c - 4, z1 - c + 1, steps, mats=(1, 1, 1, 1))
    # radial armour seams
    for k in range(steps // 4):
        a = k * 2 * math.pi / (steps // 4)
        solid_ring(bm, rin - 0.8, rout + 0.8, z0 - 0.8, z1 + 0.8, 1, mats=(1, 1, 1, 1), a0=a, a1=a + 1.2 / rout)
    obj(name + "_Hull", bm, ["Hull", "HullDark"], uv="cyl")
    bm = bmesh.new()
    detail(bm, cyl_surface(rout, z0 + c + 0.3, z1 - c - 4.3), seed, scale=scale, mats=(0, 1), window=2, glow=3, window_frac=0.04, glow_frac=0.01)
    detail(bm, cyl_surface(rin, z0 + c + 0.3, z1 - c - 0.3, inward=True), seed + 1, scale=scale, mats=(0, 1), window=2, window_frac=0.04)
    detail(bm, disc_surface(rin + c + 0.3, rout - c - 0.3, z1 + 0.02, up=True), seed + 2, scale=scale, mats=(0, 1), window=2, glow=3, window_frac=0.02, glow_frac=0.01)
    detail(bm, disc_surface(rin + c + 0.3, rout - c - 0.3, z0 - 0.02, up=False), seed + 3, scale=scale * 1.3, mats=(0, 1))
    obj(name + "_Greebles", bm, ["Hull", "HullDark", "Window", "Glow"])
    bm = bmesh.new()
    for k in range(windows):
        zc = (z0 + c) + (z1 - c - 5 - z0 - c) * (k + 1) / (windows + 1)
        window_rows(bm, cyl_surface(rout + 0.03, zc - 1.5, zc + 1.5), 2, int(rout * 3.2), size=(2.4, 0.9), mi=0, seed=seed + k, lit=0.7)
        window_rows(bm, cyl_surface(rin - 0.03, zc - 1.5, zc + 1.5, inward=True), 2, int(rin * 3.0), size=(2.4, 0.9), mi=1, seed=seed + 10 + k, lit=0.6)
    obj(name + "_Windows", bm, ["Window", "WindowCool"])

# ---------------------------------------------------------------- the rings and terraces
tiered_ring("Outer", 300, 345, -14, 14, 256, 10, 2.6, windows=3)
tiered_ring("Terrace", 205, 245, -10, 24, 192, 20, 2.3, windows=3)
tiered_ring("Inner", 110, 145, -8, 38, 160, 30, 2.0, windows=4)

# the hub: a stepped drum rising out of the inner terrace
HUB = [(0, 70), (20, 70), (28, 62), (60, 56), (60, 30), (75, 24), (75, -12), (60, -20), (45, -40), (25, -60), (12, -95), (6, -150), (0, -150)]
bm = bmesh.new()
revolve(bm, HUB, 96, lambda i, s: 1 if i in (2, 5, 8) else 0, closed=False, toward=lambda c: (radial(c) * 2 + Vector((0, 0, 1 if c.z > 0 else -1))).normalized())
obj("Hub_Hull", bm, ["Hull", "HullDark"], uv="cyl")
bm = bmesh.new()
detail(bm, cyl_surface(75, -11, 23), 40, scale=1.6, mats=(0, 1), window=2, glow=3, window_frac=0.05, glow_frac=0.02)
detail(bm, cyl_surface(60, 31, 55), 41, scale=1.6, mats=(0, 1), window=2, window_frac=0.05)
detail(bm, disc_surface(28, 59, 56.3, up=True), 42, scale=1.6, mats=(0, 1), glow=3, glow_frac=0.03)
greeble(bm, cone_surface(60, -20, 12, -95), 0.12, 43, size=(0.8, 3.0), height=(0.3, 1.5), mats=(0, 1), glow=3, glow_frac=0.03)
window_rows(bm, cyl_surface(75.03, -8, 20), 5, 300, size=(2.2, 0.8), mi=2, seed=44, lit=0.7)
window_rows(bm, cyl_surface(60.03, 34, 52), 4, 240, size=(2.0, 0.8), mi=2, seed=45, lit=0.7)
obj("Hub_Greebles", bm, ["Hull", "HullDark", "Window", "Glow"])

# ---------------------------------------------------------------- bridges across the gaps
bm = bmesh.new()
for k in range(8):
    a = k * math.pi / 4 + math.pi / 8
    rot = Rz(a)
    for (r0, r1, z, w, h) in ((145, 205, 6, 12, 9), (245, 300, 2, 14, 10)):
        mid = rot @ Vector(((r0 + r1) / 2, 0, z))
        loft(bm, [(r0 - 2, w / 2, h / 2, 0.45), (r1 + 2, w / 2, h / 2, 0.45)], rot.to_3x3(), Vector((0, 0, z)), 0)
        truss(bm, rot @ Vector((r0, 0, z - h / 2 - 3)), rot @ Vector((r1, 0, z - h / 2 - 3)), w=4, mi=1, rod=0.25)
    for (r0, r1) in ((75, 110),):
        loft(bm, [(r0 - 2, 8, 6, 0.45), (r1 + 2, 8, 6, 0.45)], rot.to_3x3(), Vector((0, 0, 12)), 0)
for k in range(8):
    a = k * math.pi / 4 + math.pi / 8
    for (r0, r1, z, w, h) in ((145, 205, 6, 12, 9), (245, 300, 2, 14, 10)):
        greeble(bm, plane_surface(Rz(a) @ Vector((r0, -w / 2, z + h / 2)), Rz(a) @ Vector((r1 - r0, 0, 0)), Rz(a) @ Vector((0, w, 0))), 0.12, 50 + k, size=(0.8, 2.6), height=(0.3, 1.2), mats=(0, 1))
obj("Bridges", bm, ["Hull", "HullDark"])

# ---------------------------------------------------------------- spires
bm = bmesh.new()
SP = [(14, 70), (14, 110), (10, 116), (10, 170), (7, 176), (7, 240), (4, 248), (4, 300), (1.5, 320), (0.4, 345)]
revolve(bm, SP, 32, lambda i, s: 1 if SP[i][0] != SP[i + 1][0] else 0, closed=False, toward=lambda c: (radial(c) + Vector((0, 0, 0.2))).normalized())
for (x, y, hgt, r) in ((-46, 22, 150, 6), (40, -30, 120, 5)):
    prof = [(r, 56), (r, 56 + hgt * 0.6), (r * 0.6, 56 + hgt * 0.65), (r * 0.6, 56 + hgt * 0.92), (0.3, 56 + hgt)]
    vs_before = set(bm.verts)
    revolve(bm, prof, 20, lambda i, s: 1 if i % 2 else 0, closed=False, toward=lambda c, x=x, y=y: Vector((c.x - x, c.y - y, 0.2)).normalized())
    bmesh.ops.translate(bm, vec=Vector((x, y, 0)), verts=[v for v in bm.verts if v not in vs_before])
obj("Spires", bm, ["Hull", "HullDark"], uv="cyl")
bm = bmesh.new()
for i in range(len(SP) - 1):
    (r0, z0), (r1, z1) = SP[i], SP[i + 1]
    if r0 == r1 and z1 - z0 > 20:
        detail(bm, cyl_surface(r0, z0 + 1, z1 - 1), 60 + i, scale=0.8, mats=(0, 1), window=2, window_frac=0.08)
        window_rows(bm, cyl_surface(r0 + 0.02, z0 + 2, z1 - 2), int((z1 - z0) / 6), int(r0 * 6), size=(0.9, 0.4), mi=2, seed=i, lit=0.6)
antennas(bm, (0, 0, 345), (0, 0, 1), 6, 30, 7, mi=1, light=3)
antennas(bm, (0, 0, -150), (0, 0, -1), 9, 60, 8, mi=1, light=3)
for (x, y, hgt) in ((-46, 22, 150), (40, -30, 120)):
    antennas(bm, (x, y, 56 + hgt), (0, 0, 1), 3, 14, int(hgt), mi=1, light=3)
obj("Spire_Detail", bm, ["Hull", "HullDark", "Window", "NavRed"])

# ---------------------------------------------------------------- docking pylons on the rim, with ships
bm = bmesh.new(); bms = bmesh.new()
for k in range(6):
    a = k * math.pi / 3 + 0.25
    rot = Rz(a)
    d = rot @ Vector((1, 0, 0))
    base = d * 345
    loft(bm, [(343, 6, 5, 0.45), (380, 5, 4, 0.45), (435, 4, 3.5, 0.45)], rot.to_3x3(), Vector((0, 0, 0)), 0)
    truss(bm, base + Vector((0, 0, -10)), base + d * 90 + Vector((0, 0, -10)), w=5, mi=1, rod=0.3)
    for j in range(3):
        c = base + d * (25 + j * 28)
        for sy in (-1, 1):
            arm = rot @ Vector((0, sy * 14, 0))
            _rod(bm, c, c + arm, 1.6, 1)                    # a docking boom out to the berth
            sphere(bm, 0.8, c + arm, mi=2 if sy > 0 else 3, seg=8)
            # a cruiser-class ship berthed alongside
            if (j + k) % 2 == 0:
                sc = 1.0 + 0.4 * ((j * 7 + k) % 3)
                sc_c = c + arm * 2.2
                ship(bms, sc_c, rot.to_3x3(), 34 * sc, (0, 1, 2, 3), seed=j * 10 + k)
    antennas(bm, base + d * 90, d, 3, 20, 70 + k, mi=1, light=3)
# spaceport: six great hangar bays in the outer wall, between the pylons
for k in range(6):
    a = k * math.pi / 3 + 0.25 + math.pi / 6
    d = Vector((math.cos(a), math.sin(a), 0))
    bay = hangar(bm, d * 345, d, (0, 0, 1), 34.0, 14.0, 38.0, mats=(0, 1, 4, 5, 6), ship_len=30.0, seed=90 + k)
    lamp(f"L_Bay{k}", bay, 6e4, COOL, 4.0)
obj("Pylons", bm, ["Hull", "HullDark", "NavGreen", "NavRed", "LightCool", "NavWhite", "Glow"])
obj("BerthedShips", bms, ["Hull", "HullDark", "Glow", "Window"])

# ---------------------------------------------------------------- lights that wash the hull
lamp("Flood_Spire", (90, -60, 40), 2e6, (1.0, 0.9, 0.75), 4, "SPOT", aim=(0, 0, 200), spot=0.5)


# ================================================================ v2 starbase pass (asset overhaul, docs/ASSET_REFERENCES.md)
AM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar"]
A_HULL, A_DARK, A_LIGHT, A_ACC, A_HAZ, A_WIN, A_WCOOL, A_STROBE, A_RAD, A_GLOW, A_RED, A_SOLAR = range(len(AM))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
restyle(col)
bm = bmesh.new()

# warm strips round every terrace, strobes on the rim, radiator banks and tank farms slung under the outer ring
for (R, z0, z1, n) in ((345, -14, 14, 420), (245, -10, 24, 300), (145, -8, 38, 200)):
    strip_lights(bm, R + 0.4, z0 + (z1 - z0) * 0.72, n, A_GLOW, seed=R, h=1.0, depth=0.4)
    strip_lights(bm, R + 0.4, z0 + (z1 - z0) * 0.3, n, A_WIN, seed=R + 1, h=0.7, depth=0.4, keep=0.45)
for k in range(24):
    a = k * math.pi / 12 + 0.13
    d = Vector((math.cos(a), math.sin(a), 0))
    strobe(bm, d * 347 + Z_ * 15, 1.6, A_RED if k % 3 == 0 else A_STROBE)
for k in range(8):
    a = k * math.pi / 4 + math.pi / 8
    d = Vector((math.cos(a), math.sin(a), 0)); side = Vector((-d.y, d.x, 0))
    radiator_bank(bm, d * 322 + Z_ * -15, -Z_, side, 8, 30, 12, 5, mi_fin=A_RAD, mi_man=A_DARK)
    for s in (-1, 1): tank_cage(bm, d * 322 + side * s * 34 + Z_ * -24, 8, axis=side, capsule=1.2, mi_tank=A_HULL, mi_cage=A_DARK)
obj("Fittings2", bm, AM)

result = export(col, root, "citadel")
