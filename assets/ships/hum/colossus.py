"""
Colossus, 35 km. BRICK family. References (assets/ships/refs/colossus_ref*): the Stellaris colossus, a long spindle
hull threaded through a great ring, the planet-cracking beam fired from a pronged emitter at the nose along the
spindle's axis; the printed model's ring on spokes with emitter nodes round it. The spindle is plated, ribbed and
windowed like the battleship and titan; the ring carries the field projectors that focus the beam.
Crew ~2,000,000.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/colossus.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipColossus")
sc = hull_scale(35000)
bm = bmesh.new()
# --- the spindle: a long octagonal hull, thickest amidships, tapering into the emitter at the nose
keel(bm, [(-17500, 1100, 1100, 0), (-16200, 1500, 1500, 0), (9000, 1500, 1500, 0), (12500, 1200, 1200, 0), (14500, 800, 800, 0)], GUN, 0.3)
for s in (1, -1):
    ribs(bm, -15800, 8600, s * 1500.5, -1000, 1000, s, 260, depth=40, seed=1700 + s, gap=0.15, mi=(GUN, HULL))
    edge_windows(bm, (-15800, s * 1545), (8600, s * 1545), -60, 30, (0, s), lit=0.25, seed=1702 + s, strip=(14, 120))
strips(bm, -15800, 8600, -1200, 1200, 1500, 16, h=30, seed=1704, mi=GUN)
strips(bm, -15800, 8600, -1200, 1200, -1500, 16, h=30, seed=1705, mi=GUN, down=True)
# stepped sponsons down the flanks (as the titan), clear of the ring
for x0, x1, z, h, d in ((-15400, -10400, -1100, 900, 300), (-9800, -4200, 300, 700, 220), (1000, 7400, -1000, 800, 280), (-15000, -11000, 400, 600, 180)):
    for s in (1, -1):
        y0, y1 = sorted((s * 1490, s * (1490 + d)))
        plate(bm, rect(x0, x1, y0, y1), z, h, HULL if (x0 // 100) % 2 else GUN, 40.0)
        ribs(bm, x0 + 120, x1 - 120, s * (1490 + d + 1), z + 60, z + h - 60, s, 180, depth=24, seed=1720 + x0 + s, mi=(GUN, LIGHT))
        edge_windows(bm, (x0 + 100, s * (1490 + d + 8)), (x1 - 100, s * (1490 + d + 8)), z + 40, 10, (0, s), lit=0.2, seed=1730 + x0 + s, strip=(10, 80))
# a raised command spine along the back, the bridge forward
plate(bm, mirror([(7000, 0), (6600, 600), (-14000, 700), (-14400, 0)]), 1500, 400, HULL, 40.0)
greeble(bm, plane_surface((-13800, -600, 1900.1), (20400, 0, 0), (0, 1200, 0)), 0.00001, 1706, size=(160, 500), height=(40, 200),
        mats=(GUN, DARK), window=WIN, window_frac=0.1, stack=0.4, pipes=0.1, dark=0.4)
bridge(bm, 6900, 2100, 600, 80.0, 300)
for k in range(10):
    turret(bm, (-12000 + k * 2000, 0, 1904), 500)
# --- the emitter: four prongs round the nose, the beam's throat glowing between them (Stellaris)
for k in range(4):
    a = math.pi / 4 + k * math.pi / 2
    off = Vector((0, math.cos(a) * 900, math.sin(a) * 900))
    loft(bm, [(12000, 420, 420, 0.3), (15000, 380, 380, 0.3), (18200, 160, 160, 0.3), (18800, 30, 30, 0.3)], I3, off, HULL)
    fast_box(bm, (5000, 80, 40), Vector((15500, 0, 0)) + off * 0.62, Matrix(((1, 0, 0), (0, math.cos(a), -math.sin(a)), (0, math.sin(a), math.cos(a)))), DRIVE, UVL(bm))
cyl(bm, 600, 600, 200, Vector((14600, 0, 0)), Ry(math.pi / 2), seg=32, mi=DARK)
cyl(bm, 480, 480, 60, Vector((14720, 0, 0)), Ry(math.pi / 2), seg=32, mi=DRIVE)
for x in (12600, 13400):
    band(bm, Vector((x, 0, 0)), (1, 0, 0), 1230 if x < 13000 else 1050, 160, 60, mi=ACC, seg=8)
# --- the ring: a great band round the spindle on four spokes, emitter nodes round it, lit on its inner face
RX = -2000
band(bm, Vector((RX, 0, 0)), (1, 0, 0), 9000, 2800, 1300, mi=HULL, seg=96)
for d in (-1, 1):
    band(bm, Vector((RX + d * 1460, 0, 0)), (1, 0, 0), 9100, 200, 1500, mi=DARK, seg=96)
band(bm, Vector((RX, 0, 0)), (1, 0, 0), 8590, 600, 30, mi=WIN, seg=96)
for k in range(48):                                                                                    # segmented armour round the ring
    a = (k + 0.5) * math.pi / 24
    u, t = Vector((0, math.cos(a), math.sin(a))), Vector((0, -math.sin(a), math.cos(a)))
    R = Matrix((u, Vector((-1, 0, 0)), t)).transposed()            # right-handed: x * y = z
    fast_box(bm, (220, 2300 if k % 3 else 2600, 1000), Vector((RX, 0, 0)) + u * 10380, R, GUN if k % 2 else HULL, UVL(bm))
for k in range(4):
    a = math.pi / 4 + k * math.pi / 2
    u, t = Vector((0, math.cos(a), math.sin(a))), Vector((0, -math.sin(a), math.cos(a)))
    R = Matrix((u, Vector((-1, 0, 0)), t)).transposed()            # right-handed: x * y = z
    fast_box(bm, (6400, 1600, 1100), Vector((RX, 0, 0)) + u * 4700, R, GUN, UVL(bm))                     # spoke
    fast_box(bm, (6400, 500, 1160), Vector((RX, 0, 0)) + u * 4700, R, DARK, UVL(bm))
for k in range(8):
    a = k * math.pi / 4
    u, t = Vector((0, math.cos(a), math.sin(a))), Vector((0, -math.sin(a), math.cos(a)))
    R = Matrix((u, Vector((-1, 0, 0)), t)).transposed()            # right-handed: x * y = z
    fast_box(bm, (2400, 4200, 3200), Vector((RX, 0, 0)) + u * 9700, R, GUN, UVL(bm))                  # field projector
    fast_box(bm, (60, 3000, 2200), Vector((RX, 0, 0)) + u * 8490, R, DRIVE, UVL(bm))
# --- the stern: a deep engine block, a ring of great drives
plate(bm, rect(-18000, -17300, -1300, 1300), -1300, 2600, DARK, 60.0)
for k in range(6):
    a = k * math.pi / 3
    engine(bm, (-18100, math.cos(a) * 820, math.sin(a) * 820), 380, 1400)
engine(bm, (-18100, 0, 0), 300, 1200)
nav(bm, [(-2000, 9700, 0), (18800, 640, 0)], [(-2000, -9700, 0), (18800, -640, 0)], [(6900, 0, 3200), (-2000, 0, 9700)])
o = obj("colossus", bm, SM)
result = export_ship(col, root, "Colossus")
