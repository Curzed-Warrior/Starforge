"""
Barracks, 300 m: an orbital training base where a colony raises its armies. References (the Bing "military space
station" concept sheets: a garrison hub with a habitat wheel, flight decks with dropships lined up, a gunnery range
boom): a squat armoured hub drum on Z, a spin-gravity barracks ring on four spokes with rows of lit bunk windows and
an owner-colour stripe round its crown, a flight deck on the hub's roof with landers parked on painted pads, and a
live-fire range boom reaching out with target boards. Lean: about 4,500 triangles.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/war/barracks.py").read())
"""
exec(open(globals().get("SF_ROOT", r"C:/Users/diamo/Desktop/starforge") + "/assets/ships/war/warkit.py").read())
col, root = setup("WarBarracks")
hull_scale(250)
bm = bmesh.new()
# --- the hub: an armoured drum with a stepped crown and a docking keel under it
cyl(bm, 30, 30, 70, Vector((0, 0, 0)), seg=12, mi=HULL)
cyl(bm, 34, 34, 10, Vector((0, 0, 30)), seg=12, mi=GUN)
cyl(bm, 30, 20, 16, Vector((0, 0, -43)), seg=12, mi=DARK)
cyl(bm, 8, 8, 40, Vector((0, 0, -66)), seg=8, mi=GUN)
band(bm, Vector((0, 0, -10)), (0, 0, 1), 30, 6, 1.5, mi=OWN, seg=12)
for z in (-24, -16, 6, 14, 22):
    strip_lights(bm, 30.2, z, 24, WIN, keep=0.6, seed=z, h=1.0, depth=0.4)
# --- the barracks ring on four spokes
solid_ring(bm, 112, 134, -12, 12, 24, mats=(GUN, DARK, HULL, LIGHT))
solid_ring(bm, 117, 129, 12, 13.6, 24, mats=(OWN, OWN, OWN, OWN), bevel=0)                               # crown stripe
for z in (-6, 0, 6):
    strip_lights(bm, 134.1, z, 96, WIN, keep=0.55, seed=7 + z, h=1.1, depth=0.4)
for k in range(12):                                                                                   # armoured bulkheads
    a = (k + 0.5) * math.pi / 6
    d = Vector((math.cos(a), math.sin(a), 0))
    fast_box(bm, (8, 30, 3), d * 134.5, Matrix((Vector((-d.y, d.x, 0)), Vector((0, 0, 1)), d)).transposed(), GUN, UVL(bm), bottom=True)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    d = Vector((math.cos(a), math.sin(a), 0))
    F = basis(d)
    fast_box(bm, (84, 10, 10), d * 72, F, GUN, UVL(bm), bottom=True)
    fast_box(bm, (80, 0.4, 1.2), d * 72 + F.col[1] * 5.1, F, WIN, UVL(bm))
# --- the flight deck on the roof: a square pad deck, four landers on painted pads, a control cupola
fast_box(bm, (96, 96, 6), Vector((0, 0, 38)), UP, DARK, UVL(bm), bottom=True)
fast_box(bm, (20, 20, 12), Vector((-36, -36, 47)), UP, LIGHT, UVL(bm), bottom=True)
fast_box(bm, (20.2, 20.2, 2), Vector((-36, -36, 48)), UP, WIN, UVL(bm), bottom=False)
for px, py in ((24, 24), (24, -24), (-12, 24), (24, 0)):
    fast_box(bm, (22, 22, 0.4), Vector((px, py, 41.2)), UP, ACC, UVL(bm))
    fast_box(bm, (18, 18, 0.5), Vector((px, py, 41.3)), UP, DARK, UVL(bm))
    keel(bm, [(px - 8, 3.2, 2.2, 44.6), (px + 3, 3.6, 2.4, 44.6), (px + 8, 1.4, 1.2, 44.2)], GUN, 0.45)   # a lander
    for s in (1, -1):
        fast_box(bm, (3, 0.6, 1.4), Vector((px - 6, py + s * 3.8, 44.6)), UP, DARK, UVL(bm), bottom=True)
for e in [(48, 0), (-48, 0), (0, 48), (0, -48)]:
    fast_box(bm, (2 if e[1] == 0 else 90, 90 if e[1] == 0 else 2, 0.6), Vector((e[0], e[1], 41.3)), UP, WIN, UVL(bm))   # deck edge lights
# --- the live-fire range: a boom out past the ring, target boards hung along it
for (a, b) in (((0, -36, 0), (0, -112, 0)), ((0, -134, 0), (0, -240, 0))):
    fast_box(bm, (8, abs(b[1] - a[1]), 8), Vector((0, (a[1] + b[1]) / 2, 0)), UP, DARK, UVL(bm), bottom=True)
for i in range(5):
    y = -150 - i * 20
    fast_box(bm, (14, 1.2, 14), Vector((0, y, 15)), UP, ACC if i % 2 else LIGHT, UVL(bm), bottom=True)
    fast_box(bm, (1.5, 1.5, 8), Vector((0, y, 6)), UP, GUN, UVL(bm), bottom=True)
fast_box(bm, (18, 18, 12), Vector((0, -246, 0)), UP, GUN, UVL(bm), bottom=True)
nav(bm, [(0, 134, 0)], [(0, -252, 0)], [(0, 0, 50), (134, 0, 12), (-134, 0, 12)])
o = obj("barracks", bm, SM)
result = export_war(col, root, "Barracks")
