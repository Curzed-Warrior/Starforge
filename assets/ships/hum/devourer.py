"""
Devourer, 400 km. A hull that eats stars (tech HUL-14, Star-Devourer). References (Bing sheet "star eater ship concept
art devouring star giant maw": Zhengyi Wang's star devourer, "The Mawer - planetary devourer", the planet-eater
concepts): a vast open maw at the front, its throat glowing furnace-orange with swallowed star-stuff, ringed by armoured
mandible petals that close over it; behind, a long dark armoured body in ribbed segments with glowing seams, great
radiator vanes to throw off the heat, and a cluster of drives at the stern. The navy's gunmetal and windows where the
crew lives in the body. Crew ~2,000,000,000.
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/devourer.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipDevourer")
hull_scale(400000)
M["DevMaw"] = mat("DevMaw", (0.4, 0.1, 0.02), emit=(0.95, 0.22, 0.03), strength=0.3)
SM2 = SM + ["DevMaw"]
MAW = len(SM)
bm = bmesh.new()
AX = Ry(math.pi / 2)                     # local z -> +X
# --- the maw: an armoured funnel opening forward, glowing inside, its lip thickened
cyl(bm, 30000, 78000, 70000, Vector((165000, 0, 0)), AX, seg=32, mi=DARK, caps=False)
n0 = set(bm.verts)
cyl(bm, 28000, 75500, 69000, Vector((165500, 0, 0)), AX, seg=32, mi=MAW, caps=False)
bmesh.ops.reverse_faces(bm, faces=list({f for v in since(bm, n0) for f in v.link_faces}))
cyl(bm, 30000, 30000, 2000, Vector((130500, 0, 0)), AX, seg=32, mi=MAW)                 # the throat's glowing floor
band(bm, Vector((199500, 0, 0)), X_, 77000, 3000, 3500, GUN, 32)                          # the lip
# dark ribs down the throat, splitting the glow into lanes
for k in range(16):
    a = k * math.pi / 8
    o = Vector((0, math.cos(a), math.sin(a)))
    p0 = Vector((132000, 0, 0)) + o * 29000; p1 = Vector((198000, 0, 0)) + o * 74500
    d = (p1 - p0); L = d.length; d.normalize()
    c = o.cross(d).normalized(); u = d.cross(c)
    fast_box(bm, (L, 6000, 2200), (p0 + p1) / 2 - u * 600, Matrix((d, c, u)).transposed(), DARK, UVL(bm))
# eight mandible petals round the lip, splayed open, armour-plated along their backs
for k in range(8):
    a = k * math.pi / 4 + math.pi / 8
    out = Vector((0, math.cos(a), math.sin(a))); across = out.cross(X_)
    n0 = set(bm.verts)
    fast_prism(bm, [(0, -14000), (0, 14000), (52000, 6000), (60000, 0), (52000, -6000)], 3500, Vector((0, 0, 0)),
               Matrix((X_, across, out)).transposed(), GUN, UVL(bm), top=(0.9, 0.85, 0, 0))
    for j in range(4):
        fast_box(bm, (9000, 16000 - j * 3000, 1200), Vector((7000 + j * 11000, 0, 3800)), Matrix.Identity(3), DARK, UVL(bm))
    vs = since(bm, n0)
    # stand the petal on the lip and splay it open, 35 degrees out from the axis
    bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(-0.6, 3, across), verts=vs)
    bmesh.ops.translate(bm, vec=Vector((198000, 0, 0)) + out * 78000, verts=vs)
    # fangs inside the lip, pointing into the maw
    for t in (-0.12, 0.12):
        b = a + t
        o2 = Vector((0, math.cos(b), math.sin(b)))
        n1 = set(bm.verts)
        cyl(bm, 5200, 300, 30000, Vector((0, 0, 0)), seg=5, mi=HULL)
        R = Matrix.Rotation(math.acos(max(-1, min(1, Vector((0, 0, 1)).dot((-o2 * 0.8 + X_ * 0.3).normalized())))),
                            3, Vector((0, 0, 1)).cross((-o2 * 0.8 + X_ * 0.3).normalized()).normalized())
        bmesh.ops.rotate(bm, cent=Vector((0, 0, 0)), matrix=R, verts=since(bm, n1))
        bmesh.ops.translate(bm, vec=Vector((194000, 0, 0)) + o2 * 66000, verts=since(bm, n1))
# --- the body: ribbed armoured segments narrowing to the stern, each joint a glowing seam
xs = [130000, 80000, 30000, -20000, -70000, -120000, -165000]
for i, (x1, x0) in enumerate(zip(xs, xs[1:])):
    r1 = 34000 - i * 3200; r0 = r1 - 2600
    keel(bm, [(x0 + 2000, r0, r0, 0), (x0 + 6000, r1, r1, 0), (x1 - 4000, r1, r1, 0), (x1 - 1500, r1 * 0.94, r1 * 0.94, 0)], DARK if i % 2 else GUN, 0.32)
    band(bm, Vector((x1 - 700, 0, 0)), X_, r1 * 0.9, 1400, 600, MAW, 16)
    for s in (1, -1):
        ribs(bm, x0 + 8000, x1 - 6000, s * r1, -r1 * 0.55, r1 * 0.55, s, 3600, depth=900, seed=1600 + i * 3 + s, mi=(DARK, GUN), gap=0.15)
        edge_windows(bm, (x0 + 8000, s * (r1 + 920)), (x1 - 6000, s * (r1 + 920)), -2000, 4, (0, s), lit=0.35, seed=1640 + i + s, strip=(60, 1500))
    strips(bm, x0 + 8000, x1 - 6000, -r1 * 0.55, r1 * 0.55, r1, 5, h=900, seed=1660 + i, mi=DARK)
# --- the radiator vanes: four great fins in an X down the body, throwing off a star's heat, seams lit
for k in range(4):
    a = math.pi / 4 + k * math.pi / 2
    out = Vector((0, math.cos(a), math.sin(a))); across = out.cross(X_)
    F = Matrix((X_, across, out)).transposed()
    n0 = set(bm.verts)
    fast_prism(bm, [(40000, -900), (40000, 900), (-130000, 900), (-130000, -900)], 52000, Vector((0, 0, 0)), F, DARK, UVL(bm),
               top=(0.55, 0.6, -30000, 0), base_cap=True)
    bmesh.ops.translate(bm, vec=out * 26000, verts=since(bm, n0))
    for j in range(7):
        fast_box(bm, (900, 2200, 46000), out * (26000 + 23000) + X_ * (25000 - j * 22000), F, MAW if j % 2 else DARK, UVL(bm))
# --- the stern: a drive cluster in a dark block
plate(bm, rect(-178000, -163000, -18000, 18000), -18000, 36000, DARK, 1500)
for y, z in ((0, 0), (11000, 9000), (-11000, 9000), (11000, -9000), (-11000, -9000)):
    engine(bm, (-179000, y, z), 6500 if (y, z) != (0, 0) else 8000, 14000, mi=GUN, bands=3, seg=16)
nav(bm, [(0, 36000, 0)], [(0, -36000, 0)], [(-150000, 0, 32000)])
o = obj("devourer", bm, SM2)
result = export_ship(col, root, "Devourer")
