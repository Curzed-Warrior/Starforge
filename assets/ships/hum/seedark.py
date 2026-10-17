"""
Seed ark, 20 km. INDUSTRIAL family: a generation ship, crewed, slow, built to arrive. A faceted dust shield at the bow
takes the interstellar medium at a tenth of light; behind it two counter-rotating habitat drums (exported as SpinA
and SpinB so the game can turn them) give the crew a floor; then a long truss spine of tank farms and cargo, radiator
vanes, and a fusion torch at the stern. Same plating, windows and drives as the navy.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/seedark.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipSeedArk")
sc = hull_scale(20000)
X = Vector((1, 0, 0))

# ---------- the spine, shield, tanks, radiators and drive ----------
bm = bmesh.new()
# the dust shield: layered faceted cones, ablative plates on the face, a whipple ring behind
keel(bm, [(9100, 300, 300, 0), (9400, 1350, 1350, 0), (9550, 1500, 1500, 0), (9800, 1150, 1150, 0), (10200, 520, 520, 0), (10450, 120, 120, 0)], GUN, 0.42)
keel(bm, [(10400, 150, 150, 0), (10650, 30, 30, 0)], HULL, 0.42)
for k in range(24):
    a = k * math.tau / 24
    u, t = Vector((0, math.cos(a), math.sin(a))), Vector((0, -math.sin(a), math.cos(a)))
    R = Matrix((X, -t, u)).transposed()
    fast_box(bm, (520, 260, 40), Vector((9950, 0, 0)) + u * 900, R, HULL if k % 2 else LIGHT, UVL(bm))                # ablative plates
band(bm, Vector((9480, 0, 0)), (1, 0, 0), 1420, 90, 60, mi=ACC, seg=24)
# the spine: a square truss the length of the ship, a pressurised core inside it
keel(bm, [(-8600, 160, 160, 0), (9300, 160, 160, 0)], GUN, 0.3)
for s, z in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
    truss(bm, (-8400, s * 240, z * 240), (9200, s * 240, z * 240), w=60, seg=120, mi=DARK, rod=6.0)
# bearings for the drums
for x in (6800, 2600):
    band(bm, Vector((x, 0, 0)), (1, 0, 0), 300, 2200, 120, mi=HULL, seg=24)
# the tank farm: rings of capsules round the spine, cargo pods between
for x in (-500, -1700, -2900, -4100):
    for k in range(6):
        a = k * math.tau / 6 + (0.5 if (x // 1200) % 2 else 0)
        tank_cage(bm, (x, math.cos(a) * 620, math.sin(a) * 620), 260, axis=(1, 0, 0), capsule=1.9, mi_tank=LIGHT if k % 2 else HULL, mi_cage=DARK, ribs=4)
    band(bm, Vector((x, 0, 0)), (1, 0, 0), 300, 180, 60, mi=ACC, seg=12)
for x in (-5200, -5900):
    for k in range(8):
        a = k * math.tau / 8
        u, t = Vector((0, math.cos(a), math.sin(a))), Vector((0, -math.sin(a), math.cos(a)))
        R = Matrix((X, -t, u)).transposed()
        fast_box(bm, (560, 300, 300), Vector((x, 0, 0)) + u * 450, R, TAN if k % 3 else HULL, UVL(bm), bottom=True)
# radiator vanes: four long dark fins, faintly warm
for k in range(4):
    a = k * math.tau / 4 + math.tau / 8
    u, t = Vector((0, math.cos(a), math.sin(a))), Vector((0, -math.sin(a), math.cos(a)))
    R = Matrix((X, -t, u)).transposed()
    fast_box(bm, (2400, 40, 1500), Vector((-7000, 0, 0)) + u * 1000, R, DARK, UVL(bm), bottom=True)                    # radial fin
# the drive: a thrust frame, a shadow shield, the magnetic nozzle ring and its glowing throat
keel(bm, [(-8600, 500, 500, 0), (-8900, 700, 700, 0), (-9300, 700, 700, 0), (-9400, 520, 520, 0)], GUN, 0.35)
band(bm, Vector((-9700, 0, 0)), (1, 0, 0), 820, 140, 90, mi=DARK, seg=32)
band(bm, Vector((-9900, 0, 0)), (1, 0, 0), 900, 100, 70, mi=DARK, seg=32)
for k in range(8):
    a = k * math.tau / 8
    truss(bm, (-9400, math.cos(a) * 500, math.sin(a) * 500), (-9900, math.cos(a) * 860, math.sin(a) * 860), w=30, seg=6, mi=DARK, rod=4.0)
cyl(bm, 380, 380, 40, Vector((-9420, 0, 0)), Ry(math.pi / 2), seg=32, mi=DRIVE)
nav(bm, [(9850, 1500, 0), (-9900, 900, 0)], [(9850, -1500, 0), (-9900, -900, 0)], [(6800, 0, 1300), (2600, 0, 1300)])
obj("seedark", bm, SM)

# ---------- the habitat drums: separate objects, centred on the spine, so they can turn ----------
for name, x, seed in (("SpinA", 6800, 1), ("SpinB", 2600, 2)):
    b2 = bmesh.new()
    cyl(b2, 1100, 1100, 1700, Vector((x, 0, 0)), Ry(math.pi / 2), seg=32, mi=HULL)
    for d in (-1, 1):
        band(b2, Vector((x + d * 830, 0, 0)), (1, 0, 0), 1100, 60, 50, mi=GUN, seg=32)
        cyl(b2, 1060, 300, 60, Vector((x + d * 880, 0, 0)), Ry(math.pi / 2), seg=32, mi=GUN)       # end caps, coned to the bearing
    for k in range(16):
        a = (k + 0.5) * math.tau / 16
        u, t = Vector((0, math.cos(a), math.sin(a))), Vector((0, -math.sin(a), math.cos(a)))
        R = Matrix((X, -t, u)).transposed()
        if k % 2:
            fast_box(b2, (1500, 70, 8), Vector((x, 0, 0)) + u * 1099, R, DARK, UVL(b2))
            fast_box(b2, (1500, 26, 10), Vector((x, 0, 0)) + u * 1100, R, WIN, UVL(b2))
        else:
            fast_box(b2, (1500, 330, 14), Vector((x, 0, 0)) + u * 1098, R, LIGHT if (k // 2 + seed) % 2 else GUN, UVL(b2))
    # spokes to the bearing
    for k in range(6):
        a = k * math.tau / 6
        truss(b2, (x, math.cos(a) * 320, math.sin(a) * 320), (x, math.cos(a) * 1080, math.sin(a) * 1080), w=60, seg=8, mi=DARK, rod=6.0)
    obj(name, b2, SM)

result = export_ship(col, root, "SeedArk")
