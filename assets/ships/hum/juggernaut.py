"""
Juggernaut, 20 km. INDUSTRIAL family: a mobile shipyard. References (assets/ships/refs/juggernaut_ref*): the Stellaris
juggernaut's long spine flanked by outrigger hulls, gold light panels down its sides and a glowing sensor orb at
the head; the Nemesis juggernaut's slab outriggers with lit trenches; the Homeworld Mothership's great hangar spine
that builds the fleet inside it. Between the spine and each outrigger runs an open slipway with hulls on the ways
under gantry cranes, tied across by trusses and bridges. Crew ~1,000,000, mostly yard workers.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/juggernaut.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipJuggernaut")
sc = hull_scale(20000)
bm = bmesh.new()
# --- the spine: a long chamfered hangar hull (Mothership), tapering to the armoured head
keel(bm, [(-10000, 700, 800, 0), (-9200, 820, 920, 0), (6000, 820, 920, 0), (8200, 620, 720, 0), (9800, 300, 360, 0)], GUN, 0.2)
plate(bm, mirror([(5800, 0), (5600, 560), (-8600, 620), (-8800, 0)]), 920, 260, HULL, 30.0)                 # the dorsal hangar deck
plate(bm, mirror([(4800, 0), (4600, 380), (2600, 420), (2400, 0)]), 1180, 380, GUN, 24.0, slope=(-60, 0))   # the command block
bridge(bm, 4760, 1560, 420, 50.0, 220)
strips(bm, -8500, 5600, -540, 540, 1176, 14, h=20, seed=1600, mi=GUN)
greeble(bm, plane_surface((-8400, -500, 1180.1), (10600, 0, 0), (0, 1000, 0)), 0.00003, 1601, size=(100, 320), height=(30, 120),
        mats=(GUN, DARK), window=WIN, window_frac=0.1, stack=0.4, pipes=0.1, dark=0.4)
# tank farms and cargo along the hangar deck, aft (industrial)
for k in range(8):
    x = -8000 + k * 520
    for s in (1, -1):
        tank_cage(bm, (x, s * 300, 1180), 180, axis=(0, 0, 1), capsule=1.3, mi_tank=HULL, mi_cage=DARK, ribs=4)
for s in (1, -1):
    ribs(bm, -9000, 5800, s * 820.5, -800, 800, s, 160, depth=24, seed=1610 + s, gap=0.18, mi=(GUN, HULL))
    edge_windows(bm, (-9000, s * 846), (5800, s * 846), 300, 20, (0, s), lit=0.2, seed=1612 + s, strip=(12, 90))
# --- the head: an armoured prow round a glowing sensor orb (Stellaris juggernaut)
keel(bm, [(9600, 420, 460, 0), (10200, 380, 400, 0), (10500, 200, 220, 0)], HULL, 0.35)
cyl(bm, 460, 420, 160, Vector((10580, 0, 0)), Ry(math.pi / 2), seg=32, mi=DARK)
cyl(bm, 360, 360, 40, Vector((10670, 0, 0)), Ry(math.pi / 2), seg=32, mi=WIN)
band(bm, Vector((10600, 0, 0)), (1, 0, 0), 470, 60, 30, mi=ACC, seg=32)
for s in (1, -1):
    plate(bm, rect(8800, 10300, *sorted((s * 300, s * 640))), -300, 600, GUN, 20.0, slope=(-80, 0))         # cheek armour
# --- the outriggers: long slab hulls either side (Stellaris / Nemesis), gold light panels down their flanks
for s in (1, -1):
    y = s * 2900
    loft(bm, [(9600, 40, 60, 0.3), (8600, 300, 360, 0.25), (7200, 520, 640, 0.25), (-8200, 560, 680, 0.25), (-9400, 440, 560, 0.25)], I3, Vector((0, y, 0)), HULL)
    # stepped armour blocks and machinery along the outrigger's back (Nemesis)
    for x0, x1, h in ((-8600, -5400, 300), (-5000, -1600, 200), (-1200, 2600, 260), (3000, 6200, 160)):
        plate(bm, rect(x0, x1, y - 400, y + 400), 680, h, GUN if h > 220 else HULL, 20.0, slope=(-30, 0))
        greeble(bm, plane_surface((x0 + 100, y - 360, 680 + h + 0.1), (x1 - x0 - 200, 0, 0), (0, 720, 0)), 0.00004, 1670 + x0 + s,
                size=(80, 240), height=(20, 90), mats=(GUN, DARK), window=WIN, window_frac=0.1, stack=0.4, pipes=0.1, dark=0.4)
    ribs(bm, -8000, 7000, y + s * 560.5, -600, 600, s, 150, depth=22, seed=1620 + s, gap=0.15, mi=(GUN, HULL))
    for k in range(9):
        x = -7400 + k * 1600
        fast_box(bm, (600, 180, 10), Vector((x, y + s * 600, 200 if k % 2 else -200)), flank(s), WIN, UVL(bm))   # gold panels
    fast_box(bm, (15000, 40, 10), Vector((-500, y + s * 595, 0)), flank(s), DRIVE, UVL(bm))
    for k in range(8):
        x = -6600 + k * 1600                                                                                     # hangar mouths (Mothership)
        zc = -300 if k % 2 else 300
        fast_box(bm, (700, 260, 8), Vector((x, y + s * 566, zc)), flank(s), DARK, UVL(bm))
        fast_box(bm, (700, 14, 12), Vector((x, y + s * 568, zc - 136)), flank(s), WIN, UVL(bm))                      # lit trench (Nemesis)
    strips(bm, -8000, 7000, y - 480, y + 480, 680, 12, h=18, seed=1630 + s, mi=GUN)
    for z in (240, -240):
        engine(bm, (-9500, y, z), 220, 700)
# --- the slipways: trusses and bridges tie spine to outriggers; hulls on the ways under gantry cranes (Mothership)
for s in (1, -1):
    y0, y1 = sorted((s * 800, s * 2360))
    for x in (-7200, -2400, 2400, 6600):
        plate(bm, rect(x - 180, x + 180, y0, y1), 500, 260, GUN, 16.0)                                       # bridge decks
        plate(bm, rect(x - 140, x + 140, y0, y1), -760, 220, DARK, 12.0)
    for x0, x1 in ((-7000, -2600), (-2200, 2200), (2600, 6400)):
        truss(bm, (x0, s * 1580, -620), (x1, s * 1580, -620), w=240, seg=int((x1 - x0) / 200), mi=DARK, rod=8.0)   # the ways
        L = (x1 - x0) * 0.7
        loft(bm, [(x0 + 500 + L, 60, 50, 0.3), (x0 + 300 + L, 240, 230, 0.25), (x0 + 600, 260, 240, 0.25), (x0 + 450, 220, 200, 0.25)],
             I3, Vector((0, s * 1580, -300)), GUN if x0 < 0 else HULL)                                          # a hull on the ways
        ribs(bm, x0 + 700, x0 + 300 + L, s * (1580 + 260.5), -480, -120, s, 60, depth=8, seed=1660 + x0 + s, gap=0.5, mi=(DARK, GUN))   # unplated frames
    gantry(bm, (-7600, s * 1250, 780), (7000, s * 1250, 780), (0, 0, 1), 520, 420, mi_rail=DARK, mi_crane=ACC, cranes=6, seed=1640 + s)
    gantry(bm, (-7600, s * 1910, 780), (7000, s * 1910, 780), (0, 0, 1), 520, 420, mi_rail=DARK, mi_crane=ACC, cranes=5, seed=1650 + s)
# --- the stern of the spine: a deep engine block, nine great drives
plate(bm, rect(-10300, -9900, -760, 760), -880, 1760, DARK, 30.0)
for y in (-460, 0, 460):
    for z in (-480, 0, 480):
        engine(bm, (-10340, y, z), 210, 800)
nav(bm, [(-8000, 3480, 0), (10500, 220, 0)], [(-8000, -3480, 0), (10500, -220, 0)], [(4760, 0, 2000), (-8000, 0, 1500)])
o = obj("juggernaut", bm, SM)
result = export_ship(col, root, "Juggernaut")
