"""
Construction kit for megastructures being built: a scaffold gantry (girders, a crane, floodlights) that rides the
build front, and the constructor drones that swarm over it. Both are instanced many times, so they stay light.
Units are metres, Z up (the game's +Y, "away from the structure"); the game scales them to the structure.
Run inside Blender:
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/construction.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
CM = ["Hull", "HullDark", "Hazard", "Glow", "Window", "NavRed", "Strobe"]
(HULL, DARK, HAZ, GLOW, WIN, RED, STROBE) = range(len(CM))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))


def scaffold():
    """A gantry 40 m long and 12 wide: two girders joined by rungs, a deck of hazard-striped plates, a crane."""
    col, root = setup("Scaffold")
    bm = bmesh.new()
    L, W = 40.0, 12.0
    for y in (-W / 2, W / 2):
        truss(bm, (-L / 2, y, 0), (L / 2, y, 0), w=2.0, seg=8, mi=DARK, rod=0.14)
    for k in range(5):
        x = -L / 2 + k * L / 4
        truss(bm, (x, -W / 2, 0), (x, W / 2, 0), w=1.4, seg=3, mi=HULL, rod=0.1)
    # deck plates, some missing (still being laid)
    rnd = random.Random(3)
    for i in range(8):
        for j in range(2):
            if rnd.random() < 0.3: continue
            box(bm, (4.6, 5.4, 0.25), (-L / 2 + 2.5 + i * 5, (j - 0.5) * 5.6, 1.2), mi=HAZ if (i + j) % 5 == 0 else HULL)
    # the crane: a mast, a jib reaching out, a hook block
    cyl(bm, 0.6, 0.45, 14, (L / 2 - 4, 0, 8), mi=DARK, seg=8)
    truss(bm, (L / 2 - 4, 0, 15), (-L / 2 + 6, 0, 15), w=1.4, seg=10, mi=HAZ, rod=0.1)
    _rod(bm, Vector((-L / 2 + 8, 0, 15)), Vector((-L / 2 + 8, 0, 6)), 0.06, DARK)
    box(bm, (1.6, 1.6, 1.2), (-L / 2 + 8, 0, 5.5), mi=HAZ)
    # floodlights on the corners, a beacon on the mast
    for x in (-L / 2, L / 2):
        for y in (-W / 2, W / 2):
            box(bm, (0.9, 0.9, 0.6), (x, y, 1.6), mi=STROBE)
    box(bm, (0.7, 0.7, 0.7), (L / 2 - 4, 0, 15.8), mi=RED)
    obj("scaffold", bm, CM)
    return export(col, root, "scaffold")


def constructor():
    """A constructor drone 18 m long: a stubby work hull, a lit cab, two manipulator arms reaching forward (+X)."""
    col, root = setup("Constructor")
    bm = bmesh.new()
    box(bm, (10, 6, 4), (0, 0, 0), mi=HULL)
    box(bm, (4, 5, 2.5), (6, 0, 0.6), mi=DARK)
    box(bm, (0.3, 4, 1.0), (8.1, 0, 0.9), mi=WIN)               # the cab's window band
    box(bm, (10.2, 6.2, 0.6), (0, 0, -2.1), mi=HAZ)             # hazard skirt
    for sy in (-1, 1):
        # the arms: shoulder, upper arm, forearm, claw
        sph = Vector((7, sy * 3.2, -1))
        elbow = sph + Vector((4, sy * 0.8, -1.5))
        hand = elbow + Vector((4, -sy * 0.6, 0.5))
        cyl(bm, 0.8, 0.8, 1.2, sph, Rx(math.pi / 2), seg=8, mi=DARK)
        _rod(bm, sph, elbow, 0.35, HAZ)
        _rod(bm, elbow, hand, 0.28, HULL)
        box(bm, (1.0, 0.4, 0.9), hand + Vector((0.4, 0.3, 0)), mi=DARK)
        box(bm, (1.0, 0.4, 0.9), hand + Vector((0.4, -0.3, 0)), mi=DARK)
        box(bm, (0.5, 0.5, 0.5), hand + Vector((1.0, 0, 0)), mi=GLOW)    # the welding torch
        # engine pods and their drives
        cyl(bm, 1.2, 0.9, 4, (-5.5, sy * 2.6, 0), Ry(math.pi / 2), seg=10, mi=DARK)
        cyl(bm, 0.8, 0.8, 0.2, (-7.6, sy * 2.6, 0), Ry(math.pi / 2), seg=10, mi=GLOW)
        box(bm, (0.4, 0.4, 0.4), (0, sy * 3.2, 2.1), mi=RED if sy < 0 else STROBE)
    obj("constructor", bm, CM)
    return export(col, root, "constructor")


result = {"scaffold": scaffold(), "constructor": constructor()}
