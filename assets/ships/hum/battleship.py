"""
Battleship, 3 km. BRICK family (Halo UNSC). References (assets/ships/refs/battleship_ref*): the UNSC Infinity: a very
long slab hull with a flat-topped bow that slopes up from beneath, a long raised dorsal superstructure, stepped flanks
with hangar bays, tall antenna masts above and below, and a cluster of drives at the stern; the user's Infinity and
long slab warships. Gunmetal and grey plating, slatted belts and lengthwise strips, as the frigate and cruiser.
Crew ~20,000. (The Bellator-style wedge is kept in battleship_wedge.py.)
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/battleship.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipBattleship")
sc = hull_scale(3000)
bm = bmesh.new()
# --- the slab: a long chamfered hull; the bow keeps a flat top and slopes up from beneath (Infinity)
keel(bm, [(-1500, 200, 150, 0), (-1300, 230, 170, 0), (700, 230, 170, 0), (1050, 210, 150, 14), (1350, 160, 104, 54), (1560, 70, 40, 98), (1600, 30, 14, 112)], GUN, 0.18)
# the lower hull steps in: a narrower keel block running most of the length, with hangar bays
keel(bm, [(-1300, 170, 70, -200), (-1200, 190, 80, -210), (700, 190, 80, -210), (950, 140, 60, -190)], HULL, 0.2)
for x in (-900, -500, -100, 300):
    for s in (1, -1):
        R = flank(s)
        fast_box(bm, (240, 70, 2.0), Vector((x, s * 190.5, -210)), R, DARK, UVL(bm))
        fast_box(bm, (240, 3, 2.4), Vector((x, s * 191, -246)), R, WIN, UVL(bm))
# --- the flanks: slatted armour belts, a dark trench of windows, lengthwise strips on top and bottom
for s in (1, -1):
    ribs(bm, -1280, 780, s * 230.5, 10, 150, s, 34, depth=6, seed=1100 + s, gap=0.12, mi=(GUN, HULL))
    ribs(bm, -1280, 780, s * 230.5, -150, -20, s, 34, depth=5, seed=1102 + s, gap=0.2, mi=(GUN, HULL))
    fast_box(bm, (2060, 22, 4), Vector((-250, s * 231, -6)), flank(s), DARK, UVL(bm))
    edge_windows(bm, (-1280, s * 233), (780, s * 233), -14, 4, (0, s), lit=0.5, seed=1104 + s, strip=(10, 60))
    edge_windows(bm, (-1280, s * 237), (680, s * 237), 100, 14, (0, s), lit=0.18, seed=1106 + s, strip=(6, 40))
    plate(bm, rect(-1250, 650, *sorted((s * 214, s * 232))), 40, 44, DARK, 2.0)                  # the deep flank channel
    greeble(bm, plane_surface((-1240, s * 228, 44) if s < 0 else (640, s * 228, 44), (1880 * (1 if s < 0 else -1), 0, 0), (0, 0, 36)), 0.002, 1108 + s,
            size=(6, 20), height=(2, 10), mats=(GUN, DARK), window=WIN, window_frac=0.1, stack=0.3, pipes=0.15, dark=0.5)
strips(bm, -1280, 1100, -190, 190, 170, 10, h=6, seed=1110, mi=TAN)
strips(bm, -1180, 680, -160, 160, -290, 8, h=6, seed=1111, mi=GUN, down=True)
# --- side volumes: big armoured sponsons standing off the flanks at different heights (Infinity's stepped sides)
for x0, x1, z, h, d in ((-1150, -650, 90, 70, 26), (-560, -80, -150, 90, 30), (0, 520, 80, 64, 22), (-1250, -900, -140, 80, 34)):
    for s in (1, -1):
        y0, y1 = sorted((s * 228, s * (228 + d)))
        plate(bm, rect(x0, x1, y0, y1), z, h, HULL if (x0 // 100) % 2 else GUN, 5.0)
        ribs(bm, x0 + 20, x1 - 20, s * (228 + d + 0.3), z + 8, z + h - 8, s, 18, depth=3, seed=1140 + x0 + s, mi=(GUN, LIGHT))
        edge_windows(bm, (x0 + 10, s * (228 + d + 1.6)), (x1 - 10, s * (228 + d + 1.6)), z + 6, int(h / DECK) - 2, (0, s), lit=0.15, seed=1150 + x0 + s, strip=(6, 30))
# engine housings swelling out at the stern quarters
for s in (1, -1):
    loft(bm, [(-1080, 40, 50, 0.3), (-1150, 80, 100, 0.3), (-1400, 90, 110, 0.3), (-1520, 80, 100, 0.3)], I3, Vector((0, s * 250, -40)), GUN)
    engine(bm, (-1560, s * 250, -40), 66, 120)
# --- the dorsal superstructure: a long raised block, greebled, with the bridge at its forward end
plate(bm, mirror([(560, 0), (520, 90), (-1180, 104), (-1220, 0)]), 170, 70, LIGHT, 10.0)
top = plane_surface((-1180, -80, 240.1), (1700, 0, 0), (0, 160, 0))
greeble(bm, top, 0.0005, 1120, size=(25, 80), height=(6, 24), mats=(GUN, HULL), window=WIN, window_frac=0.1, stack=0.4, pipes=0.05, dark=0.3)
greeble(bm, top, 0.0012, 1121, size=(6, 18), height=(2, 8), mats=(HULL, DARK), window=WIN, window_frac=0.08, stack=0.3, pipes=0.1, dark=0.4)
for s in (1, -1):
    edge_windows(bm, (525, s * 91), (-1178, s * 105), 176, 15, (0, s), lit=0.12, seed=1122 + s, strip=(8, 40))
plate(bm, mirror([(540, 0), (525, 70), (360, 80), (340, 0)]), 240, 40, LIGHT, 6.0, slope=(-10, 0))
bridge(bm, 536, 280, 110, 12.0, 50)
# --- ventral pods: two long rounded hangar modules slung under the keel (Infinity)
for s in (1, -1):
    loft(bm, [(600, 20, 20, 0.45), (500, 60, 50, 0.45), (-700, 70, 56, 0.45), (-860, 50, 40, 0.45)], I3, Vector((0, s * 110, -320)), HULL)
    fast_box(bm, (40, 70, 2), Vector((604, s * 110, -320)), Matrix(((0, 0, 1), (0, 1, 0), (-1, 0, 0))), DARK, UVL(bm))
    edge_windows(bm, (480, s * (110 + s * 61)), (-680, s * (110 + s * 71)), -330, 5, (0, s), lit=0.3, seed=1160 + s, strip=(8, 40))
# --- the bow: armour plating on the flat top, the spinal gun's muzzle at the tip, pale bands
strips(bm, 760, 1300, -150, 150, 182, 6, h=5, seed=1130, mi=LIGHT)
cyl(bm, 18, 18, 10, Vector((1598, 0, 100)), Ry(math.pi / 2), seg=28, mi=DARK)
cyl(bm, 12, 12, 4, Vector((1604, 0, 100)), Ry(math.pi / 2), seg=28, mi=DRIVE)
for x in (1000, 1040):
    for s in (1, -1):
        fast_box(bm, (14, 200, 2), Vector((x, s * 218, 40)), flank(s), LIGHT, UVL(bm))
# --- antenna masts above and below (Infinity)
for x, h in ((300, 220), (100, 160), (-300, 260), (-700, 180), (-1000, 210)):
    cyl(bm, 5, 3, h, Vector((x, 40 if x % 200 else -40, 240 + h / 2)), seg=8, mi=DARK)
for x, h in ((-600, 160), (-200, 120), (200, 150)):
    cyl(bm, 5, 3, h, Vector((x, 0, -290 - h / 2)), seg=8, mi=DARK)
# --- identification bands down the superstructure, extra masts for the Infinity's spiky skyline
for x in (-900, -880, 200, 220):
    for s in (1, -1):
        fast_box(bm, (10, 64, 1.5), Vector((x, s * (97 + 0.5), 205)), flank(s), TAN, UVL(bm))
for x, y, h in ((440, 60, 140), (420, -60, 120), (-1100, 30, 240), (-460, -50, 200)):
    cyl(bm, 4, 2.5, h, Vector((x, y, 240 + h / 2)), seg=8, mi=DARK)
# --- point defence and main batteries along the deck edges
for k in range(9):
    x = -1150 + k * 230
    for s in (1, -1):
        turret(bm, (x, s * 175, 176), 36)
# --- the stern: a deep engine block, four great drives and a ring of small ones
plate(bm, rect(-1580, -1490, -200, 200), -150, 300, HULL, 10.0)
for y, z in ((-95, 70), (95, 70), (-95, -80), (95, -80)):
    engine(bm, (-1600, y, z), 72, 200)
for k in range(8):
    a = k * math.pi / 4
    engine(bm, (-1580, math.cos(a) * 175, -5 + math.sin(a) * 125), 22, 80)
nav(bm, [(-1300, 232, 0), (1590, 32, 112)], [(-1300, -232, 0), (1590, -32, 112)], [(-300, -40, 500), (536, 0, 300)])
o = obj("battleship", bm, SM)
result = export_ship(col, root, "Battleship")
