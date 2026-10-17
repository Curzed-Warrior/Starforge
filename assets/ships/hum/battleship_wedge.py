"""
Battleship, 3 km. References (assets/ships/refs/battleship_ref*): the Bellator star dreadnought: a very broad wedge
(sd_hull) with a long central spine of stepped "city" running most of its length, a rounded ventral bulge, a row of
drives across the stern and engine pods at the wingtips; the Legator's low bridge on its spine; the Resurgent's
banded tiers. Crew ~80,000.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/battleship.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipBattleship")
hull_scale(3000)
bm = bmesh.new()
L = 3000.0
H = sd_hull(bm, L, 1900.0, 600, up_h=190, lo_h=120, batteries=12)
NOSE, STERN, HW, RZ, LOZ, LOH, sc = H["NOSE"], H["STERN"], H["HW"], H["RZ"], H["LOZ"], H["LOH"], H["sc"]
# --- the second tier: a raised inner wedge on the upper hull (the Bellator's stepped double slope)
inner = mirror([(NOSE - 380, 0.0), (STERN + 420, HW * 0.52), (STERN + 260, HW * 0.46)])
IZ = H["UPZ"] + H["UPH"] * 0.55
wedge(bm, inner, IZ, 175, ridge=0.08, along=0.86, ox=-60, mi=LIGHT)
for k, f in enumerate(wedge_faces(inner, IZ, 175, ridge=0.08, along=0.86, ox=-60)):
    if f.area < 40000: continue
    armour_grid(bm, f.tri, (110, 36), (3, 9), 650 + k, (LIGHT, HULL), fill=0.7)
for s in (1, -1):
    a0, b0 = (NOSE - 400, 0.0), (STERN + 440, s * (HW * 0.52 - 10))
    o = Vector((-(b0[1] - a0[1]), b0[0] - a0[0], 0)).normalized()
    if o.y * s < 0: o = -o
    edge_windows(bm, (a0[0], a0[1] + o.y), (b0[0], b0[1] + o.y), IZ + 3, 6, (o.x, o.y), seed=660 + s, strip=(8, 40))
# --- the ventral bulge: a rounded keel under the centre line (Bellator)
keel(bm, [(900, 40, 20, LOZ - LOH + 10), (500, 240, 110, LOZ - LOH - 30), (-600, 320, 150, LOZ - LOH - 50), (-1350, 300, 140, LOZ - LOH - 40), (-1480, 220, 100, LOZ - LOH - 20)], HULL, 0.42)
# --- the spine city: three long stepped tiers down the middle, greebled and windowed
z = RZ - 50
for k, (x0, x1, w, h) in enumerate([(1050, -1420, 170, 70), (700, -1400, 120, 64), (250, -1380, 80, 56)]):
    plate(bm, mirror([(x0, 0), (x0 - 80, w), (x1 + 20, w + 10), (x1, 0)]), z, h, LIGHT if k % 2 == 0 else HULL, 8.0)
    top = plane_surface((x1 + 40, -w + 20, z + h + 0.05), (x0 - 120 - x1 - 40, 0, 0), (0, 2 * w - 40, 0))
    greeble(bm, top, 0.0012, 610 + k, size=(10, 40), height=(4, 18), mats=(LIGHT, HULL), window=WIN, window_frac=0.25, stack=0.45, pipes=0.05, dark=0.35)
    greeble(bm, top, 0.0022, 620 + k, size=(3, 9), height=(1.5, 6), mats=(HULL, DARK), window=WIN, window_frac=0.1, stack=0.3, pipes=0.1, dark=0.45)
    for s in (1, -1):
        edge_windows(bm, (x0 - 82, s * (w + 0.5)), (x1 + 22, s * (w + 10.5)), z + 4, int(h / DECK) - 1, (0, s), lit=0.5, seed=630 + k * 2 + s, spacing=3.2, strip=(8, 40))
    z += h
# --- the bridge: low on the spine near the stern (Legator), wide, with sensor domes
plate(bm, mirror([(-1040, 0), (-1064, 80), (-1220, 88), (-1244, 0)]), z, 150, LIGHT, 8.0, slope=(-18, 0))
for s in (1, -1):
    edge_windows(bm, (-1041, s * 78), (-1041, s * 2), z + 4, 38, (1, 0), lit=0.5, seed=644 + s, strip=(6, 24))
z += 150
plate(bm, mirror([(-1010, 0), (-1034, 270), (-1210, 284), (-1236, 0)]), z, 80, LIGHT, 8.0)
for s in (1, -1):
    edge_windows(bm, (-1011, s * 268), (-1011, s * 2), z + 3, 20, (1, 0), lit=0.55, seed=640 + s, spacing=3.2, strip=(6, 30))
bridge(bm, -1006, z + 80, 220, 18.0, 70)
for s in (1, -1):
    cyl(bm, 10, 10, 34, Vector((-1150, s * 210, z + 97)), seg=10, mi=DARK)
    sphere(bm, 60, Vector((-1150, s * 210, z + 150)), mi=LIGHT, seg=14)
# --- drives: a row across the stern, and a pod on each wingtip (Bellator)
plate(bm, rect(STERN - 150, STERN + 40, -560, 560), LOZ - 260, 420, HULL, 10.0)
for k in range(8):
    y = -490 + k * 140
    engine(bm, (STERN - 80, y, -20 + (40 if k % 2 else 0)), 62, 190)
for s in (1, -1):
    y = s * (HW + 70)                                                                  # off the wingtip, on a pylon
    loft(bm, [(STERN + 520, 30, 30, 0.35), (STERN + 380, 100, 100, 0.35), (STERN + 120, 110, 110, 0.35), (STERN + 40, 100, 100, 0.35)], I3, Vector((0, y, 0)), LIGHT)
    plate(bm, rect(STERN + 120, STERN + 360, *sorted((s * (HW - 80), s * (HW - 20 + 70)))), -20, 40, HULL, 4.0)
    engine(bm, (STERN - 30, y, 0), 80, 160)
# --- the ventral hangar: a lit mouth in the bulge
fast_box(bm, (520, 200, 2.0), Vector((-500, 0, LOZ - LOH - 50 - 150 + 0.5)), DOWN, DARK, UVL(bm))
for s in (1, -1):
    fast_box(bm, (520, 3.0, 2.5), Vector((-500, s * 101, LOZ - LOH - 200)), DOWN, WIN, UVL(bm))
nav(bm, [(STERN + 150, HW + 4, 0), (NOSE - 8, 5, 0)], [(STERN + 150, -HW - 4, 0), (NOSE - 8, -5, 0)], [(-1150, 210, z + 214), (-1150, -210, z + 214)])
o = obj("battleship", bm, SM)
result = export_ship(col, root, "Battleship")
