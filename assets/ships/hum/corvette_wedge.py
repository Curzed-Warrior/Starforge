"""
Corvette, 120 m. References (assets/ships/refs/corvette_ref*): the Koensayr corvette's broad swept rear wing and
dorsal dish; the Akula's dense spine and clustered round engines; the faceted server corvette's angular prow plates;
the Warspite's long needle nose. Crew ~40 on five decks.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/corvette.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipCorvette")
bm = bmesh.new()
L = 120.0
# --- the keel: a long faceted body, deep at the stern, thinning to a needle prow
keel(bm, [(-50, 8.5, 7.5, 0), (-30, 8.0, 7.5, 0.5), (0, 6.5, 6.5, 0.5), (25, 4.5, 4.8, 0.3), (48, 2.4, 2.6, 0), (60, 0.6, 0.8, 0)], LIGHT, 0.42)
# --- prow armour: two raked plates over the forward keel, a trench between them
plate(bm, mirror([(52, 0.6), (30, 4.8), (2, 6.4), (-8, 6.6)]), 4.2, 2.2, LIGHT, 1.2, slope=(-1.0, 0))
plate(bm, mirror([(44, 0.6), (26, 4.0), (0, 5.4)]), -4.6, 1.8, HULL, 1.0, down=True)
trench(bm, -6, 30, -1.4, 1.4, 6.6, 1.6, 11)
# --- the swept rear wing (Koensayr): one broad angular slab through the stern, tips turned down
wing = mirror([(8, 0.0), (-8, 9.0), (-34, 25.0), (-46, 27.0), (-50, 24.0), (-50, 0.0)])
plate(bm, wing, -1.2, 2.4, LIGHT, 1.0)
for s in (1, -1):
    tip = [(-30, s * 24.6), (-48, s * 27.6), (-50, s * 27.6), (-50, s * 24.6)]
    if s < 0: tip = list(reversed(tip))
    plate(bm, tip, -1.2, 1.0, HULL, 0.2)
    fast_box(bm, (14, 1.2, 9), Vector((-43, s * 26.8, -4.4)), UP, HULL, UVL(bm))           # turned-down tip fin
    fast_box(bm, (12, 1.3, 0.5), Vector((-43, s * 26.8, -8.6)), UP, ACC, UVL(bm))
# --- dorsal superstructure: a stepped spine and the raised bridge, a sensor dish to port
plate(bm, mirror([(-6, 0.0), (-10, 4.2), (-38, 4.6), (-44, 0.0)]), 7.5, 3.6, HULL, 1.0)
trench(bm, -40, -12, -3.2, -1.6, 11.1, 1.4, 21)
trench(bm, -40, -12, 1.6, 3.2, 11.1, 1.4, 22)
bridge(bm, -14, 11.1, 7.0, 4.4, 12)
dish(bm, (-24, 10.5, 3.5), (0.5, 0.55, 0.65), 6.5, mi_dish=LIGHT, mi_frame=DARK)
# --- ventral keel fin and cargo blister
plate(bm, mirror([(10, 0.0), (0, 2.2), (-40, 2.6), (-46, 0.0)]), -7.5, 4.0, HULL, 0.8, down=True)
# --- engines: a cluster of four big pods and two small, behind an engine block
plate(bm, [(-44, -10), (-44, 10), (-54, 10), (-54, -10)], -7, 14, HULL, 0.8)
# four big drive pods (the Akula's cluster) on short pylons, two manoeuvring drives outboard on the wing
for y, z in ((6.2, 4.6), (-6.2, 4.6), (6.2, -4.2), (-6.2, -4.2)):
    engine(bm, (-50, y, z), 4.6, 18)
for s in (1, -1):
    engine(bm, (-44, s * 17.0, 0.0), 2.6, 12)
    fast_box(bm, (10, 6, 1.6), Vector((-44, s * 14.0, 0.0)), UP, DARK, UVL(bm))
# --- guns: two twin turrets dorsal (fore of the bridge, on the spine), one ventral
turret(bm, (8, 0, 6.4), 7)
turret(bm, (-34, 0, 11.1), 5)
turret(bm, (14, 0, -6.4), 6, down=True)
# --- hull detail: panel patchwork on the big faces, trenches along the flanks
panels(bm, plane_surface((-48, -24, 1.25), (54, 0, 0), (0, 48, 0)), 31, accent=0.015)
panels(bm, plane_surface((-6, -5.6, 6.45), (50, 0, 0), (0, 11.2, 0)), 32, scale=0.8)
for s in (1, -1):
    surf = plane_surface((-48, s * 8.6, -5.5), (60, 0, 0), (0, 0, 11)) if s > 0 else plane_surface((12, s * 8.6, -5.5), (-60, 0, 0), (0, 0, 11))
    panels(bm, surf, 40 + s, scale=0.7)
    windows_side(bm, -40, 20, s * 8.75, -3.6, 3, s, seed=50 + s)
    airlock(bm, -22, s * 8.7, -3.8, s)
    airlock(bm, 8, s * 6.9, -3.0, s)
# --- dark greeble trenches along both flanks and down the spine (the Akula's busy dark seams)
for s in (1, -1):
    for x0, x1, w in ((-44, -6, 8.4), (-6, 26, 6.6)):
        y0, y1 = (w - 0.4, w + 0.9) if s > 0 else (-w - 0.9, -w + 0.4)
        surf = plane_surface((x0, s * (w + 0.5), -1.0), (x1 - x0, 0, 0), (0, 0, 2.4)) if s > 0 else plane_surface((x1, s * (w + 0.5), -1.0), (x0 - x1, 0, 0), (0, 0, 2.4))
        fast_box(bm, (x1 - x0, 2.6, 0.6), Vector(((x0 + x1) / 2, s * (w + 0.1), 0.2)), flank(s), DARK, UVL(bm))
        greeble(bm, surf, 0.9, 70 + int(x0) + s, size=(0.3, 1.2), height=(0.15, 0.7), mats=(HULL, DARK), window=WIN, window_frac=0.03, stack=0.3, pipes=0.15, dark=0.6)
trench(bm, 0, 44, -0.9, 0.9, 6.45, 1.2, 23, scale=0.8)
# --- lights
nav(bm, [(-47, 27.8, -0.2), (55, 1.2, 0.5)], [(-47, -27.8, -0.2), (55, -1.2, 0.5)], [(-14, 0, 15.8), (-44, 0, 11.4)])
o = obj("corvette", bm, SM)
result = export_ship(col, root, "Corvette")
