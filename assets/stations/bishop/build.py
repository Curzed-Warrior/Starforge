"""
Bishop Ring: a habitat ring 2 km across and 420 m wide, open to the sky, its air held in by 60 m rim walls.
Land, seas and clouds on the inner face (a generated texture wrapped round the ring), a hull band outside with
detail and windows, sunward mirror spars, spokes to a hub, and city lights. The ring's axis is Y (the game's Z),
as the procedural ring's was. Z is up.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/bishop/build.py").read())
"""
import numpy as np
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Bishop")

# ---- the land texture: continents, seas, mountains, clouds; wraps 6 times round the ring
N = 1024
def vnoise(scale, seed):
    g = np.random.default_rng(seed).random((scale, scale)).astype(np.float32)
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / N
    x = xx * scale; y = yy * scale
    x0 = np.floor(x).astype(int) % scale; y0 = np.floor(y).astype(int) % scale
    fx = x - np.floor(x); fy = y - np.floor(y); fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    a = g[y0, x0] * (1 - fx) + g[y0, (x0 + 1) % scale] * fx
    b = g[(y0 + 1) % scale, x0] * (1 - fx) + g[(y0 + 1) % scale, (x0 + 1) % scale] * fx
    return a * (1 - fy) + b * fy
def fbm(seed, base, octs):
    v = np.zeros((N, N), np.float32); amp = 0.5; s = base
    for i in range(octs):
        v += amp * vnoise(s, seed + i); amp *= 0.5; s *= 2
    return v
h = fbm(5, 4, 6)
cl = fbm(9, 6, 5)
sea, grass, forest, rock, sand = [np.array(c, np.float32) for c in ((0.04, 0.14, 0.28), (0.2, 0.36, 0.12), (0.1, 0.24, 0.08), (0.42, 0.38, 0.32), (0.6, 0.55, 0.4))]
t = np.clip((h - 0.47) * 12, 0, 1)[..., None]
land = np.where((h > 0.62)[..., None], rock, np.where((h > 0.53)[..., None], forest, grass))
alb = sea * (1 - t) + np.where((h < 0.49)[..., None], sand, land) * t
alb = alb * (1 - np.clip((cl - 0.55) * 4, 0, 0.8)[..., None]) + np.clip((cl - 0.55) * 4, 0, 0.8)[..., None] * 0.9
orm = np.stack([np.ones((N, N)), np.where(h < 0.47, 0.15, 0.85), np.zeros((N, N))], axis=2).astype(np.float32)
nrm = np.zeros((N, N, 3), np.float32); nrm[..., 2] = 1
for name, arr, data in (("bishop_albedo", alb, False), ("bishop_orm", orm, True), ("bishop_normal", nrm * 0.5 + 0.5, True)):
    img = bpy.data.images.get(name + ".png") or bpy.data.images.new(name + ".png", N, N)
    img.scale(N, N)
    px = np.concatenate([np.clip(arr[::-1], 0, 1), np.ones((N, N, 1), np.float32)], axis=2).astype(np.float32)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = os.path.join(TEX, name + ".png"); img.file_format = "PNG"; img.save()
M["Land"] = mat("Land", tex="bishop")
M["Air"] = mat("Air", (0.5, 0.7, 1.0), 0.0, 0.1, alpha=0.08)

R, W, WALL, T = 1000.0, 210.0, 60.0, 12.0
FACE = Rx(math.pi / 2)        # Z-axis ring -> Y-axis ring

# the land: the inner face of the band, UVs wrapping the texture six times round
bm = bmesh.new()
uv = bm.loops.layers.uv.verify()
SEG = 256
ring_a = [bm.verts.new((math.cos(i * 2 * math.pi / SEG) * R, math.sin(i * 2 * math.pi / SEG) * R, -W)) for i in range(SEG)]
ring_b = [bm.verts.new((math.cos(i * 2 * math.pi / SEG) * R, math.sin(i * 2 * math.pi / SEG) * R, W)) for i in range(SEG)]
for i in range(SEG):
    j = (i + 1) % SEG
    f = bm.faces.new((ring_a[i], ring_b[i], ring_b[j], ring_a[j]))
    f.material_index = 0; f.tag = True
    u0, u1 = 6 * i / SEG, 6 * (i + 1) / SEG
    for l, (u, v) in zip(f.loops, ((u0, 0), (u0, 1), (u1, 1), (u1, 0))): l[uv].uv = (u, v)
bm.normal_update()
for f in bm.faces:
    if f.normal.dot(-radial(f.calc_center_median())) < 0: f.normal_flip()
bmesh.ops.transform(bm, matrix=FACE, verts=bm.verts[:])
obj("Land", bm, ["Land"])

bm = bmesh.new()
# hull: the outside of the band and the rim walls with their tops
revolve(bm, [(R + T, -W - 4), (R + T, W + 4)], SEG, lambda i, s: 1, closed=False, toward=radial)
for sz in (-1, 1):
    n0 = set(bm.verts)
    c = solid_ring(bm, R - WALL, R + T, -4, 4, SEG, mats=(1, 1, 1, 1))
    bmesh.ops.translate(bm, vec=Vector((0, 0, sz * W)), verts=since(bm, n0))
detail(bm, cyl_surface(R + T, -W + 6, W - 6), 1, scale=13.0, mats=(1, 0), window=2, glow=3, window_frac=0.04, glow_frac=0.02)
rs = random.Random(31)
for row in range(9):
    z = -W + 20 + row * (2 * W - 40) / 8
    for k in range(180):
        if rs.random() < 0.45: continue
        a = k * 2 * math.pi / 180 + rs.uniform(0, 0.01)
        d = Vector((math.cos(a), math.sin(a), 0))
        L = rs.uniform(12, 30)
        fast_box(bm, (L, 0.6, 2.2), d * (R + T + 0.3) + Vector((0, 0, z)), Matrix((Vector((-d.y, d.x, 0)), Vector((0, 0, 1)), d)).transposed(), 2, bm.loops.layers.uv.verify(), bottom=True)
window_rows(bm, cyl_surface(R - WALL - 0.1, -W - 2, -W + 2, inward=True), 1, 600, size=(6, 2), mi=2, seed=3, lit=0.7)
window_rows(bm, cyl_surface(R - WALL - 0.1, W - 2, W + 2, inward=True), 1, 600, size=(6, 2), mi=2, seed=4, lit=0.7)
# spokes to a hub, and mirror spars
for k in range(6):
    a = k * math.pi / 3
    loft(bm, [(60, 8, 8, 0.4), (R - WALL, 6, 6, 0.4)], Rz(a).to_3x3(), Vector((0, 0, 0)), 1)
revolve(bm, [(0, -40), (50, -40), (64, -20), (64, 20), (50, 40), (0, 40)], 32, lambda i, s: 0, closed=False,
        toward=lambda c: (radial(c) + Vector((0, 0, 0.5 if c.z > 0 else -0.5))).normalized())
antennas(bm, (0, 0, 40), (0, 0, 1), 5, 60, 7, mi=1, light=4)
bmesh.ops.transform(bm, matrix=FACE, verts=bm.verts[:])
obj("Hull", bm, ["Hull", "HullDark", "Window", "Glow", "NavRed"])

# city lights scattered on the land, sitting just above it
bm = bmesh.new()
rnd = random.Random(8)
for k in range(1400):
    a = rnd.random() * 2 * math.pi; z = rnd.uniform(-W + 12, W - 12)
    p = Vector((math.cos(a) * (R - 0.5), math.sin(a) * (R - 0.5), z))
    box(bm, (3, 3, 1.5), p, Rz(a), mi=0)
bmesh.ops.transform(bm, matrix=FACE, verts=bm.verts[:])
obj("Cities", bm, ["Window"])


# ================================================================ v2 secondary-scale pass (asset overhaul, docs/ASSET_REFERENCES.md)
AM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar", "Rock"]
A_HULL, A_DARK, A_LIGHT, A_ACC, A_HAZ, A_WIN, A_WCOOL, A_STROBE, A_RAD, A_GLOW, A_RED, A_SOLAR, A_ROCK = range(len(AM))
M["Rock"] = mat("Rock", (0.32, 0.27, 0.22), 0.0, 0.9)
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
bm = bmesh.new()

# docking spindles at the hub with ships tied up, transit tubes along the spokes, radiators on the outer hull
for k in range(3):
    a = k * 2 * math.pi / 3
    d = Vector((math.cos(a), math.sin(a), 0))
    dock_arm(bm, d * 60, (d + Z_ * 0.8).normalized(), 60, 70, mats=(A_LIGHT, A_DARK, A_GLOW, A_WIN), seed=170 + k)
for k in range(6):
    a = k * math.pi / 3
    d = Vector((math.cos(a), math.sin(a), 0)); side = Vector((-d.y, d.x, 0))
    pipe_run(bm, [d * 70 + side * 12, d * (R - WALL - 4) + side * 12], 3.0, n=2, mi=A_DARK, clamp_mi=A_HAZ)
for k in range(24):
    a = k * 2 * math.pi / 24
    d = Vector((math.cos(a), math.sin(a), 0)); side = Vector((-d.y, d.x, 0))
    radiator_bank(bm, d * (R + T + 1) + Z_ * (W * 0.5 if k % 2 else -W * 0.5), d, side, 6, 40, 16, 8, mi_fin=A_RAD, mi_man=A_DARK)
    strobe(bm, d * (R + T + 2) + Z_ * (W + 6), 2.0, A_RED if k % 3 == 0 else A_STROBE)
bmesh.ops.transform(bm, matrix=FACE, verts=bm.verts[:])
obj("Fittings", bm, AM)

result = export(col, root, "bishop")
