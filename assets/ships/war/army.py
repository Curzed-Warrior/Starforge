"""
Army, 150 m across: the marker for ground forces at a world, a landing group of dropships. References (the "Military
Troop Dropship" sheet, the Halo/Star Wars style marine landers in the Bing dropship sheets): boxy armoured troop
bodies with a glazed chin cockpit, four ducted lift engines on stub wings, a rear ramp, a tail boom with a fin; a
heavy command lander leads four troop landers in a wedge. Owner colour on each lander's flank band. Lean: about
3,200 triangles.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/war/army.py").read())
"""
exec(open(globals().get("SF_ROOT", r"C:/Users/diamo/Desktop/starforge") + "/assets/ships/war/warkit.py").read())
col, root = setup("WarArmy")
hull_scale(60)
bm = bmesh.new()


def lander(at, L, heavy=False):
    """One dropship, nose +X, L metres long, centred at `at`."""
    ax, ay, az = at
    w, h = L * 0.12, L * 0.12
    made = keel(bm, [(-L * 0.42, w * 0.9, h * 0.8, 0), (-L * 0.25, w, h, 0), (L * 0.22, w, h, 0), (L * 0.38, w * 0.7, h * 0.7, -h * 0.15),
                     (L * 0.48, w * 0.35, h * 0.35, -h * 0.35)], HULL if heavy else GUN, 0.35)
    glass = keel(bm, [(L * 0.3, w * 0.62, h * 0.3, h * 0.35), (L * 0.42, w * 0.4, h * 0.2, h * 0.05)], GLASS, 0.5)                 # chin cockpit
    boom = keel(bm, [(-L * 0.72, w * 0.3, h * 0.3, h * 0.5), (-L * 0.4, w * 0.5, h * 0.5, h * 0.3)], DARK, 0.4)              # tail boom
    vs = list({v for f in made + glass + boom for v in f.verts})
    bmesh.ops.translate(bm, vec=Vector(at), verts=vs)
    fast_prism(bm, [(L * 0.04, -0.3), (L * 0.04, 0.3), (-L * 0.14, 0.3), (-L * 0.14, -0.3)], h * 1.2,
               Vector((ax - L * 0.62, ay, az + h * 0.7)), UP, DARK, UVL(bm), top=(0.5, 1.0, -L * 0.05, 0.0))                   # tail fin
    for s in (1, -1):
        fast_box(bm, (L * 0.36, 0.3, h * 0.5), Vector((ax - L * 0.02, ay + s * (w + 0.15), az + h * 0.1)), flank(s), OWN, UVL(bm))  # flank band
        fast_box(bm, (L * 0.12, 0.3, h * 0.25), Vector((ax + L * 0.2, ay + s * (w + 0.1), az + h * 0.35)), flank(s), WIN, UVL(bm))
        for fx in (L * 0.22, -L * 0.28):
            # a ducted lift engine on a stub pylon, its fan facing down
            plate(bm, [(ax + fx + L * 0.07, ay + s * w), (ax + fx + L * 0.04, ay + s * w * 2.0), (ax + fx - L * 0.06, ay + s * w * 2.0),
                       (ax + fx - L * 0.08, ay + s * w)], az + h * 0.05, h * 0.22, GUN, h * 0.05)                              # stub wing
            c = Vector((ax + fx, ay + s * w * 2.6, az + h * 0.15))
            cyl(bm, L * 0.07, L * 0.08, h * 1.1, c, Rx(s * 0.25), seg=10, mi=GUN, caps=False)
            cyl(bm, L * 0.06, L * 0.06, h * 0.2, c - Vector((0, 0, h * 0.42)), seg=10, mi=DRIVE)
            cyl(bm, L * 0.088, L * 0.088, h * 0.12, c + Vector((0, 0, h * 0.2)), seg=10, mi=ACC if heavy else DARK, caps=False)
    plate(bm, rect(ax - L * 0.42, ax - L * 0.2, ay - w * 0.7, ay + w * 0.7), az - h, h * 0.12, DARK, h * 0.05, down=True)      # ramp
    if heavy:
        bridge(bm, ax + L * 0.12, az + h, w * 1.3, h * 0.45, L * 0.3, mi=GUN)
        cyl(bm, 0.3, 0.2, h * 1.2, Vector((ax - L * 0.1, ay, az + h * 1.6)), seg=6, mi=DARK)
    nav(bm, [(ax - L * 0.28, ay + w * 2.6 + L * 0.09, az)], [(ax - L * 0.28, ay - w * 2.6 - L * 0.09, az)], [(ax - L * 0.72, ay, az + h * 0.9)])


lander((10, 0, 0), 62, heavy=True)
for i, (x, y, z) in enumerate(((-34, 38, 6), (-34, -38, 6), (-76, 74, 12), (-76, -74, 12))):
    lander((x, y, z), 40)
o = obj("army", bm, SM)
result = export_war(col, root, "Army")
