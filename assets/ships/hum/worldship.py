"""
Worldship, 150 km. A mobile colony-city (tech HUL-11): a nation that travels. References (Bing sheet "generation
worldship concept art city hull massive": the City Ship Cheyenne, Daryl Mandryk's city ships, "Massive City Ship
navigates the stars"): a long, broad armoured hull whose whole back is a city, towers packed between three great
glass domes of open country, a raked prow bulwark to take the dust of the road, decks of lit windows down the flanks
and a stern of massed drives. The navy's gunmetal and paint, windows and drives. Population ~400,000,000.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/worldship.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipWorldship")
hull_scale(150000)
M["WorldLand"] = bpy.data.materials.get("WorldLand") or mat("WorldLand", (0.13, 0.22, 0.1), 0.0, 0.85)
SM2 = SM + ["WorldLand"]
LAND = len(SM)
bm = bmesh.new()
rnd = random.Random(1300)
L, HW, HH = 150000.0, 15000.0, 6000.0
# --- the hull: a long broad box with chamfered shoulders, its prow a raked bulwark
keel(bm, [(-75000, HW * 0.9, HH * 0.9, 0), (-70000, HW, HH, 0), (52000, HW, HH, 0), (64000, HW * 0.8, HH * 0.85, -600),
          (75000, HW * 0.35, HH * 0.5, -1500)], GUN, 0.32)
for s in (1, -1):
    # raked armour over the bow's shoulders, following the hull down to the prow
    plate(bm, [(52000, s * HW * 0.2), (52000, s * HW * 1.01), (63000, s * HW * 0.82), (72000, s * HW * 0.42), (72000, s * HW * 0.1)], HH * 0.62, 1200, HULL, 500)
for s in (1, -1):
    ribs(bm, -68000, 50000, s * HW, -HH * 0.8, HH * 0.6, s, 1600, depth=260, seed=1301 + s, gap=0.2, mi=(GUN, HULL))
    edge_windows(bm, (-68000, s * (HW + 280)), (50000, s * (HW + 280)), -HH * 0.6, 8, (0, s), lit=0.45, seed=1303 + s, strip=(30, 600))
    fast_box(bm, (110000, 300, 500), Vector((-9000, s * (HW + 300), HH * 0.75)), flank(s), ACC, UVL(bm))
    # the shoulder decks: stepped promenades along each edge of the city
    plate(bm, rect(-66000, 48000, s * (HW - 2600), s * (HW - 200)), HH, 900, LIGHT, 250)
strips(bm, -68000, 50000, -HW * 0.8, HW * 0.8, -HH, 10, h=300, seed=1305, mi=GUN, down=True)
# --- the domes: three great glass domes of open country on the back, each on a plinth with a lit rim
domes = [(30000, 10500), (-5000, 11500), (-42000, 10500)]
for x, r in domes:
    cyl(bm, r + 600, r + 900, 900, Vector((x, 0, HH + 450)), seg=48, mi=HULL)
    cyl(bm, r + 620, r + 620, 120, Vector((x, 0, HH + 960)), seg=48, mi=WIN, caps=False)
    cyl(bm, r, r, 40, Vector((x, 0, HH + 920)), seg=48, mi=LAND)
    n0 = set(bm.verts)
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=12, radius=r)
    vs = since(bm, n0)
    for v in vs:
        if v.co.z < 0: v.co.z = 0
        v.co.z *= 0.7
    bmesh.ops.translate(bm, vec=Vector((x, 0, HH + 900)), verts=vs)
    for f in {f for v in vs for f in v.link_faces}: f.material_index = GLASS
    # towns inside: a scatter of low lit blocks on the land
    for k in range(18):
        a = rnd.random() * math.tau; d = r * 0.75 * math.sqrt(rnd.random())
        fast_box(bm, (500, 500, 300 + rnd.random() * 900), Vector((x + math.cos(a) * d, math.sin(a) * d, HH + 940)), UP,
                 WIN if rnd.random() < 0.4 else LIGHT, UVL(bm), bottom=False)
# --- the city: towers packed on the deck between the domes, taller toward the hull's spine, each with a lit band
for gx in range(-66000, 48000, 2400):
    for gy in range(-12000, 12001, 2400):
        if any((gx - x) ** 2 + gy * gy < (r + 1600) ** 2 for x, r in domes): continue
        if rnd.random() < 0.18: continue
        h = (800 + rnd.random() * 2500) * (1.0 + 1.6 * (1 - abs(gy) / 12000)) * (1.6 if rnd.random() < 0.06 else 1)
        w = 1300 + rnd.random() * 700
        at = Vector((gx + rnd.uniform(-300, 300), gy + rnd.uniform(-300, 300), HH))
        fast_box(bm, (w, w * rnd.uniform(0.7, 1.0), h), at + Vector((0, 0, h / 2)), UP, rnd.choice((HULL, HULL, GUN, LIGHT)), UVL(bm))
        for z in (0.45, 0.8):
            fast_box(bm, (w * 1.02, w * 0.7, 120), at + Vector((0, 0, h * z)), UP, WIN, UVL(bm))
        if h > 5000: cyl(bm, 60, 20, 1600, at + Vector((0, 0, h + 800)), seg=5, mi=DARK)
# --- the stern: a deep drive block, nine great engines in three rows, radiators fanned above
plate(bm, rect(-78000, -70000, -HW * 0.95, HW * 0.95), -HH * 0.95, HH * 1.9, DARK, 500)
for y in (-9000, 0, 9000):
    for z in (-3500, 0, 3500):
        engine(bm, (-78500, y, z), 2800 if (y, z) != (0, 0) else 3300, 6500, mi=GUN, bands=3, seg=20)
for s in (1, -1):
    radiator_bank(bm, Vector((-66000, s * 6000, HH + 900)), Vector((0, s, 0.4)).normalized(), X_, 6, 6000, 4000, 1600, mi_fin=PANEL, mi_man=DARK)
nav(bm, [(0, HW + 400, 0)], [(0, -HW - 400, 0)], [(70000, 0, HH)])
o = obj("worldship", bm, SM2)
result = export_ship(col, root, "Worldship")
