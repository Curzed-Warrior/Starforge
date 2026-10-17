"""
Mega Shipyard v2 (asset overhaul, docs/ASSET_REFERENCES.md). After the reference drydocks (Pyatov's Horizon arm,
orbital drydock concept art): an open box-truss cradle the game grows its hull inside (along Y, the game's Z), with
travelling gantry cranes on rails along its top and flanks, clamp arms reaching in, floodlight rows facing the hull,
a fabrication block at the stern end fed by conveyors and parts tanks, a command tower, a secondary slipway arm with a
frigate on the stocks, tugs tied up at docking arms, radiators, solar wings and strobes. Bounding radius ~150 m. Z up.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/yard/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Yard")

CL, CW = 140.0, 24.0                    # cradle half-length (along Y) and half-width
Y_ = Vector((0, 1, 0)); X_ = Vector((1, 0, 0)); Z_ = Vector((0, 0, 1))
MATS = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Solar", "Heat", "NavRed", "NavGreen", "Glow"]
HULL, DARK, LIGHT, ACC, HAZ, WIN, WCOOL, STROBE, RAD, SOLAR, HEAT, RED, GREEN, GLOW = range(len(MATS))

# ---------------------------------------------------------------- the cradle: longerons, frames, bracing
bm = bmesh.new()
uv = bm.loops.layers.uv.verify()
ALONG = basis(Y_)
for sx, sz in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
    loft(bm, [(-CL, 2.6, 2.6, 0.4), (CL, 2.6, 2.6, 0.4)], ALONG, Vector((sx * CW, 0, sz * CW)), DARK)
    # a lighter armoured cap down the outside of each longeron, segmented
    for j in range(14):
        y0 = -CL + j * 20 + 1.5
        fast_box(bm, (17, 1.2, 1.2), Vector((sx * (CW + 2.6), y0 + 8.5, sz * CW)), ALONG, DARK if j % 3 else HULL, bm.loops.layers.uv.verify(), bottom=True)
for j in range(15):
    y = -CL + j * 20
    heavy = j % 3 == 0
    t = 2.4 if heavy else 1.4
    # a square frame (bulkhead) round the hull space, chamfered corners
    for (a, b) in (((-1, 1), (1, 1)), ((1, 1), (1, -1)), ((1, -1), (-1, -1)), ((-1, -1), (-1, 1))):
        pa = Vector((a[0] * CW, y, a[1] * CW)); pb = Vector((b[0] * CW, y, b[1] * CW))
        d = pb - pa
        loft(bm, [(0, t, t, 0.35), (d.length, t, t, 0.35)], basis(d, Y_), pa, DARK if not heavy else HULL)
    if heavy:
        # heavy frames are armoured octagonal bulkheads, red-banded, with buttress fins standing out at the diagonals
        n0 = set(bm.verts)
        solid_ring(bm, CW * 1.42, CW * 1.62, -3.2, 3.2, 8, mats=(DARK, DARK, DARK, HULL), bevel=1.2)
        solid_ring(bm, CW * 1.6, CW * 1.66, -0.7, 0.7, 8, mats=(ACC,) * 4, bevel=0.1)
        bmesh.ops.transform(bm, matrix=Matrix.Translation((0, y, 0)) @ Rx(math.pi / 2) @ Rz(math.pi / 8), verts=since(bm, n0))
        for k in range(4):
            a = math.pi / 4 + k * math.pi / 2
            d = Vector((math.cos(a), 0, math.sin(a)))
            loft(bm, [(CW * 1.55, 2.4, 5.5, 0.4), (CW * 2.05, 1.6, 3.0, 0.4), (CW * 2.25, 0.8, 1.2, 0.4)], basis(d, Y_), Vector((0, y, 0)), DARK)
            strobe(bm, Vector((0, y, 0)) + d * CW * 2.3, 0.6, STROBE)
            # a work pod clinging to the bulkhead, its windows lit
            pod = Vector((0, y + 5, 0)) + d * CW * 1.3
            loft(bm, [(-3, 1.6, 1.6, 0.45), (0, 2.4, 2.2, 0.45), (3, 1.4, 1.4, 0.45)], basis(Y_), pod, LIGHT)
            fast_box(bm, (1.6, 0.3, 0.7), pod + Vector((0, 3.0, 0.5)), basis(Y_), WIN, bm.loops.layers.uv.verify(), bottom=True)
        # clamp arms reaching in toward the hull, hazard-striped at the jaws
        for sx, sz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            base = Vector((sx * CW, y, sz * CW))
            tip = base * 0.55 + Vector((0, y, 0)) * 0.45
            loft(bm, [(0, 2.2, 1.6, 0.4), ((tip - base).length * 0.8, 1.6, 1.2, 0.4)], basis(tip - base, Y_), base, HULL)
            fast_box(bm, (3.0, 3.5, 2.5), base + (tip - base) * 0.85, basis(tip - base, Y_), HAZ, bm.loops.layers.uv.verify(), bottom=True)
    # floodlights on the frame's inner corners, aimed at the hull space
    for sx, sz in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        fast_box(bm, (1.2, 0.6, 0.8), Vector((sx * (CW - 2.5), y, sz * (CW - 2.5))), ALONG, WCOOL, bm.loops.layers.uv.verify(), bottom=True)
# running lights along the top longerons
for sx in (1, -1):
    for j in range(40):
        fast_box(bm, (0.6, 1.2, 0.4), Vector((sx * CW, -CL + j * 7, CW + 2.7)), ALONG, WCOOL if j % 5 else STROBE, bm.loops.layers.uv.verify(), bottom=True)

# ---------------------------------------------------------------- the layered middle scale (after Bonchune's drydock): partial
# plating on some bays, catwalk decks inside, and hundreds of small warm work lights through the frame
rnd = random.Random(77)
WARM_ = WIN
for j in range(14):
    y0 = -CL + j * 20
    for face, (o, du, dv) in enumerate((
            (Vector((CW, y0, -CW)), Vector((0, 20, 0)), Vector((0, 0, 2 * CW))),          # +X flank
            (Vector((-CW, y0 + 20, -CW)), Vector((0, -20, 0)), Vector((0, 0, 2 * CW))),   # -X flank
            (Vector((CW, y0, -CW)), Vector((-2 * CW, 0, 0)), Vector((0, 20, 0))),          # keel
    )):
        if rnd.random() > 0.7: continue
        # a sheathed bay: a plate set just inside the frame, part-covering it, with detail and lit ports
        cover = rnd.uniform(0.35, 0.8)
        nrm = du.cross(dv).normalized()
        if nrm.dot(o - Vector((0, o.y, 0))) < 0: nrm = -nrm
        v0 = rnd.uniform(0, 1 - cover)
        p0 = o + dv * v0 - nrm * 0.6
        surf = plane_surface(p0 + du * 0.04, du * 0.92, dv * cover) if face != 1 else plane_surface(p0 + du * 0.04, dv * cover, du * 0.92)
        q = [p0 + du * 0.04, p0 + du * 0.96, p0 + du * 0.96 + dv * cover, p0 + du * 0.04 + dv * cover]
        f = bm.faces.new([bm.verts.new(v) for v in q]); f.material_index = DARK
        f.normal_update()
        if f.normal.dot(nrm) < 0: f.normal_flip()
        greeble(bm, plane_surface(q[0] if f.normal.dot(du.cross(dv)) > 0 else q[1], du * 0.9 if f.normal.dot(du.cross(dv)) > 0 else -du * 0.9, dv * cover), 0.06, 300 + j * 3 + face, size=(0.8, 3.0), height=(0.3, 1.2), mats=(HULL, DARK), window=WIN, window_frac=0.12)
# catwalk decks running inside along both flanks at mid-height, railed and lit
for sx in (1, -1):
    x = sx * (CW - 4)
    fast_box(bm, (2 * CL, 3.0, 0.4), Vector((x, 0, -2)), ALONG, DARK, bm.loops.layers.uv.verify(), bottom=True)
    fast_box(bm, (2 * CL, 0.15, 1.1), Vector((x - sx * 1.5, 0, -1.3)), ALONG, HULL, bm.loops.layers.uv.verify(), bottom=True)
    for k in range(70):
        fast_box(bm, (0.5, 0.4, 0.25), Vector((x, -CL + k * 4 + 2, -1.7)), ALONG, WIN, bm.loops.layers.uv.verify(), bottom=True)
# warm work lights scattered over every member: these are what make the frame read as a working yard at a distance
for k in range(700):
    sx, sz = rnd.choice((-1, 1)), rnd.choice((-1, 1))
    y = rnd.uniform(-CL, CL)
    edge = rnd.random()
    if edge < 0.5:
        p = Vector((sx * CW, y, rnd.uniform(-CW, CW)))
    else:
        p = Vector((rnd.uniform(-CW, CW), y, sz * CW))
    sphere(bm, rnd.uniform(0.25, 0.55), p, mi=(GLOW if rnd.random() < 0.5 else WIN) if rnd.random() < 0.85 else WCOOL, seg=4)

# diagonal bracing on the flanks and the keel
for j in range(14):
    y0, y1 = -CL + j * 20, -CL + (j + 1) * 20
    for sx in (1, -1):
        _rod(bm, Vector((sx * CW, y0, -CW if j % 2 else CW)), Vector((sx * CW, y1, CW if j % 2 else -CW)), 0.7, DARK)
    _rod(bm, Vector((-CW, y0, -CW)), Vector((CW, y1, -CW)), 0.6, DARK)
obj("Cradle", bm, MATS)

# ---------------------------------------------------------------- gantry cranes on the top and both flanks
bm = bmesh.new()
gantry(bm, Vector((0, -CL, CW + 2.6)), Vector((0, CL, CW + 2.6)), Z_, CW * 1.8, 10, mi_rail=DARK, mi_crane=HAZ, cranes=3, seed=3)
for sx in (1, -1):
    n0 = set(bm.verts)
    gantry(bm, Vector((0, -CL * 0.9, 0)), Vector((0, CL * 0.9, 0)), Z_, CW * 1.5, 8, mi_rail=DARK, mi_crane=ACC, cranes=2, seed=5 + sx)
    bmesh.ops.transform(bm, matrix=Matrix.Translation((sx * (CW + 2.6), 0, 0)) @ Ry(sx * math.pi / 2), verts=since(bm, n0))
obj("Gantries", bm, MATS)

# ---------------------------------------------------------------- the fabrication block at the stern end
bm = bmesh.new()
FY = -CL - 34
# three stacked modules, a wide hall and two smaller decks, offset so the block is not a plain box
module(bm, Vector((0, FY, 0)), Y_, Z_, 60, 70, 34, mats=(HULL, DARK, WIN), seed=11, detail_scale=1.4)
module(bm, Vector((-12, FY - 6, 26)), Y_, Z_, 40, 36, 18, mats=(HULL, DARK, WIN), seed=12, detail_scale=1.0)
module(bm, Vector((22, FY + 8, -24)), Y_, Z_, 36, 26, 16, mats=(HULL, DARK, WIN), seed=13, detail_scale=1.0)
# the fab mouth: a glowing slot where plate and frames come out into the cradle
fast_box(bm, (CW * 1.5, 1.0, CW * 1.2), Vector((0, FY + 30.4, 0)), basis(X_), HEAT, bm.loops.layers.uv.verify(), bottom=True)
for k in range(6):
    fast_box(bm, (CW * 1.6, 1.2, 1.0), Vector((0, FY + 30.8, -CW * 0.6 + k * CW * 0.24)), basis(X_), DARK, bm.loops.layers.uv.verify(), bottom=True)
# conveyor pipes from the block along the keel into the cradle
for sx in (-1, 1):
    pipe_run(bm, [Vector((sx * 14, FY + 30, -18)), Vector((sx * 14, -CL + 20, -CW - 4)), Vector((sx * 14, CL * 0.6, -CW - 4))], 1.1, n=3, mi=DARK, clamp_mi=HAZ, up=X_)
# parts tanks slung beneath the block
for k, x in enumerate((-24, 0, 24)):
    tank_cage(bm, Vector((x, FY - 4, -34)), 9, axis=Y_, capsule=1.2, mi_tank=ACC if k == 1 else LIGHT, mi_cage=DARK)
# radiator banks off the block's flanks, edge-on
for sx in (-1, 1):
    radiator_bank(bm, Vector((sx * 36, FY - 10, 4)), Vector((sx, 0, 0)), Y_, 9, 26, 12, 4.5, mi_fin=LIGHT, mi_man=DARK)
# solar wings off the stern
for sz in (-1, 1):
    solar_wing(bm, Vector((sz * 30, FY - 30, 0)), Vector((sz, -0.35, 0.15)).normalized(), Z_, 55, 20, panels=6, mi_cell=SOLAR, mi_frame=DARK)
antennas(bm, Vector((-12, FY - 6, 35)), Z_, 7, 22, 7, mi=DARK, light=RED)
obj("Fab", bm, MATS)

# ---------------------------------------------------------------- the command tower over the cradle's middle
bm = bmesh.new()
TX, TZ = 0, CW + 8
# a bridge spanning the top of the cradle on two legs, with the tower rising from it
for sx in (-1, 1):
    loft(bm, [(0, 3, 3, 0.4), (12, 2.5, 2.5, 0.4)], UPX, Vector((sx * CW, 10, CW + 2.6)), DARK)
loft(bm, [(-CW - 4, 4, 3, 0.4), (CW + 4, 4, 3, 0.4)], basis(X_), Vector((0, 10, CW + 16)), LIGHT)
loft(bm, [(0, 7, 6, 0.4), (14, 6, 5, 0.4), (18, 10, 7, 0.4), (30, 10, 7, 0.4), (34, 4, 4, 0.4)], UPX, Vector((TX, 10, CW + 17)), HULL)
window_rows(bm, plane_surface(Vector((TX + 10.05, 1.5, CW + 36)), Vector((0, 17, 0)), Vector((0, 0, 9))), 3, 10, size=(1.0, 0.5), mi=WIN, seed=21, lit=0.85)
window_rows(bm, plane_surface(Vector((TX - 10.05, 18.5, CW + 36)), Vector((0, -17, 0)), Vector((0, 0, 9))), 3, 10, size=(1.0, 0.5), mi=WIN, seed=22, lit=0.85)
dish(bm, Vector((TX + 6, 4, CW + 52)), Vector((0.4, -0.3, 1)), 7, mi_dish=LIGHT, mi_frame=DARK)
antennas(bm, Vector((TX - 3, 14, CW + 51)), Z_, 5, 16, 23, mi=DARK, light=RED)
obj("Tower", bm, MATS)

# ---------------------------------------------------------------- a secondary slipway arm with a frigate on the stocks (the asymmetry)
bm = bmesh.new(); bmf = bmesh.new()
SA = Vector((-CW - 2.6, 40, -6))
OUT = Vector((-1, 0.15, -0.35)).normalized()
truss(bm, SA, SA + OUT * 60, w=6, mi=DARK, rod=0.45)
SL = SA + OUT * 60
R = basis(Vector((0, 1, 0)), Z_)
for sx, sz in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
    loft(bm, [(-34, 1.2, 1.2, 0.4), (34, 1.2, 1.2, 0.4)], R, SL + Vector((sx * 9, 0, sz * 9)), DARK)
for j in range(7):
    y = -30 + j * 10
    for (a, b) in (((-1, 1), (1, 1)), ((1, 1), (1, -1)), ((1, -1), (-1, -1)), ((-1, -1), (-1, 1))):
        pa = SL + Vector((a[0] * 9, y, a[1] * 9)); pb = SL + Vector((b[0] * 9, y, b[1] * 9))
        loft(bm, [(0, 0.8, 0.8, 0.35), ((pb - pa).length, 0.8, 0.8, 0.35)], basis(pb - pa, Y_), pa, ACC if j in (0, 6) else DARK)
    fast_box(bm, (0.8, 0.5, 0.6), SL + Vector((8.5, y, 8.5)), ALONG, WCOOL, bm.loops.layers.uv.verify(), bottom=True)
ship(bmf, SL, basis(Y_, Z_), 54, (HULL, DARK, GLOW, WIN), seed=31)
obj("Slipway", bm, MATS)
obj("SlipwayShip", bmf, MATS)

# ---------------------------------------------------------------- tugs tied up at docking arms, and strobes at the extremities
bm = bmesh.new()
dock_arm(bm, Vector((CW + 2.6, -60, -CW)), Vector((1, 0, -0.6)), 22, 20, mats=(LIGHT, DARK, GLOW, WIN), seed=41)
dock_arm(bm, Vector((CW + 2.6, 90, -CW)), Vector((1, 0.2, -0.5)), 18, 16, mats=(HULL, DARK, GLOW, WIN), seed=42)
dock_arm(bm, Vector((-CW - 2.6, -100, CW)), Vector((-1, 0, 0.5)), 20, 18, mats=(ACC, DARK, GLOW, WIN), seed=43)
for p in (Vector((CW + 3, CL, CW + 3)), Vector((-CW - 3, CL, CW + 3)), Vector((CW + 3, CL, -CW - 3)), Vector((-CW - 3, CL, -CW - 3))):
    sphere(bm, 1.6, p, mi=RED if p.x < 0 else GREEN, seg=8)
obj("Tugs", bm, MATS)

for k in range(5):
    lamp(f"L_Flood{k}", (0, -CL + 30 + k * 60, CW - 4), 6e4, (0.85, 0.9, 1.0), 6)
lamp("L_Fab", (0, FY + 34, 0), 8e4, (1.0, 0.55, 0.25), 8)
result = export(col, root, "yard")
