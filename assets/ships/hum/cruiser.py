"""
Cruiser, 1.2 km. BRICK family (Halo UNSC). References (assets/ships/refs/cruiser_ref*): the Marathon's thick hull
built of blocky "vertebrae" segments with dark seams between them; the Pillar of Autumn's long many-decked flank
with hangar bays along it and its stepped bow; the user's heavy angular warship. Gunmetal, slatted belts, lengthwise
plating and the same parts as the frigate. Crew ~4,000. (The Venator-style wedge is kept in cruiser_wedge.py.)
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/cruiser.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipCruiser")
sc = hull_scale(1200)
bm = bmesh.new()
rnd = random.Random(900)
# --- the vertebrae: blocky segments down the hull, each its own chamfered box, dark seams between (Marathon)
segs = [(-600, -470, 88, 76), (-460, -300, 100, 96), (-290, -120, 106, 108), (-110, 70, 106, 104), (80, 250, 96, 88), (260, 420, 84, 72)]
for k, (x0, x1, hw, hh) in enumerate(segs):
    zc = 4 * math.sin(k)
    keel(bm, [(x0, hw * 0.94, hh * 0.94, zc), (x0 + 14, hw, hh, zc), (x1 - 14, hw, hh, zc), (x1, hw * 0.94, hh * 0.94, zc)], HULL if k % 2 else GUN, 0.2)
    # the Marathon's chunky sub-blocks: big armour boxes and machinery on the flanks and back of each segment
    for s in (1, -1):
        fl = plane_surface((x0 + 16, s * hw, zc - hh * 0.7), (x1 - x0 - 32, 0, 0), (0, 0, hh * 1.4)) if s < 0 else              plane_surface((x1 - 16, s * hw, zc - hh * 0.7), (x0 - x1 + 32, 0, 0), (0, 0, hh * 1.4))
        greeble(bm, fl, 0.0018, 990 + k * 2 + s, size=(14, 40), height=(3, 9), mats=(GUN, HULL), window=WIN, window_frac=0.08, stack=0.35, pipes=0.0, dark=0.25)
    tp = plane_surface((x0 + 16, -hw * 0.85, zc + hh), (x1 - x0 - 32, 0, 0), (0, hw * 1.7, 0))
    greeble(bm, tp, 0.0016, 1000 + k, size=(12, 36), height=(3, 12), mats=(GUN, HULL), window=WIN, window_frac=0.06, stack=0.35, pipes=0.05, dark=0.25)
    for s in (1, -1):
        ribs(bm, x0 + 18, x1 - 18, s * (hw + 0.2), zc - hh * 0.75, zc - hh * 0.1, s, 16, depth=3, seed=910 + k * 2 + s, gap=0.15, mi=(GUN, HULL))
        edge_windows(bm, (x0 + 20, s * (hw + 1.6)), (x1 - 20, s * (hw + 1.6)), zc + 4, int(hh * 0.8 / DECK), (0, s), lit=0.35, seed=930 + k * 2 + s, strip=(6, 30))
    strips(bm, x0 + 16, x1 - 16, -hw * 0.8, hw * 0.8, zc + hh, 6, h=3, seed=950 + k, mi=GUN)
    # a stepped dorsal crest on each segment, and a pale band round its forward joint
    plate(bm, mirror([(x1 - 24, 0), (x1 - 34, hw * 0.4), (x0 + 30, hw * 0.45), (x0 + 22, 0)]), zc + hh, 10 + 4 * (k % 3), HULL if k % 2 else LIGHT, 3.0)
    for s in (1, -1):
        fast_box(bm, (8, hh * 1.6, 1.2), Vector((x1 - 20, s * (hw + 0.4), zc)), flank(s), LIGHT, UVL(bm))
    strips(bm, x0 + 16, x1 - 16, -hw * 0.8, hw * 0.8, zc - hh, 5, h=3, seed=960 + k, mi=GUN, down=True)
# the spine that ties them, recessed and dark between the segments
keel(bm, [(-600, 70, 66, 0), (440, 60, 56, 0)], DARK, 0.25)
# --- the bow: a stepped prow block with the spinal gun's mouth (Autumn / Marathon)
keel(bm, [(420, 74, 70, -4), (500, 66, 58, -6), (560, 50, 40, -4), (600, 34, 24, 0)], HULL, 0.3)
plate(bm, mirror([(560, 0), (540, 48), (430, 64), (420, 0)]), 54, 18, HULL, 4.0, slope=(-6, 0))
keel(bm, [(560, 18, 18, -10), (606, 16, 16, -10)], DARK, 0.3)
cyl(bm, 12, 12, 4, Vector((604, 0, -10)), Ry(math.pi / 2), seg=24, mi=DRIVE)
for s in (1, -1):
    ribs(bm, 430, 548, s * 67, -40, 20, s, 12, depth=2.5, seed=970 + s, mi=(GUN, LIGHT))
# the bow's overhang and notch (Autumn): a pale upper beak, a dark recess under it, windows across the face
keel(bm, [(520, 46, 22, 44), (600, 36, 16, 40), (640, 20, 8, 36)], LIGHT, 0.3)
plate(bm, rect(560, 612, -30, 30), 2, 20, DARK, 1.0)
for s in (1, -1):
    edge_windows(bm, (430, s * 74.5), (548, s * 66.5), -30, 14, (0.07, s), lit=0.4, seed=975 + s, strip=(5, 20))
    fast_box(bm, (100, 10, 1.2), Vector((480, s * 70.8, 30)), flank(s), LIGHT, UVL(bm))
# --- hangar bays along the lower flank (Autumn): dark mouths with lit sills
for x in (-380, -200, -30, 150):
    for s in (1, -1):
        R = flank(s)
        fast_box(bm, (110, 34, 1.0), Vector((x, s * 104.6, -54)), R, DARK, UVL(bm))
        fast_box(bm, (110, 1.6, 1.4), Vector((x, s * 104.8, -71.5)), R, WIN, UVL(bm))
        fast_box(bm, (110, 1.0, 1.2), Vector((x, s * 104.8, -36.5)), R, WIN, UVL(bm))
# --- the superstructure: a low armoured bridge block well forward, a sensor mast, point defence
plate(bm, mirror([(360, 0), (350, 40), (260, 44), (250, 0)]), 88, 30, HULL, 4.0, slope=(-5, 0))
bridge(bm, 358, 118, 64, 8.0, 30)
cyl(bm, 3, 2, 60, Vector((280, 0, 148)), seg=8, mi=DARK)
for s in (1, -1):
    dish(bm, (300, s * 30, 122), (0.3, s * 0.4, 0.86), 10, mi_dish=LIGHT, mi_frame=DARK)
for k, (x0, x1, hw, hh) in enumerate(segs):
    zc = 4 * math.sin(k)
    for s in (1, -1):
        turret(bm, ((x0 + x1) / 2, s * hw * 0.55, zc + hh + 3), 22)
# --- the stern: a deep engine block, four big drives and six small
plate(bm, rect(-640, -596, -96, 96), -84, 168, HULL, 6.0)
for y, z in ((-48, 32), (48, 32), (-48, -36), (48, -36)):
    engine(bm, (-650, y, z), 34, 90)
for y in (-84, 84):
    for z in (-50, 0, 50):
        engine(bm, (-640, y, z), 12, 40)
nav(bm, [(-560, 106, 0), (600, 36, 0)], [(-560, -106, 0), (600, -36, 0)], [(280, 0, 180), (-560, 0, 90)])
o = obj("cruiser", bm, SM)
result = export_ship(col, root, "Cruiser")
