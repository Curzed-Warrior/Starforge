"""
Starforge Starport: the second starbase tier. A needle spire some 430 m tall, threaded through two stacked
habitat rings, with blade-like radiator fins, docking arms with moored ships, and antenna farms at both tips.
Exterior asset (interiors are left for a later pass). Run inside Blender:
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/starport/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Starport")

# ---------------------------------------------------------------- the spire
# a stepped profile: collars, drums and tapers, so the silhouette reads in segments like the references
PROF = [(0.6, 215), (2.0, 200), (2.5, 170), (4.5, 160), (4.5, 140), (7.0, 132), (7.0, 112), (9.5, 104), (9.5, 84),
        (13.0, 78), (13.0, 58), (16.0, 52), (16.0, 20), (19.0, 14), (19.0, -14), (16.0, -20), (16.0, -48),
        (12.0, -54), (12.0, -80), (9.0, -86), (9.0, -120), (6.0, -128), (6.0, -160), (3.5, -168), (2.5, -200), (0.6, -218)]
bm = bmesh.new()
revolve(bm, PROF, 64, lambda i, s: 1 if (PROF[i][0] != PROF[i + 1][0] if i + 1 < len(PROF) else False) else 0, closed=False,
        toward=lambda c: radial(c) + Vector((0, 0, 0.0)))
obj("Spire_Hull", bm, ["Hull", "HullDark"], smooth=False, uv="cyl")

bm = bmesh.new()
for i in range(len(PROF) - 1):
    (r0, z0), (r1, z1) = PROF[i], PROF[i + 1]
    if abs(r0 - r1) < 1e-3 and abs(z1 - z0) > 6:      # vertical drums get the full kitbash
        detail(bm, cyl_surface(r0, min(z0, z1) + 0.6, max(z0, z1) - 0.6), 100 + i, mats=(0, 1), window=2, glow=3, window_frac=0.04, glow_frac=0.01)
    elif abs(z1 - z0) > 2:
        greeble(bm, cone_surface(r1, z1, r0, z0) if z0 < z1 else cone_surface(r0, z0, r1, z1), 0.25, 300 + i, size=(0.3, 1.2), height=(0.1, 0.4), mats=(0, 1))
obj("Spire_Greebles", bm, ["Hull", "HullDark", "Window", "Glow"])

bm = bmesh.new()
# window rings round every drum, and a vertical light strip running the spire's length on four sides
for i in range(len(PROF) - 1):
    (r0, z0), (r1, z1) = PROF[i], PROF[i + 1]
    if abs(r0 - r1) < 1e-3 and abs(z1 - z0) > 6:
        window_rows(bm, cyl_surface(r0 + 0.02, min(z0, z1), max(z0, z1)), 3 if r0 > 10 else 2, int(r0 * 9), size=(1.1, 0.45), mi=0, seed=i, lit=0.65)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    for (z0, z1, r) in ((-150, -90, 6.6), (-80, -54, 12.6), (20, 50, 16.6), (84, 104, 9.6)):
        box(bm, (0.25, 0.4, z1 - z0), (r * math.cos(a), r * math.sin(a), (z0 + z1) / 2), Rz(a), mi=1)
obj("Spire_Windows", bm, ["Window", "WindowCool"])

# ---------------------------------------------------------------- blade fins (radiators and sensor vanes)
bm = bmesh.new()
for (z0, z1, reach, a) in ((-205, -95, 26, 0.0), (-205, -95, 26, math.pi), (95, 190, 20, math.pi / 2), (95, 190, 20, -math.pi / 2)):
    rot = Rz(a)
    # a tapered blade: a thin slab whose outer edge swells out mid-way and sweeps back to the tips
    zm = (z0 + z1) / 2
    outline = [(5, z0), (5 + reach * 0.35, z0 + 4), (5 + reach, zm - (z1 - z0) * 0.1), (5 + reach, zm + (z1 - z0) * 0.1), (5 + reach * 0.35, z1 - 4), (5, z1)]
    for side in (-1, 1):
        vs = [bm.verts.new(rot @ Vector((x, side * 0.35, z))) for x, z in outline]
        f = bm.faces.new(vs if side > 0 else list(reversed(vs)))
        f.material_index = 0
    # the blade's edges, closing the slab, and a dark spar along its leading edge
    for i in range(len(outline) - 1):
        (x0, za), (x1, zb) = outline[i], outline[i + 1]
        q = [bm.verts.new(rot @ Vector((x, sd * 0.35, z))) for (x, z, sd) in ((x0, za, -1), (x1, zb, -1), (x1, zb, 1), (x0, za, 1))]
        bm.faces.new(q).material_index = 1
    box(bm, (1.2, 1.2, (z1 - z0) * 0.2), rot @ Vector((5 + reach - 0.6, 0, zm)), rot, mi=1)
    for j in range(9):
        z = z0 + (z1 - z0) * (j + 1) / 10
        box(bm, (reach * 0.5, 0.9, 0.35), rot @ Vector((5 + reach * 0.25, 0, z)), rot, mi=1)
    detail(bm, (lambda rot=rot, z0=z0, z1=z1, reach=reach: _fin_surface(rot, z0, z1, reach))(), 500 + int(z0), scale=1.2, mats=(0, 1), window=2, window_frac=0.05)
obj("Fins", bm, ["Hull", "HullDark", "WindowCool"])

# ---------------------------------------------------------------- the great ring and its spokes
RA, WA, HA, ZA = 150.0, 16.0, 11.0, 0.0
bm = bmesh.new()
CA = solid_ring(bm, RA - WA / 2, RA + WA / 2, ZA - HA / 2, ZA + HA / 2, 192, mats=(0, 1, 0, 0))
# a lip round the outer edge and a recessed window gallery on the inner face
# a fairing round the waist of the outer face
solid_ring(bm, RA + WA / 2 - 0.5, RA + WA / 2 + 1.4, ZA - 1.2, ZA + 1.2, 192, mats=(1, 1, 1, 1))
obj("RingA_Hull", bm, ["Hull", "HullDark"], uv="cyl")
bm = bmesh.new()
for (za, zb) in ((ZA - HA / 2 + CA + 0.2, ZA - 1.4), (ZA + 1.4, ZA + HA / 2 - CA - 0.2)):
    detail(bm, cyl_surface(RA + WA / 2, za, zb), 21 + int(za), scale=1.6, mats=(0, 1), window=2, glow=3, window_frac=0.03, glow_frac=0.01)
for sz in (-1, 1):
    detail(bm, disc_surface(RA - WA / 2 + CA + 0.2, RA + WA / 2 - CA - 0.2, ZA + sz * HA / 2, up=sz > 0), 22 + sz, scale=1.6, mats=(0, 1), window=2, window_frac=0.02)
detail(bm, cyl_surface(RA - WA / 2, ZA - HA / 2 + CA + 0.2, ZA + HA / 2 - CA - 0.2, inward=True), 25, scale=1.6, mats=(0, 1))
obj("RingA_Greebles", bm, ["Hull", "HullDark", "Window", "Glow"])
bm = bmesh.new()
window_rows(bm, cyl_surface(RA + WA / 2 + 1.42, ZA - 1.0, ZA + 1.0), 2, 900, size=(1.6, 0.6), mi=0, seed=3, lit=0.7)
window_rows(bm, cyl_surface(RA - WA / 2 - 0.02, ZA - 3, ZA + 3, inward=True), 2, 700, size=(1.6, 0.6), mi=1, seed=4, lit=0.6)
obj("RingA_Windows", bm, ["Window", "WindowCool"])

bm = bmesh.new()
for k in range(3):
    a = k * 2 * math.pi / 3 + 0.3
    d = Vector((math.cos(a), math.sin(a), 0))
    # a pressurised tube riding along a truss, from the spire's hub drum to the ring
    tunnel(bm, (RA - WA / 2 - 19.0, 5.0, 4.0), 0.6, d * (19.0 + (RA - WA / 2 - 19.0) / 2), Rz(a), mi_out=0, mi_in=1)
    truss(bm, d * 19.0 + Vector((0, 0, -6)), d * (RA - WA / 2) + Vector((0, 0, -6)), w=3.0, mi=2, rod=0.18)
    truss(bm, d * 19.0 + Vector((0, 0, 6)), d * (RA - WA / 2) + Vector((0, 0, 6)), w=2.0, mi=2, rod=0.14)
    greeble(bm, _tube_top(a, 19.0, RA - WA / 2, 2.6), 0.4, 70 + k, size=(0.3, 1.2), height=(0.1, 0.5), mats=(0, 2))
obj("RingA_Spokes", bm, ["Hull", "Wall", "HullDark"])

# ---------------------------------------------------------------- the upper ring: a flat wheel
RB, WB, HB, ZB = 82.0, 20.0, 5.0, 72.0
bm = bmesh.new()
CB = solid_ring(bm, RB - WB / 2, RB + WB / 2, ZB - HB / 2, ZB + HB / 2, 128, mats=(0, 1, 0, 0))
solid_ring(bm, RB - WB / 2 - 3, RB - WB / 2, ZB - 1.0, ZB + 1.0, 128, mats=(1, 1, 1, 1))
for k in range(6):
    a = k * math.pi / 3
    loft(bm, [(13.0, 1.6, 1.3, 0.4), (RB - WB / 2 + 1, 1.6, 1.3, 0.4)], Rz(a).to_3x3(), Vector((0, 0, ZB)), 1)
obj("RingB_Hull", bm, ["Hull", "HullDark"], uv="cyl")
bm = bmesh.new()
for sz in (-1, 1):
    detail(bm, disc_surface(RB - WB / 2 + CB + 0.2, RB + WB / 2 - CB - 0.2, ZB + sz * HB / 2, up=sz > 0), 80 + sz, scale=1.3, mats=(0, 1), window=2, glow=3, window_frac=0.03, glow_frac=0.01)
detail(bm, cyl_surface(RB + WB / 2, ZB - HB / 2 + CB + 0.1, ZB + HB / 2 - CB - 0.1), 83, scale=1.0, mats=(0, 1), window=2, window_frac=0.05)
obj("RingB_Greebles", bm, ["Hull", "HullDark", "Window", "Glow"])
bm = bmesh.new()
# skylights: the wheel's upper face carries long glazed strips over parkland
for k in range(24):
    a0 = k * 2 * math.pi / 24 + 0.04
    revolve(bm, [(RB - 4, ZB + HB / 2 + 0.05), (RB + 4, ZB + HB / 2 + 0.05)], 6, closed=False, toward=lambda c: Vector((0, 0, 1)), a0=a0, a1=a0 + 2 * math.pi / 24 - 0.08)
obj("RingB_Skylights", bm, ["WindowCool"])

# ---------------------------------------------------------------- docking arms and moored ships
bm = bmesh.new()
bms = bmesh.new()
for k, (z, a, L) in enumerate(((-30, 0.9, 46), (-30, 0.9 + math.pi, 46), (40, 2.5, 38), (40, 2.5 + math.pi, 38), (-100, 1.7, 30), (-100, 1.7 + math.pi, 30))):
    rot = Rz(a)
    d = rot @ Vector((1, 0, 0))
    r0 = 14.0 if abs(z) < 60 else 8.0
    loft(bm, [(r0 - 1, 1.8, 1.8, 0.4), (r0 + L, 1.8, 1.8, 0.4)], rot.to_3x3(), Vector((0, 0, z)), 0)
    truss(bm, d * r0 + Vector((0, 0, z - 4)), d * (r0 + L) + Vector((0, 0, z - 4)), w=1.6, mi=1, rod=0.1)
    cyl(bm, 4.0, 4.0, 2.0, d * (r0 + L + 1) + Vector((0, 0, z)), rot @ Ry(math.pi / 2), seg=20, mi=1)
    for j in range(8):
        b = j * math.pi / 4
        sphere(bm, 0.3, d * (r0 + L + 2.1) + rot @ Vector((0, 4.3 * math.cos(b), 4.3 * math.sin(b))) + Vector((0, 0, z)), mi=2 if k % 2 else 3, seg=6)
    # a ship moored nose-in: hull, engines, glowing drive bells
    s = 1.0 + 0.5 * (k % 3)
    c = d * (r0 + L + 2 + 16 * s) + Vector((0, 0, z))
    ship(bms, c, Rz(a + math.pi).to_3x3(), 34 * s, (0, 1, 2, 3), seed=k)
# spaceport: three hangar bays in the waist drum, between the ring's spokes
for k in range(3):
    a = k * 2 * math.pi / 3 + 0.3 + math.pi / 3
    d = Vector((math.cos(a), math.sin(a), 0))
    bay = hangar(bm, d * 19.0, d, (0, 0, 1), 11.0, 7.0, 13.0, mats=(0, 1, 4, 5, 6), seed=40 + k)
    lamp(f"L_Bay{k}", bay, 4000, COOL, 2.0)
obj("Docks", bm, ["Hull", "HullDark", "NavGreen", "NavRed", "LightCool", "NavWhite", "Glow"])
obj("Ships", bms, ["Hull", "HullDark", "Glow", "Window"])

# ---------------------------------------------------------------- antenna farms and navigation
bm = bmesh.new()
antennas(bm, (0, 0, 214), (0, 0, 1), 9, 30.0, 1, mi=0, light=1)
antennas(bm, (0, 0, -216), (0, 0, -1), 6, 22.0, 2, mi=0, light=1)
for k in range(6):
    a = k * math.pi / 3 + 0.2
    antennas(bm, Rz(a) @ Vector((RA, 0, ZA + HA / 2)), (0, 0, 1), 3, 8.0, 20 + k, mi=0, light=1)
for z in (198, 158, 130, 102, 76, -84, -126, -166, -198):
    r = next(p[0] for p in PROF if abs(p[1] - z) < 4)
    for k in range(4):
        a = k * math.pi / 2
        sphere(bm, 0.5, ((r + 0.4) * math.cos(a), (r + 0.4) * math.sin(a), z), mi=2, seg=8)
obj("Antennas", bm, ["HullDark", "NavRed", "NavWhite"])

# ---------------------------------------------------------------- floodlights that wash the hull
lamp("Flood_Ring", (0, 0, 30), 2e6, (0.75, 0.85, 1.0), 4, "SPOT", aim=(RA, 0, 0), spot=2.6)
for k in range(6):
    a = k * math.pi / 3
    lamp(f"Flood_Spire{k}", Rz(a) @ Vector((RA - 20, 0, 14)), 4e5, (1.0, 0.9, 0.75), 2, "SPOT", aim=(0, 0, 60), spot=0.5)


# ================================================================ v2 starbase pass (asset overhaul, docs/ASSET_REFERENCES.md)
AM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar"]
A_HULL, A_DARK, A_LIGHT, A_ACC, A_HAZ, A_WIN, A_WCOOL, A_STROBE, A_RAD, A_GLOW, A_RED, A_SOLAR = range(len(AM))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
restyle(col)
bm = bmesh.new()

# warm strips round both rings, strobes, radiator blades and tanks on the spire, two more ships tied up at the big ring
strip_lights(bm, RA + WA / 2, ZA + HA * 0.28, 260, A_GLOW, seed=1, h=0.8)
strip_lights(bm, RA + WA / 2, ZA - HA * 0.28, 260, A_WIN, seed=2, h=0.6, keep=0.5)
strip_lights(bm, RB + WB / 2, ZB, 160, A_GLOW, seed=3, h=0.6)
for k in range(12):
    a = k * math.pi / 6
    d = Vector((math.cos(a), math.sin(a), 0))
    strobe(bm, d * (RA + WA / 2 + 0.8) + Z_ * (ZA + HA / 2 + 0.6), 0.9, A_RED if k % 3 == 0 else A_STROBE)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    d = Vector((math.cos(a), math.sin(a), 0))
    radiator_bank(bm, d * 12 + Z_ * -66, d, Z_, 6, 22, 9, 2.6, mi_fin=A_RAD, mi_man=A_DARK)
    tank_cage(bm, d * 15 + Z_ * -102, 5.0, axis=Z_, capsule=1.2, mi_tank=A_HULL, mi_cage=A_DARK)
for k, a in enumerate((0.9, 0.9 + math.pi)):
    d = Vector((math.cos(a), math.sin(a), 0))
    dock_arm(bm, d * (RA + WA / 2), d, 26, 36, mats=(A_HULL, A_DARK, A_GLOW, A_WIN), seed=210 + k)
obj("Fittings2", bm, AM)

result = export(col, root, "starport")
