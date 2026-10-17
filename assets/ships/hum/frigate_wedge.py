"""
Frigate, 300 m. References (assets/ships/refs/frigate_ref*): the Arquitens' stepped dagger hull with turret sponsons
on its edges, T-shaped command tower and three big drives; (no neck: the user wants one continuous hull) the Nebulon-B's
a deep keel hanging below the stern section; the Pelta's wedge ending in a heavy engine block. Crew ~400.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/frigate.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipFrigate")
hull_scale(300)
bm = bmesh.new()
# --- the forward dagger: three stepped tiers of angular plate over a ventral wedge
plate(bm, mirror([(150, 0), (90, 17), (10, 30), (-72, 30)]), -5, 10, LIGHT, 3.0)
plate(bm, mirror([(118, 0), (70, 13), (8, 22), (-70, 22)]), 5, 5.5, HULL, 2.4)
plate(bm, mirror([(80, 0), (46, 8), (2, 13), (-60, 13)]), 10.5, 4.0, LIGHT, 1.8)
plate(bm, mirror([(140, 0), (80, 14), (6, 24), (-72, 24)]), -5, 8, HULL, 3.0, down=True)
trench(bm, 10, 70, -2.2, 2.2, 14.5, 2.0, 101, scale=1.4)
for s in (1, -1):
    trench(bm, 12, 80, s * 13 - 2.0, s * 13 + 2.0, 10.5, 1.6, 102 + s, scale=1.4) if s > 0 else trench(bm, 12, 80, -15.0, -11.0, 10.5, 1.6, 103, scale=1.4)
trench(bm, -56, 6, -3.0, 3.0, 14.5, 1.6, 104, scale=1.2)
# --- the stern section: a deep engine block (Pelta / Arquitens) with three big drives
keel(bm, [(-68, 12, 12, 0), (-80, 20, 16, 0), (-128, 21, 16, 0), (-138, 18, 14, 0)], LIGHT, 0.35)
plate(bm, mirror([(-74, 0), (-82, 12), (-130, 13), (-134, 0)]), 16, 4, HULL, 1.5)
for y, z in ((0, 2), (17, -3), (-17, -3)):
    engine(bm, (-140, y, z), 9.5, 26)
# --- the command tower: a raked neck rising off the stern block, a wide bridge crossbar on top (Arquitens)
plate(bm, mirror([(-96, 0), (-100, 5), (-118, 5), (-120, 0)]), 20, 14, LIGHT, 1.5, slope=(-2.0, 0))
plate(bm, mirror([(-100, 0), (-102, 22), (-112, 24), (-116, 0)]), 34, 5.5, LIGHT, 1.2)
bridge(bm, -98, 39.5, 14, 4.2, 10)
for s in (1, -1):
    dish(bm, (-108, s * 23, 41), (0.3, s * 0.3, 0.9), 3.2, mi_dish=LIGHT, mi_frame=DARK)
# --- the hanging keel below the stern (Nebulon-B): a deep blade with sensor pods
plate(bm, mirror([(-70, 0), (-78, 7), (-126, 7), (-132, 0)]), -16, 44, HULL, 5.0, down=True)
for x, d in ((-84, 30), (-100, 36), (-116, 42)):
    plate(bm, mirror([(x + 5, 0), (x + 3, 9), (x - 7, 9), (x - 9, 0)]), -d, 10, LIGHT, 1.2, down=True)
for s in (1, -1):
    windows_side(bm, -124, -80, s * 6.05, -52, 9, s, seed=140 + s, lit=0.5)
# --- weapons: turret sponsons along the dagger's edges, a heavy twin dorsal battery
for x, y in ((92, 16.5), (56, 23), (20, 28)):
    for s in (1, -1):
        y0, y1 = sorted((s * (y - 4), s * (y + 3)))
        plate(bm, rect(x - 8, x + 6, y0, y1), 5, 3, HULL, 0.6)
        turret(bm, (x - 1, s * (y - 0.5), 8), 9)
turret(bm, (40, 0, 14.5), 14)
turret(bm, (20, 0, -13), 11, down=True)
# --- hull detail and the human scale: panels, flank windows (one row per deck), airlocks, a ventral hangar
panels(bm, plane_surface((-8, -20, 10.6), (84, 0, 0), (0, 40, 0)), 111, scale=2.4, accent=0.01, density=0.5)
panels(bm, plane_surface((-126, -12, 16.05), (50, 0, 0), (0, 24, 0)), 112, scale=2.2, accent=0.01, density=0.5)
for s in (1, -1):
    surf = plane_surface((-126, s * 21.1, -10), (56, 0, 0), (0, 0, 20)) if s > 0 else plane_surface((-70, s * 21.1, -10), (-56, 0, 0), (0, 0, 20))
    panels(bm, surf, 120 + s, scale=2.0, accent=0.005, density=0.5)
    windows_side(bm, -124, -76, s * 21.15, -8, 5, s, seed=130 + s)
    windows_side(bm, -66, 6, s * 30.1, -3.5, 2, s, seed=132 + s)
    airlock(bm, -88, s * 21.1, -10.8, s)
    airlock(bm, -40, s * 30.05, -4.5, s)
fast_box(bm, (26, 14, 0.4), Vector((60, 0, -13.1)), DOWN, DARK, UVL(bm))           # hangar mouth (shuttles, 40 m)
fast_box(bm, (26, 0.5, 0.5), Vector((60, 7.2, -13.3)), DOWN, WIN, UVL(bm))
fast_box(bm, (26, 0.5, 0.5), Vector((60, -7.2, -13.3)), DOWN, WIN, UVL(bm))
# --- dark trench bands down both edges of the dagger (the Arquitens' stepped flanks)
for s in (1, -1):
    for (xa, ya), (xb, yb) in (((10, 30), (90, 17)), ((90, 17), (150, 0.5))):
        mx, my = (xa + xb) / 2, (ya + yb) / 2
        ang = math.atan2(yb - ya, xb - xa) * s
        L2 = math.hypot(xb - xa, yb - ya)
        R2 = (Rz(ang) @ Matrix(((1, 0, 0), (0, 0, -s), (0, s, 0))).to_4x4()).to_3x3() if False else None
        n = Vector((-(yb - ya), (xb - xa), 0)).normalized() * s
        t = Vector((xb - xa, s * (yb - ya), 0)).normalized()
        rot = Matrix((t, Vector((0, 0, 1)).cross(t) * 0 + n.cross(t).normalized() * -1, n)).transposed()
        rot = Matrix((t, n.cross(t).normalized(), n)).transposed()
        c = Vector((mx, s * my, 0.0)) + n * 0.3
        fast_box(bm, (L2 * 0.92, 3.2, 0.8), c, rot, DARK, UVL(bm))
        surf = plane_surface(c - t * L2 * 0.45 - Vector((0, 0, 1.4)) + n * 0.4, t * L2 * 0.9, Vector((0, 0, 2.8)))
        greeble(bm, surf, 0.5, 150 + int(xa) + s, size=(0.5, 2.0), height=(0.2, 0.9), mats=(HULL, DARK), window=WIN, window_frac=0.04, stack=0.3, pipes=0.15, dark=0.6)
# --- lights
nav(bm, [(-10, 30.5, 0), (148, 1.5, 0)], [(-10, -30.5, 0), (148, -1.5, 0)], [(-108, 0, 46), (-110, 24, 40), (-110, -24, 40)])
o = obj("frigate", bm, SM)
result = export_ship(col, root, "Frigate")
