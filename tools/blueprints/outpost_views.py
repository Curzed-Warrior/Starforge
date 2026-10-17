"""
Outpost starbase: sections, spoke, spine levels, dome, docking hub, general arrangement, ventilation risers and
schematics. Geometry comes from outpost_design (which mirrors assets/stations/outpost/build.py).
"""
import math
from draft import View
import outpost_design as O
from outpost_design import D, FILL_HULL, FILL_WALL, FILL_SHAFT, HW, T

# ================================================================== ring tube sections (horizontal = Z axial, vertical = 44.6 - r)
def rv(r): return r - O.R_MID          # horizontal position in a ring section: radial, hub side left, outer wall right

H2_, C_ = O.ZH + T, 1.4                 # skin half-height, chamfer on the hull's four long edges (build.py C)


def ring_outer_profile():
    """The ring's skin in section (y = r - R_MID, z), chamfered like the exterior build."""
    RH = O.RI - T
    pr = [(O.RO + T, -H2_ + C_), (O.RO + T, H2_ - C_), (O.RO + T - C_, H2_), (RH + C_, H2_), (RH, H2_ - C_), (RH, -H2_ + C_),
          (RH + C_, -H2_), (O.RO + T - C_, -H2_)]
    return [(rv(r), z) for r, z in pr]


def section_shell(v, spoke=False):
    """Hull poché with the void, the windows (outer wall both decks, hub wall Deck A) and the skylight over the corridor."""
    v.poly(ring_outer_profile(), "A-POCHE", closed=True, fill=FILL_HULL)
    v.rect(-HW, -O.ZH, HW, O.ZH, "A-VOID", fill="#ffffff")
    for dk, (z0, z1, _) in O.WIN_OUT.items():
        v.rect(HW, z0, HW + T, z1, "A-GLAZ", fill="#dbe8f5"); v.line((HW + T / 2, z0), (HW + T / 2, z1), "A-GLAZ")
    if not spoke:
        z0, z1, _ = O.WIN_IN["A"]
        v.rect(-HW - T, z0, -HW, z1, "A-GLAZ", fill="#dbe8f5"); v.line((-HW - T / 2, z0), (-HW - T / 2, z1), "A-GLAZ")
    v.rect(-0.96, O.ZH, 0.96, O.ZH + T, "A-GLAZ", fill="#dbe8f5"); v.line((-0.96, O.ZH + T / 2), (0.96, O.ZH + T / 2), "A-GLAZ")


def section_floors(v):
    """Deck B raised floor over the plenum, Deck B ceiling, the Deck A slab, Deck A ceiling with the skylight well."""
    v.rect(-HW, O.Z_B - 0.04, HW, O.Z_B, "S-STRC", fill="#9a9a9a")
    for y in [-HW + 0.3 + i * 0.6 for i in range(14)]:
        v.line((y, O.Z_SUB), (y, O.Z_B - 0.04), "S-STRC")
    v.rect(-HW, O.Z_B_CLG, HW, O.Z_B_CLG + 0.03, "A-WALL", fill="#bbbbbb")
    v.rect(-HW, O.Z_B_STR, HW, O.Z_A, "S-STRC", hatch="////", fill="#ffffff")
    for s in (-1, 1):
        v.rect(s * 0.96 - (0.06 if s > 0 else 0), O.Z_A_CLG, s * 0.96 + (0 if s > 0 else 0.06), O.ZH, "A-WALL", fill=FILL_WALL)   # skylight well
        y0, y1 = sorted((s * 0.96, s * HW))
        v.rect(y0, O.Z_A_CLG, y1, O.Z_A_CLG + 0.03, "A-WALL", fill="#bbbbbb")
    # services: Deck A supply + exhaust in the roof void, Deck A return and Deck B supply/return in the interstitial void,
    # pipes and cable tray in the Deck B subfloor plenum (sizes from the M-20x sheets)
    v.rect(2.0, O.Z_A_CLG + 0.12, 2.6, O.Z_A_CLG + 0.37, "M-DUCT-SUPP", fill="#dbe7ff"); v.text((2.3, O.Z_A_CLG + 0.55), "SA 600×250", "M-ANNO-TEXT", h=1.2)
    v.circle((-3.3, O.Z_A_CLG + 0.3), 0.12, "M-DUCT-EXHS", fill="#efdcff"); v.text((-3.3, O.Z_A_CLG + 0.58), "EA", "M-ANNO-TEXT", h=1.2)
    v.rect(-0.5, O.Z_B_CLG + 0.05, 0.5, O.Z_B_CLG + 0.35, "M-DUCT-RETN", fill="#ffe6d6"); v.text((0, O.Z_B_CLG + 0.2), "RA 1000×300 (DECK A)", "M-ANNO-TEXT", h=1.0)
    v.rect(-1.6, O.Z_B_CLG + 0.08, -1.2, O.Z_B_CLG + 0.28, "M-DUCT-SUPP", fill="#dbe7ff")
    v.rect(1.2, O.Z_B_CLG + 0.08, 1.6, O.Z_B_CLG + 0.28, "M-DUCT-RETN", fill="#ffe6d6")
    v.text((-1.4, O.Z_B_CLG - 0.12), "SA", "M-ANNO-TEXT", h=1.0); v.text((1.4, O.Z_B_CLG - 0.12), "RA", "M-ANNO-TEXT", h=1.0)
    for y, lab in ((-2.6, "PW"), (-2.3, "GW"), (-2.0, "BW"), (2.0, "CHWS"), (2.3, "CHWR")):
        v.circle((y, O.Z_SUB + 0.2), 0.06, "P-PIPE"); v.text((y, O.Z_SUB + 0.4), lab, "A-ANNO-NOTE", h=0.9)
    v.rect(-3.8, O.Z_SUB + 0.1, -3.0, O.Z_SUB + 0.18, "E-POWR"); v.text((-3.4, O.Z_SUB + 0.33), "CABLE TRAY", "A-ANNO-NOTE", h=0.9)


def section_levels(v, length=24):
    zr = HW + T + 0.8
    for z, name, val in ((H2_, "ROOF SKIN", "z +4 600"), (O.ZH, "PRESSURE HULL", "z +4 000"), (O.Z_A_CLG, "DECK A CLG", "z +3 000 · +3 200 AFF"),
                         (O.Z_A, "DECK A FFL", "z −200 · 1.0 g ↓"), (O.Z_B_STR, "DECK A SLAB U/S", "z −500"),
                         (O.Z_B_CLG, "DECK B CLG", "z −900 · +2 600 AFF"), (O.Z_B, "DECK B FFL", "z −3 500 · 1.0 g ↓"),
                         (-O.ZH, "PRESSURE HULL", "z −4 000"), (-H2_, "FLOOR SKIN", "z −4 600")):
        v.line((HW + T + 0.1, z), (zr, z), "A-ANNO-DIMS")
        v.datum((zr, z), name, val, side=1, length=length)


def ring_section(kind="typical", levels=None):
    """Transverse section of the ring (looking spinward): hub wall left, outer wall right, flat decks, down = -Z.
    kind: 'typical' (quarters C05, through a cabin bay) or 'junction' (through the spoke, C04)."""
    v = View(f"ring-section-{kind}")
    section_shell(v, spoke=(kind != "typical"))
    section_floors(v)
    if kind == "typical":
        for (z0, z1) in ((O.Z_A, O.Z_A_CLG), (O.Z_B, O.Z_B_CLG)):
            for y in (-0.96, 0.96):
                v.rect(y - 0.06, z0, y + 0.06, z1, "A-WALL", fill=FILL_WALL)
        for s in (-1, 1):
            y0, y1 = sorted((s * (HW - 0.95), s * (HW - 0.05)))
            v.rect(y0, O.Z_A, y1, O.Z_A + 0.5, "A-FURN"); v.text(((y0 + y1) / 2, O.Z_A + 0.25), "BED", "A-ANNO-NOTE", h=1.1)
            y0, y1 = sorted((s * 1.5, s * 2.7))
            v.rect(y0, O.Z_A + 0.72, y1, O.Z_A + 0.76, "A-FURN"); v.text(((y0 + y1) / 2, O.Z_A + 1.0), "DESK", "A-ANNO-NOTE", h=1.1)
            y0, y1 = sorted((s * (HW - 0.95), s * (HW - 0.05)))
            v.rect(y0, O.Z_B + 0.3, y1, O.Z_B + 0.5, "A-FURN"); v.rect(y0, O.Z_B + 1.3, y1, O.Z_B + 1.5, "A-FURN")
            v.text(((y0 + y1) / 2, O.Z_B + 0.9), "BUNKS", "A-ANNO-NOTE", h=1.1)
            v.text((s * 2.4, O.Z_A_CLG - 0.35), "CABIN", "A-ANNO-TEXT", h=1.8, bold=True)
            v.text((s * 2.4, O.Z_B_CLG - 0.3), "CABIN" if s > 0 else "CABIN (NO WINDOW)", "A-ANNO-TEXT", h=1.6, bold=True)
            v.rect(s * 2.6 - 0.15, O.Z_A_CLG - 0.03, s * 2.6 + 0.15, O.Z_A_CLG, "M-HVAC-DIFF", fill="#0050c8")
            v.rect(s * 0.6 - 0.25, O.Z_A - 0.03, s * 0.6 + 0.25, O.Z_A, "M-HVAC-GRIL", fill="#c84b00")
        v.text((0, O.Z_A + 1.6), "CORRIDOR\n1 800\nSKYLIGHT\nOVER", "A-ANNO-TEXT", h=1.5, bold=True)
        v.text((0, O.Z_B + 1.3), "CORRIDOR", "A-ANNO-TEXT", h=1.5, bold=True)
        top = H2_
        for y, lab in ((-HW, "A"), (-2.0, "B"), (0.0, "C"), (2.0, "D"), (HW, "E")):
            v.line((y, top + 0.1), (y, top + 0.75), "A-GRID"); v.bubble((y, top + 0.98), lab, 6.0)
        CO = [((3.4, 1.4), (1.2, 2.6), "OUTER WALL WINDOW W1: 1 600 × 1 800, SILL +800 · ONE PER FRAME BAY"),
              ((4.3, -2.1), (1.2, -1.2), "DECK B WINDOW W2: 1 400 × 1 200, SILL +800"),
              ((-4.3, 1.4), (-3.9, 2.6), "HUB WALL WINDOW W3: 1 200 × 1 600 (DECK A)"),
              ((0.0, 4.3), (1.2, 3.75), "SKYLIGHT OVER THE CORRIDOR · WELL THROUGH THE ROOF VOID"),
              ((2.3, 3.25), (1.2, 3.45), "ROOF VOID 1 000: SA 600×250 · EA · CABLE TRAYS"),
              ((-1.0, -0.35), (-3.9, 0.2), "DECK A SLAB 300 · RA 1000×300 IN THE VOID BELOW"),
              ((-2.0, -3.8), (-3.9, -2.4), "SUBFLOOR PLENUM 500 · PW / GW / BW · CHW · TRAY"),
              ((0.96, -1.6), (1.2, -2.6), "PARTITION 120 · Al HONEYCOMB, ACOUSTIC CORE")]
        for po, pt, txt in CO: v.leader(po, pt, txt, "A-ANNO-NOTE", h=1.35)
    else:
        # junction: the spoke tunnel leaves the hub wall at Deck A floor level; lift and RS shaft run Deck B to Deck A
        z0s, z1s = O.Z_A - O.SPOKE_T, O.Z_A - O.SPOKE_T + O.SPOKE_H
        v.rect(-HW - T - 4.0, z0s, -HW - T, z0s + O.SPOKE_T, "A-WALL-PRHU", fill=FILL_HULL)
        v.rect(-HW - T - 4.0, z1s - O.SPOKE_T, -HW - T, z1s, "A-WALL-PRHU", fill=FILL_HULL)
        v.rect(-HW - T - 0.01, O.Z_A, -HW + 0.01, z1s - O.SPOKE_T, "A-VOID", fill="#ffffff")
        v.line((-HW - T - 4.4, O.Z_A), (-HW - T - 4.0, O.Z_A), "A-HIDN")
        v.text((-HW - T - 2.0, O.Z_A + 1.3), "SPOKE\n(A-302)", "A-ANNO-TEXT", h=1.6, bold=True)
        v.rect(-HW - 0.08, O.Z_A, -HW, O.Z_A + 2.1, "A-DOOR-PRES", fill="#ffffff")
        v.text((-HW + 0.6, O.Z_A + 2.4), "ISOLATION DOOR SH", "A-ANNO-NOTE", h=1.1, ha="left")
        for y in (-1.1, 1.1):
            v.rect(y - (0.15 if y < 0 else 0), O.Z_B - 0.3, y + (0.15 if y > 0 else 0), O.Z_A_CLG, "A-WALL-SHFT", fill=FILL_SHAFT)
        v.rect(-0.95, O.Z_B, 0.95, O.Z_B + 2.3, "A-EQPM"); v.text((0, O.Z_B + 1.15), "LIFT CAR\n(PARKED AT B)", "A-ANNO-NOTE", h=1.2)
        v.rect(-1.25, O.Z_B_STR - 0.01, 1.25, O.Z_A + 0.01, "A-VOID", fill="#ffffff")
        v.text((-2.6, O.Z_A + 1.5), "SPOKE\nPORTAL", "A-ANNO-TEXT", h=1.6, bold=True)
        v.text((2.8, O.Z_A + 1.5), "CONCOURSE", "A-ANNO-TEXT", h=1.6, bold=True)
        v.text((-2.6, O.Z_B + 1.2), "PLANT · AHU", "A-ANNO-TEXT", h=1.5, bold=True)
        v.text((2.8, O.Z_B + 1.2), "LIFT LOBBY", "A-ANNO-TEXT", h=1.5, bold=True)
    section_levels(v, 18)
    v.chain([(-HW - T - 0.8, z) for z in (-H2_, -O.ZH, O.Z_B, O.Z_B_CLG, O.Z_B_STR, O.Z_A, O.Z_A_CLG, O.ZH, H2_)], 10)
    v.chain([(y, -H2_ - 0.4) for y in ((-HW - T, -HW, -0.96, 0.96, HW, HW + T) if kind == "typical" else (-HW - T, -HW, -1.1, 1.1, HW, HW + T))], -6)
    v.dim((-HW - T, -H2_ - 0.4), (HW + T, -H2_ - 0.4), -13)
    for dk, (z0, z1, _) in O.WIN_OUT.items():
        v.chain([(HW + T + 0.3, z) for z in (z0, z1)], -4)
    v.line((0, -H2_ - 0.8), (0, H2_ + 0.6), "A-CNTR")
    return v


def bulkhead_elevation():
    """Elevation of a pressure bulkhead (looking spinward): both decks' pressure doors and every penetration."""
    v = View("bulkhead-elevation")
    v.poly(ring_outer_profile(), "A-POCHE", closed=True, fill=FILL_HULL)
    v.rect(-HW, -O.ZH, HW, O.ZH, "S-STRC", fill="#d4d4d4", hatch="\\\\")
    for (z0, w, h, tag) in ((O.Z_A, 1.2, 2.1, "PD-nA  1200×2100"), (O.Z_B, 1.0, 2.0, "PD-nB  1000×2000")):
        v.rect(-w / 2 - 0.12, z0, w / 2 + 0.12, z0 + h + 0.12, "A-DOOR-PRES", fill="#ffffff")
        v.rect(-w / 2, z0, w / 2, z0 + h, "A-DOOR-PRES", fill="#ffffff")
        v.rect(w / 2 + 0.12, z0, w / 2 + 0.12 + w, z0 + h, "A-HIDN")
        v.text((w / 2 + 0.12 + w / 2, z0 + h / 2), "POCKET", "A-ANNO-NOTE", h=1.1)
        v.text((0, z0 + h + 0.35), tag, "A-ANNO-TEXT", h=1.5, bold=True)
        v.circle((-w / 2 - 0.45, z0 + 1.1), 0.08, "A-EQPM"); v.text((-w / 2 - 0.45, z0 + 1.35), "ΔP", "A-ANNO-NOTE", h=1.0)
    pen = [(2.0, 2.6, O.Z_A_CLG + 0.12, O.Z_A_CLG + 0.37, "M-DUCT-SUPP", "SA · FSD"),
           (-3.45, -3.15, O.Z_A_CLG + 0.18, O.Z_A_CLG + 0.42, "M-DUCT-EXHS", "EA · FSD"),
           (-0.5, 0.5, O.Z_B_CLG + 0.05, O.Z_B_CLG + 0.35, "M-DUCT-RETN", "RA (A) · FSD"),
           (-1.6, -1.2, O.Z_B_CLG + 0.08, O.Z_B_CLG + 0.28, "M-DUCT-SUPP", "SA (B) · FSD"),
           (1.2, 1.6, O.Z_B_CLG + 0.08, O.Z_B_CLG + 0.28, "M-DUCT-RETN", "RA (B) · FSD")]
    for a, b, c, d, lay, lab in pen:
        v.rect(a, c, b, d, lay, fill="#ffffff"); v.line((a, c), (b, d), "M-HVAC-DAMP")
        v.leader(((a + b) / 2, d), ((a + b) / 2 + 0.6, d + 0.45), lab, h=1.05)
    for y in (-2.6, -2.3, -2.0, 2.0, 2.3):
        v.circle((y, O.Z_SUB + 0.2), 0.07, "P-PIPE")
    v.leader((-2.3, O.Z_SUB + 0.27), (-3.0, O.Z_SUB + 1.6), "PIPE PENETRATIONS · ISOLATION VALVES BOTH SIDES", h=1.0, ha="right")
    v.rect(-3.8, O.Z_SUB + 0.1, -3.0, O.Z_SUB + 0.18, "E-POWR", fill="#ffffff")
    v.leader((-3.4, O.Z_SUB + 0.18), (-3.6, O.Z_SUB + 2.4), "CABLE TRANSIT · SEALED MULTI-CABLE BLOCK", h=1.0, ha="right")
    v.chain([(HW + T + 0.5, z) for z in (-O.ZH, O.Z_B, O.Z_B + 2.0, O.Z_B_CLG, O.Z_A, O.Z_A + 2.1, O.Z_A_CLG, O.ZH)], -6)
    v.chain([(y, -H2_ - 0.5) for y in (-HW, -0.6, 0.6, HW)], -6)
    return v


# ================================================================== spoke
def spoke_plan():
    """Spoke plan from +Z (x = r from the spine wall to the ring's hub wall, y tangential): a level walkway at Deck A
    floor height, the service chase along one side, isolation doors at both ends."""
    v = View("spoke-plan")
    r0, r1 = O.SRI, O.RI
    hw = O.SPOKE_W / 2
    for s in (-1, 1):
        v.rect(r0, s * hw - (O.SPOKE_T if s > 0 else 0), r1 - T, s * hw + (0 if s > 0 else O.SPOKE_T), "A-WALL-PRHU", fill=FILL_HULL)
    v.rect(O.SRI - T, -hw - 1.0, O.SRI, hw + 1.0, "A-WALL-PRHU", fill=FILL_HULL)
    v.rect(O.RI - T, -hw - 1.0, O.RI, -hw, "A-WALL-PRHU", fill=FILL_HULL); v.rect(O.RI - T, hw, O.RI, hw + 1.0, "A-WALL-PRHU", fill=FILL_HULL)
    # service chase (1.15..2.15) the whole length, enclosed; walkway the rest
    for y in (1.075, 2.15):
        v.line((r0, y), (r1 - T, y), "A-WALL-SHFT")
    v.rect(r0, 1.0, r1 - T, 1.15, "A-WALL-SHFT", fill=FILL_SHAFT)
    v.text(((r0 + r1) / 2, 1.6), "SERVICE CHASE · OA / RH / EA / PW / BW / CHW / POWER / DATA · ACCESS PANELS @ 3 000", "A-ANNO-NOTE", h=1.1)
    for y in (-2.0, 0.85):
        v.line((r0 + 0.8, y), (r1 - T - 0.8, y), "A-FURN")
    v.text(((r0 + r1) / 2, -0.55), f"WALKWAY · LEVEL WITH THE SPINE HUB DECK AND RING DECK A (z −200) · {r1 - T - r0:.1f} m", "A-ANNO-NOTE", h=1.25)
    v.text(((r0 + r1) / 2, -1.6), "HANDRAILS BOTH SIDES +950 · FLOOR LIGHT STRIPS · 2 VIEW PORTS EACH SIDE", "A-ANNO-NOTE", h=1.05)
    for rp in (16.0, 27.0):
        v.rect(rp - 0.5, -hw - 0.05, rp + 0.5, -hw + O.SPOKE_T + 0.05, "A-GLAZ", fill="#dbe8f5")
        v.text((rp, -hw - 0.5), "VIEW PORT", "A-ANNO-NOTE", h=1.0)
    for rp, lab in ((O.SRI + 0.4, "HUB ISOLATION DOOR"), (O.RI - T - 0.4, "RING ISOLATION DOOR SH")):
        v.rect(rp - 0.06, -2.1, rp + 0.06, 0.95, "A-DOOR-PRES", fill="#ffffff")
        v.text((rp + (2.2 if rp < 20 else -2.2), 2.75), lab, "A-ANNO-NOTE", h=1.15)
    v.chain([(x, -hw - 1.2) for x in (O.SRI - T, O.SRI, 16.0, 27.0, O.RI - T, O.RI)], -6)
    v.dim((O.SRI, -hw - 1.2), (O.RI - T, -hw - 1.2), -14)
    v.chain([(O.RI + 0.6, y) for y in (-hw, -2.15, 1.0, 1.15, 2.15, hw)], -5)
    for r, lab in ((O.SRI, "r 7 400"), (O.RI - T, "r 35 400"), (O.RI, "r 36 000")):
        v.line((r, hw + 1.3), (r, hw + 2.0), "A-ANNO-DIMS"); v.text((r, hw + 2.4), lab, "A-ANNO-NOTE", h=1.2)
    return v


def spoke_cross():
    """Spoke cross-section (looking outward), 1:20: walkway on a raised floor, chase on the right, down = -Z."""
    v = View("spoke-cross")
    W, H, t = O.SPOKE_W, O.SPOKE_H, O.SPOKE_T
    v.rect(-W / 2, -t, W / 2, H - t, "A-POCHE", fill=FILL_HULL)
    v.rect(-W / 2 + t, 0.0, W / 2 - t, H - 2 * t, "A-VOID", fill="#ffffff")
    v.rect(-W / 2 + t, 0.0, 1.0, 0.12, "S-STRC", fill="#9a9a9a")
    v.text((-0.6, 0.06), "", h=1)
    v.text((-0.6, 1.2), "WALKWAY\n3 150 CLEAR", "A-ANNO-TEXT", h=1.6, bold=True)
    for y in (-W / 2 + t + 0.06, 0.94):
        v.rect(y - 0.02, 0.12, y + 0.02, 1.07, "A-FURN"); v.circle((y, 1.07), 0.025, "A-FURN")
    v.rect(-0.6, H - 2 * t - 0.06, 0.2, H - 2 * t, "M-HVAC-DIFF", fill="#0050c8"); v.text((-0.2, H - 2 * t - 0.2), "LIGHT STRIP", "A-ANNO-NOTE", h=1.0)
    v.rect(1.0, 0.0, 1.15, H - 2 * t, "A-WALL-SHFT", fill=FILL_SHAFT)
    v.rect(1.25, 1.85, 2.05, 2.25, "M-DUCT-OAIR", fill="#dcf3e3"); v.text((1.65, 2.05), "OA 400×200", "M-ANNO-TEXT", h=1.0)
    v.rect(1.25, 1.35, 2.05, 1.75, "M-DUCT-RETN", fill="#ffe6d6"); v.text((1.65, 1.55), "RH 400×200", "M-ANNO-TEXT", h=1.0)
    v.circle((1.45, 1.0), 0.125, "M-DUCT-EXHS", fill="#efdcff"); v.text((1.45, 1.0), "EA", "M-ANNO-TEXT", h=0.9)
    for (x, y, r, lab) in ((1.8, 1.05, 0.045, "PW"), (1.95, 1.05, 0.05, "BW"), (1.8, 0.75, 0.06, "CHWS"), (1.98, 0.75, 0.06, "CHWR"), (1.45, 0.65, 0.035, "N₂/O₂")):
        v.circle((x, y), r, "P-PIPE", fill="#cfeeee"); v.text((x, y - 0.13 - r), lab, "A-ANNO-NOTE", h=0.8)
    v.rect(1.25, 0.1, 2.05, 0.4, "E-POWR", fill="#f5d6d6"); v.text((1.65, 0.25), "BUSWAY 400 V DC", "A-ANNO-NOTE", h=0.95)
    v.chain([(x, -t - 0.2) for x in (-W / 2, -W / 2 + t, 1.0, 1.15, W / 2 - t, W / 2)], -6, h=1.5)
    v.dim((-W / 2, -t - 0.2), (W / 2, -t - 0.2), -14, h=1.6)
    v.chain([(W / 2 + 0.2, z) for z in (-t, 0.0, 0.12, H - 2 * t, H - t)], 6, h=1.5)
    v.text((0, H - t + 0.35), "TANGENTIAL", "A-ANNO-NOTE", h=1.3)
    v.text((-W / 2 - 0.35, H / 2), "UP (+Z)", "A-ANNO-NOTE", h=1.3, rot=90)
    v.datum((-W / 2 - 0.1, 0.0), "SPOKE FFL", "z −200", side=-1, length=16)
    return v


# ================================================================== spine levels (circle plans, looking down from +Z)
def sector(v, r0, r1, a0, a1, layer="A-EQPM", label=None, h=1.2, fill=None):
    n = max(4, int(abs(a1 - a0) / 4))
    pts = [(r1 * math.cos(math.radians(a0 + (a1 - a0) * i / n)), r1 * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]
    pts += [(r0 * math.cos(math.radians(a1 - (a1 - a0) * i / n)), r0 * math.sin(math.radians(a1 - (a1 - a0) * i / n))) for i in range(n + 1)]
    v.poly(pts, layer, closed=True, fill=fill)
    if label:
        am, rm = math.radians((a0 + a1) / 2), (r0 + r1) / 2
        rot = (a0 + a1) / 2 - 90
        if rot < -90: rot += 180
        if rot > 90: rot -= 180
        v.text((rm * math.cos(am), rm * math.sin(am)), label, "A-ANNO-NOTE", h=h, rot=rot)


def polar(r, a): return (r * math.cos(math.radians(a)), r * math.sin(math.radians(a)))


SPINE_LEVELS = [
    # id, name, z floor, z ceiling, contents key
    ("S0", "Lower trunk · dock vestibule", -26.0, -22.0, "s0"),
    ("S1", "Cargo hold · logistics", -22.0, -13.5, "s1"),
    ("S2a", "ECLSS plant · water & thermal", -13.5, -7.5, "s2a"),
    ("S2b", "ECLSS plant · air revitalisation", -7.5, -0.2, "s2b"),
    ("S3a", "Hub transfer node · spoke junction", -0.2, 4.5, "s3a"),
    ("S3b", "Avionics · data · GNC", 4.5, 10.0, "s3b"),
    ("S4a", "Power management & distribution", 10.0, 15.5, "s4a"),
    ("S4b", "Energy storage", 15.5, 21.0, "s4b"),
    ("S5", "EVA · solar array drives", 21.0, 30.0, "s5"),
    ("DOME", "Command deck", 30.0, 38.0, "dome"),
]


def spine_level(key):
    lid, name, z0, z1, k = next(l for l in SPINE_LEVELS if l[4] == key)
    v = View(f"spine-{lid}")
    # hull ring, deck plate edge, trunk opening
    n = 64
    outer = [polar(O.SR, 360 * i / n) for i in range(n)]
    inner = [polar(O.SRI, 360 * i / n) for i in range(n)]
    if k == "s3a":
        # spoke portals cut the hull at 0/90/180/270
        for a in range(0, 360, 90):
            pass
    v.poly(outer, "A-POCHE", closed=True, fill=FILL_HULL)
    v.poly(inner, "A-VOID", closed=True, fill="#ffffff")
    if k != "dome":
        v.circle((0, 0), 1.8, "A-HIDN")
        v.circle((0, 0), 1.5, "A-WALL-SHFT")
        v.rect(-1.0, -1.0, 1.0, 1.0, "A-EQPM"); v.line((-1.0, -1.0), (1.0, 1.0), "A-EQPM"); v.line((-1.0, 1.0), (1.0, -1.0), "A-EQPM")
        for a in (45, 135, 225, 315): v.circle(polar(1.3, a), 0.08, "A-EQPM", fill="#000000")
        v.text((0, -1.25), "TRANSFER TRUNK Ø3 600 · CAR 2000×2000", "A-ANNO-NOTE", h=1.05)
    # external conduits and portholes (16 per deck, mid-height)
    for a in (45, 135, 225, 315):
        v.circle(polar(O.SR + 0.45, a), 0.28, "A-EQPM", fill="#d9d9d9")
    v.text(polar(O.SR + 0.45, 45 + 6), "EXT. CABLE / PIPE BUNDLE Ø560 (TYP. 4)", "A-ANNO-NOTE", h=1.0, ha="left")
    if k not in ("s3a", "dome"):
        for i in range(16):
            a = (i + 0.5) * 22.5
            p0, p1 = polar(O.SRI, a), polar(O.SR, a)
            v.line(polar(O.SRI - 0.02, a - 3.8), polar(O.SRI - 0.02, a + 3.8), "A-GLAZ")
    # risers (all levels): SA-S, RA-S, OA ring riser, electrical, water
    risers = [(135, 2.7, 0.315, "M-DUCT-SUPP", "SA-S Ø630"), (315, 2.7, 0.315, "M-DUCT-RETN", "RA-S Ø630"),
              (225, 2.6, 0.25, "M-DUCT-OAIR", "OA Ø500"), (45, 2.6, 0.25, "M-DUCT-RETN", "RH Ø500")]
    for a, r, rr, lay, lab in risers:
        if k == "dome" and lay != "M-DUCT-SUPP" and lay != "M-DUCT-RETN": continue
        if k in ("s0",) and lay in ("M-DUCT-OAIR",): continue
        v.circle(polar(r, a), rr, lay, fill="#ffffff"); v.text(polar(r + 0.7, a), lab, "M-ANNO-TEXT", h=1.05)
    for a, lab in ((90, "ER"), (270, "WR")):
        x, y = polar(2.5, a)
        v.rect(x - 0.3, y - 0.15, x + 0.3, y + 0.15, "A-WALL-SHFT", fill=FILL_SHAFT); v.text((x, y - 0.4), lab, "A-ANNO-NOTE", h=1.0)
    R0, R1 = 3.6, O.SRI - 0.15
    if k == "s0":
        for a0 in (20, 110, 200, 290): sector(v, 5.4, R1, a0, a0 + 50, label="STOWAGE")
        v.circle((0, 0), 0.8, "A-DOOR-PRES"); v.text((0, 0.0), "", h=1)
        v.text((0, 2.0), "PRESSURE HATCH Ø1 600 IN FLOOR\nTO DOCKING HUB (A-406)", "A-ANNO-NOTE", h=1.2)
        for a in (65, 155, 245, 335): v.rect(*polar(4.6, a), *(lambda p: (p[0] + 0.8, p[1] + 0.5))(polar(4.6, a)), "A-EQPM")
        v.text((0, -4.3), "PBA / FIRE POINT · HANDRAIL RINGS ON FLOOR & WALL", "A-ANNO-NOTE", h=1.05)
    elif k == "s1":
        for i in range(8):
            a0 = i * 45 + 5
            sector(v, 5.0, R1, a0, a0 + 35, label="CTB RACK", h=1.0)
        sector(v, 3.7, 4.6, 200, 250, label="PALLET", h=1.0)
        v.text((0, 4.3), "CARGO TRANSFER AISLE r 1 800–5 000", "A-ANNO-NOTE", h=1.1)
        for a in (17.2, 137.2, 257.2):
            v.line(polar(O.SR, a), polar(O.SR + 0.8, a), "A-EQPM-HIDN"); v.text(polar(O.SR - 0.7, a + 7), "TO EXT. TANK\n(A-101)", "A-ANNO-NOTE", h=1.0)
        v.rect(*polar(6.6, 17), *(lambda p: (p[0] + 0.5, p[1] + 0.8))(polar(6.6, 17)), "A-EQPM")
        v.text(polar(5.9, 30), "FILL / DRAIN\nVALVE PANEL", "A-ANNO-NOTE", h=1.0)
        v.text((0, -2.2), "LIFT CAR PARKS HERE", "A-ANNO-NOTE", h=1.05)
    elif k == "s2a":
        items = [(10, 60, "WATER RECOVERY · WPA"), (70, 110, "URINE PROCESSOR"), (125, 170, "ATCS PUMPS P-T1/T2"), (190, 235, "HEAT EXCHANGERS"),
                 (250, 290, "CONDENSATE / WASTE TANKS"), (300, 345, "POTABLE WATER BUFFER")]
        for a0, a1, lab in items: sector(v, 4.6, R1, a0, a1, label=lab, h=0.95)
        for a in (90, 270):
            v.line(polar(O.SR, a), polar(O.SR + 0.8, a), "P-PIPE")
        v.text(polar(O.SR - 0.9, 80), "COOLANT TO RADIATORS", "A-ANNO-NOTE", h=1.0)
        v.text((0, 4.1), "MEZZANINE GRATING AT z −7 500 OVER (S2b)", "A-ANNO-NOTE", h=1.0)
    elif k == "s2b":
        items = [(5, 45, "CO₂ REMOVAL 4BMS-1"), (50, 90, "CO₂ REMOVAL 4BMS-2"), (100, 140, "O₂ GENERATION OGA-1/2"), (150, 185, "SABATIER CRA"),
                 (195, 225, "TRACE CONTAM. TCCS"), (240, 300, "AHU-S (SPINE) · 3.2 m³/s"), (310, 350, "N₂ / O₂ HP STORAGE")]
        for a0, a1, lab in items: sector(v, 4.4, R1, a0, a1, label=lab, h=0.95, layer="M-HVAC-EQPM" if "AHU" in lab else "A-EQPM")
        v.text((0, 3.9), "ARS MANIFOLD: RETURN-FROM-HUB → 4BMS → OGA / SABATIER → OA", "A-ANNO-NOTE", h=1.0)
    elif k == "s3a":
        for a in (0, 90, 180, 270):
            ar = math.radians(a)
            # spoke portal: an opening 4 800 wide in the hull with an isolation hatch
            pa, pb = polar(O.SRI, a - 18.5), polar(O.SRI, a + 18.5)
            v.poly([polar(O.SRI - 0.05, a - 18.5), polar(O.SR + 0.05, a - 18.5), polar(O.SR + 0.05, a + 18.5), polar(O.SRI - 0.05, a + 18.5)], "A-VOID", closed=True, fill="#ffffff")
            for s in (-1, 1):
                v.line(polar(O.SRI, a + s * 18.5), polar(O.SR + 0.9, a + s * 18.5 * O.SR / (O.SR + 0.9)), "A-WALL-PRHU")
            v.text(polar(O.SR - 1.3, a + 10), f"SPOKE {a // 90 + 1}", "A-ANNO-TEXT", h=1.5, bold=True)
            v.rect(*polar(O.SRI - 0.35, a), *(lambda p: (p[0] + 0.01, p[1] + 0.01))(polar(O.SRI - 0.35, a)), "A-DOOR-PRES")
            v.text(polar(O.SRI - 0.9, a), f"SH-{a // 90 + 1}", "A-ANNO-NOTE", h=1.0)
        v.circle((0, 0), 3.4, "M-DUCT-OAIR"); v.circle((0, 0), 3.15, "M-DUCT-OAIR")
        v.circle((0, 0), 4.2, "M-DUCT-RETN"); v.circle((0, 0), 3.95, "M-DUCT-RETN")
        v.text((0, 3.6), "OA RING MANIFOLD Ø500 (OVERHEAD)", "M-ANNO-TEXT", h=1.0)
        v.text((0, 4.6), "RH RING MANIFOLD Ø500 (OVERHEAD)", "M-ANNO-TEXT", h=1.0)
        v.text((0, -5.0), "TRANSFER NODE · HANDRAILS · TRANSLATION AIDS · SAFE ZONE", "A-ANNO-NOTE", h=1.05)
    elif k == "s3b":
        for i in range(10):
            a0 = i * 36 + 4
            sector(v, 5.2, R1, a0, a0 + 28, label="RACK", h=0.95)
        sector(v, 3.7, 4.8, 30, 80, label="GNC / IMU", h=0.95); sector(v, 3.7, 4.8, 200, 250, label="COMMS", h=0.95)
        v.text((0, -4.3), "10 × AVIONICS RACKS · 2 × COLD-PLATE LOOPS", "A-ANNO-NOTE", h=1.05)
    elif k == "s4a":
        for i, lab in enumerate(("MBSU-1", "MBSU-2", "MBSU-3", "MBSU-4")):
            sector(v, 4.8, R1, i * 90 + 10, i * 90 + 70, label=lab + " · MAIN BUS", h=1.0, layer="E-POWR")
        for a in (80, 170, 260, 350): sector(v, 4.8, R1, a - 6, a + 6, label="DDCU", h=0.85)
        v.text((0, -4.0), "PMAD · 400 V DC MAIN BUSES · 120 V DC SECONDARY", "A-ANNO-NOTE", h=1.05)
    elif k == "s4b":
        for i in range(12):
            a0 = i * 30 + 3
            sector(v, 5.0, R1, a0, a0 + 24, label="BATT ORU", h=0.9, layer="E-POWR")
        v.text((0, -4.1), "LI-ION ENERGY STORAGE · 12 ORUs · 2.4 MWh", "A-ANNO-NOTE", h=1.05)
    elif k == "s5":
        for a in (0, 180):
            sector(v, 5.6, R1, a - 12, a + 12, label="SADA", h=1.0, layer="E-POWR")
            v.line(polar(O.SR, a), polar(O.SR + 0.8, a), "S-STRC")
            v.text(polar(O.SR - 1.0, a + 10), "TO SOLAR WING", "A-ANNO-NOTE", h=1.0)
        # EVA crew lock on +Y with an outer hatch, equipment lock beside it
        v.poly([polar(4.0, 70), polar(O.SRI, 70), polar(O.SRI, 110), polar(4.0, 110)], "A-WALL", closed=True)
        v.text(polar(5.6, 90), "CREW LOCK\nAL-E1", "A-ANNO-TEXT", h=1.3, bold=True)
        v.rect(-0.55, O.SR - 0.1, 0.55, O.SR + 0.15, "A-DOOR-PRES", fill="#ffffff"); v.text((0, O.SR + 0.6), "EVA HATCH Ø1 100", "A-ANNO-NOTE", h=1.05)
        sector(v, 4.6, R1, 120, 160, label="SUIT STOWAGE", h=1.0); sector(v, 4.6, R1, 20, 60, label="EQUIP. LOCK", h=1.0)
        sector(v, 4.6, R1, 200, 330, label="WORKSHOP · MICRO-g", h=1.0)
    elif k == "dome":
        v.circle((0, 0), O.SRI, "A-GLAZ")
        for i in range(12):
            a = i * 30
            x, y = polar(5.4, a)
            c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
            pts = [(x + c * dx - s * dy, y + s * dx + c * dy) for dx, dy in ((-0.35, -0.7), (0.35, -0.7), (0.35, 0.7), (-0.35, 0.7))]
            v.poly(pts, "A-EQPM", closed=True)
        for i in range(3):
            v.circle(polar(3.0, i * 120 + 17), 0.3, "A-FURN")
        v.circle((0, 0), 1.8, "A-DOOR-PRES"); v.text((0, 0), "FLOOR HATCH\nFROM S5", "A-ANNO-NOTE", h=1.1)
        v.text((0, -2.5), "12 CONSOLES r 5 400 · 3 COMMAND SEATS r 3 000", "A-ANNO-NOTE", h=1.1)
        v.text((0, 6.6), "GLASS DOME Ø16 000 · 12 MULLIONS", "A-ANNO-NOTE", h=1.1)
    # grid: radial lines at 0/90/180/270 (spoke axes) and dims
    for a, lab in ((0, "S1"), (90, "S2"), (180, "S3"), (270, "S4")):
        v.line(polar(0.0, a), polar(O.SR + 0.8, a), "A-GRID"); v.bubble(polar(O.SR + 1.2, a), lab, 5.0)
    v.dim((-O.SR, -O.SR - 1.2), (O.SR, -O.SR - 1.2), -6)
    v.dim((-O.SRI, -O.SR - 1.2), (O.SRI, -O.SR - 1.2), -2)
    v.text((0, O.SR + 1.0), f"LEVEL {lid}  ·  z {z0:+.1f} TO {z1:+.1f} m  ·  CLEAR {z1 - z0 - 0.3:.1f} m  ·  GRAVITY 1.0 g, DOWN = −Z",
           "A-ANNO-NOTE", h=1.25)
    return v, f"Level {lid} · {name}"


def dock_hub():
    """Docking hub plan at z -29 (arm axis level), 1:100."""
    v = View("dock-hub")
    n = 72
    v.poly([polar(10.0, 360 * i / n) for i in range(n)], "A-POCHE", closed=True, fill=FILL_HULL)
    v.poly([polar(9.6, 360 * i / n) for i in range(n)], "A-VOID", closed=True, fill="#ffffff")
    for k in range(4):
        a = 45 + 90 * k
        c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
        def P(x, y): return (x * c - y * s, x * s + y * c)
        v.poly([P(9.6, -1.8), P(24.0, -1.8), P(24.0, 1.8), P(9.6, 1.8)], "A-POCHE", closed=True, fill=FILL_HULL)
        v.poly([P(9.4, -1.55), P(24.0, -1.55), P(24.0, 1.55), P(9.4, 1.55)], "A-VOID", closed=True, fill="#ffffff")
        v.poly([P(24.0, -3.0), P(25.2, -3.0), P(25.2, 3.0), P(24.0, 3.0)], "A-WALL-PRHU", closed=True, fill="#555555")
        v.poly([P(25.2, -2.2), P(25.35, -2.2), P(25.35, 2.2), P(25.2, 2.2)], "A-DOOR-PRES", closed=True, fill="#ffffff")
        for j in range(4):
            x = 11.5 + j * 3.5
            v.line(P(x, -2.0), P(x, 2.0), "S-STRC")
        v.text(P(17.0, 0.0), f"ARM D{k + 1} · Ø3 100 CLEAR · HANDRAILS", "A-ANNO-NOTE", h=1.3, rot=a if a < 90 or a > 270 else a - 180)
        v.text(P(27.5, 0.0), f"PORT P{k + 1}\nAPAS-TYPE\nØ800 HATCH", "A-ANNO-TEXT", h=1.4)
        v.poly([P(9.6, -1.1), P(9.0, -1.1), P(9.0, 1.1), P(9.6, 1.1)], "A-DOOR-PRES", closed=True, fill="#ffffff")
    v.circle((0, 0), 1.8, "A-DOOR-PRES"); v.text((0, 0), "HATCH Ø1 600\nUP TO SPINE S0", "A-ANNO-NOTE", h=1.2)
    v.circle((0, 0), 6.0, "A-HIDN"); v.circle((0, 0), 2.5, "A-HIDN")
    v.text((0, -4.3), "AXIAL PORT P0 BELOW (PROBE / CONE) · DESPUN DOCKING", "A-ANNO-NOTE", h=1.2)
    for a, lab in ((0, "CARGO STAGING"), (90, "AIRLOCK SUPPORT"), (180, "CREW RECEPTION"), (270, "STORES")):
        x, y = polar(7.2, a)
        v.text((x, y), lab, "A-ANNO-TEXT", h=1.4, bold=True)
    v.dim((-10.0, -27.0), (10.0, -27.0), -5)
    v.dim(polar(0, 45), polar(25.35, 45), 6)
    return v


def general_plan():
    """Station plan from +Z, 1:500: ring, spokes, spine, wings, hangars; dock arms dashed below."""
    v = View("ga-plan")
    n = 120
    for r, lay, fill in ((O.RO + T, "A-WALL-PRHU", "#d6d6d6"), (O.RI - T, "A-WALL-PRHU", "#ffffff")):
        v.poly([polar(r, 360 * i / n) for i in range(n)], lay, closed=True, fill=fill)
    for a in (0, 90, 180, 270):
        c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
        P = lambda x, y: (x * c - y * s, x * s + y * c)
        v.poly([P(O.SR, -2.4), P(O.RI - T, -2.4), P(O.RI - T, 2.4), P(O.SR, 2.4)], "A-WALL-PRHU", closed=True, fill="#ececec")
    v.poly([polar(O.SR, 360 * i / 48) for i in range(48)], "A-WALL-PRHU", closed=True, fill="#bdbdbd")
    for a in (45, 135, 225, 315):
        c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
        P = lambda x, y: (x * c - y * s, x * s + y * c)
        v.poly([P(10, -1.8), P(25.2, -1.8), P(25.2, 1.8), P(10, 1.8)], "A-HIDN", closed=True)
    v.circle((0, 0), 10.0, "A-HIDN")
    for side in (-1, 1):
        v.rect(side * (O.SR + 1.0), -0.8, side * (O.SR + 52.0), 0.8, "S-STRC")
        for j in range(4):
            x = side * (O.SR + 8.0 + j * 12.0)
            v.rect(x - 5.5, -0.6, x + 5.5, 0.6, "A-EQPM", fill="#29466b")
        v.text((side * 38, 3.0), "SOLAR WING (z 22 000, ABOVE RING)", "A-ANNO-NOTE", h=1.6)
    for a in (65.05, 245.05):
        c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
        P = lambda x, y: (x * c - y * s, x * s + y * c)
        v.poly([P(O.RO + T, -6.0), P(O.RO + T + 14.0, -6.0), P(O.RO + T + 14.0, 6.0), P(O.RO + T, 6.0)], "A-WALL", closed=True, fill="#bfbfbf")
        v.text(P(O.RO + T + 7, 0), "RIM HANGAR H" + ("1" if a < 180 else "2"), "A-ANNO-TEXT", h=1.8, bold=True, rot=a - 90 if a < 180 else a - 270)
    for k in range(1, 13):
        v.text(polar((O.RO + O.RI) / 2, O.COMP_ANGLE(k)), f"C{k:02d}", "A-ANNO-TEXT", h=1.6, bold=True)
        b = O.COMP_ANGLE(k) + 15
        v.line(polar(O.RI - T, b), polar(O.RO + T, b), "A-WALL-BHD")
    for a, lab in ((0, "SPOKE 1"), (90, "SPOKE 2"), (180, "SPOKE 3"), (270, "SPOKE 4")):
        v.text(polar(22, a + 6), lab, "A-ANNO-NOTE", h=1.5, rot=90 if a in (90, 270) else 0)
    v.dim((-O.RO - T, -52), (O.RO + T, -52), -6)
    v.dim((-O.SR - 52, -58), (O.SR + 52, -58), -8)
    v.section_mark((-O.RO - T - 6, 0), (O.RO + T + 6, 0), "A", "A-102", look=1)
    return v


def general_elevation():
    """Station elevation looking along +Y, 1:500."""
    v = View("ga-elev")
    v.rect(-O.RO - T, -HW - T, O.RO + T, HW + T, "A-WALL-PRHU", fill="#d6d6d6")
    v.rect(-O.SR, O.Z0, O.SR, O.Z1, "A-WALL-PRHU", fill="#bdbdbd")
    v.arc((0, O.Z1), O.SR, 0, 180, "A-GLAZ")
    v.rect(-10, O.Z0 - 6, 10, O.Z0, "A-WALL-PRHU", fill="#bdbdbd")
    v.poly([(-6, O.Z0 - 6), (6, O.Z0 - 6), (3, O.Z0 - 10), (-3, O.Z0 - 10)], "A-WALL-PRHU", closed=True, fill="#bdbdbd")
    v.rect(-2.5, O.Z0 - 12.5, 2.5, O.Z0 - 9.5, "A-WALL-PRHU", fill="#bdbdbd")
    for side in (-1, 1):
        v.rect(side * O.SR, 21.2, side * (O.SR + 52), 22.8, "S-STRC")
        for j in range(4):
            x = side * (O.SR + 8.0 + j * 12.0)
            for sz in (-1, 1):
                v.rect(x - 5.5, 22 + sz * 5.6 - 4.4, x + 5.5, 22 + sz * 5.6 + 4.4, "A-EQPM", fill="#29466b")
    v.line((5, O.Z1 + 4), (5, O.Z1 + 11), "S-STRC"); v.circle((5, O.Z1 + 12.5), 3.0, "A-EQPM")
    for z in O.SPINE_DECKS: v.line((-O.SR, z), (O.SR, z), "A-HIDN")
    for r in (O.RO + T,):
        v.line((-r - 3, 0), (r + 3, 0), "A-CNTR")
    v.line((0, O.Z0 - 14), (0, O.Z1 + 18), "A-CNTR")
    v.text((0, O.Z1 + 20), "SPIN AXIS +Z", "A-ANNO-NOTE", h=1.6)
    v.chain([(O.RO + T + 8, z) for z in (O.Z0 - 12.5, O.Z0, -HW - T, HW + T, O.Z1, O.Z1 + O.SR)], -8)
    v.dim((O.RO + T + 8, O.Z0 - 12.5), (O.RO + T + 8, O.Z1 + O.SR), -18)
    v.dim((-O.RO - T, -HW - T - 6), (O.RO + T, -HW - T - 6), -6)
    return v


def meridional_section():
    """Section A-A on the X-Z plane (through Spokes 1 and 3), 1:200."""
    v = View("section-AA")
    # ring tube sections at both ends: flat decks (down = -Z), the spoke tunnel level with Deck A
    for s in (-1, 1):
        pts = [(s * (O.R_MID + y), z) for y, z in ring_outer_profile()]
        v.poly(pts, "A-POCHE", closed=True, fill=FILL_HULL)
        x_in0, x_in1 = sorted((s * O.RO, s * O.RI))
        v.rect(x_in0, -O.ZH, x_in1, O.ZH, "A-VOID", fill="#ffffff")
        v.rect(x_in0, O.Z_B_STR, x_in1, O.Z_A, "S-STRC", fill="#9a9a9a")
        v.line((x_in0, O.Z_B), (x_in1, O.Z_B), "S-STRC"); v.line((x_in0, O.Z_A_CLG), (x_in1, O.Z_A_CLG), "S-STRC")
        v.text((s * O.R_MID, (O.Z_A + O.Z_A_CLG) / 2), "DECK A", "A-ANNO-TEXT", h=1.4)
        v.text((s * O.R_MID, (O.Z_B + O.Z_B_CLG) / 2), "DECK B", "A-ANNO-TEXT", h=1.4)
        z0s = O.Z_A - O.SPOKE_T
        v.rect(min(s * O.SR, s * (O.RI - T)), z0s, max(s * O.SR, s * (O.RI - T)), z0s + O.SPOKE_T, "A-WALL-PRHU", fill=FILL_HULL)
        v.rect(min(s * O.SR, s * (O.RI - T)), z0s + O.SPOKE_H - O.SPOKE_T, max(s * O.SR, s * (O.RI - T)), z0s + O.SPOKE_H, "A-WALL-PRHU", fill=FILL_HULL)
        v.rect(min(s * O.RI, s * (O.RI - T)) - 0.02, O.Z_A, max(s * O.RI, s * (O.RI - T)) + 0.02, z0s + O.SPOKE_H - O.SPOKE_T, "A-VOID", fill="#ffffff")
        v.text((s * 22, O.Z_A + 4.2), f"SPOKE {1 if s > 0 else 3} · LEVEL WALKWAY · CHASE", "A-ANNO-NOTE", h=1.3)
    # spine shell and decks
    for s in (-1, 1):
        v.rect(min(s * O.SRI, s * O.SR), O.Z0, max(s * O.SRI, s * O.SR), O.Z1, "A-POCHE", fill=FILL_HULL)
    for s in (-1, 1):
        v.rect(min(s * O.SRI, s * O.SR) - 0.02, O.Z_A, max(s * O.SRI, s * O.SR) + 0.02, O.Z_A + O.SPOKE_H - 2 * O.SPOKE_T, "A-VOID", fill="#ffffff")
    for z in O.SPINE_DECKS:
        for s in (-1, 1):
            v.rect(min(s * 1.8, s * O.SRI), z - 0.3, max(s * 1.8, s * O.SRI), z, "S-STRC", fill="#9a9a9a")
    for z in (-7.5, 4.5, 15.5):
        for s in (-1, 1): v.line((s * 1.8, z), (s * O.SRI, z), "S-STRC-HIDN")
    for lid, name, z0, z1, k in SPINE_LEVELS:
        if lid == "DOME": continue
        v.text((0, (z0 + z1) / 2), f"{lid}  {name.upper()}", "A-ANNO-NOTE", h=1.2)
    v.arc((0, O.Z1), O.SR, 0, 180, "A-GLAZ"); v.arc((0, O.Z1), O.SRI, 0, 180, "A-GLAZ")
    v.rect(-O.SRI, O.Z1 - 0.3, O.SRI, O.Z1, "S-STRC", fill="#9a9a9a")
    v.text((0, O.Z1 + 3.0), "COMMAND DECK", "A-ANNO-TEXT", h=1.6, bold=True)
    # dock hub
    v.rect(-10, O.Z0 - 6, 10, O.Z0, "A-POCHE", fill=FILL_HULL); v.rect(-9.6, O.Z0 - 5.6, 9.6, O.Z0 - 0.4, "A-VOID", fill="#ffffff")
    v.poly([(-6, O.Z0 - 6), (6, O.Z0 - 6), (3, O.Z0 - 10), (-3, O.Z0 - 10)], "A-WALL-PRHU", closed=True, fill=FILL_HULL)
    v.rect(-2.5, O.Z0 - 12.5, 2.5, O.Z0 - 9.5, "A-WALL-PRHU", fill=FILL_HULL)
    v.text((0, O.Z0 - 3), "DOCKING HUB", "A-ANNO-TEXT", h=1.6, bold=True)
    v.line((0, O.Z0 - 14), (0, O.Z1 + O.SR + 3), "A-CNTR")
    zl = O.SR + 6
    for z in [O.Z0 - 12.5, O.Z0 - 6, O.Z0] + O.SPINE_DECKS + [O.Z1, O.Z1 + O.SR]:
        v.line((zl - 0.5, z), (zl + 2.0, z), "A-ANNO-DIMS")
        v.text((zl + 2.3, z), f"z {z * 1000:+.0f}", "A-ANNO-NOTE", h=1.1, ha="left")
    zb = -O.ZH - T
    v.chain([(-O.R_HULL_OUT, zb), (-O.RI + T, zb), (-O.SR, zb), (O.SR, zb), (O.RI - T, zb), (O.R_HULL_OUT, zb)], (O.Z0 - 14 - zb) * 5)
    v.dim((-O.R_HULL_OUT, zb), (O.R_HULL_OUT, zb), (O.Z0 - 14 - zb) * 5 - 10)
    return v


# ================================================================== schematic: ECLSS air & water (paper space, NTS)
def eclss_schematic(v, x, y):
    def box(bx, by, w, h, title, sub="", col="#ffffff"):
        v.rect(bx, by, bx + w, by + h, "M-HVAC-EQPM", fill=col)
        v.text((bx + w / 2, by + h / 2 + (2 if sub else 0)), title, "M-ANNO-TEXT", h=2.2, bold=True)
        if sub: v.text((bx + w / 2, by + h / 2 - 3), sub, "M-ANNO-TEXT", h=1.6)
    def arrow(p0, p1, lay, label=None, side=1):
        v.line(p0, p1, lay)
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]; L = math.hypot(dx, dy); ux, uy = dx / L, dy / L
        v.poly([p1, (p1[0] - ux * 3 - uy * 1.2, p1[1] - uy * 3 + ux * 1.2), (p1[0] - ux * 3 + uy * 1.2, p1[1] - uy * 3 - ux * 1.2)], lay, closed=True, fill=LAY_COL[lay])
        if label: v.text(((p0[0] + p1[0]) / 2 - uy * 3 * side, (p0[1] + p1[1]) / 2 + ux * 3 * side), label, "M-ANNO-TEXT", h=1.5)
    # ring zones and AHUs (x4), spoke ducts, hub manifolds, ARS
    for i in range(4):
        bx = x + i * 95
        box(bx, y + 150, 80, 22, f"RING ZONES Q{i + 1}", f"C{[1, 4, 7, 10][i] - 1 if i else 12:02d}·C{[1, 4, 7, 10][i]:02d}·C{[1, 4, 7, 10][i] + 1:02d} · DECKS A + B", "#f4f4f4")
        box(bx + 15, y + 105, 50, 22, f"AHU-R{i + 1}", "FAN · CHW COIL · HEPA H13", "#e8eef8")
        arrow((bx + 30, y + 127), (bx + 30, y + 150), "M-DUCT-SUPP", "SA", side=-1)
        arrow((bx + 50, y + 150), (bx + 50, y + 127), "M-DUCT-RETN", "RA")
        arrow((bx + 25, y + 70), (bx + 25, y + 105), "M-DUCT-OAIR", f"OA (SPOKE {i + 1})", side=-1)
        arrow((bx + 55, y + 105), (bx + 55, y + 70), "M-DUCT-RETN", "RH")
        arrow((bx + 75, y + 150), (bx + 75, y + 70), "M-DUCT-EXHS", "EA")
    box(x, y + 45, 370, 22, "HUB TRANSFER NODE S3a · OA / RH / EA RING MANIFOLDS", "", "#f4f4f4")
    box(x + 10, y + 5, 70, 26, "4BMS-1/2", "CO₂ REMOVAL", "#fff6dc")
    box(x + 95, y + 5, 70, 26, "OGA-1/2", "O₂ BY ELECTROLYSIS", "#fff6dc")
    box(x + 180, y + 5, 70, 26, "SABATIER", "CO₂ + 4H₂ → CH₄ + 2H₂O", "#fff6dc")
    box(x + 265, y + 5, 70, 26, "TCCS", "TRACE CONTAMINANTS", "#fff6dc")
    arrow((x + 45, y + 45), (x + 45, y + 31), "M-DUCT-RETN", "RH", side=-1)
    arrow((x + 300, y + 45), (x + 300, y + 31), "M-DUCT-EXHS", "EA")
    arrow((x + 80, y + 18), (x + 95, y + 18), "M-DUCT-OAIR")
    arrow((x + 130, y + 31), (x + 130, y + 45), "M-DUCT-OAIR", "OA", side=-1)
    arrow((x + 165, y + 18), (x + 180, y + 18), "P-PIPE", "H₂")
    arrow((x + 250, y + 18), (x + 265, y + 18), "M-DUCT-OAIR")
    box(x + 395, y + 5, 80, 26, "WATER RECOVERY", "WPA · UPA · 93% RECOVERY", "#dcf0f0")
    arrow((x + 250, y + 12), (x + 395, y + 12), "P-PIPE", "H₂O (SABATIER)", side=-1)
    arrow((x + 435, y + 31), (x + 435, y + 150), "P-PIPE", "CONDENSATE · GREYWATER")
    arrow((x + 455, y + 150), (x + 455, y + 31), "P-PIPE", "POTABLE")
    arrow((x + 395, y + 25), (x + 165, y + 25), "P-PIPE", "H₂O → OGA")
    box(x + 395, y + 105, 80, 22, "AHU-S (SPINE)", "S2b · 3.2 m³/s", "#e8eef8")
    arrow((x + 395, y + 60), (x + 395, y + 105), "M-DUCT-OAIR", "OA")


LAY_COL = {"M-DUCT-SUPP": "#0050c8", "M-DUCT-RETN": "#c84b00", "M-DUCT-EXHS": "#7a00a8", "M-DUCT-OAIR": "#008a3a", "P-PIPE": "#007a7a"}
