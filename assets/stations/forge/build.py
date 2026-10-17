"""
Black Hole Forge: a stellar-mass black hole wrapped in a lattice of harvesting rings, fed planet-stuff from three
feeder stations. The glowing accretion bands are "swirl" (the game spins them); the event horizon is pure black.
Z is up. ~500 m.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/forge/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Forge")
M["Void"] = mat("Void", (0, 0, 0), 0.0, 1.0)
M["Ember"] = mat("Ember", (0.8, 0.25, 0.05), emit=(0.9, 0.22, 0.03), strength=2.0)
M["Ember2"] = mat("Ember2", (0.5, 0.1, 0.03), emit=(0.6, 0.1, 0.02), strength=1.0)

bm = bmesh.new()
sphere(bm, 50, (0, 0, 0), mi=0, seg=32)
obj("Horizon", bm, ["Void"], smooth=True)

bm = bmesh.new()
# accretion bands: hot and white close in, cooling to deep orange, with dark lanes between
for i, (r0, r1, mi) in enumerate(((58, 66, 0), (67, 80, 1), (81, 92, 1), (94, 108, 2), (109, 122, 2), (124, 140, 3), (142, 150, 3))):
    revolve(bm, [(r0, 0), (r1, 0)], 96, lambda i2, s, mi=mi: mi, closed=False, toward=lambda c: Vector((0, 0, 1)))
    revolve(bm, [(r0, 0), (r1, 0)], 96, lambda i2, s, mi=mi: mi, closed=False, toward=lambda c: Vector((0, 0, -1)))
obj("swirl", bm, ["Window", "Glow", "Ember", "Ember2"], smooth=True)

bm = bmesh.new()
# harvesting rings: three tilted gimbal rings of collectors around the disk
for k, (rad, tilt, spin) in enumerate(((170, 0.35, 0.0), (190, -0.5, 1.0), (210, 0.9, 2.2))):
    n0 = set(bm.verts)
    c = solid_ring(bm, rad - 5, rad + 5, -4, 4, 128, mats=(1, 0, 1, 0))
    detail(bm, cyl_surface(rad + 5, -4 + c + 0.1, 4 - c - 0.1), 10 + k, scale=1.8, mats=(0, 1), glow=2, glow_frac=0.08)
    for j in range(8):
        a = j * math.pi / 4
        loft(bm, [(-8, 6, 6, 0.4), (8, 6, 6, 0.4)], Rz(a + math.pi / 2).to_3x3(), Rz(a) @ Vector((rad, 0, 0)), 1)
    bmesh.ops.transform(bm, matrix=Rz(spin) @ Rx(tilt), verts=since(bm, n0))
obj("Lattice", bm, ["Hull", "HullDark", "Glow"])

# matter decompressors (after Stellaris' matter decompressor, not a copy): barrel stations hanging off the
# rings, framed by rotating collars, with emitter arms firing violet beams into the singularity
M["Beam"] = mat("Beam", (0.8, 0.6, 1.0), emit=(0.75, 0.5, 1.0), strength=12)
bm = bmesh.new()
for k in range(3):
    a = k * 2 * math.pi / 3 + 0.4
    d = Vector((math.cos(a), math.sin(a), 0.18)).normalized()
    side = Vector((-d.y, d.x, 0)).normalized()
    rot = Matrix((-d, side, (-d).cross(side))).transposed()            # local +X points at the hole
    at = d * 300
    loft(bm, [(-48, 8, 8, 0.4), (-40, 18, 18, 0.35), (10, 18, 18, 0.35), (22, 13, 13, 0.35), (34, 7, 7, 0.4)], rot, at, 0)
    for x in (-30, -14, 2):
        n0 = set(bm.verts)
        solid_ring(bm, 22, 25, -1.6, 1.6, 24, mats=(1, 1, 1, 1))
        bmesh.ops.transform(bm, matrix=Matrix.Translation(at) @ rot.to_4x4() @ Ry(math.pi / 2) @ Matrix.Translation((0, 0, -x)), verts=since(bm, n0))
        n0 = set(bm.verts)
        solid_ring(bm, 21.5, 22, -0.6, 0.6, 24, mats=(2, 2, 2, 2), bevel=0)
        bmesh.ops.transform(bm, matrix=Matrix.Translation(at) @ rot.to_4x4() @ Ry(math.pi / 2) @ Matrix.Translation((0, 0, -x)), verts=since(bm, n0))
    greeble(bm, plane_surface(at + rot @ Vector((-38, -12, 18)), rot @ Vector((46, 0, 0)), rot @ Vector((0, 24, 0))), 0.15, 50 + k, size=(1, 3), height=(0.4, 1.5), mats=(0, 1), window=3, window_frac=0.08)
    sphere(bm, 6, at + rot @ Vector((36, 0, 0)), mi=4, seg=12)
    for sy in (-1, 1):
        # emitter arms: a boom out to the side ending in a stack of rings, each firing a beam at the hole
        e = at + rot @ Vector((0, sy * 46, 0))
        _rod(bm, at + rot @ Vector((0, sy * 18, 0)), e, 2.2, 1)
        for j in range(3):
            n0 = set(bm.verts)
            solid_ring(bm, 6, 8, -1, 1, 16, mats=(1, 1, 1, 1))
            bmesh.ops.transform(bm, matrix=Matrix.Translation(e + rot @ Vector((8 - j * 5, 0, 0))) @ rot.to_4x4() @ Ry(math.pi / 2), verts=since(bm, n0))
        sphere(bm, 3, e + rot @ Vector((12, 0, 0)), mi=4, seg=10)
        _rod(bm, e + rot @ Vector((14, 0, 0)), e.lerp(Vector((0, 0, 0)), 0.83), 0.8, 4)
    _rod(bm, at + rot @ Vector((40, 0, 0)), at * 0.17, 1.4, 4)
    lamp(f"L_Decomp{k}", at + rot @ Vector((40, 0, 0)), 5e4, (0.75, 0.55, 1.0), 4)
obj("Decompressors", bm, ["Hull", "HullDark", "WindowCool", "Window", "Beam"])


lamp("L_Disk", (0, 0, 20), 2e6, FIRE, 30)

# ================================================================ v2 secondary-scale pass (asset overhaul, docs/ASSET_REFERENCES.md)
AM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar", "Rock"]
A_HULL, A_DARK, A_LIGHT, A_ACC, A_HAZ, A_WIN, A_WCOOL, A_STROBE, A_RAD, A_GLOW, A_RED, A_SOLAR, A_ROCK = range(len(AM))
M["Rock"] = mat("Rock", (0.11, 0.095, 0.085), 0.0, 0.95)
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
bm = bmesh.new()

# planetoids waiting in cradles at the end of three feeder conveyors; collector towers over and under the disk
rnd2 = random.Random(9)
for k in range(3):
    a = k * 2 * math.pi / 3 + 0.4
    d = Vector((math.cos(a), math.sin(a), 0.18)).normalized()
    pipe_run(bm, [d * 348, d * 430], 3.4, n=2, mi=A_DARK, clamp_mi=A_HAZ)
    c = d * 470
    ico = bmesh.ops.create_icosphere(bm, subdivisions=3, radius=30)["verts"]
    for v in ico: v.co = v.co * (0.8 + 0.4 * rnd2.random()) + c
    for f in {f for v in ico for f in v.link_faces}: f.material_index = A_ROCK
    for j in range(3):
        n0 = set(bm.verts)
        solid_ring(bm, 34, 38, -2, 2, 32, mats=(A_DARK,) * 4)
        bmesh.ops.transform(bm, matrix=Matrix.Translation(c) @ basis(d).to_4x4() @ Rx(j * math.pi / 3) @ Ry(math.pi / 2), verts=since(bm, n0))
    radiator_bank(bm, d * 300 + Z_ * 20, Z_, Vector((-d.y, d.x, 0)), 8, 28, 10, 5, mi_fin=A_RAD, mi_man=A_DARK)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    for sz in (-1, 1):
        base = Vector((math.cos(a) * 150, math.sin(a) * 150, sz * 230))
        aim = (Vector((0, 0, 0)) - base).normalized()
        loft(bm, [(0, 14, 14, 0.4), (50, 10, 10, 0.4), (80, 6, 6, 0.4), (92, 3, 3, 0.4)], basis(aim), base, A_DARK)
        for j in range(4): band(bm, base + aim * (20 + j * 22), aim, 11 - j * 1.6, 3, 2, A_ACC if j == 0 else A_DARK, 16)
        sphere(bm, 3, base + aim * 93, mi=A_GLOW, seg=10)
obj("Feeders", bm, AM)

result = export(col, root, "forge")
