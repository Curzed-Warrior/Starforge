"""
Corvette, 120 m. ARROWHEAD family. References (assets/ships/refs/corvette_ref*): the user's arrowhead with two
ducted fan rings set into a faceted delta wing and a gold stripe down the nose; the Normandy's needle nose, swept
wings, engine nacelles under the wing roots and twin dorsal fins; the Prowler's dark faceted plating with pale
stripes. Same plating, windows, engines and lights as the rest of the navy. Crew ~40 on four decks.
(The earlier Koensayr-style wedge is kept in corvette_wedge.py.)
    exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/hum/corvette.py").read())
"""
exec(open(r"C:/Users/diamo/Desktop/starforge/assets/ships/shipkit.py").read())
col, root = setup("ShipCorvette")
hull_scale(120)
bm = bmesh.new()
# --- the fuselage: a long faceted blade, needle nose to a deep stern
keel(bm, [(62, 0.4, 0.4, 0.5), (40, 3.8, 3.2, 0.9), (10, 7.0, 6.2, 1.4), (-30, 8.8, 7.4, 1.6), (-52, 8.2, 6.8, 1.4), (-58, 6.8, 5.6, 1.2)], GUN, 0.5)
plate(bm, mirror([(48, 0.3), (24, 3.2), (-20, 5.2), (-44, 5.0)]), 7.4, 1.8, LIGHT, 0.8, slope=(-0.5, 0))        # dorsal spine plate
# --- the delta wing: one thin faceted slab, its trailing edge cut into blades (user arrowhead)
wing = mirror([(26, 0.0), (-14, 18.0), (-34, 30.0), (-44, 37.0), (-50, 35.0), (-46, 27.0), (-56, 22.0), (-54, 12.0), (-60, 8.0), (-60, 0.0)])
plate(bm, wing, -1.4, 2.2, GUN, 0.9)
for s in (1, -1):
    # raised leading-edge armour and the wingtip blades
    plate(bm, [(14, s * 6), (-28, s * 27), (-36, s * 33), (-30, s * 26)] if s > 0 else [(14, s * 6), (-30, s * 26), (-36, s * 33), (-28, s * 27)], 0.8, 0.9, LIGHT, 0.2)
    fast_box(bm, (14, 1.0, 4.2), Vector((-44, s * 36.2, 0.6)), Rz(s * -0.5), GUN, UVL(bm))
    # the ducted fan: a raised ring in the wing, a glowing disc and a hub with vanes
    c = Vector((-28, s * 18.5, 0.8))
    cyl(bm, 9.6, 9.0, 4.0, c + Vector((0, 0, 1.4)), seg=36, mi=GUN, caps=False)           # the cowl
    cyl(bm, 7.4, 7.4, 4.0, c + Vector((0, 0, 1.4)), seg=36, mi=DARK, caps=False)
    band(bm, c + Vector((0, 0, 3.4)), (0, 0, 1), 9.4, 2.2, 0.6, mi=ACC, seg=36)            # cowl lip in the accent colour
    band(bm, c + Vector((0, 0, 1.6)), (0, 0, 1), 7.45, 0.5, 0.25, mi=WIN, seg=36)         # warm inner glow ring
    cyl(bm, 7.3, 7.3, 0.3, c + Vector((0, 0, -0.2)), seg=32, mi=WIN)
    cyl(bm, 1.6, 1.6, 2.4, c + Vector((0, 0, 0.9)), seg=12, mi=DARK)
    for k in range(6):
        a = k * math.pi / 3
        fast_box(bm, (5.8, 0.5, 0.4), c + Vector((math.cos(a) * 4.4, math.sin(a) * 4.4, 1.0)), Rz(a), DARK, UVL(bm))
    # layered faceted 'feather' plates over the wing, stepping back from the root (user arrowhead)
    for k, (x0, w0, l0) in enumerate(((6, 7, 26), (-6, 11, 24), (-18, 16, 20))):
        y0 = s * (w0 + 1)
        pts = [(x0, y0), (x0 - l0 * 0.35, s * (w0 + 9)), (x0 - l0, s * (w0 + 10)), (x0 - l0 * 0.8, y0)]
        plate(bm, pts if s < 0 else list(reversed(pts)), 0.8 + k * 0.35, 1.1, GUN if k % 2 else LIGHT, 0.4, slope=(-0.6, 0))
    # engine nacelle under the wing root (Normandy)
    loft(bm, [(4, 1.2, 1.2, 0.4), (-6, 3.4, 3.0, 0.4), (-44, 3.8, 3.2, 0.4), (-54, 3.4, 2.8, 0.4)], I3, Vector((0, s * 11.5, -4.4)), LIGHT)
    engine(bm, (-60, s * 11.5, -4.4), 3.2, 12)
    # twin dorsal fins, raked back (Normandy)
    plate(bm, [(-36, s * 5.6), (-36, s * 6.4), (-56, s * 7.6), (-58, s * 6.6)] if s > 0 else [(-36, s * 5.6), (-58, s * 6.6), (-56, s * 7.6), (-36, s * 6.4)], 6.6, 8.0, GUN, 0.3, slope=(-6, s * 1.2))
    # the pale stripe down the nose (user arrowhead / Prowler)
    fast_box(bm, (46, 1.6, 0.25), Vector((20, s * 3.0, 6.0)), Rz(s * -0.1), TAN, UVL(bm))
# --- main drives in the stern, a cockpit band at the front of the spine
plate(bm, rect(-61, -56, -6, 6), -4.5, 9, DARK, 0.6)
for y in (-3.2, 3.2):
    engine(bm, (-62, y, 0), 2.8, 10)
for k in range(5):
    fast_box(bm, (0.3, 1.5, 0.8), Vector((45 - k * 0.6, -3 + k * 1.5, 4.4)), UP, WIN, UVL(bm))
# --- human scale: windows along both flanks (three decks), an airlock each side, a ventral ramp
for s in (1, -1):
    windows_side(bm, -44, 18, s * 8.6, -2.4, 3, s, seed=60 + s, lit=0.4, spacing=3.0)
    airlock(bm, -20, s * 8.4, -4.0, s)
fast_box(bm, (12, 5, 0.4), Vector((-16, 0, -5.6)), DOWN, DARK, UVL(bm))
for s in (1, -1):                                                                     # panel detail on the wing, inside its outline
    t = ((26, 0, 0.85), (-42, s * 34, 0.85), (-56, 0, 0.85))
    panels(bm, tri_surface(*(t if s > 0 else (t[0], t[2], t[1]))), 61 + s, scale=0.8, accent=0.0, density=0.3)
nav(bm, [(-44, 37.6, 0.8), (61, 0.6, 0.2)], [(-44, -37.6, 0.8), (61, -0.6, 0.2)], [(-57, 7.4, 14.6), (-57, -7.4, 14.6)])
o = obj("corvette", bm, SM)
result = export_ship(col, root, "Corvette")
