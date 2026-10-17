"""
Colony ship, 800 m. INDUSTRIAL family (civilian). References (assets/ships/refs/colony_ref*): the Nauvoo's great
habitat drum, ribbed round its girth with long window strips down its length, a tapering bow ending in a needle spire,
the drive section behind; the Nauvoo concepts' exposed frames; the Stellaris colony ship's habitat hull. Carries a
founding population of ~50,000 asleep or in the drum. Gunmetal frames, pale drum plating, the navy's drives.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/colony.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipColony")
sc = hull_scale(800)
bm = bmesh.new()
X = Vector((1, 0, 0))
# --- the drum: a great cylinder of habitat, banded and windowed (Nauvoo)
cyl(bm, 140, 140, 380, Vector((-20, 0, 0)), Ry(math.pi / 2), seg=32, mi=HULL)
for x in (-200, -140, -80, -20, 40, 100, 160):
    band(bm, Vector((x, 0, 0)), (1, 0, 0), 140, 10 if x in (-200, 160) else 6, 5 if x in (-200, 160) else 3, mi=GUN, seg=32)
for k in range(16):
    a = (k + 0.5) * math.pi / 8
    u, t = Vector((0, math.cos(a), math.sin(a))), Vector((0, -math.sin(a), math.cos(a)))
    R = Matrix((X, -t, u)).transposed()                                                                   # right-handed: x * y = z
    for x0 in (-170, -110, -50, 10, 70, 130):
        if k % 2:
            fast_box(bm, (50, 6, 1.0), Vector((x0, 0, 0)) + u * 139.6, R, DARK, UVL(bm))                  # long window strip
            fast_box(bm, (50, 3.0, 1.4), Vector((x0, 0, 0)) + u * 139.8, R, WIN, UVL(bm))
        else:
            fast_box(bm, (50, 22, 2.0), Vector((x0, 0, 0)) + u * 139.6, R, LIGHT if (k // 2 + x0 // 60) % 2 else GUN, UVL(bm))   # plating
# support pods and masts round the drum's forward rim (Nauvoo)
for k in range(6):
    a = k * math.pi / 3
    p = Vector((172, math.cos(a) * 150, math.sin(a) * 150))
    cyl(bm, 18, 18, 56, p, Ry(math.pi / 2), seg=12, mi=GUN)
    band(bm, p, (1, 0, 0), 18, 6, 2, mi=ACC, seg=12)
    cyl(bm, 1.2, 0.6, 60, p + Vector((0, math.cos(a) * 40, math.sin(a) * 40)), Matrix(((1, 0, 0), (0, math.sin(a), math.cos(a)), (0, -math.cos(a), math.sin(a)))).to_4x4(), seg=8, mi=DARK)
# the drum's end bulkheads, stepped
for x, r in ((178, 120), (-218, 120)):
    cyl(bm, r, r, 20, Vector((x, 0, 0)), Ry(math.pi / 2), seg=32, mi=GUN)
band(bm, Vector((171, 0, 0)), (1, 0, 0), 128, 4, 8, mi=WIN, seg=32)                                       # lit rim on the drum's face
# --- the bow: an octagonal taper over exposed frames into the needle spire (Nauvoo)
keel(bm, [(186, 70, 70, 0), (230, 62, 62, 0), (300, 40, 40, 0), (350, 22, 22, 0), (372, 12, 12, 0)], HULL, 0.4)
for k in range(8):
    a = k * math.pi / 4
    truss(bm, (190, math.cos(a) * 106, math.sin(a) * 106), (330, math.cos(a) * 40, math.sin(a) * 40), w=10, seg=12, mi=DARK, rod=1.0)
for x, r in ((210, 100), (260, 78), (310, 56)):
    band(bm, Vector((x, 0, 0)), (1, 0, 0), r, 6, 4, mi=GUN, seg=16)                                           # frame hoops
cyl(bm, 6, 1.5, 120, Vector((430, 0, 0)), Ry(math.pi / 2), seg=12, mi=GUN)                                 # the spire
for x in (380, 400, 420):
    band(bm, Vector((x, 0, 0)), (1, 0, 0), 5.5 - (x - 380) * 0.05, 3, 2, mi=ACC, seg=12)
for s in (1, -1):
    edge_windows(bm, (232, s * 62.5), (296, s * 41), -20, 8, (0.3, s), lit=0.4, seed=2100 + s, strip=(6, 30))
bridge(bm, 300, 40, 30, 5.0, 24)
# --- the drive section: a narrower block behind the drum, tank cages round it, a cluster of drives
keel(bm, [(-228, 96, 96, 0), (-260, 100, 100, 0), (-340, 100, 100, 0), (-372, 86, 86, 0)], GUN, 0.6)
for x in (-262, -338):
    band(bm, Vector((x, 0, 0)), (1, 0, 0), 96, 8, 6, mi=HULL, seg=16)
for k in range(6):
    a = k * math.pi / 3 + math.pi / 6
    tank_cage(bm, (-300, math.cos(a) * 120, math.sin(a) * 120), 22, axis=(1, 0, 0), capsule=1.6, mi_tank=HULL, mi_cage=DARK, ribs=4)
for s in (1, -1):
    ribs(bm, -340, -262, s * 100.3, -60, 60, s, 12, depth=2, seed=2110 + s, gap=0.2, mi=(GUN, HULL))
engine(bm, (-380, 0, 0), 46, 90)
for k in range(6):
    a = k * math.pi / 3
    engine(bm, (-376, math.cos(a) * 62, math.sin(a) * 62), 16, 50)
nav(bm, [(-20, 142, 0), (490, 2, 0)], [(-20, -142, 0), (490, -2, 0)], [(-20, 0, 142), (300, 0, 110)])
o = obj("colony", bm, SM)
result = export_ship(col, root, "Colony")
