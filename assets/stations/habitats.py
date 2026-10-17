"""
Space habitats after the NASA Ames summer studies of 1975-77 (docs/ASSET_REFERENCES.md): an O'Neill cylinder
(Rick Guidice, AC75-1085), a Stanford torus (Don Davis, AC76-0525) and a Bernal sphere (Guidice, AC76-0965).
Z is the spin axis (the game turns them about it). Model units are 100 m (O'Neill) and 10 m (torus, sphere);
the game sizes each to its true length. Exports assets/stations/<slug>/<slug>.glb.
Run inside Blender:
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/habitats.py").read())
Set ONLY = ["oneill"] (or "torus", "bernal") first to build just one.
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
M["Mirror"] = mat("Mirror", (0.82, 0.78, 0.66), 0.85, 0.12)
M["Land"] = mat("Land", (0.12, 0.2, 0.09), 0.0, 0.85)
M["Copper"] = mat("Copper", (0.55, 0.3, 0.16), 0.7, 0.4)
HM = ["Hull", "HullDark", "HullLight", "Accent", "Window", "WindowCool", "Strobe", "Glow", "Radiator", "NavRed", "Solar",
      "Mirror", "Land", "Copper", "DarkGlass", "Plant"]
(HULL, DARK, LIGHT, ACC, WIN, WCOOL, STROBE, GLOW, RAD, RED, SOLAR, MIRROR, LAND, COPPER, GLASS, PLANT) = range(len(HM))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
def U(b): return b.loops.layers.uv.verify()
def radial(c): return Vector((c.x, c.y, 0)).normalized()


# ---------------------------------------------------------------- O'Neill cylinder (units: 100 m)
def oneill():
    col, root = setup("OneillHab")
    R, L = 40.0, 320.0          # four kilometres in radius, thirty-two long
    bm = bmesh.new()
    # the hull: six strips round the drum, land and window alternating
    for k in range(6):
        a0 = k * math.pi / 3 + 0.004; a1 = a0 + math.pi / 3 - 0.008
        revolve(bm, [(R, -L / 2), (R, L / 2)], 12, lambda i, s, k=k: LAND if k % 2 == 0 else GLASS, closed=False, toward=radial, a0=a0, a1=a1)
    # window strips: ribs of mullion every kilometre, and the glow of the valleys within
    for k in range(3):
        am = (2 * k + 1) * math.pi / 3 + math.pi / 6
        d = Vector((math.cos(am), math.sin(am), 0)); t = Vector((-d.y, d.x, 0))
        for j in range(32):
            fast_box(bm, (2 * R * math.sin(math.pi / 6) * 0.98, 0.6, 0.4), d * R * math.cos(math.pi / 6) + Z_ * (-L / 2 + 5 + j * 10), Matrix((t, Z_, d)).transposed(), DARK, U(bm), bottom=True)
        for j in range(60):
            fast_box(bm, (1.2, 2.5, 0.2), d * (R * math.cos(math.pi / 6) - 0.5) + t * ((j % 7) - 3) * 3 + Z_ * (-L / 2 + 4 + j * 5.2), Matrix((t, Z_, d)).transposed(), WIN, U(bm), bottom=True)
    # longerons along the land strips, hull detail
    for k in range(3):
        a = k * 2 * math.pi / 3 + math.pi / 6
        detail(bm, cyl_surface(R + 0.05, -L / 2 + 2, L / 2 - 2, a0=a - math.pi / 6 + 0.02, a1=a + math.pi / 6 - 0.02), 10 + k, scale=6.0, mats=(DARK, HULL), window=WCOOL, window_frac=0.02)
    # the end caps: concentric bands stepping in to the spindle (Guidice's banded cap)
    for s in (1, -1):
        prof = [(R, s * L / 2), (R * 0.97, s * (L / 2 + 3)), (R * 0.8, s * (L / 2 + 7)), (R * 0.55, s * (L / 2 + 9.5)), (R * 0.25, s * (L / 2 + 10.5)), (4, s * (L / 2 + 11))]
        revolve(bm, prof, 96, lambda i, s2: HULL if i % 2 == 0 else LIGHT, closed=False, toward=lambda c, s=s: (Vector((c.x, c.y, 0)) * 0.2 + Z_ * s).normalized())
        for j, r in enumerate((R * 0.9, R * 0.68, R * 0.42)):
            band(bm, Z_ * s * (L / 2 + 5 + j * 2.2), Z_, r, 0.8, 0.5, WIN if j == 1 else DARK, 96)
    obj("Drum", bm, HM)

    # the three great mirrors, hinged at the sunward rim, opened thirty degrees
    bm = bmesh.new()
    for k in range(3):
        am = (2 * k + 1) * math.pi / 3 + math.pi / 6           # over the window strips
        d = Vector((math.cos(am), math.sin(am), 0)); t = Vector((-d.y, d.x, 0))
        hinge = d * R + Z_ * (L / 2)
        open_ = 0.52
        fwd = (-Z_ * math.cos(open_) + d * math.sin(open_)).normalized()
        nrm = t.cross(fwd).normalized()
        if nrm.dot(-d) < 0: nrm = -nrm
        W = 2 * R * math.sin(math.pi / 6) * 1.05
        for j in range(16):
            c = hinge + fwd * (L / 32 * (j + 0.5))
            fast_box(bm, (W, L / 32 * 0.97, 0.15), c, Matrix((t, fwd, nrm)).transposed(), MIRROR, U(bm))
        truss(bm, hinge, hinge + fwd * L, w=1.2, mi=DARK, rod=0.12)
        for e in (-1, 1):
            truss(bm, hinge + t * e * W / 2, hinge + t * e * W / 2 + fwd * L, w=0.6, mi=DARK, rod=0.06)
        # the hinge struts that hold it open
        truss(bm, d * R + Z_ * (L / 2 - 60), hinge + fwd * 60, w=1.0, mi=DARK, rod=0.1)
    obj("Mirrors", bm, HM)

    # the far cap: a bead necklace of agricultural pods on a ring, the spindle with dish, industry and docking
    bm = bmesh.new()
    zc = -L / 2 - 11
    rn = R * 1.75
    for k in range(72):
        a = k * 2 * math.pi / 72
        d = Vector((math.cos(a), math.sin(a), 0))
        sphere(bm, 2.4, d * rn + Z_ * (zc - 4), mi=GLASS, seg=12)
        band(bm, d * rn + Z_ * (zc - 4), Vector((-d.y, d.x, 0)), 2.5, 0.4, 0.3, HULL, 12)
    n0 = set(bm.verts)
    solid_ring(bm, rn - 0.5, rn + 0.5, -0.4, 0.4, 144, mats=(DARK,) * 4)
    bmesh.ops.translate(bm, vec=Z_ * (zc - 4), verts=since(bm, n0))
    for k in range(8):
        a = k * math.pi / 4
        d = Vector((math.cos(a), math.sin(a), 0))
        truss(bm, Z_ * (zc - 2), d * rn + Z_ * (zc - 4), w=0.8, mi=DARK, rod=0.08)
    cyl(bm, 3, 3, 40, Z_ * (zc - 20), seg=24, mi=HULL)
    for j in range(4): band(bm, Z_ * (zc - 8 - j * 9), Z_, 3, 1.2, 0.8, ACC if j == 2 else DARK, 24)
    module(bm, Z_ * (zc - 30) + X_ * 7, Z_, X_, 14, 6, 5, mats=(HULL, DARK, WIN), seed=3)
    module(bm, Z_ * (zc - 30) - X_ * 7, Z_, X_, 12, 5, 5, mats=(LIGHT, DARK, WIN), seed=4)
    radiator_bank(bm, Z_ * (zc - 26) + Y_ * 4, Y_, Z_, 8, 10, 4, 1.4, mi_fin=RAD, mi_man=DARK)
    dish(bm, Z_ * (zc - 42), -Z_, 6, mi_dish=LIGHT, mi_frame=DARK)
    for k in range(4):
        a = k * math.pi / 2
        d = Vector((math.cos(a), math.sin(a), 0))
        strobe(bm, d * rn + Z_ * (zc - 4), 0.6, RED if k % 2 else STROBE)
    # the sunward spindle: power station and solar collectors
    cyl(bm, 2, 2, 24, Z_ * (L / 2 + 22), seg=16, mi=HULL)
    for e in (-1, 1): solar_wing(bm, Z_ * (L / 2 + 26) + X_ * e * 2, X_ * e, Z_, 20, 6, panels=4, mi_cell=SOLAR, mi_frame=DARK)
    obj("Necklace", bm, HM)
    return export(col, root, "oneill")


# ---------------------------------------------------------------- Stanford torus (units: 10 m)
def torus():
    col, root = setup("TorusHab")
    RM, RT = 83.0, 6.5             # 830 m to the tube's centre, a 130 m tube
    bm = bmesh.new()
    # the habitat tube: glazed on the inner face (toward the hub), hull outside
    prof = [(RM + RT * math.cos(t), RT * math.sin(t)) for t in [i * 2 * math.pi / 24 for i in range(24)]]
    revolve(bm, prof, 192, lambda i, s: GLASS if 9 <= i <= 14 else HULL, closed=True, toward=lambda c: (c - Vector((c.x, c.y, 0)).normalized() * RM).normalized())
    # the chevron radiation shield: a loose outer sleeve of copper-coloured plates, gapped
    for k in range(96):
        a0 = k * 2 * math.pi / 96 + 0.005; a1 = a0 + 2 * math.pi / 96 - 0.01
        sh = [(RM + (RT + 1.6) * math.cos(t), (RT + 1.6) * math.sin(t)) for t in [-1.9 + i * 3.8 / 8 for i in range(9)]]
        revolve(bm, sh, 1, lambda i, s, k=k: COPPER if k % 6 else DARK, closed=False, a0=a0, a1=a1, toward=lambda c: (c - Vector((c.x, c.y, 0)).normalized() * RM).normalized())
    # lights through the glazing: window clusters on the inner face
    for k in range(240):
        a = k * 2 * math.pi / 240
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        if k % 3 == 0: continue
        fast_box(bm, (1.2, 0.8, 0.2), d * (RM - RT - 0.05) + Z_ * ((k % 5) - 2) * 1.0, Matrix((t, Z_, -d)).transposed(), WIN, U(bm), bottom=True)
    # six spokes to the hub
    for k in range(6):
        a = k * math.pi / 3
        d = Vector((math.cos(a), math.sin(a), 0))
        loft(bm, [(8, 1.8, 1.8, 0.4), (RM - RT - 0.5, 1.6, 1.6, 0.4)], basis(d), Vector((0, 0, 0)), HULL)
        for j in range(5): band(bm, d * (16 + j * 13), d, 1.9, 0.5, 0.4, DARK if j % 2 else ACC, 12)
    obj("Wheel", bm, HM)

    # the hub: drum with docking spindle and the secondary louvred mirror ring around it
    bm = bmesh.new()
    revolve(bm, [(4, 12), (8, 10), (9, 6), (9, -6), (8, -10), (4, -12)], 48, lambda i, s: HULL if i % 2 else LIGHT, closed=False, toward=lambda c: Vector((c.x, c.y, c.z)).normalized())
    detail(bm, cyl_surface(9.05, -5.5, 5.5), 30, scale=0.5, mats=(DARK, HULL), window=WIN, window_frac=0.08)
    cyl(bm, 2.2, 2.2, 70, Z_ * -40, seg=16, mi=HULL)                               # the spindle down to the docks
    for j in range(5): band(bm, Z_ * (-16 - j * 12), Z_, 2.2, 0.8, 0.6, DARK, 16)
    for k in range(3):
        a = k * 2 * math.pi / 3
        d = Vector((math.cos(a), math.sin(a), 0))
        module(bm, Z_ * -62 + d * 5, d, Z_, 9, 3.5, 3, mats=(LIGHT, DARK, WIN), seed=40 + k)
        strobe(bm, Z_ * -75 + d * 3, 0.4, RED if k else STROBE)
    for k in range(36):                                           # secondary mirrors: louvres round the hub
        a = k * 2 * math.pi / 36
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        fast_box(bm, (2 * math.pi * 22 / 36 * 0.8, 6, 0.15), d * 22 + Z_ * 14, Matrix((t, (d + Z_ * 0.6).normalized().cross(t).normalized() * -1, (d + Z_ * 0.6).normalized())).transposed(), MIRROR, U(bm))
    n0 = set(bm.verts)
    solid_ring(bm, 25, 25.6, -0.3, 0.3, 72, mats=(DARK,) * 4)
    bmesh.ops.translate(bm, vec=Z_ * 14, verts=since(bm, n0))
    obj("Hub", bm, HM)

    # the primary mirror: a great tilted ring of panels hung above, throwing sunlight down onto the secondaries
    bm = bmesh.new()
    zm, tilt = 120.0, 0.78
    rot = Matrix.Translation(Z_ * zm) @ Rx(tilt)
    n0 = set(bm.verts)
    for k in range(48):
        a0 = k * 2 * math.pi / 48 + 0.004
        solid_ring(bm, 40, 100, -0.2, 0.2, 2, mats=(MIRROR, MIRROR, DARK, MIRROR), a0=a0, a1=a0 + 2 * math.pi / 48 - 0.008, bevel=0.0)
    solid_ring(bm, 100, 102, -0.8, 0.8, 96, mats=(DARK,) * 4)
    solid_ring(bm, 38, 40, -0.8, 0.8, 72, mats=(DARK,) * 4)
    bmesh.ops.transform(bm, matrix=rot, verts=since(bm, n0))
    for k in range(3):                                            # struts from the hub up to the mirror
        a = k * 2 * math.pi / 3 + 0.3
        p = rot @ Vector((math.cos(a) * 70, math.sin(a) * 70, 0))
        truss(bm, Z_ * 12, p, w=1.4, mi=DARK, rod=0.14)
    obj("Primary", bm, HM)
    return export(col, root, "torus")


# ---------------------------------------------------------------- Bernal sphere (units: 10 m)
def bernal():
    col, root = setup("BernalHab")
    RS = 25.6                      # Island One: a sphere 512 m across
    bm = bmesh.new()
    # the sphere: hull with a glazed equatorial band (the "window" between two chevron shields)
    lats = [-90 + i * 7.5 for i in range(25)]
    prof = [(RS * math.cos(math.radians(l)), RS * math.sin(math.radians(l))) for l in lats]
    revolve(bm, prof, 96, lambda i, s: GLASS if 10 <= i <= 13 else (HULL if i % 2 else LIGHT), closed=False, toward=lambda c: c.normalized())
    for k in range(144):
        a = k * 2 * math.pi / 144
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        if k % 4 == 0: continue
        fast_box(bm, (0.9, 0.6, 0.15), d * (RS - 0.15) + Z_ * (((k % 3) - 1) * 1.2), Matrix((t, Z_, -d)).transposed(), WIN, U(bm), bottom=True)
    for s in (1, -1):
        band(bm, Z_ * s * 4.5, Z_, RS * math.cos(math.radians(10)) + 0.3, 1.0, 1.0, DARK, 96)
    obj("Sphere", bm, HM)

    # the crystal palace: stacked agricultural rings either side along the axis, glazed and lit
    bm = bmesh.new()
    for s in (1, -1):
        for j in range(9):
            z = s * (RS + 10 + j * 7.5)
            rr = 34 - abs(j - 4) * 1.6
            n0 = set(bm.verts)
            prof = [(rr + 3.2 * math.cos(t), 3.2 * math.sin(t)) for t in [i * 2 * math.pi / 12 for i in range(12)]]
            revolve(bm, prof, 72, lambda i, s2: GLASS if i in (5, 6, 7) else (PLANT if i in (4, 8) else HULL), closed=True, toward=lambda c, rr=rr: (Vector((c.x, c.y, 0)) - Vector((c.x, c.y, 0)).normalized() * rr + Vector((0, 0, c.z))).normalized())
            bmesh.ops.translate(bm, vec=Z_ * z, verts=since(bm, n0))
            for k in range(4):
                a = k * math.pi / 2 + j * 0.3
                d = Vector((math.cos(a), math.sin(a), 0))
                truss(bm, Z_ * z + d * 3, Z_ * z + d * (rr - 3), w=0.6, mi=DARK, rod=0.06)
            for k in range(36):
                a = k * 2 * math.pi / 36
                d = Vector((math.cos(a), math.sin(a), 0))
                sphere(bm, 0.25, d * (rr - 3.3) + Z_ * z, mi=GLOW if k % 2 else WIN, seg=4)
    obj("Rings", bm, HM)

    # the axle spindle: docking at both ends, solar wings, radiators, a mirror ring round the sunward end
    bm = bmesh.new()
    for s in (1, -1):
        cyl(bm, 2.4, 2.4, 95, Z_ * s * (RS + 45), seg=16, mi=HULL)
        for j in range(6): band(bm, Z_ * s * (RS + 12 + j * 14), Z_, 2.4, 0.8, 0.6, ACC if j == 0 else DARK, 16)
        loft(bm, [(0, 4, 4, 0.4), (8, 6, 6, 0.4), (14, 6, 6, 0.4), (18, 3, 3, 0.4)], basis(Z_ * s), Z_ * s * (RS + 92), HULL)
        for k in range(3):
            a = k * 2 * math.pi / 3 + (0.5 if s > 0 else 0)
            d = Vector((math.cos(a), math.sin(a), 0))
            radiator_bank(bm, Z_ * s * (RS + 80) + d * 3, d, Z_, 8, 14, 5, 1.4, mi_fin=RAD, mi_man=DARK)
            strobe(bm, Z_ * s * (RS + 108) + d * 3, 0.4, RED if k else STROBE)
        for e in (-1, 1): solar_wing(bm, Z_ * s * (RS + 100) + X_ * e * 3, X_ * e, Z_, 40, 9, panels=5, mi_cell=SOLAR, mi_frame=DARK)
    n0 = set(bm.verts)
    for k in range(24):
        a0 = k * 2 * math.pi / 24 + 0.01
        solid_ring(bm, 40, 62, -0.2, 0.2, 2, mats=(MIRROR, MIRROR, DARK, MIRROR), a0=a0, a1=a0 + 2 * math.pi / 24 - 0.02, bevel=0.0)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(Z_ * (RS + 100)), verts=since(bm, n0))
    for k in range(4):
        a = k * math.pi / 2
        truss(bm, Z_ * (RS + 96), Vector((math.cos(a) * 50, math.sin(a) * 50, RS + 100)), w=0.8, mi=DARK, rod=0.08)
    obj("Spindle", bm, HM)
    return export(col, root, "bernal")


result = {}
for _name in globals().get("ONLY", ["oneill", "torus", "bernal"]):
    result[_name] = {"oneill": oneill, "torus": torus, "bernal": bernal}[_name]()
print("RESULT", result)
