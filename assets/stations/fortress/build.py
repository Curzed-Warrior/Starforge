"""
Starforge Star Fortress: the third starbase tier. A gyroscope of three tilted rings around an armoured core
drum, lit from inside by furnace-orange light through every opening, still partly open trusswork where it is
being extended, with a long lattice mast carrying a deep-space dish and banks of solar arrays.
Exterior asset. Run inside Blender:
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/fortress/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Fortress")

# ---------------------------------------------------------------- the core drum
CR, CH = 38.0, 70.0
PROF = [(10, CH / 2 + 22), (18, CH / 2 + 14), (CR - 6, CH / 2 + 6), (CR, CH / 2), (CR, CH / 6), (CR + 4, CH / 6 - 2),
        (CR + 4, -CH / 6 + 2), (CR, -CH / 6), (CR, -CH / 2), (CR - 6, -CH / 2 - 6), (18, -CH / 2 - 14), (10, -CH / 2 - 22)]
bm = bmesh.new()
revolve(bm, PROF, 96, lambda i, s: 1 if i in (4, 5, 6) else 0, closed=False, toward=lambda c: (radial(c) + Vector((0, 0, 0.3 if c.z > 0 else -0.3))).normalized())
obj("Core_Hull", bm, ["Hull", "HullDark"], uv="cyl")

bm = bmesh.new()
detail(bm, cyl_surface(CR, CH / 6 + 0.5, CH / 2 - 0.5), 1, scale=1.2, mats=(0, 1), window=2, glow=3, window_frac=0.04, glow_frac=0.04)
detail(bm, cyl_surface(CR, -CH / 2 + 0.5, -CH / 6 - 0.5), 2, scale=1.2, mats=(0, 1), window=2, glow=3, window_frac=0.04, glow_frac=0.04)
detail(bm, cyl_surface(CR + 4, -CH / 6 + 2.4, CH / 6 - 2.4), 3, scale=1.0, mats=(0, 1), window=2, glow=3, window_frac=0.03, glow_frac=0.06)
for sz in (-1, 1):
    greeble(bm, cone_surface(CR, sz * CH / 2, CR - 6, sz * (CH / 2 + 6)) if sz > 0 else cone_surface(CR - 6, -CH / 2 - 6, CR, -CH / 2), 0.25, 4 + sz, size=(0.5, 2.0), height=(0.2, 0.8), mats=(0, 1), glow=3, glow_frac=0.05)
obj("Core_Greebles", bm, ["Hull", "HullDark", "Window", "Glow"])

bm = bmesh.new()
# furnace slots: deep vertical openings round the belt, glowing from within
for k in range(36):
    a = k * 2 * math.pi / 36
    rot = Rz(a)
    box(bm, (0.6, 3.0, CH / 3 - 6), rot @ Vector((CR + 4.05, 0, 0)), rot, mi=0)
    box(bm, (0.8, 3.6, 0.6), rot @ Vector((CR + 4.1, 0, CH / 6 - 2.8)), rot, mi=1)
    box(bm, (0.8, 3.6, 0.6), rot @ Vector((CR + 4.1, 0, -CH / 6 + 2.8)), rot, mi=1)
window_rows(bm, cyl_surface(CR + 0.02, CH / 6 + 2, CH / 2 - 2), 6, 120, size=(2.0, 0.6), mi=2, seed=1, lit=0.5)
window_rows(bm, cyl_surface(CR + 0.02, -CH / 2 + 2, -CH / 6 - 2), 6, 120, size=(2.0, 0.6), mi=2, seed=2, lit=0.5)
obj("Core_Lights", bm, ["Glow", "HullDark", "Window"])

# ---------------------------------------------------------------- three tilted rings
def fortress_ring(name, R, W, H, tilt, spin, seed, open_arc=0.0):
    """A heavy box-section ring. A stretch of open_arc radians is still bare trusswork under construction."""
    a1 = 2 * math.pi - open_arc
    bm = bmesh.new()
    if open_arc > 0:
        c = solid_ring(bm, R - W / 2, R + W / 2, -H / 2, H / 2, 160, mats=(0, 1, 0, 0), a0=0, a1=a1)
    else:
        c = solid_ring(bm, R - W / 2, R + W / 2, -H / 2, H / 2, 160, mats=(0, 1, 0, 0))
    # armour bands every 15 degrees
    for k in range(24):
        a = k * 2 * math.pi / 24
        if a > a1: continue
        solid_ring(bm, R - W / 2 - 0.6, R + W / 2 + 0.6, -H / 2 - 0.6, H / 2 + 0.6, 2, mats=(1, 1, 1, 1), a0=a, a1=a + 0.03)
    hull = obj(name + "_Hull", bm, ["Hull", "HullDark"], uv="cyl")
    bm = bmesh.new()
    s = (R / 100) ** 0.5
    detail(bm, cyl_surface(R + W / 2, -H / 2 + c + 0.2, H / 2 - c - 0.2, a1=a1), seed, scale=1.4 * s, mats=(0, 1), window=2, glow=3, window_frac=0.03, glow_frac=0.05)
    detail(bm, cyl_surface(R - W / 2, -H / 2 + c + 0.2, H / 2 - c - 0.2, a1=a1, inward=True), seed + 5, scale=1.4 * s, mats=(0, 1), glow=3, glow_frac=0.08)
    for sz in (-1, 1):
        detail(bm, disc_surface(R - W / 2 + c + 0.2, R + W / 2 - c - 0.2, sz * H / 2, up=sz > 0, a1=a1), seed + 10 + sz, scale=1.4 * s, mats=(0, 1), window=2, glow=3, window_frac=0.03, glow_frac=0.04)
    window_rows(bm, cyl_surface(R + W / 2 + 0.02, -H / 4, H / 4, a1=a1), 2, int(R * 4), size=(1.4, 0.5), mi=2, seed=seed, lit=0.55)
    window_rows(bm, cyl_surface(R - W / 2 - 0.02, -H / 4, H / 4, a1=a1, inward=True), 3, int(R * 3), size=(2.0, 0.7), mi=3, seed=seed + 1, lit=0.6)
    det = obj(name + "_Greebles", bm, ["Hull", "HullDark", "Window", "Glow"])
    bm = bmesh.new()
    if open_arc > 0:
        # the unfinished stretch: four chords of lattice following the curve, with ribs and a gantry or two
        n = 18
        for k in range(n):
            a = a1 + open_arc * k / n; b = a1 + open_arc * (k + 1) / n
            for (dr, dz) in ((-W / 2, -H / 2), (W / 2, -H / 2), (W / 2, H / 2), (-W / 2, H / 2)):
                p0 = Rz(a) @ Vector((R + dr, 0, dz)); p1 = Rz(b) @ Vector((R + dr, 0, dz))
                truss(bm, p0, p1, w=0.9, seg=3, mi=0, rod=0.1)
            if k % 3 == 0:
                c = Rz(a) @ Vector((R, 0, 0))
                truss(bm, c + Vector((0, 0, -H / 2)), c + Vector((0, 0, H / 2)), w=0.9, seg=4, mi=0, rod=0.1)
                truss(bm, Rz(a) @ Vector((R - W / 2, 0, 0)), Rz(a) @ Vector((R + W / 2, 0, 0)), w=0.9, seg=4, mi=0, rod=0.1)
            if k % 6 == 3:
                box(bm, (3, 3, 3), Rz(a) @ Vector((R + W / 2 + 3, 0, 0)), Rz(a), mi=1)        # a construction tug
                sphere(bm, 0.5, Rz(a) @ Vector((R + W / 2 + 4.6, 0, 0)), mi=2, seg=6)
    # spokes back to the core: lattice booms with a pressurised tube
    for k in range(4 if R < 120 else 6):
        a = k * 2 * math.pi / (4 if R < 120 else 6) + 0.2
        if a > a1: continue
        d = Vector((math.cos(a), math.sin(a), 0))
        truss(bm, d * (CR + 6), d * (R - W / 2), w=3.0, mi=0, rod=0.2)
        cyl(bm, 1.2, 1.2, R - W / 2 - CR - 6, d * ((CR + 6 + R - W / 2) / 2), Rz(a) @ Ry(math.pi / 2), seg=10, mi=1)
    st = obj(name + "_Structure", bm, ["HullDark", "Hull", "Glow"])
    for o in (hull, det, st):
        o.rotation_euler = (tilt, 0, spin)
    return hull

fortress_ring("RingInner", 78.0, 14.0, 10.0, math.radians(0), 0.0, 100)
fortress_ring("RingMid", 112.0, 12.0, 9.0, math.radians(32), 0.6, 200, open_arc=math.radians(70))
fortress_ring("RingOuter", 150.0, 11.0, 8.0, math.radians(-24), 2.1, 300, open_arc=math.radians(45))

# ---------------------------------------------------------------- the mast and the dish
bm = bmesh.new()
top = Vector((0, 0, CH / 2 + 22))
mast_end = top + Vector((0, 0, 210))
truss(bm, top, mast_end, w=5.0, mi=0, rod=0.3)
for z in (60, 120, 180):
    box(bm, (9, 9, 4), top + Vector((0, 0, z)), mi=1)
    for k in range(4):
        a = k * math.pi / 2
        antennas(bm, top + Vector((4.5 * math.cos(a), 4.5 * math.sin(a), z + 2)), (math.cos(a), math.sin(a), 0.4), 2, 8.0, int(z) + k, mi=0, light=2)
# the dish: a shallow lattice bowl on a yoke
res = bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=34)
vs = res["verts"]
bmesh.ops.delete(bm, geom=[v for v in vs if v.co.z > -24], context="VERTS")
rest = [v for v in vs if v.is_valid]
bmesh.ops.transform(bm, matrix=Matrix.Translation(mast_end + Vector((0, 0, 30))) @ Rx(-0.5), verts=rest)
for f in {f for v in rest for f in v.link_faces}: f.material_index = 1
cyl(bm, 1.0, 1.0, 26, mast_end + Vector((0, 8, 22)), Rx(-0.5), seg=8, mi=0)
sphere(bm, 2.2, mast_end + Vector((0, 14, 33)), mi=2, seg=12)
obj("Mast", bm, ["HullDark", "Hull", "Glow"], smooth=False)

# ---------------------------------------------------------------- solar arrays on outriggers below
bm = bmesh.new(); bmp = bmesh.new()
base = Vector((0, 0, -CH / 2 - 22))
for k in range(3):
    a = k * 2 * math.pi / 3 + 0.5
    d = Vector((math.cos(a), math.sin(a), -0.35)).normalized()
    end = base + d * 170
    truss(bm, base, end, w=4.0, mi=0, rod=0.25)
    side = Vector((-d.y, d.x, 0)).normalized()
    for j in range(6):
        c = base + d * (40 + j * 22)
        for sgn in (-1, 1):
            rot = Matrix((d, side, d.cross(side))).transposed().to_4x4()
            box(bmp, (20, 26, 0.2), c + side * sgn * 16, rot, mi=0)
            for cx in range(5):
                box(bmp, (0.3, 26, 0.3), c + side * sgn * 16 + d * (-8 + cx * 4), rot, mi=1)
obj("SolarBooms", bm, ["HullDark"])
obj("SolarArrays", bmp, ["Solar", "HullDark"])

# ---------------------------------------------------------------- nav lights
bm = bmesh.new()
for z in (CH / 2 + 22, -CH / 2 - 22):
    sphere(bm, 0.8, (0, 0, z), mi=0, seg=8)
for k in range(12):
    a = k * math.pi / 6
    sphere(bm, 0.6, ((CR + 4.4) * math.cos(a), (CR + 4.4) * math.sin(a), 0), mi=1 if k % 2 else 2, seg=8)
# spaceport: four hangar bays in the armoured belt, between the inner ring's spokes
for k in range(4):
    a = k * math.pi / 2 + 0.2 + math.pi / 4
    d = Vector((math.cos(a), math.sin(a), 0))
    bay = hangar(bm, d * (CR + 4), d, (0, 0, 1), 16.0, 8.0, 18.0, mats=(3, 4, 5, 0, 6), seed=30 + k)
    lamp(f"L_Bay{k}", bay, 1.2e4, COOL, 3.0)
obj("NavLights", bm, ["NavWhite", "NavRed", "NavGreen", "Hull", "HullDark", "LightCool", "Glow"])

lamp("Core_Glow", (0, 0, 0), 5e5, FIRE, 20)
for k in range(4):
    a = k * math.pi / 2 + 0.4
    lamp(f"Flood{k}", Rz(a) @ Vector((95, 0, 10)), 1.2e5, (1.0, 0.85, 0.7), 3, "SPOT", aim=(0, 0, 0), spot=0.9)


# ================================================================ v2 starbase pass (asset overhaul, docs/ASSET_REFERENCES.md)
AM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar"]
A_HULL, A_DARK, A_LIGHT, A_ACC, A_HAZ, A_WIN, A_WCOOL, A_STROBE, A_RAD, A_GLOW, A_RED, A_SOLAR = range(len(AM))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
restyle(col)
bm = bmesh.new()

# warm strips on the three gyroscope rings (in each ring's own frame), radiators and tanks on the core's crown,
# strobes up the mast, a freighter tied up below
for (R, W, H, tilt, spin, open_arc) in ((78.0, 14.0, 10.0, 0.0, 0.0, 0.0), (112.0, 12.0, 9.0, math.radians(32), 0.6, math.radians(70)),
                                       (150.0, 11.0, 8.0, math.radians(-24), 2.1, math.radians(45))):
    rot = (Rz(spin) @ Rx(tilt)).to_3x3()
    strip_lights(bm, R + W / 2, H * 0.32, int(R * 1.6), A_GLOW, rot=rot, a1=2 * math.pi - open_arc, seed=int(R), h=0.5)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    d = Vector((math.cos(a), math.sin(a), 0))
    radiator_bank(bm, d * 20 + Z_ * (CH / 2 + 12), (d + Z_ * 0.5).normalized(), Vector((-d.y, d.x, 0)), 7, 22, 8, 2.8, mi_fin=A_RAD, mi_man=A_DARK)
    tank_cage(bm, d * 22 + Z_ * -(CH / 2 + 8), 6.0, axis=Z_, capsule=1.0, mi_tank=A_HULL, mi_cage=A_DARK)
for z in (60, 120, 180, 230): strobe(bm, Z_ * (CH / 2 + 22 + z) + X_ * 3, 0.8, A_RED if z % 120 == 0 else A_STROBE)
dock_arm(bm, Z_ * -(CH / 2 + 18), Vector((0.6, -0.4, -1)), 30, 44, mats=(A_HULL, A_DARK, A_GLOW, A_WIN), seed=230)
obj("Fittings2", bm, AM)

result = export(col, root, "fortress")
