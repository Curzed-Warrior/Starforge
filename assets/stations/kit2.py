"""
Station kit v2: the secondary scale (docs/ASSET_REFERENCES.md). Loaded by kit.py after the base helpers.
Functional parts that make a structure read as built and used: pipe runs, caged tanks, radiator banks, solar wings,
docking arms with ships tied up, gantry cranes, dishes, collars, strobes and pressurised modules.
"""
M.update({
    "HullLight":   mat("HullLight", tex="hulllight"),
    "Accent":      mat("Accent", tex="accent"),
    "Hazard":      mat("Hazard", tex="hazard"),
    "Gold":        mat("Gold", (0.85, 0.62, 0.22), 1.0, 0.32),
    "Chrome":      mat("Chrome", (0.8, 0.82, 0.85), 1.0, 0.12),
    "DarkGlass":   mat("DarkGlass", (0.02, 0.03, 0.05), 0.4, 0.06),
    "Strobe":      mat("Strobe", (1, 1, 1), emit=(1, 0.95, 0.9), strength=30),
    "Plasma":      mat("Plasma", (0.6, 0.8, 1.0), emit=(0.5, 0.75, 1.0), strength=8),
    "Heat":        mat("Heat", (1, 0.35, 0.05), emit=(1, 0.3, 0.04), strength=6),
})


def basis(fwd, up=(0, 0, 1)):
    """A rotation (3x3) whose local X points along fwd and local Z is as close to up as fwd allows."""
    x = Vector(fwd).normalized()
    u = Vector(up)
    if abs(x.dot(u.normalized())) > 0.98:
        u = Vector((1, 0, 0)) if abs(x.x) < 0.9 else Vector((0, 1, 0))
    y = u.cross(x).normalized()
    z = x.cross(y)
    return Matrix((x, y, z)).transposed()


def pipe_run(bm, pts, r, n=3, gap=None, mi=1, clamp_mi=0, clamp_every=None, up=(0, 0, 1)):
    """A bundle of n parallel pipes along a polyline, with clamp collars at intervals."""
    pts = [Vector(p) for p in pts]
    gap = gap or r * 2.6
    clamp_every = clamp_every or r * 18
    uv = bm.loops.layers.uv.verify()
    for a, b in zip(pts, pts[1:]):
        d = b - a
        L = d.length
        if L < 1e-3:
            continue
        R = basis(d, up)
        Y = R.col[1]
        for i in range(n):
            off = Y * (i - (n - 1) / 2) * gap
            cyl(bm, r, r, L, (a + b) / 2 + off, R.to_4x4() @ Ry(math.pi / 2), seg=8, mi=mi)
        k = max(1, int(L / clamp_every))
        for j in range(k + 1):
            fast_box(bm, (r * 1.4, gap * n + r * 1.2, r * 2.6), a + d * j / k, R, clamp_mi, uv, bottom=True)


def band(bm, at, axis, r, width, depth, mi=1, seg=32):
    """A collar round a cylinder of radius r (axis direction), standing depth proud of it."""
    n0 = set(bm.verts)
    solid_ring(bm, r - depth * 0.3, r + depth, -width / 2, width / 2, seg, mats=(mi, mi, mi, mi))
    R = basis(axis).to_4x4() @ Ry(math.pi / 2)          # the ring's own axis (local Z) turned onto `axis`
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector(at)) @ R, verts=since(bm, n0))


def tank_cage(bm, at, r, axis=(0, 0, 1), capsule=0.0, mi_tank=0, mi_cage=1, ribs=4):
    """A pressure tank (a sphere, or a capsule stretched by `capsule` x r) inside ribbed hoops and end collars."""
    at = Vector(at)
    R = basis(axis)
    ax = R.col[0]
    R4 = R.to_4x4()
    if capsule > 0:
        cyl(bm, r, r, r * capsule, at, R4 @ Ry(math.pi / 2), seg=16, mi=mi_tank, caps=False)
        for s in (-1, 1):
            sphere(bm, r, at + ax * s * r * capsule / 2, mi=mi_tank, seg=16)
    else:
        sphere(bm, r, at, mi=mi_tank, seg=20)
    for k in range(ribs):
        n0 = set(bm.verts)
        solid_ring(bm, r * 1.05, r * 1.12, -r * 0.035, r * 0.035, 24, mats=(mi_cage,) * 4)
        vs = since(bm, n0)
        # the hoop stands in a plane containing the tank's axis, turned k/ribs of a half-turn about it
        bmesh.ops.transform(bm, matrix=Matrix.Diagonal((1, 1, 1, 1)), verts=vs)
        stretch = Matrix.Diagonal((1 + capsule / 2, 1, 1, 1))
        bmesh.ops.transform(bm, matrix=Matrix.Translation(at) @ R4 @ Rx(k * math.pi / ribs) @ stretch @ Rx(math.pi / 2), verts=vs)
    half = r * (1 + capsule / 2)
    for s in (-1, 1):
        band(bm, at + ax * s * half * 0.72, ax, r * 0.7, r * 0.08, r * 0.08, mi_cage, 20)


def radiator_bank(bm, at, out, along, n, fin_len, fin_h, gap, mi_fin=0, mi_man=1):
    """A manifold running `along`, with n thin fins standing out of it in direction `out`."""
    at = Vector(at)
    out = Vector(out).normalized()
    along = Vector(along).normalized()
    side = out.cross(along).normalized()
    R = Matrix((out, along, side)).transposed()
    uv = bm.loops.layers.uv.verify()
    L = gap * (n - 1)
    cyl(bm, fin_h * 0.07, fin_h * 0.07, L + gap, at, basis(along).to_4x4() @ Ry(math.pi / 2), seg=8, mi=mi_man)
    for i in range(n):
        p = at + along * (i * gap - L / 2) + out * fin_len / 2
        fast_box(bm, (fin_len, fin_h * 0.035, fin_h), p, R, mi_fin, uv, bottom=True)
        fast_box(bm, (fin_len * 0.97, fin_h * 0.06, fin_h * 0.05), p + side * fin_h * 0.5, R, mi_man, uv, bottom=True)


def solar_wing(bm, at, out, up, length, width, panels=6, mi_cell=0, mi_frame=1, mast=True):
    """A deployable solar array: a mast from `at` along `out`, carrying panel pairs with frames."""
    at = Vector(at)
    out = Vector(out).normalized()
    R = basis(out, up)
    side = R.col[1]
    uv = bm.loops.layers.uv.verify()
    if mast:
        truss(bm, at, at + out * length, w=width * 0.06, mi=mi_frame, rod=width * 0.006)
    pl = length / panels
    for i in range(panels):
        c = at + out * (pl * (i + 0.5))
        for s in (-1, 1):
            pc = c + side * s * (width * 0.27)
            fast_box(bm, (pl * 0.94, width * 0.5, width * 0.012), pc, R, mi_cell, uv, bottom=True)
            for e in (-1, 1):
                fast_box(bm, (pl * 0.95, width * 0.012, width * 0.02), pc + side * e * width * 0.25, R, mi_frame, uv, bottom=True)
            fast_box(bm, (width * 0.012, width * 0.5, width * 0.02), pc, R, mi_frame, uv, bottom=True)


def dock_arm(bm, at, out, length, ship_len, mats=(0, 1, 2, 3), up=(0, 0, 1), seed=0, w=None):
    """A docking arm: a truss boom out of the hull, a collar at its end, and a ship tied up alongside."""
    at = Vector(at)
    out = Vector(out).normalized()
    R = basis(out, up)
    side, upv = R.col[1], R.col[2]
    w = w or max(ship_len * 0.08, 0.8)
    truss(bm, at, at + out * length, w=w, mi=mats[1], rod=w * 0.07)
    end = at + out * length
    loft(bm, [(-w * 0.8, w * 0.8, w * 0.8, 0.4), (w * 0.8, w * 0.9, w * 0.9, 0.4)], R, end, mats[0])
    sp = end - out * ship_len * 0.45 + side * (w * 0.9 + ship_len * 0.12)
    ship(bm, sp, Matrix((-out, -side, upv)).transposed(), ship_len, mats, seed)
    _rod(bm, end + side * w * 0.8, sp - side * ship_len * 0.1 + out * ship_len * 0.1, w * 0.15, mats[1])
    sphere(bm, w * 0.18, end + out * w, mi=mats[3], seg=6)


def gantry(bm, a, b, up, width, height, mi_rail=1, mi_crane=0, cranes=2, seed=0):
    """Two rails from a to b with travelling crane bridges spanning them, each with a trolley and a cable."""
    a, b = Vector(a), Vector(b)
    R = basis(b - a, up)
    side, upv = R.col[1], R.col[2]
    uv = bm.loops.layers.uv.verify()
    L = (b - a).length
    for s in (-1, 1):
        fast_box(bm, (L, width * 0.04, width * 0.06), (a + b) / 2 + side * s * width / 2, R, mi_rail, uv, bottom=True)
    rnd = random.Random(seed)
    for k in range(cranes):
        c = a + (b - a) * rnd.uniform(0.15, 0.85)
        for s in (-1, 1):
            fast_box(bm, (width * 0.08, width * 0.06, height), c + side * s * width / 2 + upv * height / 2, R, mi_crane, uv, bottom=True)
        fast_box(bm, (width * 0.1, width * 1.05, width * 0.08), c + upv * height, R, mi_crane, uv, bottom=True)
        t = c + side * rnd.uniform(-0.3, 0.3) * width + upv * (height - width * 0.06)
        fast_box(bm, (width * 0.12, width * 0.1, width * 0.06), t, R, mi_rail, uv, bottom=True)
        _rod(bm, t, t - upv * height * 0.6, width * 0.008, mi_rail)


def dish(bm, at, aim, r, mi_dish=0, mi_frame=1, mi_feed=None, depth=0.28, ribs=12, yoke=True):
    """A parabolic dish facing `aim`, with radial ribs, a feed on a tripod and a yoke behind."""
    at = Vector(at)
    aim = Vector(aim).normalized()
    R = basis(aim).to_4x4() @ Ry(-math.pi / 2)              # local Z -> aim
    prof = [(r * i / 8 + 1e-3, depth * r * (i / 8) ** 2) for i in range(9)]
    n0 = set(bm.verts)
    revolve(bm, prof, 40, lambda i, s: mi_dish, closed=False)
    back = revolve(bm, [(p[0], p[1] - r * 0.02) for p in prof], 40, lambda i, s: mi_frame, closed=False)
    bmesh.ops.reverse_faces(bm, faces=back)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(at) @ R, verts=since(bm, n0))
    def P(x, y, z): return at + (R @ Vector((x, y, z, 1))).to_3d() - Vector(R.translation)
    for k in range(ribs):
        a = k * 2 * math.pi / ribs
        _rod(bm, P(math.cos(a) * r * 0.12, math.sin(a) * r * 0.12, -r * 0.03), P(math.cos(a) * r, math.sin(a) * r, depth * r - r * 0.03), r * 0.012, mi_frame)
    focal = at + aim * (r / (4 * depth)) * 0.9
    for k in range(3):
        a = k * 2 * math.pi / 3 + 0.3
        _rod(bm, P(math.cos(a) * r * 0.95, math.sin(a) * r * 0.95, depth * r * 0.9), focal, r * 0.01, mi_frame)
    sphere(bm, r * 0.05, focal, mi=mi_feed if mi_feed is not None else mi_frame, seg=10)
    if yoke:
        cyl(bm, r * 0.08, r * 0.1, r * 0.4, at - aim * r * 0.2, R, seg=10, mi=mi_frame)


def strobe(bm, at, r=0.4, mi=0):
    sphere(bm, r, at, mi=mi, seg=6)


def module(bm, at, fwd, up, L, W, H, mats=(0, 1, 3), seed=0, windows=True, detail_scale=None):
    """A pressurised module: a chamfered hull with end collars, window rows on its flanks and kitbash on top."""
    R = basis(fwd, up)
    at = Vector(at)
    X, side, upv = R.col[0], R.col[1], R.col[2]
    loft(bm, [(-L / 2, W / 2, H / 2, 0.35), (L / 2, W / 2, H / 2, 0.35)], R, at, mats[0])
    for s in (-1, 1):
        loft(bm, [(s * L * 0.47, W * 0.56, H * 0.56, 0.35), (s * L * 0.5, W * 0.56, H * 0.56, 0.35)], R, at, mats[1])
    ds = max(detail_scale or min(W, H) * 0.08, 0.6)
    detail(bm, plane_surface(at - X * L * 0.45 - side * W * 0.4 + upv * H * 0.5, X * L * 0.9, side * W * 0.8), seed, scale=ds, mats=(mats[0], mats[1]))
    if windows and len(mats) > 2:
        for s in (-1, 1):
            o = at - X * L * 0.42 + side * s * W * 0.505 - upv * H * 0.1
            surf = plane_surface(o, upv * H * 0.3, X * L * 0.84) if s > 0 else plane_surface(o, X * L * 0.84, upv * H * 0.3)
            window_rows(bm, surf, max(3, int(L / (H * 0.25))), 2, size=(H * 0.05, H * 0.1), mi=mats[2], seed=seed + s, lit=0.7)


def restyle(col):
    """v2 palette for older builds: dark metal becomes the primary hull and the light plating the accent."""
    for o in col.all_objects:
        if o.type != "MESH": continue
        for i, m in enumerate(o.data.materials):
            if m and m.name == "Hull": o.data.materials[i] = M["HullDark"]
            elif m and m.name == "HullDark": o.data.materials[i] = M["Hull"]


def strip_lights(bm, R, z, n, mi, rot=None, a1=2 * math.pi, keep=0.7, seed=0, h=0.6, depth=0.3):
    """Broken runs of warm light round the outside of a ring of radius R at height z (optionally rotated)."""
    rs = random.Random(seed)
    uv = bm.loops.layers.uv.verify()
    for k in range(n):
        a = k * 2 * math.pi / n
        if a > a1 or rs.random() > keep: continue
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0)); zz = Vector((0, 0, 1))
        p, R3 = d * (R + 0.05) + zz * z, Matrix((t, zz, d)).transposed()
        if rot is not None: p, R3 = rot @ p, rot @ R3
        fast_box(bm, (2 * math.pi * R / n * 0.75, h, depth), p, R3, mi, uv, bottom=True)
