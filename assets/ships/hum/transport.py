"""
Transport, 300 m. INDUSTRIAL family (civilian). References (assets/ships/refs/transport_ref*): the MISC Hull C's
slim spine with cargo arms folded out from it in a cross, containers stacked along every arm, a rounded crew
section at the nose and a drive block at the tail; the Hull series' longer variants with more arm sets. Gunmetal
spine, tan containers, the navy's drives and lights. Crew ~40.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/transport.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipTransport")
sc = hull_scale(300)
bm = bmesh.new()
rnd = random.Random(2000)
# --- the spine: a long slim box truss-plated, carrying the cargo arms
keel(bm, [(-100, 7, 7, 0), (70, 7, 7, 0)], GUN, 0.3)
for s in (1, -1):
    ribs(bm, -96, 66, s * 7.1, -5, 5, s, 3, depth=0.4, seed=2001 + s, gap=0.3, mi=(DARK, GUN))
# --- the crew section: a rounded, faceted nose block with a glazed brow (Hull C)
keel(bm, [(70, 12, 11, 0), (80, 17, 15, 1), (112, 17, 15, 1), (132, 13, 11, 0), (146, 6, 5, -1), (150, 2, 2, -1)], LIGHT, 0.42)
keel(bm, [(134, 12.6, 9.0, 2.0), (143, 7.0, 5.0, 1.0)], DARK, 0.42)                                         # the brow glazing
for s in (1, -1):
    windows_side(bm, 84, 128, s * 17.4, -6, 3, s, seed=2004 + s, lit=0.35, spacing=2.6)
    fast_box(bm, (40, 1.2, 0.3), Vector((104, s * 17.2, 8)), flank(s), ACC, UVL(bm))                         # accent stripe
    airlock(bm, 96, s * 17.2, -10, s)
bridge(bm, 120, 16, 10, 2.5, 8)
for s in (1, -1):
    cyl(bm, 2.4, 2.4, 1.2, Vector((140.5, s * 6.5, -5)), Ry(math.pi / 2), seg=16, mi=DARK)                  # the twin lamps under the brow (Hull C)
    cyl(bm, 1.7, 1.7, 0.6, Vector((141.2, s * 6.5, -5)), Ry(math.pi / 2), seg=16, mi=DRIVE)
cyl(bm, 0.4, 0.25, 14, Vector((100, 0, 23)), seg=8, mi=DARK)
# --- the cargo arms: four in a cross down the cargo section, containers packed solid along them (Hull C)
for xs in (-88, -20, 48):
    band(bm, Vector((xs, 0, 0)), (1, 0, 0), 7.4, 6, 2.0, mi=ACC, seg=8)                                     # arm collars
for k in range(4):
    a = math.pi / 4 + k * math.pi / 2
    u = Vector((0, math.cos(a), math.sin(a))); t = Vector((0, -math.sin(a), math.cos(a)))
    R = Matrix((Vector((1, 0, 0)), -t, u)).transposed()                                            # right-handed: x * y = z
    fast_box(bm, (144, 2.4, 36), Vector((-20, 0, 0)) + u * 22, R, TAN, UVL(bm))                             # the arm, full length
    for i in range(7):
        for j in range(3):
            if j == 2 and i in (0, 6): continue                                                              # stepped ends (Hull C's diamond)
            c = Vector((-83 + i * 21, 0, 0)) + u * (12.5 + j * 9.6)
            r = rnd.random()
            mi = TAN if r < 0.25 else (HULL if r < 0.65 else (GUN if r < 0.92 else ACC))
            for side in (-1, 1):
                fast_box(bm, (20, 8.8, 9.0), c + t * side * 5.4, R, mi, UVL(bm), bottom=True)
for s in (1, -1):
    fast_box(bm, (200, 0.6, 0.3), Vector((-15, s * 7.2, 0)), flank(s), WIN, UVL(bm))                       # spine running lights
# --- the tail: a drive block, four drives, fins
keel(bm, [(-130, 10, 10, 0), (-118, 14, 13, 0), (-100, 14, 13, 0), (-94, 10, 10, 0)], HULL, 0.35)
for y, z in ((-6, 6), (6, 6), (-6, -6), (6, -6)):
    engine(bm, (-132, y, z), 4.6, 14)
for s in (1, -1):
    plate(bm, [(-100, s * 2), (-100, s * 3), (-124, s * 22), (-128, s * 20)] if s > 0 else [(-100, s * 2), (-128, s * 20), (-124, s * 22), (-100, s * 3)],
          -0.6, 1.2, GUN, 0.2)
nav(bm, [(-126, 22, 0), (150, 2, 0)], [(-126, -22, 0), (150, -2, 0)], [(100, 0, 30), (-110, 0, 14)])
o = obj("transport", bm, SM)
result = export_ship(col, root, "Transport")
