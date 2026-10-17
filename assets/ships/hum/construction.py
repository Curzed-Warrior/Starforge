"""
Construction ship, 400 m. INDUSTRIAL family (civilian). References (assets/ships/refs/construction_ref*): the
Pioneer's broad flat hull, nearly all of it a fabrication deck under great clamshell hatches, with a cab block at the
nose, hazard-painted plating, gantry cranes over the deck and heavy drives in the stern; the same gunmetal barge,
tank cages and lift pads as the carrier, small. It builds stations and outposts in place. Crew ~600.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/construction.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipConstruction")
sc = hull_scale(400)
bm = bmesh.new()
# --- the barge: a broad, flat, chamfered hull (Pioneer), a stepped lower keel
keel(bm, [(-200, 70, 18, 0), (-180, 78, 22, 0), (150, 78, 22, 0), (185, 60, 18, -2), (200, 36, 12, -4)], GUN, 0.42)
keel(bm, [(-170, 50, 10, -26), (140, 50, 10, -26), (165, 34, 8, -24)], HULL, 0.3)
# --- the fabrication deck: an open bay framed by the hull, the clamshell hatches folded back either side (Pioneer)
plate(bm, rect(-150, 110, -56, 56), 22, 1.5, DARK, 0.3)                                               # bay floor
for k in range(6):
    fast_box(bm, (24, 1.2, 0.3), Vector((90 - k * 44, 0, 23.6)), UP, ACC, UVL(bm))                      # lane dashes
    for s in (1, -1):
        fast_box(bm, (18, 14, 0.3), Vector((80 - k * 44, s * 38, 23.6)), UP, TAN, UVL(bm))             # lift pads
for s in (1, -1):
    y0, y1 = sorted((s * 56, s * 76))
    plate(bm, rect(-160, 120, y0, y1), 22, 8, HULL, 1.0)                                              # deck coaming
    plate(bm, rect(-150, 110, *sorted((s * 72, s * 78))), 30, 10, HULL, 0.8)            # clamshell hatch standing open
    ribs(bm, -146, 106, s * 78.2, 31, 39, s, 6, depth=0.6, seed=1904 + s, gap=0.4, mi=(ACC, TAN))
    ribs(bm, -136, 96, s * 76.3, 23, 29, s, 8, depth=0.8, seed=1900 + s, mi=(ACC, TAN))                # hazard bars on the hatch hinge
    gantry(bm, (-150, s * 62, 30), (110, s * 62, 30), (0, 0, 1), 14, 26, mi_rail=DARK, mi_crane=ACC, cranes=2, seed=1910 + s)
# a station module half-built on the deck: frames, a truss and a tank in its cradle
truss(bm, (-110, 0, 24), (40, 0, 24), w=16, seg=12, mi=DARK, rod=0.8)
loft(bm, [(40, 10, 10, 0.4), (30, 18, 18, 0.4), (-40, 18, 18, 0.4), (-50, 10, 10, 0.4)], I3, Vector((0, 0, 44)), HULL)
ribs(bm, -40, 30, 18.2, 34, 54, 1, 5, depth=1.0, seed=1920, gap=0.6, mi=(DARK, GUN))
tank_cage(bm, (75, 0, 24), 14, axis=(0, 0, 1), capsule=1.3, mi_tank=HULL, mi_cage=DARK, ribs=4)
# --- the cab: a block over the nose, a band of glazing across its front (Pioneer)
plate(bm, mirror([(196, 0), (186, 40), (130, 48), (124, 0)]), 22, 16, LIGHT, 2.0, slope=(-6, 0))
for s in (1, -1):
    edge_windows(bm, (194, s * 6), (184, s * 40), 26, 2, (1, s * 0.3), lit=0.8, seed=1930 + s, strip=(4, 12))
bridge(bm, 160, 38, 24, 3.0, 12)
plate(bm, mirror([(186, 0), (178, 30), (136, 36), (132, 0)]), 38.2, 1.2, ACC, 0.3, slope=(-6, 0))        # red roof panels (Pioneer)
for s in (1, -1):
    fast_box(bm, (60, 10, 0.4), Vector((158, s * 44, 30)), flank(s), ACC, UVL(bm))
cyl(bm, 0.6, 0.4, 24, Vector((140, -20, 50)), seg=8, mi=DARK)
dish(bm, (150, 24, 44), (0.3, 0.3, 0.9), 5, mi_dish=LIGHT, mi_frame=DARK)
# --- the flanks: plated, ribbed, windowed, with tank cages and cargo pods (industrial)
for s in (1, -1):
    ribs(bm, -175, 140, s * 78.3, -18, 18, s, 8, depth=1.0, seed=1940 + s, gap=0.3, mi=(GUN, HULL))
    windows_side(bm, -160, 120, s * 78.6, -14, 3, s, seed=1942 + s, lit=0.2, spacing=4.0)
    for x in (-130, -60, 10, 80):
        tank_cage(bm, (x, s * 86, -4), 7, axis=(1, 0, 0), capsule=2.0, mi_tank=HULL, mi_cage=DARK, ribs=3)
    fast_box(bm, (300, 3, 0.4), Vector((-20, s * 78.8, 14)), flank(s), ACC, UVL(bm))                    # hazard stripe
# --- the stern: a deep engine block, four heavy drives; landing pads underneath
plate(bm, rect(-212, -196, -60, 60), -24, 46, DARK, 2.0)
for y in (-38, -13, 13, 38):
    engine(bm, (-214, y, -2), 11, 34)
for x in (-140, 100):
    for s in (1, -1):
        fast_box(bm, (20, 20, 4), Vector((x, s * 44, -38)), DOWN, DARK, UVL(bm))
nav(bm, [(-180, 108, 26), (200, 36, 0)], [(-180, -108, 26), (200, -36, 0)], [(140, -20, 63), (-150, 0, 60)])
o = obj("construction", bm, SM)
result = export_ship(col, root, "Construction")
