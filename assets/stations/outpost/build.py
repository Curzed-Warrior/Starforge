"""
Starforge Outpost: the first starbase a civilisation builds, at real scale (1 unit = 1 metre).
Run inside Blender:  exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/outpost/build.py").read())
Exports assets/stations/outpost/outpost.glb.

Layout (Z up, ~90 m across, ~75 m tall):
  - Spine: a hollow tower 16 m wide with five decks, a lift shaft through hatches in every deck, and
    doorways into the four spokes on the spoke deck
  - Command dome on top: glass hemisphere over a ring of consoles, reached by the lift
  - Habitat ring (REV B, one build with docs/blueprints): gravity is generated and points DOWN = -Z everywhere,
    so the ring holds two FLAT decks (Deck A floor z -0.2, Deck B floor z -3.5) and its curved faces are walls.
    Every window, skylight, spoke portal and hangar door here comes from docs/blueprints/openings.json, written by
    tools/blueprints/outpost_design.py. This file is the SHELL: the furnished interior is a separate build.
  - Four spokes: 4.8 x 3.2 m tunnels with ladder rungs, open at both ends into the spine and the ring
  - Docking hub below: four arms ending in lit docking collars, one with a shuttle moored
  - Outside: kitbash detail over every hull, window rows, conduits, ribs, antenna farms, solar wings,
    radiators, a comms dish and navigation lights
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Outpost")

# ------------------------------------------------------------------ dimensions
SR, SRI, Z0, Z1 = 8.0, 7.4, -26.0, 30.0          # spine outer/inner radius, bottom, top
DECKS = [-22.0, -13.5, -0.2, 10.0, 21.0]          # hub deck -0.2 = spoke floor = ring Deck A floor
SPOKE_W, SPOKE_H, SPOKE_Z = 4.8, 3.2, 1.4          # spoke tunnel: width, height, centre height (floor at Deck A)
RO, RI, HW, T = 44.0, 36.0, 4.0, 0.6              # ring: outer wall r, hub wall r, half-height, shell
import json
M.update({
    "WinGlass":  mat("WinGlass", (0.05, 0.07, 0.09), 0.0, 0.04, alpha=0.14),
    "WinFrame":  mat("WinFrame", (0.07, 0.075, 0.085), 0.85, 0.35),
    "RoomWarm":  mat("RoomWarm", (0.8, 0.72, 0.6), emit=(1, 0.8, 0.6), strength=0.9),
    "RoomCool":  mat("RoomCool", (0.65, 0.75, 0.85), emit=(0.7, 0.85, 1.0), strength=0.8),
    "RoomDim":   mat("RoomDim", (0.3, 0.3, 0.32), emit=(1, 0.75, 0.5), strength=0.12),
    "RoomFloor": mat("RoomFloor", (0.2, 0.2, 0.22), 0.2, 0.6, emit=(1, 0.85, 0.7), strength=0.2),
    "RoomFurn":  mat("RoomFurn", (0.12, 0.12, 0.13), 0.1, 0.7),
    "RoomWall":  mat("RoomWall", (0.35, 0.36, 0.4), emit=(0.8, 0.85, 1.0), strength=0.3),
    "Blind":     mat("Blind", (0.85, 0.8, 0.7), emit=(1, 0.85, 0.65), strength=0.6),
})
_gb = M["WinGlass"].node_tree.nodes["Principled BSDF"]
_gb.inputs["IOR"].default_value = 1.2; _gb.inputs["Specular IOR Level"].default_value = 0.25
OPEN = json.load(open(r"C:/Users/diamo/Desktop/starforge/docs/blueprints/openings.json"))
SKY0, SKY1 = OPEN["roof"][0]["r0"], OPEN["roof"][0]["r1"]   # skylight band over the corridor
Z_DECK_B = -3.5
STEPS = 96
SPOKE_ANGLES = [k * math.pi / 2 for k in range(4)]

def near_spoke(a, r):
    """Is angle a (radians) within half a spoke-width of a spoke, at radius r?"""
    for sa in SPOKE_ANGLES:
        d = (a - sa + math.pi) % (2 * math.pi) - math.pi
        if abs(d) * r < SPOKE_W / 2: return True
    return False

# ================================================================== SPINE
# outer skin: ribbed bands; the spoke deck band is split so the doorways can be cut
zs = sorted(set([Z0 + 4 * k for k in range(15)] + [SPOKE_Z - SPOKE_H / 2, SPOKE_Z + SPOKE_H / 2]))
SEG = 64
def spine_door(i, s, zl):
    z = (zl[i] + zl[i + 1]) / 2
    a = 2 * math.pi * (s + 0.5) / SEG
    return abs(z - SPOKE_Z) < SPOKE_H / 2 and near_spoke(a, SR)

bm = bmesh.new()
prof = [(SR, z) for z in zs]
revolve(bm, prof, SEG, lambda i, s: 1 if (i % 4 == 1) else 0, closed=False,
        toward=lambda c: radial(c), skip=lambda i, s: spine_door(i, s, zs))
obj("Spine_Hull", bm, ["Hull", "HullDark"], smooth=True, uv="cyl")

bm = bmesh.new()
revolve(bm, [(SRI, z) for z in zs], SEG, closed=False, toward=lambda c: -radial(c), skip=lambda i, s: spine_door(i, s, zs))
obj("Spine_Interior-col", bm, ["Wall"], smooth=True, uv="cyl")

bm = bmesh.new()
for z in DECKS:
    # floor annulus with a hatch at the centre for the lift; top faces up, underside down, rims outward
    revolve(bm, [(1.8, z), (SRI, z)], SEG, closed=False, toward=lambda c: Vector((0, 0, 1)))
    revolve(bm, [(1.8, z - 0.3), (SRI, z - 0.3)], SEG, closed=False, toward=lambda c: Vector((0, 0, -1)), mat_of=lambda i, s: 1)
    revolve(bm, [(1.8, z - 0.3), (1.8, z)], SEG, closed=False, toward=lambda c: -radial(c), mat_of=lambda i, s: 1)
obj("Spine_Decks-col", bm, ["Floor", "Ceiling"])

bm = bmesh.new()
# lift shaft: an open cage of four rails and rings; the car lives at the bottom deck
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    cyl(bm, 0.08, 0.08, Z1 - Z0, (1.3 * math.cos(a), 1.3 * math.sin(a), (Z0 + Z1) / 2), seg=8)
for d in DECKS + [Z1]:
    for zz in (d + 0.05, d + 2.8):
        cyl(bm, 1.5, 1.5, 0.1, (0, 0, zz), seg=24, caps=False)
obj("Spine_LiftShaft", bm, ["HullDark"])
bm = bmesh.new()
box(bm, (2.0, 2.0, 0.15), (0, 0, DECKS[0] + 0.08), mi=0)
box(bm, (2.0, 2.0, 0.08), (0, 0, DECKS[0] + 2.7), mi=1)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    box(bm, (0.1, 0.1, 2.6), (1.0 * math.cos(a), 1.0 * math.sin(a), DECKS[0] + 1.4), mi=2)
liftcar = obj("LiftCar-col", bm, ["Floor", "LightCool", "HullDark"])

bm = bmesh.new()
# light rings under every deck, facing in
for z in DECKS[1:] + [Z1 - 0.3]:
    revolve(bm, [(SRI - 0.05, z - 0.75), (SRI - 0.05, z - 0.45)], SEG, closed=False, toward=lambda c: -radial(c))
obj("Spine_Lights", bm, ["LightWarm"])

bm = bmesh.new()
# portholes in the outer skin: a lit pane in a dark frame
for z in [d + 1.6 for d in DECKS if abs(d - DECKS[2]) > 0.1]:
    for k in range(16):
        a = (k + 0.5) * 2 * math.pi / 16
        rot = Rz(a) @ Ry(math.pi / 2)
        cyl(bm, 0.7, 0.7, 0.16, ((SR + 0.04) * math.cos(a), (SR + 0.04) * math.sin(a), z), rot, seg=16, mi=0)
        cyl(bm, 0.5, 0.5, 0.2, ((SR + 0.06) * math.cos(a), (SR + 0.06) * math.sin(a), z), rot, seg=16, mi=1)
obj("Spine_Portholes", bm, ["HullDark", "Window"])

bm = bmesh.new()
# outside the spine: four vertical conduits with clamps, and handrail rungs
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    p = Vector(((SR + 0.45) * math.cos(a), (SR + 0.45) * math.sin(a), 0))
    cyl(bm, 0.28, 0.28, Z1 - Z0 - 2, (p.x, p.y, (Z0 + Z1) / 2), seg=10, mi=0)
    for j in range(13):
        z = Z0 + 3 + j * 4.3
        cyl(bm, 0.38, 0.38, 0.35, (p.x, p.y, z), seg=10, mi=1)
    for j in range(20):
        b = a + 0.12
        q = Vector(((SR + 0.2) * math.cos(b), (SR + 0.2) * math.sin(b), Z0 + 2 + j * 2.6))
        box(bm, (0.06, 0.06, 0.6), q, Rz(b) @ Ry(math.pi / 2), mi=2)
obj("Spine_Conduits", bm, ["HullDark", "Hull", "Trim"], uv="box")

# deck furnishing: lockers along the wall (clear of the doorways), tables, crates
bm = bmesh.new()
for di, z in enumerate(DECKS):
    for k in range(12):
        a = (k + 0.5) * 2 * math.pi / 12
        if di == 2 and near_spoke(a, SRI - 0.4) or any(abs(((a - sa + math.pi) % (2 * math.pi)) - math.pi) < 0.45 for sa in SPOKE_ANGLES) and di == 2:
            continue
        box(bm, (0.6, 1.5, 2.2), ((SRI - 0.35) * math.cos(a), (SRI - 0.35) * math.sin(a), z + 1.1), Rz(a), mi=0)
    if di in (1, 3):
        for k in range(3):
            a = k * 2 * math.pi / 3 + 0.5
            c = Vector((4.2 * math.cos(a), 4.2 * math.sin(a), z))
            box(bm, (1.6, 0.9, 0.08), c + Vector((0, 0, 0.78)), Rz(a), mi=1)
            box(bm, (0.15, 0.15, 0.78), c + Vector((0, 0, 0.39)), mi=1)
    if di == 0:
        for k in range(6):
            a = k * 1.1
            box(bm, (1.2, 1.2, 1.2), (4.5 * math.cos(a), 4.5 * math.sin(a), z + 0.6), Rz(a * 2), mi=2)
obj("Spine_Furniture-col", bm, ["Wall", "HullDark", "Trim"])

# ================================================================== COMMAND DOME
bm = bmesh.new()
res = bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=SR)
bmesh.ops.delete(bm, geom=[v for v in res["verts"] if v.co.z < -0.01], context="VERTS")
bmesh.ops.translate(bm, vec=Vector((0, 0, Z1)), verts=bm.verts[:])
for f in bm.faces:
    c = f.calc_center_median()
    ang = math.atan2(c.y, c.x)
    f.material_index = 0 if (abs(math.sin(ang * 6)) < 0.09 or (c.z - Z1) < 1.0 or (c.z - Z1) > 7.6) else 1
obj("Dome", bm, ["Hull", "Glass"], smooth=True, uv="cyl")
bm = bmesh.new()
revolve(bm, [(1.8, Z1), (SRI, Z1)], SEG, closed=False, toward=lambda c: Vector((0, 0, 1)))
obj("Dome_Floor-col", bm, ["Floor"])
bm = bmesh.new()
for k in range(12):
    a = k * 2 * math.pi / 12
    c = Vector((5.4 * math.cos(a), 5.4 * math.sin(a), Z1))
    box(bm, (0.7, 1.4, 0.9), c + Vector((0, 0, 0.45)), Rz(a), mi=0)
    box(bm, (0.05, 1.2, 0.7), c + Vector((0.3 * math.cos(a), 0.3 * math.sin(a), 1.3)), Rz(a) @ Ry(-0.3), mi=1)
for k in range(3):
    a = k * 2 * math.pi / 3 + 0.3
    c = Vector((3.0 * math.cos(a), 3.0 * math.sin(a), Z1))
    cyl(bm, 0.3, 0.3, 0.5, c + Vector((0, 0, 0.25)), seg=12, mi=0)
    box(bm, (0.6, 0.6, 0.12), c + Vector((0, 0, 0.55)), mi=2)
obj("Dome_Consoles-col", bm, ["HullDark", "Screen", "Fabric"])

# ================================================================== HABITAT RING
# Profiles in (r, z). Side walls are split into bands so the middle band can be windows and the
# ceiling into thirds so the spoke openings can be cut.
ZS = SPOKE_H / 2
def ring_angle(s): return 2 * math.pi * (s + 0.5) / STEPS
WIN = lambda s: s % 6 not in (0, 5)
TUBE_C = (RO + RI) / 2
tube_out = tube_dir(TUBE_C)       # direction from the ring tube's centreline out to a point
C = 1.4                            # chamfer on the hull's four long edges

def edge_class(prof, i, r_hull):
    """What a profile edge is, from where it sits: 'window', 'spoke' (the hole a spoke enters through), 'outer', 'other'."""
    (r0, z0), (r1, z1) = prof[i], prof[(i + 1) % len(prof)]
    rm, zm = (r0 + r1) / 2, (z0 + z1) / 2
    if abs(z0 - z1) < 1e-6 and SKY0 < rm < SKY1 and zm > 0: return "window"
    if abs(r0 - r1) < 1e-6 and abs(r0 - r_hull) < 1e-6 and abs(zm - SPOKE_Z) < ZS - 1e-6: return "spoke"
    if abs(r0 - r1) < 1e-6 and r0 > RO - 1e-6: return "outer"
    if abs(r0 - r1) < 1e-6 and abs(r0 - r_hull) < 1e-6: return "ceiling"
    return "other"

# outer skin: chamfered on all four long edges; side faces carry the window band, the hub face the spoke holes
H2, RH = HW + T, RI - T
outer = [(RO + T, -H2 + C), (RO + T, H2 - C), (RO + T - C, H2),
         (SKY1, H2), (SKY0, H2), (RH + C, H2), (RH, H2 - C),
         (RH, SPOKE_Z + ZS), (RH, SPOKE_Z - ZS), (RH, -H2 + C), (RH + C, -H2),
         (RO + T - C, -H2)]
OCLS = [edge_class(outer, i, RH) for i in range(len(outer))]
def outer_mat(i, s):
    k = OCLS[i]
    if k == "window": return 3
    if k == "outer": return 2 if s % 24 == 3 else 0       # registry stripes, four round the wheel
    return 1 if k in ("ceiling", "spoke") else 0
bm = bmesh.new()
revolve(bm, outer, STEPS, outer_mat, toward=tube_out, skip=lambda i, s: OCLS[i] in ("outer", "ceiling", "spoke"))

# REV B windows: openings in the outer face (both decks) and the hub face (Deck A), one per frame bay, from openings.json
BAY = 2 * math.pi / STEPS
OCT_C, OCT_F, LIP, DG = 0.3, 0.24, 0.12, 0.45      # corner chamfer, frame width, frame lip, glass set back from the skin
FFL_ = {"A": -0.2, "B": -3.5}; CLG_ = {"A": 3.0, "B": -0.9}
WIN_BY = {"outer": {}, "inner": {}}
for side in ("outer", "inner"):
    for w in OPEN[side]: WIN_BY[side].setdefault(int(w["a"] // 3.75), []).append(w)

def bay_plane(s_, r):
    am = (s_ + 0.5) * BAY
    d = Vector((math.cos(am), math.sin(am), 0)); t = Vector((-d.y, d.x, 0))
    return d * (r * math.cos(BAY / 2)), d, t, r * math.sin(BAY / 2)

def face_with_holes(bm, c, t, n, U, zlo, zhi, holes, mi):
    """A flat bay of hull from u -U..U, z zlo..zhi, with rectangular holes (u0, u1, z0, z1), facing n."""
    zs = sorted({zlo, zhi} | {h[2] for h in holes} | {h[3] for h in holes})
    for za, zb in zip(zs, zs[1:]):
        hs = sorted(h for h in holes if h[2] <= za + 1e-6 and h[3] >= zb - 1e-6)
        us = [-U] + [x for h in hs for x in (h[0], h[1])] + [U]
        for ua, ub in zip(us[::2], us[1::2]):
            if ub - ua < 1e-4: continue
            f = bm.faces.new([bm.verts.new(c + t * u + Vector((0, 0, z))) for u, z in ((ua, za), (ub, za), (ub, zb), (ua, zb))])
            f.material_index = mi; f.normal_update()
            if f.normal.dot(n) < 0: f.normal_flip()

for s_ in range(STEPS):
    c, d, t, U = bay_plane(s_, RO + T)
    holes = [(-w["w"] / 2, w["w"] / 2, w["z0"], w["z1"]) for w in WIN_BY["outer"].get(s_, [])]
    face_with_holes(bm, c, t, d, U, -H2 + C, H2 - C, holes, 2 if s_ % 24 == 3 else 0)
    c, d, t, U = bay_plane(s_, RH)
    holes = [(-w["w"] / 2, w["w"] / 2, w["z0"], w["z1"]) for w in WIN_BY["inner"].get(s_, [])]
    if near_spoke(ring_angle(s_), RI): holes.append((-U, U, SPOKE_Z - ZS, SPOKE_Z + ZS))
    face_with_holes(bm, c, t, -d, U, -H2 + C, H2 - C, holes, 1)
obj("Ring_Hull", bm, ["Hull", "HullDark", "Trim", "WinGlass"], uv="cyl")

def oct8(A, B, K):
    return [(-(K - B), -B), (K - B, -B), (A, -(K - A)), (A, K - A), (K - B, B), (-(K - B), B), (-A, K - A), (-A, -(K - A))]

def window_unit(bm, bmr, c, t, n, w, z0, z1, deck, rnd, U):
    """An angled, chamfered frame round the opening, a recessed reveal, glass with mullions, and behind it (bmr) a shallow
    room: floor, walls, ceiling light, a desk with a screen, sometimes a blind. n points out of the hull."""
    A, B, zc = w / 2, (z1 - z0) / 2, (z0 + z1) / 2
    K = A + B - OCT_C
    inner, outer8 = oct8(A, B, K), oct8(A + OCT_F, B + OCT_F, K + OCT_F * math.sqrt(2))
    P3 = lambda u, v, dd: c + t * u + Vector((0, 0, zc + v)) + n * dd
    ring = lambda pts, dd: [bm.verts.new(P3(u, v, dd)) for u, v in pts]
    lo, hi, gl = ring(outer8, 0.01), ring(inner, LIP), ring(inner, -DG)
    axis = lambda dd: P3(0, 0, dd)
    for i in range(8):
        j = (i + 1) % 8
        f = bm.faces.new((lo[i], lo[j], hi[j], hi[i])); f.material_index = 0; f.normal_update()
        if f.normal.dot(n) < 0: f.normal_flip()
        f = bm.faces.new((hi[i], hi[j], gl[j], gl[i])); f.material_index = 0; f.normal_update()
        fc = f.calc_center_median(); to_axis = axis(0) - fc; to_axis -= n * to_axis.dot(n)
        if f.normal.dot(to_axis) < 0: f.normal_flip()
    f = bm.faces.new(gl); f.material_index = 1; f.normal_update()
    if f.normal.dot(n) < 0: f.normal_flip()
    R3 = Matrix((t, Vector((0, 0, 1)), n)).transposed().to_4x4()
    m = max(1, round(w / 0.6) - 1)
    for k in range(m):
        box(bm, (0.07, 2 * B, 0.1), P3(-A + w * (k + 1) / (m + 1), 0, -DG + 0.05), R3, mi=0)
    box(bm, (w, 0.06, 0.1), P3(0, -B + 2 * B * 0.62, -DG + 0.05), R3, mi=0)
    # the room behind: bigger than the opening (the skin hides the rest), so it shows parallax as you fly past
    ffl, clg = FFL_[deck], CLG_[deck]
    D_ = 2.6
    lit = rnd.random()
    wall = 2 if lit < 0.15 else (1 if lit < 0.4 else 0)       # RoomDim / RoomCool / RoomWarm
    Q = lambda u, z, dd: c + t * u + Vector((0, 0, z)) + n * dd
    ua, ub, d0, d1 = -U + 0.05, U - 0.05, -DG - 0.02, -DG - D_
    quads = [((ua, ffl, d0), (ub, ffl, d0), (ub, ffl, d1), (ua, ffl, d1), 3),            # floor
             ((ua, clg, d0), (ua, clg, d1), (ub, clg, d1), (ub, clg, d0), wall),         # ceiling
             ((ua, ffl, d1), (ub, ffl, d1), (ub, clg, d1), (ua, clg, d1), wall),         # back wall
             ((ua, ffl, d0), (ua, ffl, d1), (ua, clg, d1), (ua, clg, d0), wall),         # sides
             ((ub, ffl, d0), (ub, clg, d0), (ub, clg, d1), (ub, ffl, d1), wall)]
    centre = Q(0, (ffl + clg) / 2, (d0 + d1) / 2)
    for *pts, mi_ in quads:
        f = bmr.faces.new([bmr.verts.new(Q(*q)) for q in pts]); f.material_index = mi_; f.normal_update()
        if f.normal.dot(centre - f.calc_center_median()) < 0: f.normal_flip()
    if wall != 2:
        box(bmr, (0.6, 0.03, 0.3), Q(0, clg - 0.02, (d0 + d1) / 2), R3, mi=5)                   # ceiling light
        sd = 1 if rnd.random() < 0.5 else -1
        box(bmr, (0.55, 0.05, 1.1), Q(sd * (U - 0.35), ffl + 0.75, d1 + 0.9), R3, mi=4)         # desk
        box(bmr, (0.04, 0.4, 0.6), Q(sd * (U - 0.1), ffl + 1.2, d1 + 0.9), R3, mi=7)            # wall screen
        box(bmr, (0.6, 2.0, 0.6), Q(-sd * (U - 0.35), ffl + 1.0, d1 + 0.35), R3, mi=4)          # wardrobe
        if rnd.random() < 0.3:
            box(bmr, (w, 2 * B * rnd.uniform(0.25, 0.7), 0.02), Q(0, z1 - B * 0.4, -DG - 0.06), R3, mi=6)   # blind

import random as _rnd
_r = _rnd.Random(11)
bm = bmesh.new(); bmr = bmesh.new()
for side, r_ in (("outer", RO + T), ("inner", RH)):
    for s_, ws in WIN_BY[side].items():
        c, d, t, U = bay_plane(s_, r_)
        n = d if side == "outer" else -d
        for w in ws: window_unit(bm, bmr, c, t if side == "outer" else -t, n, w["w"], w["z0"], w["z1"], w["deck"], _r, U)
# under every skylight: a glimpse of the lit corridor below (the furnished build replaces this with the real one)
for s_ in range(STEPS):
    a0, a1 = s_ * BAY, (s_ + 1) * BAY
    pts = lambda r, z: [Vector((r * math.cos(a), r * math.sin(a), z)) for a in (a0, a1)]
    for (p0, p1), (q0, q1), mi_ in ((pts(SKY0, -0.2), pts(SKY1, -0.2), 3), (pts(SKY0, -0.2), pts(SKY0, HW + T), 8), (pts(SKY1, -0.2), pts(SKY1, HW + T), 8)):
        f = bmr.faces.new([bmr.verts.new(v) for v in (p0, p1, q1, q0)]); f.material_index = mi_; f.normal_update()
        mid = Vector((((SKY0 + SKY1) / 2) * math.cos((a0 + a1) / 2), ((SKY0 + SKY1) / 2) * math.sin((a0 + a1) / 2), 1.5))
        if f.normal.dot(mid - f.calc_center_median()) < 0: f.normal_flip()
obj("Ring_Windows", bm, ["WinFrame", "WinGlass"])
obj("Ring_Rooms", bmr, ["RoomWarm", "RoomCool", "RoomDim", "RoomFloor", "RoomFurn", "LightWarm", "Blind", "Screen", "RoomWall"])

bm = bmesh.new()
# skylight mullions: a bar across the band at every frame
for s_ in range(STEPS):
    rot = Rz(2 * math.pi * s_ / STEPS)
    box(bm, (SKY1 - SKY0 + 0.3, 0.3, 0.25), rot @ Vector(((SKY0 + SKY1) / 2, 0, HW + T + 0.05)), rot)
obj("Ring_Mullions", bm, ["HullDark"])

bm = bmesh.new()
# outside the ring: ribs every 15 degrees wrapping the tube, conduits along both faces, antennas
# ribs: bands wrapping the outside of the hull (hollow: the corridor runs straight through them)
RIB = [(r + (r - TUBE_C) / math.hypot(r - TUBE_C, z) * 0.45, z + z / math.hypot(r - TUBE_C, z) * 0.45) for r, z in outer]
for k in range(24):
    a0 = k * 2 * math.pi / 24 + math.pi / 24; a1 = a0 + 0.7 / RO
    def ring_at(prof, a): return [bm.verts.new((r * math.cos(a), r * math.sin(a), z)) for r, z in prof]
    o0, o1, i0, i1 = ring_at(RIB, a0), ring_at(RIB, a1), ring_at(outer, a0), ring_at(outer, a1)
    made = []
    n = len(outer)
    for i in range(n):
        j = (i + 1) % n
        made.append(bm.faces.new((o0[i], o0[j], o1[j], o1[i])))          # the band's outer skin
        made.append(bm.faces.new((o0[i], i0[i], i0[j], o0[j])))          # its two edges, down to the hull
        made.append(bm.faces.new((o1[i], o1[j], i1[j], i1[i])))
    bm.normal_update()
    mid = (a0 + a1) / 2
    for f in made:
        c = f.calc_center_median()
        ca = math.atan2(c.y, c.x)
        if abs(((ca - mid + math.pi) % (2 * math.pi)) - math.pi) > (a1 - a0) * 0.45:     # an edge face: point away from the band's middle
            d = Vector((-math.sin(ca), math.cos(ca), 0)) * (1 if ((ca - mid + math.pi) % (2 * math.pi)) - math.pi > 0 else -1)
        else:
            d = tube_out(c)
        if f.normal.dot(d) < 0: f.normal_flip()
for sz in (-1, 1):
    for r in (RI + 0.8, RO - 0.6):
        revolve(bm, [(r - 0.25, sz * (HW + T + 0.02)), (r + 0.25, sz * (HW + T + 0.02))], STEPS, closed=False,
                toward=lambda c, sz=sz: Vector((0, 0, sz)), mat_of=lambda i, s: 1)
for k in range(8):
    a = k * 2 * math.pi / 8 + 0.3
    cyl(bm, 0.08, 0.04, 4.0, Rz(a) @ Vector((RO + T + 2.0, 0, HW)), Rz(a) @ Ry(math.pi / 2), seg=6, mi=1)
obj("Ring_Exterior", bm, ["HullDark", "Hull"])

# ================================================================== SPOKES
bm = bmesh.new()
bmi = bmesh.new()
for a in SPOKE_ANGLES:
    rot = Rz(a)
    r0, r1 = SRI, RI
    L = r1 - r0
    mid = rot @ Vector((r0 + L / 2, 0, SPOKE_Z))
    w, h, t = SPOKE_W, SPOKE_H, 0.25
    tunnel(bm, (L, w, h), t, mid, rot, mi_out=2, mi_in=0, mi_floor=1)
    # a level walkway (gravity -Z): handrails both sides, a light strip overhead
    for sy in (-1, 1):
        box(bmi, (L, 0.06, 0.06), rot @ Vector((r0 + L / 2, sy * (w / 2 - 0.4), SPOKE_Z - h / 2 + 0.95)), rot, mi=0)
    box(bmi, (L, 0.3, 0.04), mid + Vector((0, 0, h / 2 - 0.02)), rot, mi=1)
    # outside: a structural strut above and below each spoke
    for sz in (-1, 1):
        box(bmi, (RI - SR, 0.5, 0.5), rot @ Vector(((RI + SR) / 2, 0, SPOKE_Z + sz * 3.2)), rot, mi=2)
obj("Spokes-col", bm, ["Wall", "Floor", "Hull"])
obj("Spokes_Detail", bmi, ["HullDark", "LightCool", "Hull"])

# ================================================================== DOCKING HUB
bm = bmesh.new()
cyl(bm, 10.0, 10.0, 6.0, (0, 0, Z0 - 3.0), seg=48, mi=0)
cyl(bm, 10.4, 10.4, 0.6, (0, 0, Z0 - 0.5), seg=48, mi=1)
cyl(bm, 10.4, 10.4, 0.6, (0, 0, Z0 - 5.5), seg=48, mi=1)
cyl(bm, 6.0, 3.0, 4.0, (0, 0, Z0 - 8.0), seg=32, mi=0)
cyl(bm, 2.5, 2.5, 3.0, (0, 0, Z0 - 11.0), seg=24, mi=1)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    rot = Rz(a)
    cyl(bm, 1.8, 1.8, 14.0, rot @ Vector((17.0, 0, Z0 - 3.0)), rot @ Ry(math.pi / 2), seg=16, mi=0)
    for j in range(4):
        cyl(bm, 2.0, 2.0, 0.4, rot @ Vector((11.5 + j * 3.5, 0, Z0 - 3.0)), rot @ Ry(math.pi / 2), seg=16, mi=1)
    cyl(bm, 3.0, 3.0, 1.2, rot @ Vector((24.6, 0, Z0 - 3.0)), rot @ Ry(math.pi / 2), seg=24, mi=1)
    cyl(bm, 2.2, 2.2, 0.2, rot @ Vector((25.25, 0, Z0 - 3.0)), rot @ Ry(math.pi / 2), seg=24, mi=2)
obj("Dock_Hub-col", bm, ["Hull", "HullDark", "Trim"])

bm = bmesh.new()
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    rot = Rz(a)
    for j in range(8):
        b = j * 2 * math.pi / 8
        sphere(bm, 0.18, rot @ Vector((25.3, 3.1 * math.cos(b), Z0 - 3.0 + 3.1 * math.sin(b))), mi=0 if k % 2 else 1, seg=8)
obj("Dock_Lights", bm, ["NavGreen", "NavRed"])

# a shuttle moored at the first collar
bm = bmesh.new()
box(bm, (14.0, 5.0, 2.4), (0, 0, 0), mi=0)
cyl(bm, 0.3, 2.4, 4.0, (9.0, 0, 0), Ry(-math.pi / 2), seg=12, mi=0)
box(bm, (6.0, 13.0, 0.4), (-3.5, 0, -0.6), mi=0)
box(bm, (3.0, 0.4, 3.0), (-5.5, 0, 2.2), mi=1)
for sy in (-1.4, 1.4):
    cyl(bm, 0.9, 1.2, 2.2, (-8.0, sy, 0), Ry(math.pi / 2), seg=16, mi=1)
    cyl(bm, 0.75, 0.75, 0.1, (-9.15, sy, 0), Ry(math.pi / 2), seg=16, mi=3)
box(bm, (2.4, 3.6, 0.6), (8.2, 0, 1.0), Ry(0.35), mi=2)
box(bm, (0.1, 0.6, 0.4), (2.0, 2.55, 0.3), mi=4)
box(bm, (0.1, 0.6, 0.4), (2.0, -2.55, 0.3), mi=4)
sh = obj("Shuttle-col", bm, ["Hull", "HullDark", "Glass", "LightCool", "Window"])
a = math.pi / 4
sh.location = Rz(a) @ Vector((25.6 + 9.6, 0, Z0 - 3.0))
sh.rotation_euler = (0, 0, a + math.pi)

# ================================================================== SOLAR WINGS AND RADIATORS
bm = bmesh.new()
bmp = bmesh.new()
for side in (-1, 1):
    for (dy, dz) in [(-0.8, -0.8), (-0.8, 0.8), (0.8, -0.8), (0.8, 0.8)]:
        cyl(bm, 0.12, 0.12, 52.0, (side * (SR + 26.0), dy, 22.0 + dz), Ry(math.pi / 2), seg=6)
    for j in range(26):
        x = side * (SR + 1.0 + j * 2.0)
        box(bm, (0.08, 1.6, 0.08), (x, 0, 21.2)); box(bm, (0.08, 1.6, 0.08), (x, 0, 22.8))
        box(bm, (0.08, 0.08, 1.6), (x, -0.8, 22.0)); box(bm, (0.08, 0.08, 1.6), (x, 0.8, 22.0))
        box(bm, (2.2, 0.06, 0.06), (x + side, -0.8, 22.0), Ry(math.pi / 4))
    for j in range(4):
        x = side * (SR + 8.0 + j * 12.0)
        for sz in (-1, 1):
            # a panel of cells: dark backing, a grid of cells raised just proud of it
            box(bmp, (11.0, 0.1, 9.0), (x, 0, 22.0 + sz * 5.6), Rx(0.25), mi=1)
            for cx in range(6):
                for cz in range(5):
                    box(bmp, (1.7, 0.12, 1.65), (x - 4.6 + cx * 1.84, 0.0, 22.0 + sz * 5.6 - 3.6 + cz * 1.8), Rx(0.25), mi=0)
    for j in range(3):
        y = side * (SR + 4.0 + j * 7.0)
        box(bmp, (0.15, 6.0, 12.0), (0, y, -12.0), mi=2)
        span = abs(y) - SR - 0.1                       # coolant pipe from the spine's skin out to the fin
        cyl(bm, 0.3, 0.3, span, (0, side * (SR + 0.1 + span / 2), -12.0), Rx(math.pi / 2), seg=8)
obj("Truss", bm, ["HullDark"])
obj("Panels", bmp, ["Solar", "HullDark", "Radiator"])

bm = bmesh.new()
cyl(bm, 0.25, 0.25, 14.0, (5.0, 5.0, Z1 + 4.0), seg=8, mi=0)
res = bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=16, radius=3.5)
dish = [v for v in res["verts"]]
bmesh.ops.delete(bm, geom=[v for v in dish if v.co.z > -1.2], context="VERTS")
rest = [v for v in bm.verts if v.is_valid and v not in set()]
bmesh.ops.transform(bm, matrix=Matrix.Translation(Vector((5.0, 5.0, Z1 + 12.5))) @ Rx(-2.2), verts=[v for v in bm.verts if v.co.z <= -1.2])
cyl(bm, 0.12, 0.05, 3.0, (5.0, 5.0 + 1.6, Z1 + 11.5), Rx(-0.6), seg=6, mi=0)
obj("Comms", bm, ["Hull"], smooth=True)
bm = bmesh.new()
sphere(bm, 0.3, (5.0, 5.0, Z1 + 11.2), mi=2)
for k in range(8):
    a = k * math.pi / 4
    sphere(bm, 0.3, ((RO + T + 0.55) * math.cos(a), (RO + T + 0.55) * math.sin(a), 0), mi=0 if k < 4 else 1, seg=8)
for side in (-1, 1):
    sphere(bm, 0.35, (side * (SR + 52.0), 0, 22.0), mi=2, seg=8)
obj("NavLights", bm, ["NavRed", "NavGreen", "NavWhite"])

bm = bmesh.new()
for k in range(5):
    a = 0.3 + k * 0.03
    box(bm, (0.1, 1.4, 1.6), Rz(a) @ Vector((RO + T + 0.02, 0, 0)), Rz(a), mi=0)
obj("Registry", bm, ["NavWhite"])

# ================================================================== LIGHTS (exported as KHR_lights_punctual)
for di, z in enumerate(DECKS):
    top = (DECKS[di + 1] - 0.4) if di + 1 < len(DECKS) else Z1 - 0.4
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        lamp(f"L_Deck{di}_{k}", (4.5 * math.cos(a), 4.5 * math.sin(a), min(z + 3.2, top)), 500, WARM, 0.8)
for a in SPOKE_ANGLES:
    for j in range(3):
        lamp(f"L_Spoke{int(math.degrees(a))}_{j}", Rz(a) @ Vector((SRI + 6 + j * 9, 0, SPOKE_Z + 1.2)), 120, COOL, 0.3)
lamp("L_Dome", (0, 0, Z1 + 5.0), 2500, COOL, 2.0)
for k in range(4):
    a = k * math.pi / 2
    lamp(f"L_DomeConsole{k}", (3.0 * math.cos(a), 3.0 * math.sin(a), Z1 + 2.6), 300, WARM, 0.5)
lamp("L_DockFlood", (30, -30, Z0 - 15), 30000, (1, 0.95, 0.85), 1, "SPOT", aim=(0, 0, Z0 - 3), spot=0.6)


# ================================================================== KITBASH DETAIL (outside only)
bm = bmesh.new()
# the ring: outer face, both side faces either side of the window band, and the hub-facing face
# outer face: between the Deck B and Deck A window rows only (windows: openings.json)
detail(bm, cyl_surface(RO + T, -1.3, 0.4), 11, mats=(0, 1), glow=3, glow_frac=0.01)
for sz in (-1, 1):
    for (r0, r1, sd) in ((RH + C + 0.3, SKY0 - 0.3, 20), (SKY1 + 0.3, RO + T - C - 0.3, 30)):
        detail(bm, disc_surface(r0, r1, sz * (HW + T), up=sz > 0), sd + sz, mats=(0, 1))
detail(bm, cyl_surface(RI - T, -H2 + C + 0.3, SPOKE_Z - ZS - 0.3, inward=True), 41, mats=(0, 1))
# the spine, between its porthole rows and clear of the spoke deck
for (z0, z1) in ((Z0 + 1, DECKS[0] + 0.8), (DECKS[0] + 2.4, DECKS[1] + 0.8), (DECKS[1] + 2.4, SPOKE_Z - 2.2),
                 (SPOKE_Z + 2.2, DECKS[3] + 0.8), (DECKS[3] + 2.4, DECKS[4] + 0.8), (DECKS[4] + 2.4, Z1 - 0.5)):
    detail(bm, cyl_surface(SR + 0.25, z0, z1), int(abs(z0) * 7) % 997, mats=(0, 1), window=2, window_frac=0.03, pipes=0.05)
# the docking hub
detail(bm, cyl_surface(10.0, Z0 - 5.0, Z0 - 1.0), 61, mats=(0, 1), window=2, window_frac=0.04)
detail(bm, disc_surface(6.5, 9.8, Z0 - 6.0, up=False), 63, mats=(0, 1), glow=3, glow_frac=0.03)
obj("Greebles", bm, ["Hull", "HullDark", "Window", "Glow"])

bm = bmesh.new()
# antenna farms: on the dome's crown, under the docking hub, and at four points round the ring
antennas(bm, (0, 0, Z1 + SR - 0.3), (0, 0, 1), 7, 9.0, 3, mi=0, light=1)
antennas(bm, (0, 0, Z0 - 12.5), (0, 0, -1), 5, 7.0, 4, mi=0, light=1)
for k in range(4):
    a = k * math.pi / 2 + math.pi / 4
    antennas(bm, Rz(a) @ Vector((RO - 1.6, 0, HW + T)), (0, 0, 1), 4, 5.0, 10 + k, mi=0, light=1)
obj("Antennas", bm, ["HullDark", "NavRed"])

bm = bmesh.new()
# floodlights along the ring's top edge that wash the hull
for k in range(48):
    a = 2 * math.pi * (k + 0.5) / 48
    box(bm, (0.25, 0.15, 0.12), Rz(a) @ Vector((RO + T + 0.3, 0, HW + T + 0.1)), Rz(a), mi=0)
obj("Ring_Floods", bm, ["LightCool"])

bm = bmesh.new()
for a in (math.pi / 4 + 0.35, 5 * math.pi / 4 + 0.35):
    d = Vector((math.cos(a), math.sin(a), 0))
    bay = hangar(bm, d * (RO + T) + Vector((0, 0, Z_DECK_B + 3.0)), d, (0, 0, 1), 12.0, 6.0, 14.0, mats=(0, 1, 2, 3, 4), seed=int(a * 10))   # floor = Deck B
    lamp(f"L_Hangar{int(a * 10)}", bay, 1500, COOL, 2.0)
obj("Hangars-col", bm, ["Hull", "HullDark", "LightCool", "NavWhite", "Glow"])


# ================================================================ v2 starbase pass (asset overhaul, docs/ASSET_REFERENCES.md)
AM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar"]
A_HULL, A_DARK, A_LIGHT, A_ACC, A_HAZ, A_WIN, A_WCOOL, A_STROBE, A_RAD, A_GLOW, A_RED, A_SOLAR = range(len(AM))
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
restyle(col)
bm = bmesh.new()

# warm strips round the wheel, strobes at the spoke roots and the tips, a tank farm and radiators on the keel, a tug in
strip_lights(bm, RO + T, -0.45, 120, A_GLOW, seed=1, h=0.25, depth=0.15)      # deck line between the window rows
for k in range(8):
    a = k * math.pi / 4 + math.pi / 8
    strobe(bm, Vector((math.cos(a), math.sin(a), 0)) * (RO + T + 0.4) + Z_ * (HW + T + 0.4), 0.35, A_RED if k % 2 else A_STROBE)
for z in (Z1 + 0.5, Z0 - 0.5): strobe(bm, Z_ * z + X_ * SR, 0.4, A_RED)
for k in range(3):
    a = k * 2 * math.pi / 3 + 0.3
    d = Vector((math.cos(a), math.sin(a), 0))
    tank_cage(bm, d * (SR + 3.2) + Z_ * (Z0 + 8), 2.6, axis=Z_, capsule=1.4, mi_tank=A_HULL, mi_cage=A_DARK)
for d in (X_ + Y_, -X_ - Y_):
    d = d.normalized()
    radiator_bank(bm, d * SR + Z_ * (Z0 + 14), d, Z_, 5, 9, 4, 1.4, mi_fin=A_RAD, mi_man=A_DARK)
obj("Fittings2", bm, AM)

result = export(col, root, "outpost")
