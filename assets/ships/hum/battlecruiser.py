"""
Battlecruiser, 2 km. References (assets/ships/refs/battlecruiser_ref*): the Imperial-class Star Destroyer: the same
wedge as our destroyer and cruiser (sd_hull), a stepped ziggurat "city" of greebled tiers rising toward the stern, a
narrow neck carrying the broad bridge block with its two geodesic domes, three huge drives and four small in a
diamond; the Tector's clean hangar-less underside and its red-striped variant. Crew ~37,000.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/battlecruiser.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipBattlecruiser")
hull_scale(2000)
bm = bmesh.new()
L = 2000.0
H = sd_hull(bm, L, 1150.0, 500, batteries=10)
NOSE, STERN, HW, RZ, LOZ, LOH, sc = H["NOSE"], H["STERN"], H["HW"], H["RZ"], H["LOZ"], H["LOH"], H["sc"]
# --- the ziggurat: tiers stepping up toward the stern, each with a greebled trench at its foot
tiers = [(-150, 300, 48), (-330, 230, 54), (-500, 170, 58), (-640, 120, 60)]
z = RZ - 30
for k, (x0, w, h) in enumerate(tiers):
    plate(bm, mirror([(x0, 0), (x0 - 30, w), (STERN + 70, w + 20), (STERN + 50, 0)]), z, h, LIGHT if k % 2 == 0 else HULL, 6.0)
    for s in (1, -1):
        y0, y1 = sorted((s * (w - 18), s * (w + 4)))
        trench(bm, x0 - 260 if k else x0 - 420, x0 - 30, y0, y1, z + h, 6.0, 510 + k * 2 + s, scale=sc * 1.5)
        edge_windows(bm, (x0 - 32, s * (w + 0.5)), (STERN + 72, s * (w + 20.5)), z + 3, int(h / DECK) - 1, (0, s), lit=0.55, seed=520 + k * 2 + s, spacing=3.0)
    top = plane_surface((STERN + 90, -w + 24, z + h + 0.05), (x0 - 70 - STERN - 90, 0, 0), (0, 2 * w - 48, 0))
    # the "city": dense blocks, towers and lit slots on every tier (the ISD's superstructure)
    greeble(bm, top, 0.0014, 530 + k, size=(8, 30), height=(3, 14), mats=(LIGHT, HULL), window=WIN, window_frac=0.25, stack=0.45, pipes=0.05, dark=0.35)
    greeble(bm, top, 0.0028, 560 + k, size=(2, 7), height=(1, 5), mats=(HULL, DARK), window=WIN, window_frac=0.1, stack=0.3, pipes=0.1, dark=0.45)
    z += h
# --- the tower: a raked neck carrying the bridge block and its twin geodesic domes
plate(bm, mirror([(-700, 0), (-712, 34), (-800, 40), (-812, 0)]), z, 140, LIGHT, 6.0, slope=(-14.0, 0))
for s in (1, -1):
    edge_windows(bm, (-701, s * 33), (-701, s * 2), z + 4, 36, (1, 0), lit=0.5, seed=548 + s, spacing=3.0)
z += 140
plate(bm, mirror([(-684, 0), (-696, 160), (-794, 170), (-810, 0)]), z, 58, LIGHT, 6.0)
for s in (1, -1):                                                                    # eleven decks of windows across the bridge block's face
    edge_windows(bm, (-685, s * 158), (-685, s * 2), z + 3, 14, (1, 0), lit=0.55, seed=540 + s, spacing=3.0)
bridge(bm, -682, z + 58, 130, 14.0, 40)
greeble(bm, plane_surface((-790, -160, z + 58.1), (90, 0, 0), (0, 320, 0)), 0.012, 545, size=(3, 12), height=(1, 6), mats=(HULL, DARK), stack=0.3, pipes=0.1, dark=0.4)
for s in (1, -1):
    cyl(bm, 6, 6, 22, Vector((-760, s * 128, z + 69)), seg=10, mi=DARK)
    sphere(bm, 34, Vector((-760, s * 128, z + 104)), mi=LIGHT, seg=14)          # low-poly sphere reads as geodesic
# --- the drives: three huge and four small in a diamond across a deep engine block
plate(bm, rect(STERN - 112, STERN + 30, -300, 300), LOZ - 160, 262, HULL, 8.0)
for y, zz, r in ((-160, -10, 92), (160, -10, 92), (0, 8, 100), (-80, 80, 38), (80, 80, 38), (-80, -100, 38), (80, -100, 38)):
    engine(bm, (STERN - 50, y, zz), r, 140)
# --- the ventral hangar: a long lit mouth for the frigate and corvette wings
fast_box(bm, (360, 160, 1.5), Vector((-300, 0, LOZ - LOH + 0.4)), DOWN, DARK, UVL(bm))
for s in (1, -1):
    fast_box(bm, (360, 3.0, 2.0), Vector((-300, s * 82, LOZ - LOH)), DOWN, WIN, UVL(bm))
nav(bm, [(STERN + 100, HW + 3, 0), (NOSE - 6, 4, 0)], [(STERN + 100, -HW - 3, 0), (NOSE - 6, -4, 0)], [(-760, 128, z + 141), (-760, -128, z + 141)])
o = obj("battlecruiser", bm, SM)
result = export_ship(col, root, "Battlecruiser")
