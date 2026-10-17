"""
Destroyer, 600 m. References (assets/ships/refs/destroyer_ref*): the Acclamator's pyramid wedge with a central
ridge and the engine cluster under the stern; the Gladiator's stepped superstructure topped by a broad hammerhead
bridge and its flank hangar; the Victory's dark waist trench with batteries along the edges and twin sensor domes.
Crew ~3,000.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/destroyer.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipDestroyer")
sc = hull_scale(600)
bm = bmesh.new()
NOSE, STERN, HW = 300.0, -300.0, 125.0
half = [(NOSE, 0.0), (STERN + 30, HW), (STERN, HW - 10)]
# --- the wedge: an upper pyramid over a dark waist, a shallower inverted pyramid below
wedge(bm, mirror(half), 6.0, 46.0, ridge=0.05, along=0.88, ox=-24, mi=LIGHT)
wedge(bm, mirror([(NOSE - 16, 0.0), (STERN + 26, HW - 8), (STERN + 2, HW - 16)]), -8.0, 14.0, ridge=1.0, along=1.0, mi=DARK)    # the waist
wedge(bm, mirror(half), -8.0, 30.0, ridge=0.12, along=0.85, ox=-30, mi=HULL, down=True)
# window rows and batteries in the waist, all the way round both flanks
for s in (1, -1):
    a, b = (NOSE - 14, 0.0), (STERN + 28, s * (HW - 7))
    out = (-(b[1] - a[1]) * s * -1, (b[0] - a[0]) * s * -1)
    n = Vector((s * (b[1] - a[1]), -s * (b[0] - a[0]) * -1, 0))
    o = Vector((-(b[1] - a[1]), b[0] - a[0], 0)).normalized()
    if o.y * s < 0: o = -o
    edge_windows(bm, (a[0], a[1] + o.y * 1.0), (b[0], b[1] + o.y * 1.0), -2.6, 2, (o.x, o.y), seed=200 + s, spacing=3.0)
    for k in range(6):
        u = 0.18 + 0.13 * k
        p = Vector((a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u, 4.0)) - o * 6
        turret(bm, (p.x, p.y, p.z + 1.5), 11 if k % 2 else 15)
# --- sloped deck detail: panels and Acclamator-style raised armour over the real upper and lower faces
for k, f in enumerate(wedge_faces(mirror(half), 6.0, 46.0, ridge=0.05, along=0.88, ox=-24)):
    if f.area < 2000: continue
    panels(bm, f, 210 + k, scale=sc * 1.4, accent=0.0, density=0.15)
    armour_grid(bm, f.tri, (26, 9), (0.8, 2.4), 230 + k, (LIGHT, HULL), fill=0.7)
for k, f in enumerate(wedge_faces(mirror(half), -8.0, 30.0, ridge=0.12, along=0.85, ox=-30, down=True)):
    if f.area < 2000: continue
    panels(bm, f, 250 + k, scale=sc * 1.6, accent=0.0, density=0.4)
    armour_grid(bm, f.tri, (30, 10), (0.8, 2.0), 260 + k, (HULL, DARK), fill=0.6)
trench(bm, -150, 220, -3.5, 3.5, 49.5, 3.0, 215, scale=sc * 1.2)
# --- the superstructure: stepped blocks off the stern ridge, a raked tower, the hammerhead bridge, twin domes
plate(bm, mirror([(-130, 0), (-142, 48), (-268, 54), (-280, 0)]), 44, 18, LIGHT, 3.0)
plate(bm, mirror([(-170, 0), (-180, 34), (-262, 38), (-272, 0)]), 62, 16, HULL, 2.4)
plate(bm, mirror([(-208, 0), (-216, 16), (-252, 17), (-260, 0)]), 78, 30, LIGHT, 2.4, slope=(-4.0, 0))
plate(bm, mirror([(-206, 0), (-214, 78), (-252, 82), (-260, 0)]), 108, 14, LIGHT, 2.6)
for s in (1, -1):
    edge_windows(bm, (-213, s * 78.5), (-213, -s * 0.0), 111, 2, (1, 0), seed=240 + s, spacing=2.6)
bridge(bm, -210, 122, 44, 8.0, 22)
for s in (1, -1):
    cyl(bm, 10, 10, 7, Vector((-240, s * 58, 125.5)), seg=24, mi=HULL)
    sphere(bm, 10, Vector((-240, s * 58, 129)), mi=LIGHT, seg=24)
    plate(bm, rect(-254, -226, *(sorted((s * 50, s * 66)))), 122, 3, HULL, 0.8)
trench(bm, -266, -140, -44, -36, 62, 2.4, 216, scale=sc)
trench(bm, -266, -140, 36, 44, 62, 2.4, 217, scale=sc)
panels(bm, plane_surface((-266, -32, 78.1), (100, 0, 0), (0, 64, 0)), 218, scale=sc * 1.2, accent=0.0, density=0.5)
# --- drives: a bank of four main engines and two auxiliaries across the stern, a ventral pod pair
plate(bm, rect(STERN - 14, STERN + 6, -60, 60), -22, 40, HULL, 2.0)
for y, z, r in ((-36, 2, 14), (36, 2, 14), (-12, 6, 15), (12, 6, 15), (-58, -8, 8), (58, -8, 8)):
    engine(bm, (STERN - 18, y, z), r, 36)
# --- the ventral hangar: a lit mouth sized for frigates (300 m long, 60 wide), on the centreline
fast_box(bm, (120, 44, 0.6), Vector((-90, 0, -33.7)), DOWN, DARK, UVL(bm))
for s in (1, -1):
    fast_box(bm, (120, 1.2, 0.8), Vector((-90, s * 22.5, -34.0)), DOWN, WIN, UVL(bm))
# --- lights
nav(bm, [(STERN + 30, HW + 1, 0), (NOSE - 2, 2, 0)], [(STERN + 30, -HW - 1, 0), (NOSE - 2, -2, 0)], [(-240, 58, 140), (-240, -58, 140), (-232, 0, 131)])
o = obj("destroyer", bm, SM)
result = export_ship(col, root, "Destroyer")
