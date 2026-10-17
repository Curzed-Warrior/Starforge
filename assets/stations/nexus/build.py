"""
Science Nexus v2 (asset overhaul, docs/ASSET_REFERENCES.md). After rotating-ring station references: a faceted core of
uploaded minds with glowing seams, cooling towers and docking spindles, inside three thick counter-rotating gimbal
rings. Each ring is a box-section band dense with data-vault modules, warm light strips on its inner face, window
clusters and bearing pods. Rings are separate objects "r0", "r1", "r2" centred on the origin (the game spins them).
Z is up.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/nexus/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Nexus")
MATS = ["Hull", "HullDark", "HullLight", "Accent", "Window", "WindowCool", "Strobe", "Glow", "Screen", "NavRed"]
HULL, DARK, LIGHT, ACC, WIN, WCOOL, STROBE, GLOW, SCREEN, RED = range(len(MATS))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
def U(b): return b.loops.layers.uv.verify()

# ---------------------------------------------------------------- the core: a faceted shell with glowing seams
bm = bmesh.new()
ico = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=24)["verts"]
faces = list({f for v in ico for f in v.link_faces})
for f in faces: f.material_index = DARK
inset = bmesh.ops.inset_individual(bm, faces=faces, thickness=1.0, depth=0.0)
for f in faces: f.material_index = HULL if hash(tuple(round(c) for c in f.calc_center_median())) % 5 else LIGHT
bm.normal_update()
for f in faces:
    c = f.calc_center_median()
    if f.material_index == LIGHT or hash(tuple(round(x) for x in c)) % 3 == 0:
        for v in f.verts: v.co += f.normal * 1.2
for e in [f for f in bm.faces if f.material_index == DARK]: e.material_index = SCREEN       # the seams glow
for sz in (-1, 1):
    loft(bm, [(0, 9, 9, 0.4), (sz * 36, 6, 6, 0.4), (sz * 40, 9, 9, 0.4), (sz * 50, 9, 9, 0.4), (sz * 56, 3, 3, 0.4)], UPX, Vector((0, 0, sz * 18)), HULL)
    for j in range(3): band(bm, Vector((0, 0, sz * (30 + j * 7))), Z_, 6.4, 1.4, 1.0, DARK, 12)
    antennas(bm, Vector((0, 0, sz * 74)), Vector((0, 0, sz)), 4, 16, 3 + sz, mi=DARK, light=RED)
    for k in range(3):                                   # cooling towers, angled out from the core
        a = k * 2 * math.pi / 3 + (0.5 if sz > 0 else 0)
        d = Vector((math.cos(a) * 0.7, math.sin(a) * 0.7, sz * 0.7)).normalized()
        loft(bm, [(20, 6, 6, 0.4), (34, 4.5, 4.5, 0.4), (40, 5.5, 5.5, 0.4)], basis(d), Vector((0, 0, 0)), DARK)
        cyl(bm, 4.2, 4.2, 0.4, d * 40.4, basis(d).to_4x4() @ Ry(math.pi / 2), seg=12, mi=GLOW)
obj("core", bm, MATS)

# ---------------------------------------------------------------- three thick gimbal rings
rnd = random.Random(5)
for i, (rad, w, h) in enumerate(((62, 12, 9), (82, 11, 8), (102, 10, 8))):
    bm = bmesh.new()
    c = solid_ring(bm, rad - w / 2, rad + w / 2, -h / 2, h / 2, 160, mats=(DARK, DARK, DARK, HULL))
    detail(bm, cyl_surface(rad + w / 2, -h / 2 + c + 0.1, h / 2 - c - 0.1), 10 + i, scale=1.5, mats=(DARK, HULL), window=WIN, window_frac=0.1)
    detail(bm, disc_surface(rad - w / 2 + c + 0.1, rad + w / 2 - c - 0.1, h / 2, up=True), 20 + i, scale=1.5, mats=(HULL, DARK), window=WCOOL, window_frac=0.06)
    detail(bm, disc_surface(rad - w / 2 + c + 0.1, rad + w / 2 - c - 0.1, -h / 2, up=False), 30 + i, scale=1.5, mats=(DARK, HULL))
    # warm light strips running round the inner face, broken into lit segments
    n = int(rad * 1.2)
    for k in range(n):
        if rnd.random() < 0.3: continue
        a = k * 2 * math.pi / n
        d = Vector((math.cos(a), math.sin(a), 0))
        R3 = Matrix((Vector((-d.y, d.x, 0)), Z_, -d)).transposed()
        fast_box(bm, (2 * math.pi * rad / n * 0.8, 0.5, 0.3), d * (rad - w / 2 - 0.1) + Z_ * (h * 0.22), R3, GLOW if k % 5 else WIN, U(bm), bottom=True)
        if k % 3 == 0: fast_box(bm, (2 * math.pi * rad / n * 0.6, 0.4, 0.3), d * (rad - w / 2 - 0.1) - Z_ * (h * 0.22), R3, WCOOL, U(bm), bottom=True)
    # data-vault modules strung round the outside, and bearing pods where the gimbal pivots
    for k in range(16):
        a = k * 2 * math.pi / 16 + i * 0.2
        d = Vector((math.cos(a), math.sin(a), 0))
        if k % 4 == 0:
            loft(bm, [(-10, 6, 6, 0.4), (-6, 9, 9, 0.4), (6, 9, 9, 0.4), (10, 6, 6, 0.4)], basis(Vector((-d.y, d.x, 0))), d * (rad + w / 2 + 6), HULL)
            strobe(bm, d * (rad + w / 2 + 15.5), 0.8, RED if k == 0 else STROBE)
        else:
            module(bm, d * (rad + w / 2 + 3), Vector((-d.y, d.x, 0)), d, 12, 6, 5, mats=(DARK if k % 3 else HULL, DARK, WIN), seed=i * 40 + k)
    for e in (-1, 1):
        n0 = set(bm.verts)
        solid_ring(bm, rad + w / 2 - 0.2, rad + w / 2 + 0.3, e * h * 0.3 - 0.3, e * h * 0.3 + 0.3, 160, mats=(GLOW,) * 4, bevel=0.0)
    obj(f"r{i}", bm, MATS)

lamp("L_Core", (0, 0, 0), 5e5, (0.4, 0.9, 1.0), 10)
result = export(col, root, "nexus")
