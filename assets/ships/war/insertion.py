"""
Insertion craft, 48 m: the special forces' boat. References (Tae Pang's stealth transport, Archer Ding's Illusion
dropship): a low faceted blade of a fuselage in dark gunmetal, a raked glazed canopy, swept wings that turn down at
the tips, twin canted tail fins, two buried drives with shrouded exhausts, a ventral troop bay. Few lights: one
owner-colour flash down the spine. About 1,300 triangles.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/war/insertion.py").read())
"""
exec(open(globals().get("SF_ROOT", r"C:/Users/diamo/Desktop/starforge") + "/assets/ships/war/warkit.py").read())
col, root = setup("WarInsertion")
hull_scale(48)
bm = bmesh.new()
# --- the fuselage: a flattened octagon, wedge nose, wide hips over the drives
keel(bm, [(-22, 4.4, 2.2, 0.2), (-12, 5.4, 2.8, 0.2), (2, 4.8, 2.6, 0.0), (12, 3.2, 1.9, -0.3), (18, 1.6, 1.0, -0.6), (20.5, 0.4, 0.3, -0.7)], DARK, 0.45)
# the canopy: a raked glazed hump over the forward fuselage
keel(bm, [(1, 1.8, 0.6, 2.6), (7, 2.0, 0.9, 2.1), (13, 0.9, 0.4, 1.3)], GLASS, 0.5)
# dorsal spine plate and the owner flash along it
plate(bm, mirror([(2, 0.8), (-6, 2.6), (-18, 3.2), (-21, 2.4)]), 2.6, 0.7, GUN, 0.3)
fast_box(bm, (14, 1.2, 0.2), Vector((-10, 0, 3.4)), UP, OWN, UVL(bm))
# --- the wings: swept blades from the hips, turned down at the tips
for s in (1, -1):
    w = [(-2, s * 5.0), (-15, s * 15.5), (-19, s * 15.5), (-19, s * 5.0)]
    plate(bm, w, -0.5, 0.5, GUN, 0.25)
    fast_box(bm, (3.6, 0.5, 2.6), Vector((-17.2, s * 15.9, -1.4)), UP, DARK, UVL(bm), bottom=True)
    fast_box(bm, (3.0, 0.2, 0.25), Vector((-12.5, s * 10.5, 0.05)), UP, OWN, UVL(bm))                # wing chevrons
    # the leading-edge strakes running forward along the fuselage
    plate(bm, [(-2, s * 5.0), (10, s * 3.4), (10, s * 2.9), (-2, s * 4.3)], -0.6, 0.35, DARK, 0.1)
# --- twin canted tail fins
for s in (1, -1):
    R = Matrix.Rotation(-s * 0.45, 3, "X")                                                             # leaning outboard
    fast_prism(bm, [(1.5, -0.25), (1.5, 0.25), (-5.0, 0.25), (-5.0, -0.25)], 5.5, Vector((-15.5, s * 3.4, 2.2)), R, GUN, UVL(bm),
               top=(0.55, 1.0, -2.2, 0.0))
# --- the drives: two buried pods with shrouded, flattened exhausts
for s in (1, -1):
    made = keel(bm, [(-21, 2.0, 1.9, 0.0), (-8, 2.2, 2.0, 0.0), (-2, 1.4, 1.2, 0.0)], GUN, 0.4)        # nacelle, built on the axis
    bmesh.ops.translate(bm, vec=Vector((0, s * 6.6, 0.4)), verts=list({v for f in made for v in f.verts}))
    engine(bm, (-21.5, s * 6.6, 0.4), 1.7, 4.5, mi=GUN, bands=2, seg=12)
    fast_box(bm, (2.6, 4.4, 0.4), Vector((-24.2, s * 6.6, 2.5)), UP, DARK, UVL(bm), bottom=True)        # shroud
    owner_band(bm, Vector((-6, s * 6.6, 0.4)), (1, 0, 0), 2.0, 1.2, seg=8)
# --- the ventral troop bay: a hinged ramp outline and landing skids
plate(bm, rect(-12, 4, -3.0, 3.0), -1.6, 0.4, GUN, 0.3, down=True)
fast_box(bm, (0.15, 3.6, 0.1), Vector((-12.1, 0, -2.6)), DOWN, RED, UVL(bm))                            # ramp lamp
for x in (-14, 6):
    for s in (1, -1):
        fast_box(bm, (2.4, 0.5, 0.9), Vector((x, s * 3.4, -2.5)), DOWN, DARK, UVL(bm))
nav(bm, [(-17, 16.3, -1.0)], [(-17, -16.3, -1.0)], [(-19.5, 0, 3.4)])
o = obj("insertion", bm, SM)
result = export_war(col, root, "Insertion")
