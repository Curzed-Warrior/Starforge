"""
Carrier, 4 km. INDUSTRIAL family. References (assets/ships/refs/carrier_ref*): the user's industrial carriers with
open framework, exposed tanks and cargo modules down their flanks and a marked flight deck; the Kushan carrier's broad
chamfered barge hull with coloured deck pads; the Hiigaran carrier's great launch-bay mouth at the bow; the Epoch's
stepped hangar superstructure. A carrier's island stands to starboard. Carries ~3,000 strike craft and two dozen
corvettes. Crew ~50,000. (The Secutor/Battlestar wedge is kept in carrier_wedge.py.)
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/carrier.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipCarrier")
sc = hull_scale(4000)
bm = bmesh.new()
# --- the barge: a broad, flat, heavily chamfered hull (Kushan / Hiigaran)
keel(bm, [(-2000, 520, 170, 0), (-1800, 600, 200, 0), (1500, 600, 200, 0), (1850, 480, 160, -10), (2000, 300, 110, -20)], GUN, 0.42)
# --- the flight deck: a flat top laid in plates, lane markings and coloured lift pads (Kushan)
plate(bm, mirror([(1880, 0), (1780, 420), (-1760, 440), (-1840, 0)]), 200, 14, HULL, 8.0)
strips(bm, -1700, 1700, -380, 380, 214, 12, h=3, seed=1200, mi=GUN)
for k in range(8):
    x = 1500 - k * 330
    fast_box(bm, (220, 8, 1.5), Vector((x, 0, 218)), UP, ACC, UVL(bm))                              # centre lane dashes
    for s in (1, -1):
        fast_box(bm, (190, 130, 2.5), Vector((x - 60, s * 230, 217)), UP, ACC, UVL(bm))           # lift pads, framed in the accent colour
        fast_box(bm, (160, 100, 3.0), Vector((x - 60, s * 230, 217.5)), UP, TAN, UVL(bm))
        if k % 3 == 0: fast_box(bm, (170, 110, 3.0), Vector((x - 60, s * 230, 218)), UP, DARK, UVL(bm))      # a lift lowered
for s in (1, -1):
    fast_box(bm, (3400, 6, 1.5), Vector((-60, s * 360, 218)), UP, WIN, UVL(bm))                   # deck-edge lights
# gantry cranes riding rails down both deck edges, machinery along the edges (industrial)
for s in (1, -1):
    gantry(bm, (-1600, s * 400, 216), (1500, s * 400, 216), (0, 0, 1), 110, 120, mi_rail=DARK, mi_crane=ACC, cranes=4, seed=1250 + s)
    greeble(bm, plane_surface((-1700, 370 if s > 0 else -430, 214), (3300, 0, 0), (0, 60, 0)), 0.0012, 1260 + s,
            size=(10, 40), height=(4, 16), mats=(HULL, DARK), window=WIN, window_frac=0.08, stack=0.3, pipes=0.15, dark=0.4)
# --- the stepped aft hangar block over the stern deck (Epoch), greebled, lit along its faces
plate(bm, rect(-1900, -1120, -390, 390), 214, 80, GUN, 8.0)
plate(bm, rect(-1860, -1260, -300, 300), 294, 60, HULL, 6.0, slope=(-10, 0))
plate(bm, rect(-1800, -1420, -180, 180), 354, 40, GUN, 4.0)
greeble(bm, plane_surface((-1840, -280, 354.1), (560, 0, 0), (0, 560, 0)), 0.0015, 1270, size=(10, 36), height=(4, 16), mats=(GUN, DARK), window=WIN, window_frac=0.1, stack=0.4, pipes=0.15, dark=0.4)
for z, n in ((222, 8), (300, 6)):
    edge_windows(bm, (-1118 if z < 300 else -1258, -380 if z < 300 else -290), (-1118 if z < 300 else -1258, 380 if z < 300 else 290), z, n, (1, 0), lit=0.3, seed=1280 + z, strip=(6, 30))
fast_box(bm, (50, 500, 2), Vector((-1119, 0, 250)), Matrix(((0, 0, 1), (0, 1, 0), (-1, 0, 0))), DARK, UVL(bm))   # aft hangar mouth onto the deck
# --- the island: a tall tower to starboard with the bridge, masts and dishes
plate(bm, rect(-900, -100, -500, -320), 214, 90, HULL, 8.0)
plate(bm, rect(-760, -160, -480, -330), 304, 140, LIGHT, 8.0, slope=(-12, 0))
plate(bm, rect(-700, -220, -500, -310), 444, 70, HULL, 6.0)
bridge(bm, -222, 514, 190, 16.0, 70)
for k in range(4):
    edge_windows(bm, (-158, -335 - k * 35), (-158, -335 - k * 35 - 25), 306, 36, (1, 0), lit=0.35, seed=1210 + k, strip=(4, 14))
cyl(bm, 6, 3, 260, Vector((-500, -400, 644)), seg=8, mi=DARK)
dish(bm, (-640, -400, 530), (0.2, -0.3, 0.9), 34, mi_dish=LIGHT, mi_frame=DARK)
# the deck's forward overhang, raked down over the launch bay
wedge(bm, mirror([(2060, 0), (1900, 400), (1760, 420), (1760, 0)]), 200, 18, ridge=1.0, along=0.92, ox=-20, mi=LIGHT, down=False)
# --- the bow: a great launch-bay mouth across the front (Hiigaran), lit inside
fast_box(bm, (260, 520, 2), Vector((1990, 0, -10)), Matrix(((0, 0, 1), (0, 1, 0), (-1, 0, 0))), DARK, UVL(bm))
for z in (-120, -40, 40, 100):
    fast_box(bm, (4, 500, 4), Vector((1994, 0, z)), Matrix(((0, 0, 1), (0, 1, 0), (-1, 0, 0))), WIN, UVL(bm))
# --- the flanks: exposed framework, tank farms and cargo modules (user's industrial carrier)
for s in (1, -1):
    y = s * 612
    for x0, x1 in ((-1700, -900), (-700, 100), (300, 1200)):
        truss(bm, (x0, y + s * 30, 150), (x1, y + s * 30, 150), w=40, seg=int((x1 - x0) / 60), mi=DARK, rod=2.0)
        truss(bm, (x0, y + s * 30, -150), (x1, y + s * 30, -150), w=40, seg=int((x1 - x0) / 60), mi=DARK, rod=2.0)
        for k in range(int((x1 - x0) / 160)):
            x = x0 + 80 + k * 160
            if (k + (x0 // 100)) % 3 == 0:
                tank_cage(bm, (x, y + s * 70, 0), 62, axis=(0, 0, 1), capsule=1.4, mi_tank=HULL, mi_cage=DARK, ribs=4)
            else:
                plate(bm, rect(x - 70, x + 70, *sorted((y, y + s * 110))), -120, 240, HULL if k % 2 else GUN, 6.0)
                edge_windows(bm, (x - 60, y + s * 110.5), (x + 60, y + s * 110.5), -110, 50, (0, s), lit=0.2, seed=1220 + x + s, strip=(6, 20))
    ribs(bm, -1750, 1450, y + s * 0.5, -180, 180, s, 60, depth=10, seed=1230 + s, gap=0.6, mi=(GUN, HULL))
# --- the underside: hangar doors and a keel of lift machinery
strips(bm, -1700, 1500, -420, 420, -200, 10, h=6, seed=1240, mi=GUN, down=True)
for x in (-1200, -400, 400, 1100):
    fast_box(bm, (300, 260, 3), Vector((x, 0, -208)), DOWN, DARK, UVL(bm))
    for s in (1, -1):
        fast_box(bm, (300, 4, 3.5), Vector((x, s * 132, -209)), DOWN, WIN, UVL(bm))
# --- drives: six across a deep stern block
plate(bm, rect(-2120, -1980, -440, 440), -150, 300, GUN, 10.0)
for y in (-360, -216, -72, 72, 216, 360):
    engine(bm, (-2140, y, 0), 64, 200)
nav(bm, [(-1800, 720, 0), (1990, 300, 0)], [(-1800, -720, 0), (1990, -300, 0)], [(-500, -400, 780), (1800, 0, 220)])
o = obj("carrier", bm, SM)
result = export_ship(col, root, "Carrier")
