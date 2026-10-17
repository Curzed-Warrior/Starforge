"""
Frigate, 300 m. BRICK family (Halo UNSC). References (assets/ships/refs/frigate_ref*): the Charon's long boxy hull
with a split bow (a long upper prow over a lower jaw, the spinal gun's mouth between them) and its blocky stern;
the Paris's slatted armour belts, lengthwise plating and side engine nacelles; the user's dark slab carrier. Same
plating, windows, engines and bridge parts as the wedge ships, so the navy still reads as one. Crew ~300.
(The earlier Arquitens-style wedge is kept in frigate_wedge.py.)
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/frigate.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipFrigate")
sc = hull_scale(300)
bm = bmesh.new()
# --- the main hull: a long chamfered box, deepest amidships, stepping down to the stern block
keel(bm, [(-150, 22, 21, 0), (-100, 24, 23, 1), (40, 23, 22, 2), (70, 19, 18, 3)], GUN, 0.22)
# --- the split bow: a long upper prow over a shorter lower jaw, the gun's mouth between them (Charon)
keel(bm, [(66, 17, 9, 12), (120, 15, 8, 12), (150, 11, 6, 11), (156, 7, 3, 10)], GUN, 0.3)
keel(bm, [(66, 15, 7, -13), (110, 13, 6, -13), (126, 9, 4, -12)], HULL, 0.3)
plate(bm, rect(70, 128, -10, 10), -6.5, 9, DARK, 0.5)                                   # the recess between them
for k in range(3):
    cyl(bm, 5.2 - k * 0.6, 5.2 - k * 0.6, 4, Vector((128 + k * 6, 0, -2)), Ry(math.pi / 2), seg=20, mi=DARK)
cyl(bm, 3.0, 3.0, 2, Vector((146, 0, -2)), Ry(math.pi / 2), seg=16, mi=DRIVE)             # the spinal gun's muzzle glow
# --- armour: slatted belts down both flanks, lengthwise plating on top and bottom (Paris)
for s in (1, -1):
    ribs(bm, -96, 64, s * 24.6, 1, 15, s, 5.0, depth=1.4, seed=800 + s, gap=0.1, mi=(GUN, HULL))
    ribs(bm, -96, 64, s * 24.6, -16, -3, s, 5.0, depth=1.2, seed=806 + s, gap=0.15, mi=(GUN, HULL))
    fast_box(bm, (160, 3.4, 0.8), Vector((-16, s * 24.2, -1.0)), flank(s), DARK, UVL(bm))           # the dark waist recess
    ribs(bm, 70, 118, s * 17.4, 6, 18, s, 4.0, depth=1.0, seed=802 + s, mi=(GUN, HULL))
    windows_side(bm, -90, 60, s * 25.4, -12, 7, s, seed=804 + s, lit=0.22, spacing=3.4)
    airlock(bm, -60, s * 25.2, -16, s)
    airlock(bm, 20, s * 24.4, -15, s)
strips(bm, -96, 64, -20, 20, 24.0, 8, h=0.8, seed=810, mi=GUN)
strips(bm, -96, 60, -18, 18, -22.0, 7, h=0.8, seed=811, down=True)
strips(bm, 72, 150, -12, 12, 21.0, 5, h=0.6, seed=812)
# --- the profile: a raised armoured hump over the forward hull (Charon) and shoulder modules on the flanks (Paris)
plate(bm, mirror([(70, 0), (66, 20), (-20, 22), (-28, 0)]), 23.5, 7, GUN, 2.0, slope=(-3.0, 0))
strips(bm, -18, 62, -16, 16, 30.5, 6, h=0.8, seed=813, mi=GUN)
for s in (1, -1):
    y0, y1 = sorted((s * 24, s * 31))
    plate(bm, rect(-70, -10, y0, y1), -12, 22, HULL, 1.5)
    ribs(bm, -68, -12, s * 31.2, -10, 8, s, 3.5, depth=0.9, seed=840 + s, mi=(GUN, LIGHT))
# --- identification bands round the bow (the Charon's pale stripes)
for x in (96, 104):
    for s in (1, -1):
        fast_box(bm, (3.0, 18, 0.5), Vector((x, s * 15.3, 12)), flank(s), LIGHT, UVL(bm))
    fast_box(bm, (3.0, 30, 0.5), Vector((x, 0, 21.3)), UP, LIGHT, UVL(bm))
# --- the spine: a low trench of machinery with the bridge raised well forward (Charon's tower over the bow)
trench(bm, -80, 30, -6, 6, 25.0, 2.0, 820, scale=1.2)
plate(bm, mirror([(62, 0), (58, 9), (36, 10), (32, 0)]), 30.5, 8, LIGHT, 1.5, slope=(-1.2, 0))
bridge(bm, 61, 38.5, 16, 4.5, 14)
for s in (1, -1):
    dish(bm, (40, s * 8, 40.5), (0.2, s * 0.5, 0.85), 2.8, mi_dish=LIGHT, mi_frame=DARK)
plate(bm, mirror([(-100, 0), (-104, 12), (-132, 14), (-136, 0)]), 24.0, 6, LIGHT, 1.2)      # aft sensor block
turret(bm, (0, 0, 30.5), 7)
turret(bm, (-40, 0, 25), 7)
for s in (1, -1):
    turret(bm, (-120, s * 16, 30), 5)
    turret(bm, (90, s * 10, -19), 5, down=True)
# --- the stern: a deep block, four drives and two side nacelles (Paris)
plate(bm, rect(-158, -142, -20, 20), -18, 38, HULL, 1.5)
for y, z in ((7.5, 8), (-7.5, 8), (7.5, -8), (-7.5, -8)):
    engine(bm, (-160, y, z), 6.6, 22)
for s in (1, -1):
    loft(bm, [(-52, 4, 5, 0.3), (-62, 7, 9, 0.3), (-130, 8, 11, 0.3), (-150, 6.5, 9, 0.3)], I3, Vector((0, s * 32, -6)), GUN)
    plate(bm, rect(-120, -70, *sorted((s * 23, s * 26))), -14, 10, HULL, 0.8)              # nacelle pylon
    engine(bm, (-156, s * 32, -6), 5.6, 16)
    ribs(bm, -128, -66, s * 40.2, -14, 2, s, 3.0, depth=0.8, seed=830 + s)
# --- ventral hangar for the shuttles (40 m), lit
fast_box(bm, (34, 18, 0.5), Vector((-30, 0, -22.3)), DOWN, DARK, UVL(bm))
for s in (1, -1):
    fast_box(bm, (34, 0.6, 0.6), Vector((-30, s * 9.2, -22.6)), DOWN, WIN, UVL(bm))
nav(bm, [(-140, 40.5, -6), (154, 3, 10)], [(-140, -40.5, -6), (154, -3, 10)], [(61, 0, 47.5), (-118, 0, 31)])
o = obj("frigate", bm, SM)
result = export_ship(col, root, "Frigate")
