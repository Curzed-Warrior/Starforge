"""
Shuttle, 40 m. ARROWHEAD family (the smallest hull: crew rotations, ship-to-surface runs). References (Bing sheets
"sci-fi orbital shuttle concept art": the Marconi MCA-28 transport shuttle, Mitchell Stuart's shuttles, Col Price's
spaceship sketches): a deep lifting body with a raised glazed cockpit, a cargo bay behind it under two dorsal doors,
cranked delta wings with turned-down tips, twin canted fins, two main drives and an aft ramp. Same gunmetal, paint,
windows and lights as the navy. Crew 2, up to 24 seated.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/shuttle.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipShuttle")
hull_scale(40)
bm = bmesh.new()
# --- the lifting body: blunt rounded nose, deep cabin, a flat belly that widens over the drives
keel(bm, [(20.5, 0.8, 0.6, -0.4), (18.5, 2.6, 1.8, -0.1), (14, 4.0, 2.8, 0.3), (6, 4.6, 3.2, 0.5), (-10, 5.0, 3.2, 0.5),
          (-17, 4.8, 2.8, 0.4), (-19.5, 4.0, 2.3, 0.4)], GUN, 0.42)
plate(bm, mirror([(15.5, 0.3), (13, 2.4), (-17, 3.4), (-19, 2.6)]), 3.55, 0.3, LIGHT, 0.4)                # pale dorsal plating
# the raised cockpit: a glazed hump on the forward body, a frame band behind it
keel(bm, [(17.8, 1.2, 0.5, 2.0), (15.5, 2.4, 1.2, 3.1), (12, 2.6, 1.4, 3.6), (9.5, 2.2, 1.0, 3.6)], DARK, 0.5)
keel(bm, [(17.4, 1.0, 0.35, 2.25), (15.6, 2.25, 0.7, 3.3), (13.6, 2.45, 0.75, 3.75)], GLASS, 0.5)       # canopy glazing
fast_box(bm, (0.5, 5.0, 0.5), Vector((13.3, 0, 4.0)), UP, DARK, UVL(bm))
# the cargo bay doors: two long dorsal panels with a seam down the middle, the ship's one accent stripe beside them
for s in (1, -1):
    plate(bm, rect(-15, 8, s * 0.12, s * 2.6), 3.85, 0.35, HULL, 0.15)
    fast_box(bm, (18, 0.25, 0.15), Vector((-3.5, s * 2.75, 4.0)), UP, ACC, UVL(bm))
    windows_side(bm, 1, 8.5, s * 4.85, 0.6, 1, s, seed=31 + s, lit=0.6, spacing=1.2)
    airlock(bm, 4.0, s * 4.85, -1.2, s)
# --- the wings: cranked deltas from the hips, armoured leading edge, tips turned down
for s in (1, -1):
    plate(bm, [(2, s * 4.6), (-9, s * 10.5), (-15, s * 15.0), (-18.5, s * 15.0), (-18.5, s * 4.6)], -1.6, 0.6, LIGHT, 0.2)
    plate(bm, [(2, s * 4.6), (-9, s * 10.5), (-9.6, s * 10.4), (1.0, s * 4.6)], -1.65, 0.75, DARK, 0.1)
    # a drive nacelle under each wing root (Marconi MCA-28)
    made = keel(bm, [(-2, 0.6, 0.6, 0), (-6, 1.3, 1.1, 0), (-16, 1.4, 1.2, 0), (-18.5, 1.2, 1.0, 0)], GUN, 0.4)
    bmesh.ops.translate(bm, vec=Vector((0, s * 6.8, -1.9)), verts=list({v for f in made for v in f.verts}))
    engine(bm, (-18.8, s * 6.8, -1.9), 0.9, 2.4, mi=GUN, bands=2, seg=12)
    fast_box(bm, (6, 0.12, 0.3), Vector((-11, s * 8.25, -1.5)), flank(s), ACC, UVL(bm))
    fast_prism(bm, [(0, -0.2), (0, 0.2), (-3.5, 0.2), (-3.5, -0.2)], 2.4, Vector((-15.0, s * 15.0, -1.1)),
               Matrix.Rotation(s * 2.7, 3, "X"), GUN, UVL(bm), top=(0.6, 1.0, -1.0, 0.0))       # downturned tip
    fast_box(bm, (2.4, 0.15, 0.12), Vector((-11.5, s * 12.0, -0.95)), UP, ACC, UVL(bm))           # wing chevron
# --- twin canted tail fins
for s in (1, -1):
    fast_prism(bm, [(0.5, -0.18), (0.5, 0.18), (-4.5, 0.18), (-4.5, -0.18)], 4.6, Vector((-14.5, s * 3.0, 3.4)),
               Matrix.Rotation(-s * 0.35, 3, "X"), LIGHT, UVL(bm), top=(0.5, 1.0, -2.2, 0.0))
# --- two main drives in the tail, a manoeuvring block over them, the aft ramp under
for s in (1, -1):
    engine(bm, (-18.6, s * 1.5, 0.3), 1.05, 3.0, mi=GUN, bands=2, seg=14)
fast_box(bm, (1.6, 3.4, 1.2), Vector((-19.6, 0, 2.6)), UP, DARK, UVL(bm))
plate(bm, rect(-19.5, -13, -2.6, 2.6), -2.75, 0.25, DARK, 0.1, down=True)
fast_box(bm, (0.12, 3.0, 0.08), Vector((-13.1, 0, -3.0)), DOWN, RED, UVL(bm))                     # ramp lamp
# landing gear doors and RCS quads
for x in (12, -8):
    for s in (1, -1):
        fast_box(bm, (2.6, 0.9, 0.1), Vector((x, s * 2.0, -2.75)), DOWN, DARK, UVL(bm))
for x, z in ((17, 0.6), (-17, 1.6)):
    for s in (1, -1):
        fast_box(bm, (0.5, 0.2, 0.5), Vector((x, s * 4.5, z)), flank(s), DARK, UVL(bm))
nav(bm, [(-17, 15.2, -1.2)], [(-17, -15.2, -1.2)], [(-18.8, 3.7, 7.8), (-18.8, -3.7, 7.8)])
o = obj("shuttle", bm, SM)
result = export_ship(col, root, "Shuttle")
