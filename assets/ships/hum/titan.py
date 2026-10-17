"""
Titan, 12 km. BRICK family (Halo UNSC). References (assets/ships/refs/titan_ref*): the Punic supercarrier's two-tier
slab, a long armoured hull with a second, shorter block stacked on its back and a taller citadel forward; the EVE
Leviathan's dark tiered bulk and jutting prow; the Stellaris titan and flagship: a glowing weapon channel cut down
each flank, crossed by armoured arches, and rows of turret domes along the back. Same plating, ribs, strips, windows
and drives as the battleship, scaled up. Crew ~250,000. The spinal channel feeds the titan's main gun at the prow.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/titan.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipTitan")
sc = hull_scale(12000)
bm = bmesh.new()
# --- the slab: a long chamfered hull; the bow narrows and rises into the prow (Punic / Leviathan)
keel(bm, [(-6000, 880, 640, 0), (-5300, 960, 700, 0), (2600, 960, 700, 0), (3800, 880, 620, 40), (5000, 620, 420, 140), (5800, 300, 200, 220), (6000, 140, 80, 240)], GUN, 0.18)
# the lower jaw: a narrower keel block under the forward hull, jutting past the upper prow (Leviathan)
keel(bm, [(-4800, 700, 260, -760), (-4400, 760, 300, -800), (3600, 760, 300, -800), (5200, 420, 180, -720), (6300, 160, 70, -640)], DARK, 0.22)
# --- the second tier: a shorter armoured block on the back, and a taller citadel forward of it (Punic)
plate(bm, mirror([(2400, 0), (2280, 640), (-4700, 700), (-4900, 0)]), 700, 380, HULL, 30.0)
plate(bm, mirror([(2300, 0), (2200, 520), (900, 560), (840, 0)]), 1080, 300, GUN, 24.0, slope=(-40, 0))
plate(bm, mirror([(-3600, 0), (-3700, 460), (-4600, 500), (-4700, 0)]), 1080, 220, GUN, 20.0)
for z0, x0, x1, hw in ((704, -4880, 2380, 690), (1084, 850, 2290, 550)):
    strips(bm, x0 + 60, x1 - 60, -hw * 0.85, hw * 0.85, z0 + (380 if z0 < 1000 else 300) - 4, 14, h=16, seed=1500 + z0, mi=GUN)
top = plane_surface((-4600, -560, 1080.1), (3400, 0, 0), (0, 1120, 0))
greeble(bm, top, 0.00005, 1510, size=(80, 260), height=(20, 80), mats=(GUN, HULL), window=WIN, window_frac=0.1, stack=0.4, pipes=0.05, dark=0.3)
greeble(bm, top, 0.0001, 1511, size=(24, 70), height=(8, 30), mats=(HULL, DARK), window=WIN, window_frac=0.08, stack=0.3, pipes=0.1, dark=0.4)
bridge(bm, 2280, 1380, 360, 40.0, 180)
for s in (1, -1):
    edge_windows(bm, (2390, s * 642), (-4690, s * 702), 720, 30, (0, s), lit=0.12, seed=1520 + s, strip=(10, 60))
    edge_windows(bm, (2290, s * 522), (850, s * 562), 1100, 24, (0, s), lit=0.2, seed=1522 + s, strip=(8, 40))
# --- the flanks: slatted belts above and below the spinal channel, a lit window band, deep side sponsons
for s in (1, -1):
    ribs(bm, -5200, 2500, s * 960.5, 180, 640, s, 120, depth=20, seed=1530 + s, gap=0.12, mi=(GUN, HULL))
    ribs(bm, -5200, 2500, s * 960.5, -640, -180, s, 120, depth=18, seed=1532 + s, gap=0.2, mi=(GUN, HULL))
    edge_windows(bm, (-5200, s * 982), (2500, s * 982), 420, 16, (0, s), lit=0.3, seed=1534 + s, strip=(12, 80))
    edge_windows(bm, (-5200, s * 982), (2500, s * 982), -460, 16, (0, s), lit=0.15, seed=1536 + s, strip=(12, 80))
# --- side volumes: big armoured sponsons standing off the lower flanks at different lengths (Punic's stepped sides)
for x0, x1, z, h, d in ((-5000, -3200, -660, 360, 110), (-2900, -400, -620, 300, 80), (-100, 2300, -660, 360, 120), (-4400, -1800, 330, 300, 60)):
    for s in (1, -1):
        y0, y1 = sorted((s * 958, s * (958 + d)))
        plate(bm, rect(x0, x1, y0, y1), z, h, HULL if (x0 // 100) % 2 else GUN, 16.0)
        ribs(bm, x0 + 60, x1 - 60, s * (958 + d + 0.5), z + 30, z + h - 30, s, 70, depth=10, seed=1570 + x0 + s, mi=(GUN, LIGHT))
        edge_windows(bm, (x0 + 40, s * (958 + d + 4)), (x1 - 40, s * (958 + d + 4)), z + 20, 8, (0, s), lit=0.2, seed=1580 + x0 + s, strip=(8, 50))
# --- the spinal channel down each flank, glowing, crossed by armoured arches (Stellaris titan)
for s in (1, -1):
    R = flank(s)
    fast_box(bm, (7600, 460, 6), Vector((-1300, s * 963, 0)), R, DARK, UVL(bm))
    fast_box(bm, (7600, 420, 8), Vector((-1300, s * 964, 0)), R, DRIVE, UVL(bm))
    for z in (250, -250):
        fast_box(bm, (7700, 50, 90), Vector((-1300, s * 1000, z)), R, GUN, UVL(bm))                 # the channel's armoured lips
    for k in range(16):
        x = -4900 + k * 480
        fast_box(bm, (110, 560, 110), Vector((x, s * 1010, 0)), R, GUN, UVL(bm))
    # the channel runs into the prow and lights its throat
    fast_box(bm, (1400, 70, 8), Vector((3300, s * (930 - 0), 0)), Rz(s * 0.07).to_3x3() @ R, DRIVE, UVL(bm))
# --- the forward hull: belts and lengthwise plating carried onto the narrowing bow, a raised armour spine
for s in (1, -1):
    ribs(bm, 2700, 3700, s * 935, -500, 500, s, 100, depth=16, seed=1550 + s, gap=0.15, mi=(GUN, HULL))
strips(bm, 2700, 5400, -500, 500, 700, 14, h=14, seed=1552, mi=GUN)
plate(bm, mirror([(5600, 0), (5200, 200), (2500, 300), (2400, 0)]), 640, 160, HULL, 12.0, slope=(-60, 0))
for s in (1, -1):
    ribs(bm, -4600, 2200, s * 690, 760, 1040, s, 90, depth=12, seed=1560 + s, gap=0.15, mi=(GUN, LIGHT))     # second-tier armour panels
# --- the prow: the main gun's muzzle between the upper prow and the lower jaw
cyl(bm, 110, 110, 40, Vector((6010, 0, -200)), Ry(math.pi / 2), seg=32, mi=DARK)
cyl(bm, 80, 80, 10, Vector((6034, 0, -200)), Ry(math.pi / 2), seg=32, mi=DRIVE)
plate(bm, rect(4600, 6000, -260, 260), -560, 300, DARK, 6.0)
for x in (4200, 4300):
    for s in (1, -1):
        fast_box(bm, (50, 900, 6), Vector((x, s * 860, 100)), flank(s), LIGHT, UVL(bm))
# --- turret domes in rows along the back (Stellaris titan / flagship)
for k in range(12):
    x = -4500 + k * 560
    for s in (1, -1):
        cyl(bm, 190, 220, 70, Vector((x, s * 470, 1115)), seg=24, mi=HULL)                        # the barbette
        turret(bm, (x, s * 470, 1150), 420)
for k in range(4):
    cyl(bm, 150, 170, 60, Vector((1050 + k * 330, 0, 1410)), seg=24, mi=HULL)
    turret(bm, (1050 + k * 330, 0, 1440), 320)
# --- identification bands round the second tier and the forward citadel (as the battleship)
for x in (-3300, -3220, 1900, 1980):
    for s in (1, -1):
        fast_box(bm, (40, 300, 6), Vector((x, s * (702 if x < 0 else 562), 900 if x < 0 else 1230)), flank(s), TAN, UVL(bm))
# --- antenna masts above and below
for x, y, h in ((1600, 120, 700), (1300, -160, 520), (-1000, 200, 600), (-2600, -180, 820), (-4100, 100, 500)):
    cyl(bm, 16, 10, h, Vector((x, y, (1380 if x > 800 else 1300 if x < -3500 else 1080) + h / 2)), seg=8, mi=DARK)
for x, h in ((-2000, 500), (0, 420), (2000, 380)):
    cyl(bm, 16, 10, h, Vector((x, 0, -1100 - h / 2)), seg=8, mi=DARK)
# --- the underside: lengthwise plating and lit hangar bays for the titan's escort wings
strips(bm, -4600, 3400, -650, 650, -1100, 12, h=18, seed=1540, mi=GUN, down=True)
for x in (-3600, -2200, -800, 600, 2000):
    fast_box(bm, (900, 700, 8), Vector((x, 0, -1108)), DOWN, DARK, UVL(bm))
    for s in (1, -1):
        fast_box(bm, (900, 14, 10), Vector((x, s * 356, -1110)), DOWN, WIN, UVL(bm))
# --- the stern: a deep engine block, six great drives and a ring of small ones
plate(bm, rect(-6300, -5960, -820, 820), -600, 1300, DARK, 30.0)
for y in (-520, 0, 520):
    for z in (260, -260):
        engine(bm, (-6340, y, z), 230, 700)
for k in range(10):
    a = k * math.pi / 5
    engine(bm, (-6320, math.cos(a) * 760, 40 + math.sin(a) * 520), 70, 260)
nav(bm, [(-5200, 962, 0), (6000, 140, 240)], [(-5200, -962, 0), (6000, -140, 240)], [(-2600, -180, 1900), (2280, 0, 1560)])
o = obj("titan", bm, SM)
result = export_ship(col, root, "Titan")
