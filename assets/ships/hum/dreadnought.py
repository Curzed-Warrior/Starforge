"""
Dreadnought, 50 km. BRICK family (NSC3). References (assets/ships/refs/dreadnought_ref*): the NSC3 dreadnought's long
slab, broad aft and narrowing to the bow, with a raised armoured ridge down its back crowned by rows of turret domes
and lit trim along its edges; the Executor's city-like dorsal surface, bridge tower set far aft and wall of drives.
Plated, ribbed and windowed like the battleship and titan. Crew ~5,000,000.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/dreadnought.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipDreadnought")
sc = hull_scale(50000)
bm = bmesh.new()
# --- the slab: broad aft, narrowing to the bow (NSC3 dreadnought / Executor), a deeper keel below
keel(bm, [(-25000, 4200, 1500, 0), (-23500, 4600, 1700, 0), (-2000, 4000, 1600, 0), (12000, 2800, 1300, 0), (21000, 1300, 800, 0), (25000, 300, 240, 0)], GUN, 0.2)
keel(bm, [(-22000, 3000, 700, -1900), (8000, 2200, 600, -1800), (16000, 900, 300, -1500)], HULL, 0.3)
# --- the armoured ridge down the back, stepped in three tiers (NSC3)
plate(bm, mirror([(16000, 0), (12000, 1700), (-22000, 2800), (-23000, 0)]), 1500, 700, HULL, 80.0)
plate(bm, mirror([(9000, 0), (6000, 1100), (-20000, 1700), (-21000, 0)]), 2200, 600, GUN, 60.0, slope=(-200, 0))
plate(bm, mirror([(-6000, 0), (-7000, 700), (-18000, 1000), (-18600, 0)]), 2800, 500, HULL, 40.0)
for z0, x0, x1, hw in ((2200, -22800, 15800, 2600), (2800, -20800, 8800, 1600)):
    strips(bm, x0 + 300, x1 - 300, -hw * 0.8, hw * 0.8, z0 - 2, 18, h=60, seed=2200 + z0, mi=GUN)
city = plane_surface((-20500, -1500, 2800.1), (26000, 0, 0), (0, 3000, 0))
greeble(bm, city, 0.000003, 2210, size=(300, 1100), height=(80, 400), mats=(GUN, HULL), window=WIN, window_frac=0.3, stack=0.45, pipes=0.05, dark=0.3)
greeble(bm, plane_surface((-22000, -2600, 2200.1), (30000, 0, 0), (0, 5200, 0)), 0.0000025, 2211, size=(200, 700), height=(60, 260),
        mats=(HULL, DARK), window=WIN, window_frac=0.08, stack=0.3, pipes=0.1, dark=0.4)
for s in (1, -1):
    edge_windows(bm, (15900, s * 1720), (-22900, s * 2820), 1560, 40, (0, s), lit=0.12, seed=2212 + s, strip=(14, 140))
    edge_windows(bm, (8900, s * 1120), (-20900, s * 1720), 2260, 36, (0, s), lit=0.18, seed=2214 + s, strip=(12, 120))
# --- turret domes along the ridge (NSC3)
for k in range(12):
    x = -19500 + k * 2600
    for s in (1, -1):
        y = s * (2100 if x < 6000 else 1500)
        cyl(bm, 950, 1100, 300, Vector((x, y, 2340 if x < 9000 else 1650)), seg=24, mi=HULL)
        turret(bm, (x, y, 2490 if x < 9000 else 1800), 2300)
for k in range(5):
    x = -16500 + k * 2200
    cyl(bm, 700, 800, 240, Vector((x, 0, 3420)), seg=24, mi=HULL)
    turret(bm, (x, 0, 3540), 1700)
# --- the bridge tower, set far aft (Executor)
plate(bm, mirror([(-19000, 0), (-19400, 700), (-21000, 800), (-21300, 0)]), 3300, 900, LIGHT, 40.0, slope=(-150, 0))
bridge(bm, -19100, 4200, 1400, 120.0, 800)
for x, h in ((-20200, 1600), (-20800, 1200)):
    cyl(bm, 40, 24, h, Vector((x, 0, 4200 + h / 2)), seg=8, mi=DARK)
# --- the flanks: slatted belts, lit trim along the edges (NSC3), window bands
for s in (1, -1):
    ribs(bm, -22800, -2500, s * 4000.5, -1100, 1100, s, 400, depth=60, seed=2220 + s, gap=0.15, mi=(GUN, HULL))
    fast_box(bm, (34000, 60, 30), Vector((-6000, s * 4030, 1180)), Rz(s * -0.035).to_3x3() @ flank(s), ACC, UVL(bm))   # trim
    edge_windows(bm, (-22800, s * 4060), (-2600, s * 4060), -200, 40, (0, s), lit=0.2, seed=2222 + s, strip=(16, 160))
# segmented armour sponsons down the whole flank, following the taper, trimmed in the accent colour (NSC3)
SEC = [(-25000, 4200), (-23500, 4600), (-2000, 4000), (12000, 2800), (21000, 1300)]
def hw(x):
    for (xa, wa), (xb, wb) in zip(SEC, SEC[1:]):
        if xa <= x <= xb: return wa + (wb - wa) * (x - xa) / (xb - xa)
    return SEC[-1][1]
for i in range(14):
    x0 = -22600 + i * 2900; x1 = x0 + 2500
    if x1 > 19500: break
    w = min(hw(x0), hw(x1)) - 120
    for s in (1, -1):
        y0, y1 = sorted((s * w, s * (w + 260)))
        plate(bm, rect(x0, x1, y0, y1), -900, 1700, HULL if i % 2 else GUN, 50.0)
        fast_box(bm, (2500, 40, 40), Vector(((x0 + x1) / 2, s * (w + 262), 760)), flank(s), ACC, UVL(bm))
        edge_windows(bm, (x0 + 200, s * (w + 266)), (x1 - 200, s * (w + 266)), -700, 14, (0, s), lit=0.2, seed=2240 + i * 2 + s, strip=(10, 90))
# --- the underside: plating and lit hangar bays
strips(bm, -21500, 7500, -2700, 2700, -2600, 18, h=60, seed=2230, mi=GUN, down=True)
for x in (-18000, -12000, -6000, 0, 5000):
    fast_box(bm, (3000, 2400, 30), Vector((x, 0, -2630)), DOWN, DARK, UVL(bm))
    for s in (1, -1):
        fast_box(bm, (3000, 50, 40), Vector((x, s * 1220, -2640)), DOWN, WIN, UVL(bm))
# stepped armour decks down the narrowing bow, each with machinery and a lit band
for x0, x1, z in ((12000, 15000, 1240), (15000, 18000, 1060), (18000, 21000, 870), (21000, 23500, 600)):
    w = min(hw(x0), hw(x1)) * 0.55
    plate(bm, rect(x0 + 120, x1 - 120, -w, w), z - 80, 180, GUN if (x0 // 3000) % 2 else HULL, 30.0)
    greeble(bm, plane_surface((x0 + 300, -w * 0.8, z + 100.1), (x1 - x0 - 600, 0, 0), (0, w * 1.6, 0)), 0.000006, 2250 + x0,
            size=(120, 400), height=(30, 140), mats=(GUN, DARK), window=WIN, window_frac=0.15, stack=0.3, pipes=0.1, dark=0.4)
    for s in (1, -1):
        edge_windows(bm, (x0 + 200, s * (w + 2)), (x1 - 200, s * (w + 2)), z - 40, 3, (0, s), lit=0.5, seed=2260 + x0 + s, strip=(10, 80))
# --- the bow: armoured cap and a spinal battery's muzzle
cyl(bm, 160, 160, 80, Vector((25020, 0, 0)), Ry(math.pi / 2), seg=24, mi=DRIVE)
# --- the stern: a wall of drives (Executor)
plate(bm, rect(-25900, -24900, -3900, 3900), -1500, 3000, DARK, 80.0)
for y in (-3000, -1500, 0, 1500, 3000):
    for z in (700, -700):
        engine(bm, (-26000, y, z), 640, 2000)
nav(bm, [(-22000, 4700, 0), (25000, 320, 0)], [(-22000, -4700, 0), (25000, -320, 0)], [(-20200, 0, 5800), (-19100, 0, 5600)])
o = obj("dreadnought", bm, SM)
result = export_ship(col, root, "Dreadnought")
