"""
Science vessel, 200 m. ARROWHEAD family (civilian). References (assets/ships/refs/science_ref*): the Stellaris science
ship's long faceted hull and survey beam; the Carrack's heavy armoured body with a wraparound canopy at the nose, a
spine of sensor masts and fins, a big survey dish, drives slung on pylons either side and a lab/hangar module
underneath. Carries the corvette's swept delta wing and accent cowls so it still reads as one navy. Crew ~120.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/science.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipScience")
sc = hull_scale(200)
bm = bmesh.new()
# --- the body: a deep faceted hull, blunt armoured nose over a wraparound canopy (Carrack)
keel(bm, [(104, 2, 2, -4), (90, 8, 7, -3), (66, 14, 12, -1), (20, 17, 15, 1), (-60, 17, 15, 1), (-90, 14, 12, 0), (-98, 11, 9, 0)], HULL, 0.32)
keel(bm, [(97, 4.4, 4.0, -3.6), (86, 10.6, 9.4, -2.4), (74, 13.2, 11.6, -1.4), (70, 13.6, 11.8, -1.2)], DARK, 0.4)
for s_ in (1, -1):
    edge_windows(bm, (92, s_ * 9.6), (74, s_ * 13.4), -4, 2, (0.2, s_), lit=0.9, seed=1806 + s_, strip=(3, 6))       # lit canopy slits                                   # the canopy band across the nose
plate(bm, mirror([(64, 0), (58, 10), (-70, 12), (-80, 0)]), 15, 4, GUN, 1.0, slope=(-2, 0))
plate(bm, mirror([(30, 0), (24, 8), (-50, 9), (-56, 0)]), 19, 5, LIGHT, 1.0, slope=(-3, 0))
plate(bm, mirror([(0, 0), (-4, 6), (-36, 7), (-40, 0)]), 24, 4, GUN, 0.8)               # dorsal armour spine
strips(bm, -70, 60, -10, 10, 19, 6, h=0.8, seed=1800, mi=GUN)
for s in (1, -1):
    ribs(bm, -84, 60, s * 17.2, -10, 6, s, 6, depth=1.2, seed=1802 + s, gap=0.2, mi=(GUN, HULL))
    windows_side(bm, -70, 50, s * 17.6, -6, 4, s, seed=1804 + s, lit=0.18, spacing=3.4)
    airlock(bm, -20, s * 17.4, -10, s)
    fast_box(bm, (60, 1.6, 0.3), Vector((40, s * 12.5, 15.3)), Rz(s * -0.12), ACC, UVL(bm))                # accent stripes down the nose
# --- the delta wing: low and swept, as the corvette's, carrying the drive pylons
wing = mirror([(30, 0.0), (-40, 30.0), (-62, 44.0), (-74, 44.0), (-70, 30.0), (-90, 14.0), (-90, 0.0)])
plate(bm, wing, -8, 2.6, GUN, 1.0)
for s in (1, -1):
    # drive nacelles on the wingtips (Carrack's slung engines), cowled in the accent colour
    loft(bm, [(-26, 2, 2, 0.4), (-34, 5.6, 5.6, 0.4), (-80, 6.2, 6.2, 0.4), (-88, 5.4, 5.4, 0.4)], I3, Vector((0, s * 42, -4)), HULL)
    band(bm, Vector((-36, s * 42, -4)), (1, 0, 0), 5.8, 3.0, 0.8, mi=ACC, seg=24)
    engine(bm, (-92, s * 42, -4), 5.0, 14)
    plate(bm, rect(-70, -44, *sorted((s * 16, s * 38))), -6, 3, HULL, 0.6)                                  # pylon
# --- sensors: a spine of masts and swept fins, a big survey dish forward (Carrack / Stellaris survey beam)
for x, h in ((10, 18), (-6, 26), (-30, 14), (-46, 20)):
    cyl(bm, 0.6, 0.35, h, Vector((x, 0, 28 + h / 2)), seg=8, mi=DARK)
for s in (1, -1):
    plate(bm, [(-60, s * 9), (-60, s * 10), (-84, s * 13), (-86, s * 12)] if s > 0 else [(-60, s * 9), (-86, s * 12), (-84, s * 13), (-60, s * 10)],
          16, 8, GUN, 0.3, slope=(-6, s * 1.5))
# the aft sensor tower (Carrack), stepped, with its own fin
plate(bm, mirror([(-56, 0), (-60, 7), (-80, 8), (-84, 0)]), 19, 16, GUN, 1.0, slope=(-6, 0))
plate(bm, mirror([(-64, 0), (-66, 5), (-78, 5), (-80, 0)]), 35, 8, LIGHT, 0.6)
plate(bm, [(-68, -0.5), (-68, 0.5), (-84, 0.5), (-84, -0.5)], 43, 14, GUN, 0.2, slope=(-8, 0))
edge_windows(bm, (-56.5, -6.8), (-56.5, 6.8), 26, 2, (1, 0), lit=0.6, seed=1808, strip=(3, 10))
dish(bm, (-20, 0, 31), (0.6, 0, 0.8), 8, mi_dish=LIGHT, mi_frame=DARK)
bridge(bm, 28, 24, 14, 4.0, 12)
# --- the lab module underneath: a ventral pod with a hangar door and the survey emitter at its nose
loft(bm, [(60, 3, 3, 0.4), (50, 8, 6, 0.4), (-40, 9, 7, 0.4), (-52, 7, 5, 0.4)], I3, Vector((0, 0, -19)), DARK)
fast_box(bm, (24, 10, 0.4), Vector((-10, 0, -26.2)), DOWN, DARK, UVL(bm))
for s in (1, -1):
    fast_box(bm, (24, 0.5, 0.5), Vector((-10, s * 5.2, -26.4)), DOWN, WIN, UVL(bm))
cyl(bm, 2.0, 2.0, 1.0, Vector((61, 0, -19)), Ry(math.pi / 2), seg=16, mi=DRIVE)
# --- main drives in the stern
plate(bm, rect(-102, -96, -10, 10), -10, 20, DARK, 1.0)
for y in (-5.5, 5.5):
    engine(bm, (-104, y, 0), 5.0, 14)
nav(bm, [(-74, 44.6, -8), (100, 3, 0)], [(-74, -44.6, -8), (100, -3, 0)], [(34, 0, 46), (-30, 0, 40)])
o = obj("science", bm, SM)
result = export_ship(col, root, "Science")
