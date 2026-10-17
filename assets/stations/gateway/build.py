"""
Gateway v2 (asset overhaul, docs/ASSET_REFERENCES.md). After jump-gate references: a heavy ring 300 m across built of
armoured segments, red and amber accent lines along its faces, eight field emitters with glowing throats aimed into the
mouth; two great power-coupler blocks on opposite sides (modules, radiators, conduits); a rotating inner collar
("disc", spun by the game about the ring's axis) studded with emitter points; approach-light piers out in front;
a traffic-control tower and a docking station with ships waiting. The ring's axis is Y (the game's Z), the mouth facing
-Y. Z is up.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/gateway/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Gateway")
MATS = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Glow", "Plasma", "Radiator", "NavRed", "NavGreen"]
HULL, DARK, LIGHT, ACC, HAZ, WIN, WCOOL, STROBE, GLOW, PLASMA, RADI, RED, GREEN = range(len(MATS))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
def U(b): return b.loops.layers.uv.verify()
R0 = 150.0
RY = Rx(math.pi / 2)            # a ring built round Z, turned onto the Y axis

# ---------------------------------------------------------------- the ring
bm = bmesh.new()
n0 = set(bm.verts)
c = solid_ring(bm, R0 - 18, R0 + 18, -22, 22, 128, mats=(DARK, DARK, DARK, HULL), bevel=5)
solid_ring(bm, R0 - 19, R0 - 18, -18, 18, 128, mats=(GLOW,) * 4, bevel=0.0)        # the inner rim glows, like the reference
detail(bm, cyl_surface(R0 + 18, -22 + c + 0.1, 22 - c - 0.1), 3, scale=3.0, mats=(DARK, HULL), window=WIN, window_frac=0.08)
detail(bm, disc_surface(R0 - 18 + c, R0 + 18 - c, 22, up=True), 4, scale=3.0, mats=(HULL, DARK), window=WCOOL, window_frac=0.04)
detail(bm, disc_surface(R0 - 18 + c, R0 + 18 - c, -22, up=False), 5, scale=3.0, mats=(DARK, HULL))
for e in (-1, 1):
    for k in range(64):                               # red/amber accent lines on both faces
        a = k * 2 * math.pi / 64
        d = Vector((math.cos(a), math.sin(a), 0))
        fast_box(bm, (2 * math.pi * (R0 + 10) / 64 * 0.92, 1.2, 0.4), d * (R0 + 10) + Z_ * e * 22.2, Matrix((Vector((-d.y, d.x, 0)), d, Z_ * e)).transposed(), ACC if k % 8 else HAZ, U(bm), bottom=True)
        if k % 2 == 0: fast_box(bm, (2.0, 0.8, 0.3), d * (R0 - 10) + Z_ * e * 22.2, Matrix((Vector((-d.y, d.x, 0)), d, Z_ * e)).transposed(), WIN, U(bm), bottom=True)
for k in range(8):                                    # field emitters: armoured blocks with glowing throats aimed inward
    a = k * math.pi / 4 + math.pi / 8
    d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
    fast_prism(bm, octo(40, 48, 9), 60, d * (R0 - 2) - Z_ * 30, Matrix((t, d, Z_)).transposed(), HULL if k % 2 else DARK, U(bm), top=(0.85, 0.85, 0, 0), base_cap=True)
    loft(bm, [(0, 9, 9, 0.4), (-12, 7, 7, 0.4)], basis(-d), d * (R0 - 18), DARK)
    cyl(bm, 6, 6, 0.6, d * (R0 - 30.4), basis(-d).to_4x4() @ Ry(math.pi / 2), seg=12, mi=PLASMA)
    for j in range(3): fast_box(bm, (2.0, 30, 0.6), d * (R0 + 8 + j * 4) + Z_ * 23.2, Matrix((t, d, Z_)).transposed(), RADI, U(bm), bottom=True)
    strobe(bm, d * (R0 + 22) + Z_ * 18, 1.4, RED if k % 2 else STROBE)
bmesh.ops.transform(bm, matrix=RY, verts=since(bm, n0))
obj("Ring", bm, MATS)

# ---------------------------------------------------------------- two power-coupler blocks on opposite flanks
bm = bmesh.new()
for sx in (1, -1):
    base = Vector((sx * (R0 + 34), 0, 0))
    module(bm, base + Vector((sx * 10, 0, 0)), Y_, Z_, 90, 60, 70, mats=(DARK, HULL, WIN), seed=11 + sx, detail_scale=1.6)
    module(bm, base + Vector((sx * 30, -10, -32)), Y_, Z_, 50, 30, 26, mats=(HULL, DARK, WIN), seed=15 + sx, detail_scale=1.2)
    module(bm, base + Vector((sx * 34, -6, 30)), Y_, Z_, 46, 22, 20, mats=(DARK, HULL, WIN), seed=13 + sx)
    tank_cage(bm, base + Vector((sx * 26, 46, -22)), 13, axis=Y_, capsule=1.0, mi_tank=ACC, mi_cage=DARK)
    tank_cage(bm, base + Vector((sx * 26, 46, 14)), 11, axis=Y_, capsule=1.0, mi_tank=HULL, mi_cage=DARK)
    radiator_bank(bm, base + Vector((sx * 42, 10, 36)), Vector((sx, 0, 0.6)).normalized(), Y_, 10, 34, 12, 4, mi_fin=RADI, mi_man=DARK)
    radiator_bank(bm, base + Vector((sx * 42, 10, -36)), Vector((sx, 0, -0.6)).normalized(), Y_, 10, 34, 12, 4, mi_fin=RADI, mi_man=DARK)
    pipe_run(bm, [base + Vector((-sx * 17, -20, 12)), Vector((sx * (R0 + 16), -20, 12))], 2.0, n=3, mi=DARK, clamp_mi=HAZ, up=Y_)
    # machinery on the coupler's flanks and front: the reference's density
    cx = base.x + sx * 10
    for face in (plane_surface(Vector((cx + sx * 30.2, -45, -35)), Vector((0, 90, 0)) * sx, Vector((0, 0, 70))),
                 plane_surface(Vector((cx - 30, -45.2, -35)), Vector((60, 0, 0)), Vector((0, 0, 70))),
                 plane_surface(Vector((cx - 30, 45.2, 35)), Vector((60, 0, 0)), Vector((0, 0, -70)))):
        greeble(bm, face, 0.03, 41 + sx, size=(3.0, 10), height=(0.6, 2.4), mats=(HULL, DARK), window=WIN, glow=GLOW, window_frac=0.1, glow_frac=0.03)
    antennas(bm, base + Vector((0, 0, 20)), Z_, 5, 22, 17 + sx, mi=DARK, light=RED)
# a traffic-control tower over the top of the ring, and a waiting dock with ships
loft(bm, [(0, 8, 8, 0.4), (40, 6, 6, 0.4), (46, 12, 10, 0.4), (60, 12, 10, 0.4), (64, 5, 5, 0.4)], UPX, Vector((0, 0, R0 + 14)), HULL)
window_rows(bm, plane_surface(Vector((12.05, -9, R0 + 62)), Vector((0, 18, 0)), Vector((0, 0, 11))), 3, 12, size=(1.0, 0.5), mi=WIN, seed=21, lit=0.9)
dish(bm, Vector((0, 8, R0 + 82)), Vector((0, -1, 0.4)), 8, mi_dish=LIGHT, mi_frame=DARK)
for k, sx in enumerate((-1, 1)):
    dock_arm(bm, Vector((sx * 40, 10, -R0 - 10)), Vector((sx * 0.4, -0.4, -1)), 36, 34, mats=(LIGHT if k else HULL, DARK, GLOW, WIN), seed=31 + k)
obj("Couplers", bm, MATS)

# ---------------------------------------------------------------- the rotating inner collar
bm = bmesh.new()
n0 = set(bm.verts)
solid_ring(bm, R0 - 30, R0 - 22, -6, 6, 128, mats=(HULL, DARK, DARK, HULL))
for k in range(32):
    a = k * 2 * math.pi / 32
    d = Vector((math.cos(a), math.sin(a), 0))
    fast_box(bm, (6, 3, 8), d * (R0 - 31.5), Matrix((Vector((-d.y, d.x, 0)), -d, Z_)).transposed(), HULL if k % 2 else LIGHT, U(bm), bottom=True)
    if k % 2 == 0: sphere(bm, 1.6, d * (R0 - 34), mi=PLASMA, seg=8)
bmesh.ops.transform(bm, matrix=RY, verts=since(bm, n0))
obj("disc", bm, MATS)

# ---------------------------------------------------------------- approach piers in front of the mouth
bm = bmesh.new()
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    d = Vector((math.cos(a), 0, math.sin(a)))
    for j in range(6):
        p = d * (R0 * 0.75) - Y_ * (60 + j * 45)
        loft(bm, [(-4, 3, 3, 0.4), (4, 3, 3, 0.4)], basis(Y_), p, DARK)
        sphere(bm, 1.8, p + d * 4, mi=GREEN if k % 2 else RED, seg=8)
obj("Piers", bm, MATS)
result = export(col, root, "gateway")
