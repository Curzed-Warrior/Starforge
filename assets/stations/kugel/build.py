"""
Kugelblitz Reactor: a black hole made from pure light, caged by three gimbal rings of containment emitters
("c0", "c1", "c2", spun by the game) inside a fixed outer frame with power taps and radiators. The black core
and its glow stay procedural. Z is up.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kugel/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Kugel")

for i, rad in enumerate((60.0, 68.0, 76.0)):
    bm = bmesh.new()
    c = solid_ring(bm, rad - 3, rad + 3, -2.5, 2.5, 64, mats=(0, 1, 0, 0))
    for k in range(8):
        a = k * math.pi / 4 + i * 0.3
        # emitters pointing in at the singularity, glowing at the tip
        loft(bm, [(rad + 4, 4, 4, 0.4), (rad - 2, 3.5, 3.5, 0.4), (rad - 10, 1.4, 1.4, 0.4)], Rz(a).to_3x3(), Vector((0, 0, 0)), 1)
        sphere(bm, 1.6, Rz(a) @ Vector((rad - 11, 0, 0)), mi=2, seg=8)
    greeble(bm, disc_surface(rad - 3 + c, rad + 3 - c, 2.5, up=True), 0.3, 5 + i, size=(0.5, 1.6), height=(0.2, 0.7), mats=(0, 1))
    obj(f"c{i}", bm, ["Hull", "HullDark", "WindowCool"])

bm = bmesh.new()
# the fixed frame: two heavy hoops at right angles, power taps, radiator vanes
for rot in (Rx(math.pi / 2), Ry(math.pi / 2)):
    n0 = set(bm.verts)
    c = solid_ring(bm, 86, 96, -5, 5, 96, mats=(0, 1, 0, 1))
    bmesh.ops.transform(bm, matrix=rot, verts=since(bm, n0))
for d in (Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 0, 1)), Vector((0, 0, -1)), Vector((0, 1, 0)), Vector((0, -1, 0))):
    y = d.cross(Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))).normalized()
    rot = Matrix((d, y, d.cross(y))).transposed()
    loft(bm, [(92, 12, 12, 0.4), (104, 14, 14, 0.4), (120, 9, 9, 0.4), (128, 4, 4, 0.4)], rot, Vector((0, 0, 0)), 0)
    for j in range(4):
        box(bm, (0.6, 18, 26), d * (110 + j * 4) , Matrix.Identity(4) if abs(d.z) > 0.9 else rot.to_4x4(), mi=2)
    sphere(bm, 2.0, d * 130, mi=3, seg=8)
obj("Frame", bm, ["Hull", "HullDark", "Radiator", "NavWhite"])


# ================================================================ v2 secondary-scale pass (asset overhaul, docs/ASSET_REFERENCES.md)
AM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar", "Rock"]
A_HULL, A_DARK, A_LIGHT, A_ACC, A_HAZ, A_WIN, A_WCOOL, A_STROBE, A_RAD, A_GLOW, A_RED, A_SOLAR, A_ROCK = range(len(AM))
M["Rock"] = mat("Rock", (0.32, 0.27, 0.22), 0.0, 0.9)
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
bm = bmesh.new()

# power trunks out to four substations, each a module with radiators and a tank; a tender docked; strobes
for k, d in enumerate((X_, -X_, Y_, -Y_)):
    side = Z_.cross(d)
    pipe_run(bm, [d * 132, d * 210], 3.0, n=3, mi=A_DARK, clamp_mi=A_HAZ)
    module(bm, d * 236, d, Z_, 48, 26, 22, mats=(A_DARK if k % 2 else A_HULL, A_DARK, A_WIN), seed=60 + k)
    radiator_bank(bm, d * 236 + Z_ * 11, Z_, d, 8, 30, 12, 5, mi_fin=A_RAD, mi_man=A_DARK)
    tank_cage(bm, d * 236 + side * 22 - Z_ * 8, 9, axis=d, capsule=1.0, mi_tank=A_DARK if k % 2 else A_HULL, mi_cage=A_DARK)
    strobe(bm, d * 262, 1.4, A_RED if k % 2 else A_STROBE)
dock_arm(bm, Vector((0, 236, -11)), -Z_, 30, 34, mats=(A_LIGHT, A_DARK, A_GLOW, A_WIN), seed=71)
obj("Substations", bm, AM)

result = export(col, root, "kugel")
