"""
Hyper Relay beacon (the game strings three of these across the system): a lance-shaped mast with a ring of
fold-field vanes, solar petals at its foot, and a glowing node at the tip (the procedural pulse sits there).
Z is up. ~80 m tall.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/relay/build.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/stations/kit.py").read())
col, root = setup("Relay")

bm = bmesh.new()
revolve(bm, [(0, -40), (5, -36), (6, -20), (3.5, 10), (2, 30), (0, 36)], 16, lambda i, s: i % 2, closed=False,
        toward=lambda c: (radial(c) + Vector((0, 0, 0.1))).normalized())
c = solid_ring(bm, 18, 24, -3, 3, 48, mats=(0, 1, 0, 0))
solid_ring(bm, 17.6, 18, -1, 1, 48, mats=(2, 2, 2, 2), bevel=0)
for k in range(8):
    a = k * math.pi / 4
    loft(bm, [(5, 1.2, 1.2, 0.4), (18, 1.2, 1.2, 0.4)], Rz(a).to_3x3(), Vector((0, 0, 0)), 1)
    # vanes: swept blades standing off the ring
    loft(bm, [(24, 0.6, 6, 0.3), (34, 0.5, 3, 0.3), (40, 0.4, 0.8, 0.3)], Rz(a).to_3x3(), Vector((0, 0, 0)), 0)
for k in range(3):
    a = k * 2 * math.pi / 3
    q = [Vector((6, -3, -30)), Vector((30, -10, -42)), Vector((30, 10, -42)), Vector((6, 3, -30))]
    lo = [bm.verts.new(Rz(a) @ v) for v in q]
    hi = [bm.verts.new(Rz(a) @ (v + Vector((0, 0, 0.5)))) for v in q]
    fs = [bm.faces.new(hi), bm.faces.new(list(reversed(lo)))] + [bm.faces.new((lo[i], lo[(i + 1) % 4], hi[(i + 1) % 4], hi[i])) for i in range(4)]
    bm.normal_update()
    cen = Rz(a) @ (sum(q, Vector()) / 4)
    for f in fs:
        f.material_index = 3
        if f.normal.dot(f.calc_center_median() - cen) < 0: f.normal_flip()
sphere(bm, 2.5, (0, 0, 37), mi=2, seg=10)
greeble(bm, cyl_surface(6, -34, -22), 0.4, 3, size=(0.3, 1.2), height=(0.1, 0.5), mats=(0, 1))
obj("Beacon", bm, ["Hull", "HullDark", "WindowCool", "Solar"])

# ================================================================ v2 secondary-scale pass (asset overhaul, docs/ASSET_REFERENCES.md)
AM = ["Hull", "HullDark", "HullLight", "Accent", "Hazard", "Window", "WindowCool", "Strobe", "Radiator", "Glow", "NavRed", "Solar", "Rock"]
A_HULL, A_DARK, A_LIGHT, A_ACC, A_HAZ, A_WIN, A_WCOOL, A_STROBE, A_RAD, A_GLOW, A_RED, A_SOLAR, A_ROCK = range(len(AM))
M["Rock"] = mat("Rock", (0.32, 0.27, 0.22), 0.0, 0.9)
X_, Y_, Z_ = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
bm = bmesh.new()

# a comm dish, a pair of solar wings, an equipment module, a radiator and strobes on the mast
dish(bm, Vector((0, 0, 14)), Vector((1, 0, 0.3)), 6, mi_dish=A_LIGHT, mi_frame=A_DARK)
for sy in (1, -1):
    solar_wing(bm, Vector((0, sy * 6, -24)), Vector((0, sy, 0)), Z_, 40, 12, panels=5, mi_cell=A_SOLAR, mi_frame=A_DARK)
module(bm, Vector((0, 0, -12)), Z_, X_, 14, 8, 8, mats=(A_LIGHT, A_DARK, A_WIN), seed=160)
radiator_bank(bm, Vector((-6, 0, -16)), -X_, Z_, 5, 10, 4, 2.4, mi_fin=A_RAD, mi_man=A_DARK)
for z in (-40, 36): strobe(bm, Vector((0, 0, z)), 0.6, A_RED)
obj("Fittings", bm, AM)

result = export(col, root, "relay")
