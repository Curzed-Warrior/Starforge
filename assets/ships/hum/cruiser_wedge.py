"""
Cruiser, 1.2 km. References (assets/ships/refs/cruiser_ref*): the Venator's long dorsal flight deck splitting the
wedge, its twin dome-topped bridge towers side by side and signal-red chevrons; the Interdictor's four great domes
(here the shield and sensor generators) sunk into the upper hull. Same wedge, waist and plating as the destroyer,
so they read as one navy. Crew ~7,000; carries a wing of corvettes in the flight deck.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/cruiser.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipCruiser")
sc = hull_scale(1200)
bm = bmesh.new()
NOSE, STERN, HW = 600.0, -600.0, 280.0
half = [(NOSE, 0.0), (STERN + 60, HW), (STERN, HW - 24)]
UPZ, UPH, LOZ, LOH = 10.0, 84.0, -14.0, 56.0
# --- the wedge, waist and keel (destroyer pattern, scaled)
wedge(bm, mirror(half), UPZ, UPH, ridge=0.06, along=0.86, ox=-60, mi=LIGHT)
wedge(bm, mirror([(NOSE - 30, 0.0), (STERN + 56, HW - 14), (STERN + 4, HW - 34)]), LOZ, UPZ - LOZ, ridge=1.0, along=1.0, mi=DARK)
wedge(bm, mirror(half), LOZ, LOH, ridge=0.12, along=0.84, ox=-60, mi=HULL, down=True)
for k, f in enumerate(wedge_faces(mirror(half), UPZ, UPH, ridge=0.06, along=0.86, ox=-60)):
    if f.area < 8000: continue
    panels(bm, f, 300 + k, scale=sc * 1.6, accent=0.0, density=0.12)
    armour_grid(bm, f.tri, (52, 16), (1.2, 4.0), 320 + k, (LIGHT, HULL), fill=0.7)
for k, f in enumerate(wedge_faces(mirror(half), LOZ, LOH, ridge=0.12, along=0.84, ox=-60, down=True)):
    if f.area < 8000: continue
    armour_grid(bm, f.tri, (60, 18), (1.2, 3.5), 340 + k, (HULL, DARK), fill=0.6)
# --- the flight deck: a long lit channel along the ridge between two raised walls (Venator)
DX0, DX1, DW = -260.0, 470.0, 30.0
RZ = UPZ + UPH                                                                     # ridge height
plate(bm, mirror([(DX1 + 40, 0), (DX1, DW + 26), (DX0, DW + 34), (DX0 - 30, 0)]), RZ - 26, 24, HULL, 2.0)   # deck block
plate(bm, rect(DX0, DX1, -DW, DW), RZ - 2.0, 2.4, DARK, 0.6)                       # the deck itself
for s in (1, -1):
    y0, y1 = sorted((s * DW, s * (DW + 26)))
    wall = [(DX1 + 30, y0), (DX1 + 30, y1), (DX0, y1), (DX0, y0)] if s > 0 else [(DX1 + 30, y0), (DX1 + 30, y1), (DX0, y1), (DX0, y0)]
    plate(bm, rect(DX0, DX1 + 30, y0, y1), RZ - 2, 28, LIGHT, 4.0, slope=(0, -s * 7.0))
    fast_box(bm, (DX1 - DX0 - 30, 2.5, 0.8), Vector(((DX0 + DX1) / 2, s * (DW - 4), RZ + 0.8)), UP, DRIVE, UVL(bm))    # guide lights
    for k in range(4):                                                                  # signal-red stripes along the wall tops
        x0 = DX0 + 30 + k * (DX1 - DX0) / 4
        fast_box(bm, ((DX1 - DX0) / 4 - 30, 12, 0.8), Vector((x0 + (DX1 - DX0) / 8 - 15, s * (DW + 13), RZ + 26.4)), UP, ACC, UVL(bm))
    edge_windows(bm, (DX0 + 10, s * (DW + 0.2)), (DX1 + 10, s * (DW + 0.2)), RZ + 4, 7, (0, -s), lit=0.6, seed=350 + s, spacing=4.0)
greeble(bm, plane_surface((DX0 + 10, -DW + 6, RZ + 0.4), (DX1 - DX0 - 20, 0, 0), (0, 2 * DW - 12, 0)), 0.012, 360,
        size=(3, 10), height=(0.5, 3.0), mats=(HULL, DARK), window=WIN, window_frac=0.04, stack=0.3, pipes=0.1, dark=0.5)
# --- four domes in the upper hull (Interdictor): shield and sensor generators
for x, y in ((-170, 118), (-170, -118), (60, 92), (60, -92)):
    hw_at = HW * (NOSE - x) / (NOSE - STERN - 60)                                  # half-width of the wedge here
    zs = UPZ + UPH * max(0.0, 1 - abs(y) / max(hw_at, 1)) * 0.95                    # deck height under the dome
    sphere(bm, 56, Vector((x, y, zs - 4)), mi=LIGHT, seg=40)
    cyl(bm, 59, 59, 6, Vector((x, y, zs + 2)), seg=48, mi=DARK)
    cyl(bm, 62, 66, 14, Vector((x, y, zs - 8)), seg=48, mi=HULL)
# --- twin bridge towers side by side at the stern (Venator), each with a sensor dome
plate(bm, mirror([(-300, 0), (-320, 90), (-540, 100), (-560, 0)]), UPZ + UPH - 6, 26, LIGHT, 5.0)
for s in (1, -1):
    y = s * 52
    plate(bm, [(-370, y - 22), (-370, y + 22), (-470, y + 28), (-482, y - 28)], UPZ + UPH + 20, 110, LIGHT, 6.0, slope=(-10.0, 0))
    plate(bm, rect(-462, -380, y - 32, y + 32), UPZ + UPH + 130, 18, HULL, 3.0)
    bridge(bm, -378, UPZ + UPH + 148, 58, 11.0, 28)
    edge_windows(bm, (-371, y + 21), (-371, y - 21), UPZ + UPH + 26, 28, (1, 0), lit=0.5, seed=380 + s, spacing=3.0)
    cyl(bm, 20, 20, 12, Vector((-430, y, UPZ + UPH + 165)), seg=28, mi=HULL)
    sphere(bm, 20, Vector((-430, y, UPZ + UPH + 171)), mi=LIGHT, seg=28)
# --- waist: window rows and batteries round both flanks
for s in (1, -1):
    a, b = (NOSE - 32, 0.0), (STERN + 58, s * (HW - 15))
    o = Vector((-(b[1] - a[1]), b[0] - a[0], 0)).normalized()
    if o.y * s < 0: o = -o
    edge_windows(bm, (a[0], a[1] + o.y * 1.0), (b[0], b[1] + o.y * 1.0), LOZ + 2, 6, (o.x, o.y), seed=370 + s, spacing=3.0)
    for k in range(9):
        u = 0.14 + 0.09 * k
        p = Vector((a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u, UPZ)) - o * 12
        turret(bm, (p.x, p.y, p.z + 3), 20 if k % 2 else 28)
    # signal-red chevrons on the prow flanks
    for k in range(3):
        u = 0.06 + 0.035 * k
        c = Vector((a[0] + (b[0] - a[0]) * u, a[1] + (b[1] - a[1]) * u, UPZ + 4)) - o * 22
        fast_box(bm, (34, 10, 1.0), c, Matrix((Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))).transposed(), ACC, UVL(bm))
# --- drives: three main engines, four auxiliaries
plate(bm, rect(STERN - 24, STERN + 10, -140, 140), LOZ - 40, 110, HULL, 4.0)
for y, z, r in ((-70, 10, 34), (70, 10, 34), (0, 30, 36), (-122, -16, 16), (122, -16, 16), (-40, -30, 14), (40, -30, 14)):
    engine(bm, (STERN - 30, y, z), r, 80)
# --- ventral hangar for the heavy wing
fast_box(bm, (220, 90, 1.0), Vector((-220, 0, LOZ - LOH + 0.2)), DOWN, DARK, UVL(bm))
for s in (1, -1):
    fast_box(bm, (220, 2.0, 1.2), Vector((-220, s * 46, LOZ - LOH - 0.1)), DOWN, WIN, UVL(bm))
nav(bm, [(STERN + 60, HW + 2, 0), (NOSE - 4, 3, 0)], [(STERN + 60, -HW - 2, 0), (NOSE - 4, -3, 0)], [(-430, 52, 286), (-430, -52, 286)])
o = obj("cruiser", bm, SM)
result = export_ship(col, root, "Cruiser")
