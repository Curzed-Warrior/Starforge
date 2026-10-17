"""
The tech bible's great works: sixteen new megastructures, each exported to assets/stations/<slug>/<slug>.glb like the
others. Parts the game animates are separate objects named r0..r2, ring, disc, core, halo (see Megastructures.Animated).
Z is up. Run headless:
    blender -b --python-expr "ONLY=['ringworld']; exec(open(r'C:/Users/diamo/Desktop/starforge/assets/stations/greatworks2.py').read())"
Leave ONLY unset to build all of them.
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())

M["Umbra"] = mat("Umbra", (0.3, 0.08, 0.55), emit=(0.5, 0.15, 1.0), strength=3)
M["Rune"] = mat("Rune", (1.0, 0.75, 0.3), emit=(1.0, 0.7, 0.28), strength=3)
M["Lucent"] = mat("Lucent", (0.88, 0.9, 0.96), 0.6, 0.18)
M["LucentGlow"] = mat("LucentGlow", (0.9, 0.95, 1.0), emit=(0.85, 0.92, 1.0), strength=1.2)
M["Void"] = mat("Void", (0.005, 0.005, 0.01), 0.0, 0.2)
M["Ocean"] = mat("Ocean", (0.03, 0.12, 0.3), 0.2, 0.08)
M["Rock"] = mat("Rock", (0.32, 0.27, 0.22), 0.0, 0.9)
M["Ice"] = mat("Ice", (0.75, 0.82, 0.9), 0.0, 0.4)
M["Scorch"] = mat("Scorch", (0.38, 0.2, 0.09), 0.0, 0.9)
M["Stone"] = mat("Stone", (0.055, 0.055, 0.065), 0.0, 0.8)
M["GoldMirror"] = mat("GoldMirror", (1.0, 0.68, 0.22), 0.55, 0.3)     # less metallic until the game has reflections to show
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))

def rot_to(d):
    """A 3x3 rotation whose Z axis points along d."""
    d = Vector(d).normalized()
    x = d.cross(Z if abs(d.z) < 0.9 else X).normalized()
    return Matrix((x, d.cross(x), d)).transposed()

def ring_at(bm, rin, rout, h, steps, mats, at=(0, 0, 0), rot=None):
    n0 = set(bm.verts)
    c = solid_ring(bm, rin, rout, -h / 2, h / 2, steps, mats=mats)
    m = Matrix.Translation(Vector(at)) @ (rot if rot is not None else Matrix.Identity(4))
    bmesh.ops.transform(bm, matrix=m, verts=since(bm, n0))
    return c

def shell_patch(bm, r, lat0, lat1, lon0, lon1, nlat, nlon, mi):
    """A curved patch of a sphere (one face per cell), facing outward."""
    grid = []
    for i in range(nlat + 1):
        la = lat0 + (lat1 - lat0) * i / nlat
        row = []
        for j in range(nlon + 1):
            lo = lon0 + (lon1 - lon0) * j / nlon
            row.append(bm.verts.new((r * math.cos(la) * math.cos(lo), r * math.cos(la) * math.sin(lo), r * math.sin(la))))
        grid.append(row)
    for i in range(nlat):
        for j in range(nlon):
            f = bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
            f.material_index = mi

BUILD = {}
def works(fn): BUILD[fn.__name__] = fn; return fn

# ================================================================ v2 palette and helpers (asset overhaul, docs/ASSET_REFERENCES.md)
M["Coil"] = mat("Coil", (0.15, 0.45, 1.0), emit=(0.1, 0.4, 1.0), strength=2.5)        # saturated: the bright ones bloom to white
M["DiscIn"] = mat("DiscIn", (1.0, 0.75, 0.45), emit=(1.0, 0.7, 0.4), strength=3)
M["DiscOut"] = mat("DiscOut", (1.0, 0.3, 0.05), emit=(1.0, 0.25, 0.03), strength=1.6)
GM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar",
      "Plasma", "Heat", "Screen", "Void", "Coil", "DiscIn", "DiscOut"]
(HULL, DARK, LIGHT, ACC, HAZ, WIN, WCOOL, STROBE, RAD, GLOW, RED, SOLAR, PLASMA, HEAT, SCREEN, VOID, COIL, DISCIN, DISCOUT) = range(len(GM))
def U(b): return b.loops.layers.uv.verify()

def sweep(bm, pts, w, h, mi, side=Y, closed=False, lift=0.0, frac=1.02):
    """Boxes laid end to end along a polyline: w across (along `side`), h deep (along t x side), raised `lift`."""
    uv = U(bm)
    n = len(pts) if closed else len(pts) - 1
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % len(pts)]
        t = b - a; L = t.length
        if L < 1e-4: continue
        t.normalize(); nrm = t.cross(side).normalized()
        fast_box(bm, (L * frac, w, h), (a + b) / 2 + nrm * lift, Matrix((t, side, nrm)).transposed(), mi, uv)

def seamed_ball(bm, r, at, mi_panel, mi_seam, mi_raised, sub=2, seed=0):
    """An icosphere of inset panels with glowing seams between them; a few panels stand proud."""
    ico = bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)["verts"]
    for v in ico: v.co += Vector(at)
    faces = list({f for v in ico for f in v.link_faces})
    for f in faces: f.material_index = mi_seam
    bmesh.ops.inset_individual(bm, faces=faces, thickness=r * 0.04, depth=0.0)
    rs = random.Random(seed)
    bm.normal_update()
    for f in faces:
        f.material_index = mi_raised if rs.random() < 0.2 else mi_panel
        if f.material_index == mi_raised:
            for v in f.verts: v.co += f.normal * r * 0.05

# ---------------------------------------------------------------- 1. Launch Loop
@works
def launchloop():
    """After Lofstrom's launch loop: a stadium of maglev track, its upper run arched and held by fans of stay cables
    from a keel truss, deflector stations at both ends turning the rotor round, cargo sleds riding the track."""
    col, root = setup("LaunchLoop")
    S, B, ZC = 500.0, 100.0, 100.0                      # half the straight, the end radius, the loop's centre height
    top = [Vector((x, 0, ZC + B + 55 * math.cos(math.pi / 2 * x / S) ** 2)) for x in [-S + 2 * S * i / 90 for i in range(91)]]
    right = [Vector((S + B * math.sin(a), 0, ZC + B * math.cos(a))) for a in [math.pi * i / 28 for i in range(1, 28)]]
    bot = [Vector((S - 2 * S * i / 90, 0, ZC - B)) for i in range(91)]
    left = [Vector((-S - B * math.sin(a), 0, ZC - B * math.cos(a))) for a in [math.pi * i / 28 for i in range(1, 28)]]
    loop = top + right + bot + left
    bm = bmesh.new()
    sweep(bm, loop, 12, 7, DARK, closed=True)                       # the sheath
    sweep(bm, loop, 3, 1.2, GLOW, closed=True, lift=4.0, frac=0.7)  # the rotor showing through its slot
    for i in range(0, len(loop), 6):                                # magnet collars
        a, b = loop[i], loop[(i + 1) % len(loop)]
        band(bm, a, b - a, 8, 3, 2.2, ACC if i % 24 == 0 else HULL, 12)
    # the keel and its fans of stay cables
    truss(bm, Vector((-S, 0, ZC)), Vector((S, 0, ZC)), w=8, mi=DARK, rod=0.6)
    for k in range(-9, 10):
        x = k * S / 10
        node = Vector((x, 0, ZC))
        box(bm, (6, 14, 6), node, mi=HULL)
        for dx in (-40, -15, 15, 40):
            tx = max(-S, min(S, x + dx))
            zt = ZC + B + 55 * math.cos(math.pi / 2 * tx / S) ** 2
            _rod(bm, node + Vector((0, 4 * (1 if dx > 0 else -1), 3)), Vector((tx, 0, zt - 4)), 0.5, DARK)
            _rod(bm, node + Vector((0, 4 * (1 if dx > 0 else -1), -3)), Vector((tx, 0, ZC - B + 4)), 0.5, DARK)
    # cargo sleds riding the track
    for i in list(range(8, 90, 13)) + list(range(126, 210, 17)):
        a, b = loop[i], loop[(i + 1) % len(loop)]
        t = (b - a).normalized(); n = t.cross(Y).normalized()
        R = basis(t, n)
        loft(bm, [(-14, 3, 2.4, 0.4), (-10, 5, 4, 0.4), (10, 5, 4, 0.4), (16, 2, 2, 0.4)], R, a + n * 9, HULL if i % 2 else LIGHT)
        sphere(bm, 1.6, a + n * 9 - t * 15, mi=PLASMA, seg=6)
    obj("Loop", bm, GM)

    # deflector stations: a drum across each end, spokes out to the turning track, modules, radiators, a dock
    bm = bmesh.new()
    for s in (-1, 1):
        c = Vector((s * (S + 20), 0, ZC))
        loft(bm, [(-50, 26, 26, 0.35), (-40, 40, 40, 0.3), (40, 40, 40, 0.3), (50, 26, 26, 0.35)], basis(Y), c, DARK)
        for e in (-1, 1):
            band(bm, c + Y * e * 30, Y, 40, 4, 2, GLOW if e > 0 else ACC, 32)
            module(bm, c + Y * e * 60 + Z * 10, Y * e, Z, 26, 16, 14, mats=(HULL, DARK, WIN), seed=10 + s + e)
        for k in range(5):
            a = (k - 2) * 0.55
            d = Vector((s * math.cos(a), 0, math.sin(a)))
            truss(bm, c + d * 40, Vector((s * S, 0, ZC)) + d * (B - 8), w=5, mi=DARK, rod=0.4)
        radiator_bank(bm, c + Z * 40, Z, Y, 10, 40, 14, 4.4, mi_fin=RAD, mi_man=DARK)
        tank_cage(bm, c - Z * 46 + Y * 16, 10, axis=Y, capsule=1.2, mi_tank=HULL, mi_cage=DARK)
        dish(bm, c + Z * 44 + Y * 30, Vector((s, -0.4, 0.6)), 8, mi_dish=LIGHT, mi_frame=DARK)
        for k in range(6): strobe(bm, Vector((s * (S + B + 6), 0, ZC)) + Vector((0, 0, (k - 2.5) * 30)), 1.4, RED if k % 2 else STROBE)
    # the launch end is the busier one: a cargo yard with a gantry over stacked containers, and a ship waiting
    c = Vector((S + 20, 0, ZC))
    for i in range(4):
        for j in range(3):
            box(bm, (14, 6, 6), c + Vector((-60 - i * 16, 40 + j * 7, -40)), mi=(ACC, HULL, LIGHT, DARK)[(i + j) % 4])
    gantry(bm, c + Vector((-50, 30, -46)), c + Vector((-130, 30, -46)), Z, 30, 18, mi_rail=DARK, mi_crane=HAZ, cranes=2, seed=4)
    dock_arm(bm, c + Vector((0, -40, -30)), Vector((0.3, -1, -0.4)), 40, 50, mats=(HULL, DARK, GLOW, WIN), seed=5)
    dock_arm(bm, Vector((-S - 20, -40, ZC - 20)), Vector((-0.2, -1, -0.3)), 30, 34, mats=(LIGHT, DARK, GLOW, WIN), seed=6)
    obj("Stations", bm, GM)
    lamp("L_A", (S + 20, 0, ZC), 2e5, COOL, 20); lamp("L_B", (-S - 20, 0, ZC), 2e5, COOL, 20)
    return export(col, root, "launchloop")

# ---------------------------------------------------------------- 2. Singularity Decompressor
@works
def decompressor():
    """After the matter-decompressor references: two great drill stations aimed at the black hole from its poles,
    their shafts ringed with blue coils down to emitter tips in the ergosphere, a cage of struts holding an
    equatorial containment ring, and a hot accretion disc ("ring") the game spins."""
    col, root = setup("Decompressor")
    bm = bmesh.new()
    sphere(bm, 30, (0, 0, 0), mi=VOID, seg=32)                  # the horizon
    obj("core", bm, GM)
    bm = bmesh.new()
    for k, (r0, r1, mi) in enumerate(((33, 33.8, STROBE), (40, 45, DISCIN), (46, 52, DISCIN), (53.5, 61, HEAT), (63, 72, DISCOUT), (74, 80, DISCOUT), (83, 92, DISCOUT))):
        ring_at(bm, r0, r1, 1.6 - k * 0.15, 128, (mi,) * 4)
    obj("ring", bm, GM)

    bm = bmesh.new()
    for s in (1, -1):
        sv = Vector((0, 0, s))
        # the drill station: a heavy drum, detailed, with a glowing belt
        loft(bm, [(250, 60, 60, 0.3), (262, 92, 92, 0.25), (300, 92, 92, 0.25), (318, 70, 70, 0.3), (332, 40, 40, 0.4)], basis(sv, X), Vector((0, 0, 0)), DARK)
        for k in range(3): band(bm, sv * (268 + k * 13), sv, 92, 3, 2, GLOW if k == 1 else HULL, 48)
        # the shaft, ringed with coils that glow blue toward the tip
        loft(bm, [(60, 6, 6, 0.4), (120, 16, 16, 0.4), (200, 30, 30, 0.35), (252, 44, 44, 0.3)], basis(sv, X), Vector((0, 0, 0)), DARK)
        for k in range(9):
            z = 230 - k * 20
            r = 6 + (z - 60) / 192 * 34
            band(bm, sv * z, sv, r + 1, 5, 3, HULL, 24)
            band(bm, sv * (z - 4), sv, r + 0.6, 1.4, 1.6, COIL, 24)
        sphere(bm, 5, sv * 56, mi=PLASMA, seg=12)
        for k, (rr, z) in enumerate(((72, 236), (58, 214), (44, 190))):     # the glowing ring frames round the drill head
            n0 = set(bm.verts)
            solid_ring(bm, rr, rr + 5, -3, 3, 48, mats=(COIL, DARK, COIL, DARK))
            bmesh.ops.translate(bm, vec=sv * z, verts=since(bm, n0))
            for j in range(4):
                a = j * math.pi / 2 + k * 0.4
                d = Vector((math.cos(a), math.sin(a), 0))
                _rod(bm, d * (rr + 2) + sv * z, d * 40 + sv * 250, 1.2, DARK)
        cyl(bm, 1.2, 1.2, 22, sv * 44, seg=8, mi=PLASMA)          # the stream drawn up out of the ergosphere
        # modules, radiators, tanks and a dock on the drum's back
        for k in range(4):
            a = k * math.pi / 2 + 0.4
            d = Vector((math.cos(a), math.sin(a), 0))
            module(bm, d * 70 + sv * 345, d, sv, 30, 16, 14, mats=(HULL, DARK, WIN), seed=20 + k + s)
            radiator_bank(bm, d * 92 + sv * 285, d, sv, 9, 40, 16, 5, mi_fin=RAD, mi_man=DARK)
            tank_cage(bm, d.cross(Z) * 80 + d * 30 + sv * 330, 10, axis=sv, capsule=1.0, mi_tank=HULL, mi_cage=DARK)
            strobe(bm, d * 94 + sv * 300, 1.6, RED if k % 2 else STROBE)
        dish(bm, sv * 346 + X * 20, Vector((0.4, 0.2, s)), 10, mi_dish=LIGHT, mi_frame=DARK)
        dock_arm(bm, sv * 320 + Vector((0, -70, 0)), Vector((0, -1, s * 0.3)), 40, 48, mats=(HULL, DARK, GLOW, WIN), seed=30 + s)
    # the containment ring at the equator and the cage of struts holding the stations to it
    c = solid_ring(bm, 150, 166, -8, 8, 128, mats=(DARK, DARK, DARK, HULL))
    detail(bm, cyl_surface(166, -8 + c + 0.1, 8 - c - 0.1), 9, scale=2.4, mats=(DARK, HULL), window=WIN, window_frac=0.06)
    strip_lights(bm, 150 - 0.1, 0, 160, GLOW, seed=3, h=1.2)
    for k in range(8):
        a = k * math.pi / 4
        d = Vector((math.cos(a), math.sin(a), 0))
        fast_prism(bm, octo(26, 30, 6), 26, d * 158 - Z * 13, basis(d.cross(Z), Z), DARK, U(bm), top=(0.85, 0.85, 0, 0), base_cap=True)
        for s in (1, -1):
            truss(bm, d * 158 + Z * s * 13, d * 80 + Z * s * 262, w=6, mi=DARK, rod=0.5)
    obj("Frame", bm, GM)
    lamp("L_Ring", (0, 0, 0), 3e5, FIRE, 40)
    return export(col, root, "decompressor")

# ---------------------------------------------------------------- 3. Fleet Command Lattice
@works
def lattice():
    """An octahedron of six command stations, each a seamed sensor dome over a drum of modules, joined by box
    trusses carrying lit transit tubes; a thinking core at the centre on six spokes; two crossed sensor rings "r0"."""
    col, root = setup("Lattice")
    bm = bmesh.new()
    nodes = [Vector(p) * 160 for p in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))]
    for i, a in enumerate(nodes):
        for b in nodes[i + 1:]:
            if (a - b).length < 300:
                d = (b - a).normalized()
                truss(bm, a + d * 26, b - d * 26, w=9, mi=DARK, rod=0.7)
                side = d.cross(a + b).normalized()
                cyl(bm, 2.2, 2.2, (b - a).length - 56, (a + b) / 2 + side * 8, rot_to(d).to_4x4(), seg=10, mi=HULL)
                for k in range(1, 12):
                    sphere(bm, 0.8, a + (b - a) * k / 12 + side * 10.4, mi=GLOW if k % 3 else WCOOL, seg=6)
    for i, p in enumerate(nodes):
        d = p.normalized()
        R = basis(d, Z if abs(d.z) < 0.9 else X)
        loft(bm, [(-34, 12, 12, 0.4), (-26, 24, 24, 0.35), (10, 24, 24, 0.35), (16, 18, 18, 0.4)], R, p, DARK)
        band(bm, p - d * 22, d, 24, 3, 2, GLOW, 24)
        band(bm, p - d * 6, d, 24, 2, 1.5, ACC, 24)
        seamed_ball(bm, 20, p + d * 24, DARK, WCOOL, HULL, seed=i)
        side = R.col[1]; up = R.col[2]
        for e in (-1, 1):
            module(bm, p - d * 10 + side * e * 32, d, up, 30, 14, 12, mats=(HULL if e > 0 else LIGHT, DARK, WIN), seed=50 + i * 2 + e)
            radiator_bank(bm, p - d * 14 + up * e * 26, up * e, d, 7, 22, 9, 3, mi_fin=RAD, mi_man=DARK)
        dish(bm, p + up * 26 + d * 4, (d + up).normalized(), 6, mi_dish=LIGHT, mi_frame=DARK)
        strobe(bm, p + d * 46, 1.4, RED if i % 2 else STROBE)
        if i in (0, 3):
            dock_arm(bm, p - side * 26 - d * 20, (-side - d * 0.4).normalized(), 30, 40, mats=(HULL, DARK, GLOW, WIN), up=up, seed=60 + i)
        # the spoke back to the core
        loft(bm, [(42, 5, 5, 0.4), (126, 7, 7, 0.4)], basis(d, Z if abs(d.z) < 0.9 else X), Vector((0, 0, 0)), HULL)
    seamed_ball(bm, 40, (0, 0, 0), DARK, SCREEN, HULL, sub=3, seed=9)
    obj("Lattice", bm, GM)
    bm = bmesh.new()
    for k, rot in enumerate((Rx(0.5), Ry(1.2))):
        n0 = set(bm.verts)
        c = solid_ring(bm, 228, 240, -5, 5, 160, mats=(DARK, DARK, DARK, HULL))
        detail(bm, cyl_surface(240, -5 + c + 0.1, 5 - c - 0.1), 70 + k, scale=1.6, mats=(DARK, HULL), window=WIN, window_frac=0.08)
        strip_lights(bm, 228 - 0.1, 0, 200, GLOW, seed=k, h=1.0)
        for j in range(12):
            a = j * math.pi / 6 + k * 0.26
            dd = Vector((math.cos(a), math.sin(a), 0))
            loft(bm, [(-8, 5, 5, 0.4), (-5, 8, 8, 0.4), (5, 8, 8, 0.4), (8, 5, 5, 0.4)], basis(dd.cross(Z), dd), dd * 246, HULL if j % 3 else LIGHT)
            strobe(bm, dd * 255, 1.0, RED if j % 4 == 0 else STROBE)
        bmesh.ops.transform(bm, matrix=rot, verts=since(bm, n0))
    obj("r0", bm, GM)
    lamp("L_C", (0, 0, 0), 1e5, COOL, 30)
    return export(col, root, "lattice")

# ---------------------------------------------------------------- 4. Cognition Spire
@works
def spire():
    """After Marius Andrei's station and needle references: a stepped needle of thinking matter rising 780 m from a
    broad disc whose rim bristles with radial fins, buttress blades up its foot, glowing seams at every step, and a
    tilted broken ring of pods ("r0") turning round it."""
    col, root = setup("Spire")
    PROF = [(1.0, 780), (3, 740), (4, 700), (8, 690), (8, 600), (12, 590), (12, 480), (17, 470), (17, 360), (23, 350),
            (23, 240), (30, 230), (30, 130), (40, 118), (40, 40), (60, 20), (60, 0)]
    bm = bmesh.new()
    revolve(bm, PROF, 48, lambda i, s: HULL if (i + 1 < len(PROF) and PROF[i][0] != PROF[i + 1][0]) else DARK, closed=False, toward=radial)
    for i in range(len(PROF) - 1):
        (r0, z0), (r1, z1) = PROF[i], PROF[i + 1]
        if abs(r0 - r1) < 1e-3 and abs(z1 - z0) > 30:
            detail(bm, cyl_surface(r0, min(z0, z1) + 1, max(z0, z1) - 1), 100 + i, scale=max(3.2, r0 * 0.16), mats=(DARK, HULL), window=SCREEN, glow=WCOOL, window_frac=0.07, glow_frac=0.02)
        if r0 < r1 and r1 < 50:
            band(bm, Vector((0, 0, z0 - 2)), Z, r1, 2.0, 1.2, SCREEN, 48)
    for k in range(6):                                          # buttress blades up the foot
        a = k * math.pi / 3 + 0.26
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        fast_prism(bm, [(40, 0), (120, 0), (40, 230)], 3.0, t * -1.5, Matrix((d, Z, t)).transposed(), DARK, U(bm), base_cap=True)
        for j in range(5): sphere(bm, 1.0, d * (42 + j * 12) + Z * (8 + j * 30) + t * 1.8, mi=SCREEN, seg=6)
    antennas(bm, Vector((0, 0, 780)), Z, 4, 30, 7, mi=DARK, light=RED)
    obj("Spire", bm, GM)

    # the base disc: a broad saucer with radial fins round its rim (Andrei), modules on the deck, ships docked
    bm = bmesh.new()
    revolve(bm, [(60, 0), (150, -6), (160, -16), (150, -30), (90, -46), (40, -60)], 96, lambda i, s: DARK if i != 1 else HULL, closed=False, toward=lambda c: Vector((c.x, c.y, 0)).normalized() * 0.4 + Vector((0, 0, 1 if c.z > -10 else -1)))
    detail(bm, disc_surface(62, 148, -5.5, up=True), 120, scale=4.2, mats=(DARK, HULL), window=WIN, glow=GLOW, window_frac=0.05, glow_frac=0.02)
    uv = U(bm)
    for k in range(48):
        a = k * 2 * math.pi / 48
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        fast_prism(bm, [(150, -12), (186, -24), (182, -30), (150, -26)], 1.6, t * -0.8, Matrix((d, Z, t)).transposed(), DARK if k % 4 else HULL, uv, base_cap=True)
        sphere(bm, 0.9, d * 186 - Z * 25, mi=GLOW if k % 2 else STROBE, seg=6)
    strip_lights(bm, 160.2, -16, 200, GLOW, seed=12, h=1.4)
    for k in range(8):
        a = k * math.pi / 4 + 0.2
        d = Vector((math.cos(a), math.sin(a), 0))
        module(bm, d * 100 - Z * 2, Vector((-d.y, d.x, 0)), Z, 34, 16, 12, mats=(HULL if k % 2 else LIGHT, DARK, WIN), seed=130 + k)
        if k % 2 == 0: radiator_bank(bm, d * 72 - Z * 50, -Z, Vector((-d.y, d.x, 0)), 8, 30, 12, 4, mi_fin=RAD, mi_man=DARK)
    for k, a in enumerate((0.6, 2.7, 4.4)):
        d = Vector((math.cos(a), math.sin(a), 0))
        dock_arm(bm, d * 150 - Z * 30, (d - Z * 0.3).normalized(), 30, 40, mats=(HULL, DARK, GLOW, WIN), seed=140 + k)
    # two lesser saucer decks up the needle, finned and lit like the base
    for (zc, r, nf) in ((240, 80, 32), (480, 52, 24)):
        n0 = set(bm.verts)
        revolve(bm, [(r * 0.38, 6), (r, 2), (r * 1.06, -4), (r, -10), (r * 0.45, -18)], 64, lambda i, s: DARK if i != 2 else HULL, closed=False,
                toward=lambda c, zc=zc: Vector((0, 0, 1 if c.z > zc - 4 else -1)))
        bmesh.ops.translate(bm, vec=Vector((0, 0, zc)), verts=since(bm, n0))
        strip_lights(bm, r * 1.06 + 0.1, zc - 4, 120, GLOW, seed=zc, h=0.9)
        for k in range(nf):
            a = k * 2 * math.pi / nf
            d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
            fast_prism(bm, [(r, zc - 2), (r * 1.25, zc - 10), (r * 1.23, zc - 14), (r, zc - 12)], 1.2, t * -0.6, Matrix((d, Z, t)).transposed(), DARK if k % 3 else HULL, uv, base_cap=True)
        for k in range(4):
            a = k * math.pi / 2 + zc * 0.01
            d = Vector((math.cos(a), math.sin(a), 0))
            module(bm, d * r * 0.7 + Z * (zc + 8), Vector((-d.y, d.x, 0)), Z, r * 0.3, 8, 7, mats=(HULL, DARK, WIN), seed=zc + k)
    obj("Base", bm, GM)

    # r0: a tilted ring broken into arcs, with heavy pods at the breaks
    bm = bmesh.new()
    for k in range(3):
        a0 = k * 2 * math.pi / 3 + 0.12; a1 = a0 + 2 * math.pi / 3 - 0.24
        c = solid_ring(bm, 176, 192, -5, 5, 40, mats=(DARK, DARK, DARK, HULL), a0=a0, a1=a1)
        detail(bm, cyl_surface(192, -5 + c + 0.1, 5 - c - 0.1, a0=a0, a1=a1), 150 + k, scale=1.6, mats=(DARK, HULL), window=WIN, window_frac=0.08)
        for a in (a0, a1):
            d = Vector((math.cos(a), math.sin(a), 0))
            loft(bm, [(-10, 8, 8, 0.4), (-6, 13, 13, 0.35), (6, 13, 13, 0.35), (10, 8, 8, 0.4)], basis(Vector((-d.y, d.x, 0))), d * 184, HULL)
            strobe(bm, d * 184 + Z * 14, 1.2, RED)
        am = (a0 + a1) / 2
        dm = Vector((math.cos(am), math.sin(am), 0))
        truss(bm, dm * 176, dm * 34, w=4, mi=DARK, rod=0.35)
    solid_ring(bm, 205, 207, -1, 1, 160, mats=(GLOW,) * 4, bevel=0.0)
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0, 0, 300)) @ Rx(0.3), verts=bm.verts[:])
    # a second, smaller ring higher up at the opposite tilt
    n0 = set(bm.verts)
    c = solid_ring(bm, 112, 122, -4, 4, 96, mats=(DARK, DARK, DARK, HULL))
    detail(bm, cyl_surface(122, -4 + c + 0.1, 4 - c - 0.1), 160, scale=1.4, mats=(DARK, HULL), window=WIN, window_frac=0.1)
    strip_lights(bm, 112 - 0.1, 0, 120, GLOW, seed=16, h=0.8)
    for k in range(6):
        a = k * math.pi / 3
        d = Vector((math.cos(a), math.sin(a), 0))
        loft(bm, [(-7, 5, 5, 0.4), (-4, 8, 8, 0.4), (4, 8, 8, 0.4), (7, 5, 5, 0.4)], basis(Vector((-d.y, d.x, 0))), d * 128, HULL if k % 2 else LIGHT)
        if k % 2 == 0: truss(bm, d * 112, d * 16, w=3, mi=DARK, rod=0.3)
    bmesh.ops.transform(bm, matrix=Matrix.Translation((0, 0, 560)) @ Rx(-0.4), verts=since(bm, n0))
    obj("r0", bm, GM)
    lamp("L_Top", (0, 0, 760), 5e4, COOL, 10)
    return export(col, root, "spire")

# ---------------------------------------------------------------- 5. Ringworld
@works
def ringworld():
    """After the Halo installation art: the floor a painted band of sea, land and cloud facing inward; outside, a
    hull of huge repeating plates with recessed machinery sockets and a spine ridge; rim walls with lit crests and
    spaceports; an inner ring of framed shadow squares on tethers ("r0"). "ring" carries the band, the game spins it."""
    col, root = setup("Ringworld")
    bm = bmesh.new()
    R, W = 1000.0, 90.0
    steps = 256
    rnd = random.Random(7)
    LMI = {0: len(GM), 1: len(GM) + 1, 2: len(GM) + 2}       # Ocean, Plant, Rock, appended to this object's list
    FS, FR = 720, 10                                      # the floor is finer than the hull, so coasts read as coasts
    G = [[bm.verts.new((R * math.cos(i * 2 * math.pi / FS), R * math.sin(i * 2 * math.pi / FS), -W / 2 + j * W / FR)) for j in range(FR + 1)] for i in range(FS)]
    for i in range(FS):
        a0, a1 = i * 2 * math.pi / FS, (i + 1) * 2 * math.pi / FS
        for j in range(FR):
            f = bm.faces.new((G[i][j], G[i][j + 1], G[(i + 1) % FS][j + 1], G[(i + 1) % FS][j]))
            u, v = i * 256 / FS, j * 6 / FR
            land = math.sin(u * 0.21 + v * 0.9) + 0.6 * math.sin(u * 0.071 + v * 2.1) + 0.35 * math.sin(u * 0.6 + v * 3.7) + 0.08 * rnd.random()
            f.material_index = LMI[0] if land < 0.1 else (LMI[1] if land < 0.9 else LMI[2])
        if i % 3 == 0 and rnd.random() < 0.35:            # drifting cloud banks a little above the floor
            zc = rnd.uniform(-W / 2 + 10, W / 2 - 10); L = rnd.uniform(0.6, 1.0)
            vs = [bm.verts.new(((R - 6) * math.cos(a), (R - 6) * math.sin(a), z)) for a, z in
                  ((a0, zc - 8 * L), (a0, zc + 8 * L), (a1 + 0.01, zc + 6 * L), (a1 + 0.01, zc - 6 * L))]
            bm.faces.new(vs).material_index = len(GM) + 3
    bm.normal_update()
    for f in bm.faces:
        c = f.calc_center_median()
        if f.normal.dot(-Vector((c.x, c.y, 0))) < 0: f.normal_flip()      # the floor and clouds face the star
    # the hull: a shell under the floor, then great plates with gaps, sockets in some, and a ridge down the middle
    c = solid_ring(bm, R + 2, R + 8, -W / 2 - 10, W / 2 + 10, steps, mats=(DARK, DARK, DARK, DARK))     # clear of the floor (z-fighting)
    uv = U(bm)
    for i in range(128):
        a = (i + 0.5) * 2 * math.pi / 128
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        R3 = Matrix((t, Z, d)).transposed()
        L = 2 * math.pi * (R + 8) / 128
        for e in (-1, 1):
            fast_box(bm, (L * 0.86, W / 2 - 6, 2.5), d * (R + 8) + Z * e * (W / 4 + 4), R3, HULL if (i // 4) % 3 else DARK, uv, bottom=True)
        fast_box(bm, (L * 0.95, 8, 5), d * (R + 8), R3, DARK, uv, bottom=True)
        if i % 8 == 3:
            fast_box(bm, (L * 0.5, 22, 1.0), d * (R + 10.6) + Z * (W / 4 + 4), R3, DARK, uv, bottom=True)
            fast_box(bm, (L * 0.4, 2, 0.4), d * (R + 11.6) + Z * (W / 4 + 4), R3, GLOW, uv, bottom=True)
            cyl(bm, 7, 7, 1.2, d * (R + 11.2) - Z * (W / 4 + 4), (Matrix((t, Z.cross(d), d)).transposed()).to_4x4(), seg=16, mi=GLOW)
        if i % 4 == 0: strobe(bm, d * (R + 13.5), 1.8, RED if i % 16 == 0 else STROBE)
    # rim walls: dark, crested with lights, a spaceport every so often
    for s in (-1, 1):
        n0 = set(bm.verts)
        solid_ring(bm, R - 40, R + 8, -3, 3, steps, mats=(DARK, HULL, DARK, DARK))
        bmesh.ops.translate(bm, vec=Z * s * (W / 2 + 7), verts=since(bm, n0))
    for k in range(8):
        a = k * math.pi / 4 + 0.2
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        for s in (-1, 1):
            module(bm, d * (R - 20) + Z * s * (W / 2 + 18), t, Z * s, 40, 18, 14, mats=(HULL, DARK, WIN), seed=60 + k * 2 + s)
        dock_arm(bm, d * (R - 30) + Z * (W / 2 + 12), (Z - d * 0.4).normalized(), 30, 40, mats=(HULL, DARK, GLOW, WIN), up=d, seed=70 + k)
    obj("ring", bm, GM + ["Ocean", "Plant", "Rock", "Ice"])
    # rim crest lights as their own pass (strip_lights faces outward; turn them to face the floor)
    bm = bmesh.new()
    for s in (-1, 1):
        rs = random.Random(40 + s)
        for k in range(360):
            if rs.random() < 0.3: continue
            a = k * 2 * math.pi / 360
            d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
            fast_box(bm, (2 * math.pi * (R - 40) / 360 * 0.7, 2.0, 0.6), d * (R - 40.3) + Z * s * (W / 2 + 7), Matrix((t, Z, -d)).transposed(), GLOW if k % 7 else WCOOL, U(bm))
    obj("ring_lights", bm, GM)
    bm = bmesh.new()
    # shadow squares: framed panels on an inner ring, braced by tethers
    for k in range(20):
        a = k * 2 * math.pi / 20
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        R3 = Matrix((t, Z, d)).transposed()
        fast_box(bm, (62, 74, 1.2), d * 420, R3, SOLAR, U(bm), bottom=True)
        for e in (-1, 1):
            fast_box(bm, (64, 2.4, 2.4), d * 420 + Z * e * 37, R3, DARK, U(bm), bottom=True)
            fast_box(bm, (2.4, 76, 2.4), d * 420 + t * e * 31, R3, DARK, U(bm), bottom=True)
            _rod(bm, d * 420 + t * e * 31 + Z * 37, d * 420 + t * e * 31 + Z * 70, 0.4, DARK)
        strobe(bm, d * 421 + Z * 38, 1.2, RED)
    ring_at(bm, 418, 422, 2, 128, (DARK,) * 4)
    for e in (-1, 1): ring_at(bm, 419, 421, 1, 128, (DARK,) * 4, (0, 0, e * 70))
    obj("r0", bm, GM)
    return export(col, root, "ringworld")

# ---------------------------------------------------------------- 6. Umbral Fortress
UMB = RUNE = len(GM)               # the one extra material these two works append to GM
def spike(bm, base, d, L, r, mi, glow=None, seed=0):
    """A jagged dark spire: an octagon tapering in uneven steps to a point, with a glowing vein up one face."""
    rs = random.Random(seed)
    secs = [(0, r, r * 0.9, 0.3)]
    for k in range(1, 5):
        f = 1 - k / 5
        secs.append((L * k / 5 - L * 0.04, r * (f + 0.08) * rs.uniform(0.9, 1.1), r * 0.9 * (f + 0.08), 0.3))
        secs.append((L * k / 5, r * f * rs.uniform(0.85, 1.0), r * 0.9 * f, 0.3))
    secs.append((L, r * 0.02, r * 0.02, 0.3))
    R = basis(d)
    loft(bm, secs, R, base, mi)
    if glow is not None:
        for k in range(4):
            x = L * (0.1 + k * 0.2)
            fast_box(bm, (L * 0.12, r * 0.08, r * 0.06), base + R @ Vector((x, 0, r * 0.9 * (1 - x / L) + 0.02)), R, glow, U(bm), bottom=True)

@works
def umbralfort():
    """After dark-citadel mood art: an inverted cathedral of jagged spires, up and down, growing from a faceted
    keep; a crown of blades round its waist, veins of umbral light, hangar mouths glowing violet, and a halo of
    floating shards on a thin violet ring ("halo", built flat: the game tilts it)."""
    col, root = setup("UmbralFort")
    bm = bmesh.new()
    uv = U(bm)
    fast_prism(bm, octo(140, 140, 40), 60, Vector((0, 0, -30)), Matrix.Identity(3), DARK, uv, top=(0.75, 0.75, 0, 0), base_cap=True)
    fast_prism(bm, octo(140, 140, 40), 40, Vector((0, 0, -30)), Matrix(((1, 0, 0), (0, 1, 0), (0, 0, -1))), DARK, uv, top=(0.6, 0.6, 0, 0), base_cap=True)
    detail(bm, cyl_surface(66, -26, 26), 61, scale=4.0, mats=(DARK, HULL), window=UMB, window_frac=0.06)
    rs = random.Random(3)
    spike(bm, Vector((0, 0, 30)), Z, 260, 34, DARK, glow=UMB, seed=1)
    spike(bm, Vector((0, 0, -60)), -Z, 200, 30, DARK, glow=UMB, seed=2)
    for k in range(10):
        a = k * 2 * math.pi / 10 + rs.uniform(-0.15, 0.15)
        r = rs.uniform(30, 55)
        for s in (1, -1):
            if rs.random() < 0.25: continue
            base = Vector((math.cos(a) * r, math.sin(a) * r, s * (25 if s > 0 else 55)))
            d = (Z * s + Vector((math.cos(a), math.sin(a), 0)) * rs.uniform(0.15, 0.45)).normalized()
            spike(bm, base, d, rs.uniform(70, 170) * (1 if s > 0 else 0.8), rs.uniform(9, 16), DARK, glow=UMB if rs.random() < 0.6 else None, seed=10 + k * 2 + s)
    # the crown: blades raking out round the waist, with hangar mouths between them
    for k in range(16):
        a = k * math.pi / 8
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        fast_prism(bm, [(60, -20), (150 + (k % 2) * 40, 8 + (k % 3) * 10), (140, 14), (60, 12)], 4, t * -2, Matrix((d, Z, t)).transposed(), DARK if k % 4 else HULL, uv, base_cap=True)
        if k % 2:
            fast_box(bm, (14, 10, 1), d * 70.2 + Z * -4, Matrix((t, Z, d)).transposed(), UMB, uv, bottom=True)
            strobe(bm, d * 72 + Z * 12, 1.2, UMB)
    for k, a in enumerate((0.4, 2.5, 4.6)):
        d = Vector((math.cos(a), math.sin(a), 0))
        dock_arm(bm, d * 66 - Z * 20, (d - Z * 0.5).normalized(), 30, 40, mats=(DARK, DARK, UMB, UMB), seed=80 + k)
    obj("Fort", bm, GM + ["Umbra"])
    lamp("L_U", (0, 0, 0), 1e5, (0.6, 0.3, 1.0), 40)
    bm = bmesh.new()
    ring_at(bm, 214, 216, 1.5, 128, (UMB,) * 4)
    for k in range(28):
        a = k * 2 * math.pi / 28 + rs.uniform(-0.05, 0.05)
        d = Vector((math.cos(a), math.sin(a), 0))
        dd = (d * rs.uniform(-0.3, 0.3) + Z * rs.choice((-1, 1)) + Vector((-d.y, d.x, 0)) * rs.uniform(-0.4, 0.4)).normalized()
        L = rs.uniform(14, 34)
        spike(bm, d * 215 - dd * L * 0.4, dd, L, rs.uniform(3, 6), DARK, glow=UMB if k % 3 == 0 else None, seed=200 + k)
    obj("halo", bm, GM + ["Umbra"])
    return export(col, root, "umbralfort")

# ---------------------------------------------------------------- 7. Gate Bastion
@works
def gatebastion():
    """A fortified jump gate after the jump-gate references: an armoured ring of bevelled segments joined by
    clamp blocks, six bastion towers with twin-gun turrets and shield emitters, hangars and dock arms, and a
    rune-lit inner collar ("disc") the game turns. Ring axis Y."""
    col, root = setup("GateBastion")
    bm = bmesh.new()
    uv = U(bm)
    for k in range(24):
        a0 = k * 2 * math.pi / 24
        solid_ring(bm, 222, 262, -22, 22, 6, mats=(DARK, DARK, HULL, DARK), a0=a0 + 0.008, a1=a0 + 2 * math.pi / 24 - 0.008, bevel=4)
        dj = Vector((math.cos(a0), math.sin(a0), 0)); tj = Vector((-dj.y, dj.x, 0))
        if k % 4:
            fast_prism(bm, octo(14, 50, 4), 52, dj * 242 - Z * 26, Matrix((tj, dj, Z)).transposed(), HULL if k % 2 else DARK, uv, top=(0.85, 0.9, 0, 0), base_cap=True)
        fast_box(bm, (2 * math.pi * 262 / 24 * 0.7, 1.4, 0.4), Vector((math.cos(a0 + 0.13), math.sin(a0 + 0.13), 0)) * 262.1, Matrix((Vector((-math.sin(a0 + 0.13), math.cos(a0 + 0.13), 0)), Z, Vector((math.cos(a0 + 0.13), math.sin(a0 + 0.13), 0)))).transposed(), GLOW, uv, bottom=True)
    detail(bm, disc_surface(226, 258, 22, up=True), 72, scale=3.0, mats=(DARK, HULL), window=WIN, window_frac=0.05)
    detail(bm, disc_surface(226, 258, -22, up=False), 73, scale=3.0, mats=(DARK, HULL), window=WIN, window_frac=0.05)
    solid_ring(bm, 219, 222, -6, 6, 128, mats=(GLOW,) * 4, bevel=0.0)                  # the inner rim burns
    for k in range(6):                                          # bastion towers round the gate
        a = k * math.pi / 3 + math.pi / 6
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        p = d * 262
        loft(bm, [(0, 34, 30, 0.3), (24, 40, 36, 0.3), (60, 26, 26, 0.35), (80, 14, 14, 0.4)], basis(d), p, DARK)
        for j in (-1, 1):                                       # a twin-gun turret either side
            q = p + d * 40 + Z * j * 32
            fast_prism(bm, octo(18, 18, 5), 8, q, basis(t, Z * j), HULL, uv, top=(0.8, 0.8, 0, 0), base_cap=True)
            for g in (-1, 1): cyl(bm, 1.6, 1.4, 40, q + Z * j * 6 + t * g * 4 + d * 20, basis(d).to_4x4() @ Ry(math.pi / 2), seg=8, mi=DARK)
        cyl(bm, 7, 9, 10, p + d * 84, basis(d).to_4x4() @ Ry(math.pi / 2), seg=16, mi=HULL)
        sphere(bm, 5, p + d * 92, mi=PLASMA, seg=12)                                  # the shield emitter
        radiator_bank(bm, p + d * 20 + t * 34, t, d, 6, 20, 10, 4, mi_fin=RAD, mi_man=DARK)
        strobe(bm, p + d * 70 + t * 16, 1.4, RED if k % 2 else STROBE)
    for k, a in enumerate((0.0, math.pi)):
        d = Vector((math.cos(a), math.sin(a), 0))
        dock_arm(bm, d * 262 + Z * 16, (d + Z * 0.6).normalized(), 40, 56, mats=(HULL, DARK, GLOW, WIN), seed=90 + k)
        bay = hangar(bm, d * 262 - Z * 4, d, (0, 0, 1), 22.0, 10.0, 26.0, mats=(DARK, HULL, WCOOL, STROBE, GLOW), seed=95 + k)
    bmesh.ops.transform(bm, matrix=Rx(math.pi / 2), verts=bm.verts[:])
    obj("Bastion", bm, GM)
    bm = bmesh.new()
    uv = U(bm)
    solid_ring(bm, 200, 214, -4, 4, 128, mats=(DARK, DARK, DARK, DARK))
    for k in range(48):                                         # runes: lit glyph blocks round the collar's face
        a = k * 2 * math.pi / 48
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        for e in (-1, 1):
            w = (4, 9, 6, 2)[k % 4]
            fast_box(bm, (w, 5 + (k % 3) * 2, 0.6), d * 207 + Z * e * 4, Matrix((t, d, Z * e)).transposed(), RUNE if k % 5 else GLOW, uv, bottom=True)
    bmesh.ops.transform(bm, matrix=Rx(math.pi / 2), verts=bm.verts[:])
    obj("disc", bm, GM + ["Rune"])
    return export(col, root, "gatebastion")

# ---------------------------------------------------------------- 8. System Aegis (an emitter pylon; the game strings six round the system)
@works
def aegis():
    """After shield-emitter references: a dark angular platform whose prong cradle holds a focus burning blue,
    capacitor drums round its waist, radiator wings, a keel of modules beneath, solar wings and a dock."""
    col, root = setup("Aegis")
    bm = bmesh.new()
    uv = U(bm)
    fast_prism(bm, octo(90, 90, 26), 24, Vector((0, 0, -12)), Matrix.Identity(3), DARK, uv, top=(0.8, 0.8, 0, 0), base_cap=True)
    fast_prism(bm, octo(90, 90, 26), 30, Vector((0, 0, -12)), Matrix(((1, 0, 0), (0, 1, 0), (0, 0, -1))), DARK, uv, top=(0.45, 0.45, 0, 0), base_cap=True)
    detail(bm, disc_surface(14, 40, 12.1, up=True), 81, scale=1.8, mats=(DARK, HULL), window=WIN, glow=GLOW, window_frac=0.05, glow_frac=0.02)
    loft(bm, [(-120, 3, 3, 0.4), (-90, 10, 10, 0.4), (-42, 14, 14, 0.4)], UPX, Vector((0, 0, 0)), HULL)       # the keel
    for k in range(4):                                          # the prong cradle, curving up round the focus
        a = k * math.pi / 2 + math.pi / 4
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        fast_prism(bm, [(18, 6), (27, 6), (36, 40), (25, 88), (20, 90), (25, 42)], 3.5, t * -1.75, Matrix((d, Z, t)).transposed(), DARK, uv, base_cap=True)
        fast_prism(bm, [(27, 6), (28.5, 6), (37.5, 40), (36, 40)], 4.0, t * -2.0, Matrix((d, Z, t)).transposed(), HULL, uv, base_cap=True)
        fast_prism(bm, [(17, 8), (18.6, 8), (24.6, 42), (21.6, 86), (20, 86), (23, 42)], 4.4, t * -2.2, Matrix((d, Z, t)).transposed(), COIL, uv, base_cap=True)   # the inner edge burns blue
        tank_cage(bm, d * 54 + Z * 2, 9, axis=Z, capsule=0.8, mi_tank=DARK, mi_cage=HULL)               # capacitor drums
        if k < 2: radiator_bank(bm, t * 46 - Z * 6, t, Z, 6, 24, 10, 4, mi_fin=RAD, mi_man=DARK)
        module(bm, d * 22 - Z * 70, t, d, 26, 10, 9, mats=(HULL if k % 2 else LIGHT, DARK, WIN), seed=82 + k)
        strobe(bm, d * 64 + Z * 12, 1.2, RED if k % 2 else STROBE)
    for e in (-1, 1):
        solar_wing(bm, Vector((e * 12, 0, -96)), Vector((e, 0, 0)), Z, 70, 16, panels=5, mi_cell=SOLAR, mi_frame=DARK)
    cyl(bm, 12, 16, 10, Vector((0, 0, 17)), seg=24, mi=HULL)
    for k in range(3): band(bm, Vector((0, 0, 26 + k * 10)), Z, 10 - k * 2, 2, 1.5, PLASMA if k % 2 else DARK, 24)
    sphere(bm, 9, Vector((0, 0, 66)), mi=PLASMA, seg=16)       # the focus
    for k, (r, z) in enumerate(((16, 58), (13, 74))):
        n0 = set(bm.verts)
        solid_ring(bm, r, r + 1.6, -0.8, 0.8, 32, mats=(COIL,) * 4, bevel=0.0)
        bmesh.ops.translate(bm, vec=Vector((0, 0, z)), verts=since(bm, n0))
    dock_arm(bm, Vector((0, -40, -40)), Vector((0, -1, -0.4)), 30, 36, mats=(HULL, DARK, GLOW, WIN), seed=88)
    obj("Pylon", bm, GM)
    return export(col, root, "aegis")

# ---------------------------------------------------------------- geodesic shells (Birch, Penrose)
def geodesic(bm, r, sub, fill, seed, strut=2.0, node=3.0, mi_strut=DARK, mi_node=HULL, mi_panels=(HULL, DARK), mi_lit=WIN, lit=0.12):
    """A geodesic frame of radius r after the Dyson-lattice references: box struts on every edge, hub nodes at the
    vertices, and a share `fill` of the cells closed with recessed panels (some lit); the rest stay open."""
    tmp = bmesh.new()
    bmesh.ops.create_icosphere(tmp, subdivisions=sub, radius=r)
    rs = random.Random(seed)
    uv = U(bm)
    for e in tmp.edges:
        a, b = e.verts[0].co.copy(), e.verts[1].co.copy()
        t = (b - a); L = t.length; t.normalize()
        n = ((a + b) / 2).normalized(); side = n.cross(t).normalized()
        fast_box(bm, (L, strut, strut * 1.4), (a + b) / 2, Matrix((t, side, n)).transposed(), mi_strut, uv, bottom=True)
    for v in tmp.verts:
        n = v.co.normalized()
        fast_prism(bm, octo(node * 2, node * 2, node * 0.6), node * 1.2, v.co - n * node * 0.4, basis(n.cross(Z if abs(n.z) < 0.9 else X), n), mi_node, uv, top=(0.7, 0.7, 0, 0), base_cap=True)
    for f in tmp.faces:
        if rs.random() > fill: continue
        c = f.calc_center_median()
        vs = [bm.verts.new(c + (v.co - c) * 0.86 - c.normalized() * strut * 0.3) for v in f.verts]
        nf = bm.faces.new(vs)
        nf.material_index = mi_lit if rs.random() < lit else mi_panels[rs.random() < 0.35]
        nf.normal_update()
        if nf.normal.dot(c) < 0: nf.normal_flip()
    tmp.free()

# ---------------------------------------------------------------- 9. Birch Shellworld
@works
def birch():
    """Nested geodesic shells round a black hole, each a lattice of struts and hub nodes part-closed with habitat
    plates (some lit), spinning independently ("r0".."r2"); the hole ("core") wears a thin bright accretion disc."""
    col, root = setup("Birch")
    bm = bmesh.new()
    sphere(bm, 40, (0, 0, 0), mi=VOID, seg=32)
    for k, (r0, r1, mi) in enumerate(((44, 47, STROBE), (50, 58, DISCIN), (60, 72, DISCOUT))):
        ring_at(bm, r0, r1, 0.8, 96, (mi,) * 4)
    obj("core", bm, GM)
    for i, (r, sub, fill) in enumerate(((110, 2, 0.5), (170, 2, 0.45), (230, 3, 0.4))):
        bm = bmesh.new()
        geodesic(bm, r, sub, fill, 300 + i, strut=1.4 + i * 0.5, node=2.2 + i * 0.8, mi_panels=(DARK, DARK), mi_lit=GLOW, lit=0.14)
        for k in range(3 + i):                                  # tenders working each shell, strobes on the hubs
            a = k * 2 * math.pi / (3 + i) + i
            d = Vector((math.cos(a), math.sin(a), 0.3 * (k % 2 - 0.5))).normalized()
            module(bm, d * (r + 8), Vector((-d.y, d.x, 0)).normalized(), d, 18 + i * 6, 8, 7, mats=(HULL, DARK, WIN), seed=310 + i * 10 + k)
            strobe(bm, d * (r + 14), 1.0 + i * 0.3, RED if k % 2 else STROBE)
        obj(f"r{i}", bm, GM)
    lamp("L_Core", (0, 0, 0), 2e5, (1.0, 0.75, 0.45), 30)
    return export(col, root, "birch")

# ---------------------------------------------------------------- 10. Penrose Sphere
@works
def penrose():
    """A closed geodesic sphere round a spinning black hole: dark collector panels on a strut lattice, a few cells
    open on the furnace inside, collars round the polar jets, an equatorial belt of radiators and docks, and the
    hot accretion disc ("ring") the game spins inside."""
    col, root = setup("Penrose")
    bm = bmesh.new()
    sphere(bm, 50, (0, 0, 0), mi=VOID, seg=32)
    obj("core", bm, GM)
    bm = bmesh.new()
    geodesic(bm, 200, 3, 0.9, 400, strut=3.0, node=4.5, mi_panels=(DARK, DARK), mi_lit=GLOW, lit=0.05)
    for s in (1, -1):                                           # the polar jets' collars
        for k, (rin, rout, z) in enumerate(((36, 64, 196), (30, 52, 214), (24, 40, 230))):
            ring_at(bm, rin, rout, 8, 48, (DARK, DARK, HULL, DARK) if k != 1 else (COIL,) * 4, (0, 0, s * z))
        cyl(bm, 22, 14, 40, Vector((0, 0, s * 214)), seg=24, mi=DARK)
    c = solid_ring(bm, 202, 222, -10, 10, 160, mats=(DARK, DARK, DARK, HULL))
    detail(bm, cyl_surface(222, -10 + c + 0.1, 10 - c - 0.1), 401, scale=2.4, mats=(DARK, HULL), window=WIN, window_frac=0.08)
    strip_lights(bm, 222.1, 0, 220, GLOW, seed=41, h=1.0)
    for k in range(12):
        a = k * math.pi / 6
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        if k % 3 == 0:
            dock_arm(bm, d * 222, d, 34, 44, mats=(HULL, DARK, GLOW, WIN), seed=410 + k)
        else:
            for e in (-1, 1): radiator_bank(bm, d * 222 + Z * e * 10, Z * e, t, 7, 30, 12, 4, mi_fin=RAD, mi_man=DARK)
        strobe(bm, d * 224 + Z * 11, 1.6, RED if k % 2 else STROBE)
    obj("Sphere", bm, GM)
    bm = bmesh.new()
    for k, (r0, r1, mi) in enumerate(((54, 56, STROBE), (62, 72, DISCIN), (74, 86, HEAT), (88, 104, DISCOUT), (106, 120, DISCOUT))):
        ring_at(bm, r0, r1, 1.6, 96, (mi,) * 4)
    obj("ring", bm, GM)
    lamp("L_In", (0, 0, 0), 3e5, FIRE, 40)
    return export(col, root, "penrose")

# ---------------------------------------------------------------- 11. Alderson Disk
@works
def alderson():
    """After Neil Blevins' Alderson disk: a scorched inner zone round the star, a narrow habitable band of sea and
    land, and a broad icy outer plain; a dark ribbed underside, a glowing inner rim wall facing the star and a lit
    outer wall. The whole is "disc", which the game turns."""
    col, root = setup("Alderson")
    bm = bmesh.new()
    rin, rout, steps, rings = 300.0, 1000.0, 360, 44
    rnd = random.Random(11)
    OC, PL, RK, IC, SC = len(GM), len(GM) + 1, len(GM) + 2, len(GM) + 3, len(GM) + 4
    G = [[bm.verts.new(((rin + (rout - rin) * j / rings) * math.cos(i * 2 * math.pi / steps), (rin + (rout - rin) * j / rings) * math.sin(i * 2 * math.pi / steps), 0))
          for i in range(steps)] for j in range(rings + 1)]          # shared vertices: no hairline cracks between cells
    for j in range(rings):
        for i in range(steps):
            i1 = (i + 1) % steps
            f = bm.faces.new((G[j][i], G[j + 1][i], G[j + 1][i1], G[j][i1]))
            u = i * 288 / steps; w = j * 28 / rings
            fr = (j + 0.5) / rings + 0.04 * math.sin(u * 0.11) + 0.02 * math.sin(u * 0.37 + w)
            v = math.sin(u * 0.09 + w * 0.5) + 0.5 * math.sin(u * 0.23 - w * 0.9) + 0.25 * math.sin(u * 0.05 + w * 1.7)
            if fr < 0.15: mi = SC if v < 0.7 else RK                    # scorched
            elif fr < 0.22: mi = RK if v < 0.3 else PL
            elif fr < 0.34: mi = OC if v < 0.0 else PL                  # the habitable band
            elif fr < 0.42: mi = IC if v < 0.4 else PL
            else: mi = IC if (v > -1.1 or fr > 0.6) else RK             # the ice plains
            f.material_index = mi
    bm.normal_update()
    for f in bm.faces:
        if f.normal.z < 0: f.normal_flip()
    # the underside and its radial ribs
    ring_at(bm, rin, rout, 12, steps, (DARK,) * 4, (0, 0, -12))        # well clear of the floor: 1 m apart z-fights at this size
    uv = U(bm)
    for k in range(72):
        a = k * 2 * math.pi / 72
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        fast_box(bm, (rout - rin, 6, 6), d * (rin + rout) / 2 - Z * 19, Matrix((d, t, -Z)).transposed(), HULL if k % 6 else DARK, uv, bottom=True)
    # rim walls: the inner one glows where it faces the star, the outer one is lit along its crest
    ring_at(bm, rout, rout + 10, 60, steps, (DARK, DARK, DARK, DARK), (0, 0, 20))
    ring_at(bm, rin - 10, rin, 60, steps, (DARK, DARK, DARK, DARK), (0, 0, 20))
    ring_at(bm, rin - 10.6, rin - 10, 8, steps, (GLOW,) * 4, (0, 0, 44))
    strip_lights(bm, rout + 10.1, 44, 400, GLOW, seed=12, h=2.0, depth=0.6)
    obj("disc", bm, GM + ["Ocean", "Plant", "Rock", "Ice", "Scorch"])
    return export(col, root, "alderson")

# ---------------------------------------------------------------- 12. Glyph Sanctum
@works
def sanctum():
    """After floating-monolith references: a stepped keep of dark stone, every tier banded with glowing glyphs, a
    ring of standing monoliths on its crown, and three rune rings ("r0".."r2") each carrying carved monoliths whose
    glyphs burn, turning about the keep."""
    col, root = setup("Sanctum")
    ST, RU = len(GM), len(GM) + 1
    bm = bmesh.new()
    uv = U(bm)
    rs = random.Random(12)
    tiers = ((120, 40, -60), (92, 50, -20), (66, 70, 30), (40, 60, 100))
    for k, (w, h, z) in enumerate(tiers):
        fast_prism(bm, octo(w, w, w * 0.3), h, Vector((0, 0, z)), Matrix.Identity(3), ST, uv, top=(0.88, 0.88, 0, 0), base_cap=True)
        for j in range(8):                                      # a glyph band round each tier
            a = j * math.pi / 4 + math.pi / 8
            d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
            apo = w / 2 * math.cos(math.pi / 8) * 0.98
            for g in range(5):
                gw, gh = rs.uniform(2, 6), rs.uniform(4, 10)
                fast_box(bm, (gw, gh, 0.6), d * (apo + 0.2) + t * (g - 2) * w * 0.07 + Z * (z + h * 0.5), Matrix((t, Z, d)).transposed(), RU if rs.random() < 0.8 else GLOW, uv, bottom=True)
    fast_prism(bm, octo(40, 40, 12), 60, Vector((0, 0, -60)), Matrix(((1, 0, 0), (0, 1, 0), (0, 0, -1))), ST, uv, top=(0.2, 0.2, 0, 0), base_cap=True)
    for k in range(8):                                          # standing stones on the crown
        a = k * math.pi / 4
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        fast_prism(bm, octo(6, 4, 1), rs.uniform(26, 44), d * 15 + Z * 160, Matrix((t, d, Z)).transposed(), ST, uv, top=(0.8, 0.7, 0, 0), base_cap=True)
    sphere(bm, 5, Vector((0, 0, 200)), mi=RU, seg=12)
    for k, a in enumerate((0.3, 3.4)):
        d = Vector((math.cos(a), math.sin(a), 0))
        dock_arm(bm, d * 58 - Z * 40, (d - Z * 0.4).normalized(), 30, 36, mats=(HULL, DARK, RU, WIN), seed=120 + k)
    obj("Keep", bm, GM + ["Stone", "Rune"])
    for i, r in enumerate((160, 190, 220)):
        bm = bmesh.new()
        uv = U(bm)
        ring_at(bm, r - 3, r + 3, 2.5, 128, (DARK, DARK, DARK, DARK))
        ring_at(bm, r + 3, r + 3.6, 1.0, 128, (RU,) * 4)
        n = 10 + i * 2
        for k in range(n):                                      # carved monoliths riding the ring
            a = k * 2 * math.pi / n
            d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
            h = rs.uniform(30, 48); w = rs.uniform(10, 14)
            lean = Matrix.Rotation(rs.uniform(-0.12, 0.12), 3, d)
            R3 = lean @ Matrix((t, d, Z)).transposed()
            base = d * r - Z * h / 2
            fast_prism(bm, octo(w, 6, 1.6), h, base, R3, ST, uv, top=(0.85, 0.8, 0, 0), base_cap=True)
            for g in range(4):
                fast_box(bm, (w * rs.uniform(0.3, 0.6), h * 0.1, 0.5), base + R3 @ Vector((0, -3.1, h * (0.2 + g * 0.2))), R3 @ Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0))), RU, uv, bottom=True)
        obj(f"r{i}", bm, GM + ["Stone", "Rune"])
    lamp("L_S", (0, 0, 40), 8e4, (1.0, 0.75, 0.35), 30)
    return export(col, root, "sanctum")

# ---------------------------------------------------------------- 13. Corona Forge (a ring of heat-shielded foundries hugging the star; unit star radius 100)
@works
def coronaforge():
    """After star-hugging station references: twelve foundries each hiding behind a curved heat shield that faces
    the star, its inner face glowing at the rim, intake slots burning; behind the shields, foundry hulls with
    modules and tanks, radiator fins trailing outward, all strung on a lit truss ring ("ring", which the game turns)."""
    col, root = setup("CoronaForge")
    bm = bmesh.new()
    uv = U(bm)
    for e in (-1, 1):                                           # the ring: two rails and a lattice between
        ring_at(bm, 166, 170, 3, 128, (DARK,) * 4, (0, 0, e * 8))
    for k in range(48):
        a = k * 2 * math.pi / 48
        d = Vector((math.cos(a), math.sin(a), 0))
        _rod(bm, d * 168 - Z * 8, Rz(2 * math.pi / 48) @ (d * 168) + Z * 8, 0.6, DARK)
    strip_lights(bm, 170.05, 8, 160, GLOW, seed=13, h=0.8)
    for k in range(12):
        a = k * math.pi / 6
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        # the shield: a curved plate facing the star, its inner face lit hot at the rim
        n0 = set(bm.verts)
        solid_ring(bm, 150, 154, -22, 22, 6, mats=(LIGHT, DARK, LIGHT, LIGHT), a0=a - 0.2, a1=a + 0.2, bevel=1.5)
        solid_ring(bm, 149.4, 150, -22, -19, 6, mats=(HEAT,) * 4, a0=a - 0.19, a1=a + 0.19, bevel=0.0)
        solid_ring(bm, 149.4, 150, 19, 22, 6, mats=(HEAT,) * 4, a0=a - 0.19, a1=a + 0.19, bevel=0.0)
        for j in (-1, 0, 1):
            fast_box(bm, (5, 2, 0.4), d * 149.5 + t * j * 12, Matrix((t, Z, -d)).transposed(), HEAT, uv)       # intakes
        # the foundry behind it
        loft(bm, [(156, 10, 14, 0.35), (164, 16, 18, 0.3), (182, 14, 16, 0.3), (192, 6, 8, 0.4)], basis(d), Vector((0, 0, 0)), DARK)
        band(bm, d * 172, d, 18, 3, 1.2, GLOW if k % 2 else HULL, 16)
        module(bm, d * 178 + t * 18, d, Z, 22, 8, 8, mats=(HULL, DARK, WIN), seed=500 + k)
        tank_cage(bm, d * 176 - t * 18, 6, axis=d, capsule=1.0, mi_tank=DARK, mi_cage=HULL)
        for e in (-1, 1): radiator_bank(bm, d * 186 + Z * e * 12, d, t, 5, 18, 10, 2.6, mi_fin=RAD, mi_man=DARK)
        strobe(bm, d * 194, 1.0, RED if k % 3 == 0 else STROBE)
        if k % 4 == 1: dock_arm(bm, d * 184 + Z * 14, (d + Z).normalized(), 16, 22, mats=(HULL, DARK, GLOW, WIN), seed=520 + k)
    obj("ring", bm, GM)
    return export(col, root, "coronaforge")

# ---------------------------------------------------------------- 14. Sun Citadel
@works
def suncitadel():
    """A citadel drinking the star: a great dish of gold mirror tiles on spars facing the star (-X), a burning
    collector at its focus, and behind it a keel carrying stacked spires lit with rune windows, radiator wings
    and docks."""
    col, root = setup("SunCitadel")
    GOLD = len(GM); RU = len(GM) + 1
    bm = bmesh.new()
    uv = U(bm)
    F, X0 = 120.0, -60.0                                        # the dish: x = X0 - r^2 / 4F, opening toward -X, focus at X0 - F
    for ring_i, (r, n) in enumerate(((22, 8), (40, 14), (58, 20), (76, 26), (94, 32))):
        for k in range(n):
            a = (k + 0.5 * (ring_i % 2)) * 2 * math.pi / n
            p = Vector((X0 - r * r / (4 * F), r * math.cos(a), r * math.sin(a)))
            nrm = -Vector((1, r * math.cos(a) / (2 * F), r * math.sin(a) / (2 * F))).normalized()      # into the bowl, toward the focus
            tang = Vector((0, -math.sin(a), math.cos(a)))
            side = nrm.cross(tang).normalized()
            w = 2 * math.pi * r / n * 0.9
            fast_box(bm, (w, 16, 1.2), p, Matrix((tang, side, nrm)).transposed(), GOLD, uv)
            fast_box(bm, (w * 0.95, 15, 1.0), p - nrm * 1.1, Matrix((tang, side, nrm)).transposed(), DARK, uv)
    for k in range(8):                                          # spars from the keel out to the rim
        a = k * math.pi / 4
        rim = Vector((X0 - 100 * 100 / (4 * F), 100 * math.cos(a), 100 * math.sin(a)))
        truss(bm, Vector((X0 + 14, 0, 0)), rim + Vector((6, 0, 0)), w=4, mi=DARK, rod=0.35)
        strobe(bm, rim + Vector((-2, 0, 0)), 1.4, RED if k % 2 else STROBE)
    for k in range(4):                                          # the collector's tripod and its burning eye
        a = k * math.pi / 2 + math.pi / 4
        _rod(bm, Vector((X0 - 94 * 94 / (4 * F), 94 * math.cos(a), 94 * math.sin(a))), Vector((X0 - F + 8, 0, 0)), 1.2, DARK)
    cyl(bm, 8, 12, 16, Vector((X0 - F + 10, 0, 0)), Ry(math.pi / 2), seg=16, mi=DARK)
    sphere(bm, 7, Vector((X0 - F, 0, 0)), mi=HEAT, seg=16)
    # the keel and the citadel behind the dish
    loft(bm, [(X0 + 4, 16, 16, 0.4), (X0 + 20, 30, 34, 0.35), (60, 30, 34, 0.35), (90, 14, 16, 0.4)], Matrix.Identity(3), Vector((0, 0, 0)), DARK)
    for (x, y, h, r) in ((10, 0, 120, 14), (36, 16, 80, 9), (36, -16, 90, 9), (-10, 12, 60, 7), (60, 0, 50, 8)):
        for s in (1, -1):
            loft(bm, [(0, r, r, 0.4), (h * 0.7, r * 0.7, r * 0.7, 0.4), (h * 0.85, r * 0.45, r * 0.45, 0.4), (h, r * 0.08, r * 0.08, 0.4)], basis(Z * s, X), Vector((x, y, s * 28)), DARK if s > 0 else HULL)
            for j in range(4):
                fast_box(bm, (r * 0.3, r * 0.2, 0.4), Vector((x + r * 0.71, y, s * (36 + j * h * 0.15))), Matrix((Y, Z, X)).transposed(), RU, uv, bottom=True)
            band(bm, Vector((x, y, s * (28 + h * 0.4))), Z, r * 0.85, 2, 1.2, GLOW, 16)
    for e in (-1, 1):
        radiator_bank(bm, Vector((60, e * 32, 0)), Vector((0, e, 0)), X, 8, 40, 14, 5, mi_fin=RAD, mi_man=DARK)
        dock_arm(bm, Vector((76, e * 20, -20)), Vector((0.6, e, -0.4)), 30, 36, mats=(HULL, DARK, GLOW, WIN), seed=600 + e)
    obj("Citadel", bm, GM + ["GoldMirror", "Rune"])
    lamp("L_C", (X0 - F, 0, 0), 1e5, (1.0, 0.8, 0.4), 30)
    return export(col, root, "suncitadel")

# ---------------------------------------------------------------- 15. Starforge (the stellar seed glows procedurally inside "core")
@works
def starforge():
    """After star-engineering references: six arms on the axes, each ending in a great dish emitter aimed at the
    seed with a beam burning into it, the arm roots joined by three orthogonal track rings with hub stations at the
    crossings. The cage is "r0", which the game turns; the seed is "core", which the game pulses."""
    col, root = setup("Starforge")
    bm = bmesh.new()
    sphere(bm, 60, (0, 0, 0), mi=0, seg=32)
    obj("core", bm, ["LightWarm"])
    bm = bmesh.new()
    RR = 380.0
    for k, rot in enumerate((Matrix.Identity(4), Rx(math.pi / 2), Ry(math.pi / 2))):     # three track rings
        n0 = set(bm.verts)
        c = solid_ring(bm, RR - 9, RR + 9, -6, 6, 128, mats=(DARK, DARK, DARK, HULL))
        detail(bm, cyl_surface(RR + 9, -6 + c + 0.1, 6 - c - 0.1), 700 + k, scale=3.0, mats=(DARK, HULL), window=WIN, window_frac=0.06)
        strip_lights(bm, RR + 9.05, 4, 220, GLOW, seed=k, h=1.0)
        bmesh.ops.transform(bm, matrix=rot, verts=since(bm, n0))
    for d in (X, -X, Y, -Y, Z, -Z):
        up = Z if abs(d.z) < 0.9 else X
        R3 = basis(-d, up)
        # the hub where two rings cross, and the arm reaching in to the dish
        loft(bm, [(-30, 24, 24, 0.35), (-20, 34, 34, 0.3), (12, 34, 34, 0.3), (24, 22, 22, 0.35)], R3, d * RR, DARK)
        band(bm, d * (RR - 14), d, 34, 3, 1.5, GLOW, 24)
        truss(bm, d * (RR - 24), d * 236, w=14, mi=DARK, rod=1.2)
        side = R3.col[1]
        for e in (-1, 1):
            module(bm, d * RR + side * e * 46, -d, up, 34, 14, 12, mats=(HULL, DARK, WIN), seed=710 + e)
            radiator_bank(bm, d * (RR + 6) + R3.col[2] * e * 34, R3.col[2] * e, d, 8, 30, 12, 4.4, mi_fin=RAD, mi_man=DARK)
        dish(bm, d * 214, -d, 52, mi_dish=LIGHT, mi_frame=DARK, depth=0.32, ribs=16)
        for j in range(3): band(bm, d * (226 + j * 6), d, 10, 2, 2, COIL, 16)
        cyl(bm, 3.0, 3.0, 214 - 70, d * (70 + (214 - 70) / 2), basis(d).to_4x4() @ Ry(math.pi / 2), seg=12, mi=PLASMA)    # the beam
        strobe(bm, d * (RR + 30), 1.8, RED)
    obj("r0", bm, GM)
    lamp("L_Seed", (0, 0, 0), 5e5, FIRE, 60)
    return export(col, root, "starforge")

# ---------------------------------------------------------------- 16. Lucent Throne
@works
def throne():
    """A seat of light in ivory and gold: a stepped dais trimmed with gold and lines of light, the seat's tall back
    a fan of blades burning at their edges, a colonnade of lit spires, shards drifting, and a sunburst halo
    ("halo") behind the seat, pivoting on its own centre so the game can turn it in place."""
    col, root = setup("Throne")
    LU, LG, GO = len(GM), len(GM) + 1, len(GM) + 2
    bm = bmesh.new()
    uv = U(bm)
    for k, (w, h, z) in enumerate(((170, 14, -80), (136, 14, -66), (104, 14, -52), (76, 10, -38))):
        fast_prism(bm, octo(w, w, w * 0.25), h, Vector((0, 0, z)), Matrix.Identity(3), LU, uv, top=(0.96, 0.96, 0, 0), base_cap=True)
        fast_prism(bm, octo(w * 0.965, w * 0.965, w * 0.24), 1.2, Vector((0, 0, z + h - 0.2)), Matrix.Identity(3), GO, uv, top=(1, 1, 0, 0))
        for j in range(8):                                      # lines of light down each tier's faces
            a = j * math.pi / 4 + math.pi / 8
            d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
            fast_box(bm, (w * 0.25, 1.0, 0.4), d * (w / 2 * math.cos(math.pi / 8) * 0.985 + 0.2) + Z * (z + h * 0.5), Matrix((t, Z, d)).transposed(), LG, uv, bottom=True)
    fast_prism(bm, octo(40, 30, 8), 26, Vector((0, 10, -28)), Matrix.Identity(3), LU, uv, top=(1, 1, 0, 0), base_cap=True)   # the seat
    for k in range(9):                                          # its back: a fan of blades, edges burning
        a = (k - 4) * 0.16
        dirv = Vector((math.sin(a), 0.12, math.cos(a))).normalized()
        L = 150 - abs(k - 4) * 16
        R3 = basis(dirv, Vector((0, -1, 0)))
        loft(bm, [(0, 2.5, 7, 0.3), (L * 0.8, 2.0, 5, 0.3), (L, 0.3, 0.6, 0.3)], R3, Vector((0, 30, -2)), LU if k % 2 else GO)
        fast_box(bm, (L * 0.8, 0.6, 0.5), Vector((0, 30, -2)) + dirv * L * 0.42 + R3.col[2] * 5.2, R3, LG, uv, bottom=True)
    for k in range(12):                                         # a colonnade of lit spires round the top tier
        a = k * math.pi / 6
        d = Vector((math.cos(a), math.sin(a), 0))
        p = d * 62 + Z * -28
        loft(bm, [(0, 3, 3, 0.4), (30 + (k % 3) * 12, 1.8, 1.8, 0.4), (40 + (k % 3) * 14, 0.2, 0.2, 0.4)], UPX, p, LU)
        sphere(bm, 1.6, p + Z * (41 + (k % 3) * 14), mi=LG, seg=8)
    rs = random.Random(16)
    for k in range(14):                                         # drifting shards of light
        p = Vector((rs.uniform(-90, 90), rs.uniform(-60, 70), rs.uniform(-20, 140)))
        if p.length < 70: continue
        dd = Vector((rs.uniform(-0.3, 0.3), rs.uniform(-0.3, 0.3), 1)).normalized()
        loft(bm, [(0, 0.2, 0.2, 0.3), (rs.uniform(4, 8), 2, 2, 0.3), (rs.uniform(12, 18), 0.2, 0.2, 0.3)], basis(dd), p, LU if k % 3 else LG)
    obj("Throne", bm, GM + ["Lucent", "LucentGlow", "Gold"])
    lamp("L_T", (0, 20, 60), 1e5, (0.9, 0.95, 1.0), 40)
    # the halo: built about its own centre and placed there, so turning it spins it in place
    bm = bmesh.new()
    uv = U(bm)
    ring_at(bm, 68, 76, 3, 96, (LU, LU, GO, LU))
    ring_at(bm, 78, 79.5, 1.5, 96, (LG,) * 4)
    for k in range(24):
        a = k * 2 * math.pi / 24
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        L = 26 if k % 2 else 14
        fast_prism(bm, [(80, -2), (80 + L, -0.3), (80 + L, 0.3), (80, 2)], 1.2, Z * -0.6, Matrix((d, t, Z)).transposed(), GO if k % 2 else LG, uv, base_cap=True)
    bmesh.ops.transform(bm, matrix=Rx(math.pi / 2), verts=bm.verts[:])
    halo = obj("halo", bm, GM + ["Lucent", "LucentGlow", "Gold"])
    halo.location = (0, 40, 150)
    return export(col, root, "throne")

result = {}
for name in globals().get("ONLY", list(BUILD.keys())):
    result[name] = BUILD[name]()
print("RESULT", result)
