"""
Shipyard slip, 520 m: one building berth of a colony's yard, an open cradle a hull is built inside (the game sets
the growing hull in its middle). References (assets/stations/renders/refs/yard_pyatov.png, the ZEO orbital drydock
and the "Orbital Drydock Facility 34" concept): a row of heavy octagonal frames on four longerons, floodlights on the
frames' inner faces aimed at the hull, clamp arms reaching in, a gantry crane riding the top longerons, a control
block at the stern. The two mouth frames are painted in the owner's colour. About 8,000 triangles, the heaviest prop:
a system has at most seven slips.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/war/slip.py").read())
"""
exec(open(globals().get("SF_ROOT", r"C:/Users/diamo/Desktop/starforge") + "/assets/ships/war/warkit.py").read())
col, root = setup("WarSlip")
hull_scale(400)
bm = bmesh.new()
L, R, T, D = 520.0, 52.0, 13.0, 22.0          # length along X, frame inner apothem, frame thickness, frame depth
NF = 7
X = Vector((1, 0, 0))


def side_frame(a):
    """Orientation of an octagon side at angle a: local x along the cradle, z radially out (right-handed)."""
    z = Vector((0, math.cos(a), math.sin(a)))
    y = z.cross(X)
    return Matrix((X, y, z)).transposed()


def frame(x, mi, owner=False):
    side = 2 * (R + T) * math.tan(math.pi / 8)
    for k in range(8):
        a = k * math.pi / 4
        F = side_frame(a)
        n = F.col[2]
        fast_box(bm, (D, side, T), Vector((x, 0, 0)) + n * (R + T / 2), F, mi, UVL(bm), bottom=True)
        # a band of lit windows round the frame's face, both sides (the frames are crewed)
        for sx in (-1, 1):
            for o in (-0.27, 0.0, 0.27):
                fast_box(bm, (0.4, side * 0.18, 1.6), Vector((x + sx * D / 2, 0, 0)) + n * (R + T * 0.62) + F.col[1] * side * o, F, WIN, UVL(bm))
        if owner:
            fast_box(bm, (D * 0.85, side * 0.9, 0.8), Vector((x, 0, 0)) + n * (R + T + 0.3), F, OWN, UVL(bm))
        if k % 2 == 0:
            # floodlights on the inner face, aimed at the hull
            for o in (-0.25, 0.25):
                fast_box(bm, (2.4, 3.0, 1.2), Vector((x, 0, 0)) + n * (R - 0.6) + F.col[1] * side * o, F, WIN, UVL(bm), bottom=True)
        else:
            # a clamp arm reaching in from each diagonal side
            fast_box(bm, (D * 0.6, 4.0, 16.0), Vector((x, 0, 0)) + n * (R - 8.0), F, GUN, UVL(bm), bottom=True)
            fast_box(bm, (D * 0.9, 6.0, 2.0), Vector((x, 0, 0)) + n * (R - 16.5), F, DARK, UVL(bm), bottom=True)


for i in range(NF):
    x = -L / 2 + 6 + i * (L - 12) / (NF - 1)
    frame(x, HULL if i in (0, NF - 1) else GUN, owner=i in (0, NF - 1))
# --- four longerons on the diagonals, outside the frames, with an owner stripe down the top pair
for k in (1, 3, 5, 7):
    a = k * math.pi / 4
    n = Vector((0, math.cos(a), math.sin(a)))
    F = side_frame(a)
    c = n * (R + T + 7)
    fast_box(bm, (L, 14.0, 14.0), c, F, DARK, UVL(bm), bottom=True)
    fast_box(bm, (L * 0.96, 5.0, 1.2), c + n * 7.6, F, OWN if n.z > 0 else HULL, UVL(bm))
# --- the keel: a deep box spine under the cradle carrying the yard's services, with tanks and radiators
kz = -(R + T + 22)
fast_box(bm, (L * 0.9, 26, 18), Vector((0, 0, kz)), UP, HULL, UVL(bm), bottom=True)
for i in range(6):
    x = -L * 0.38 + i * L * 0.152
    cyl(bm, 7, 7, 20, Vector((x, 0, kz - 14)), Ry(math.pi / 2), seg=10, mi=LIGHT)
    fast_box(bm, (2.0, 60, 0.8), Vector((x + 30, 0, kz - 4)), UP, PANEL, UVL(bm), bottom=True)
for x in range(-210, 220, 84):
    fast_box(bm, (18, 1, 2), Vector((x, 13.6, kz + 2)), flank(1), WIN, UVL(bm))
    fast_box(bm, (18, 1, 2), Vector((x, -13.6, kz + 2)), flank(-1), WIN, UVL(bm))
# --- a gantry crane riding the top longerons: a bridge beam with a trolley and a hanging hook block
top = R + T
for gx in (-90.0, 120.0):
    fast_box(bm, (12, 2 * (R + 10), 6), Vector((gx, 0, top + 14)), UP, LIGHT, UVL(bm), bottom=True)
    for s in (1, -1):
        fast_box(bm, (10, 6, 10), Vector((gx, s * (R + 6), top + 7)), UP, GUN, UVL(bm), bottom=True)
    fast_box(bm, (9, 9, 7), Vector((gx, 12, top + 8)), UP, ACC, UVL(bm), bottom=True)
    fast_box(bm, (2, 2, 26), Vector((gx, 12, top - 7)), UP, DARK, UVL(bm), bottom=True)
    fast_box(bm, (8, 8, 4), Vector((gx, 12, top - 21)), UP, GUN, UVL(bm), bottom=True)
# --- the control block at the stern mouth: a raked tower with lit window bands, a dish and a mast
cx = -L / 2 - 26
fast_box(bm, (50, 70, 60), Vector((cx - 6, 0, 0)), UP, LIGHT, UVL(bm), bottom=True)
fast_box(bm, (34, 40, 26), Vector((cx - 10, 0, 43)), UP, HULL, UVL(bm), bottom=True)
for z in (-14, -4, 6, 16, 32):
    for s in (1, -1):
        fast_box(bm, (40 if z < 20 else 26, 0.4, 1.6), Vector((cx - (10 if z > 20 else 6), s * (35.2 if z < 20 else 20.2), z + (12 if z > 20 else 0))), flank(s), WIN, UVL(bm))
fast_box(bm, (40, 1.2, 5.0), Vector((cx - 6, 35.2, -24)), flank(1), OWN, UVL(bm))
fast_box(bm, (40, 1.2, 5.0), Vector((cx - 6, -35.2, -24)), flank(-1), OWN, UVL(bm))
cyl(bm, 1.2, 0.6, 40, Vector((cx - 10, 10, 76)), seg=6, mi=DARK)
dish(bm, Vector((cx - 10, -10, 60)), Vector((0.3, -0.5, 1)), 7.0, mi_dish=LIGHT, mi_frame=DARK, ribs=6)
for k in (1, 3, 5, 7):
    a = k * math.pi / 4
    n = Vector((0, math.cos(a), math.sin(a)))
    fast_box(bm, (22, 6, 6), Vector((cx + 14, 0, 0)) + n * 30, side_frame(a), GUN, UVL(bm), bottom=True)   # struts to the frames
nav(bm, [(L / 2, R + T + 9, 0)], [(L / 2, -(R + T + 9), 0)], [(L / 2, 0, R + T + 9), (-L / 2, 0, R + T + 9)])
o = obj("slip", bm, SM)
result = export_war(col, root, "Slip")
