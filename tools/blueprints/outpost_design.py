"""
Outpost starbase (tier 0): the interior design, as geometry. ONE BUILD: the exterior (assets/stations/outpost/build.py)
reads openings.json written from here, so every window, skylight, spoke and hangar door outside is a room inside.

GRAVITY (REV B): generated, 1.0 g, pointing DOWN = -Z everywhere (toward the docking hub), like the spine and hangars.
So the ring's decks are FLAT floors stacked along Z, and its curved hull faces are WALLS: the outer face (r 44) and the
hub face (r 36) carry the windows; the roof (+Z face) carries the skylights over the corridor.

Station axes: Z is the spin axis (+Z toward the command dome). Angle theta is measured from +X, counter-clockwise seen
from +Z. Spoke 1 is at 0 deg, then 90, 180, 270.
Plan geometry is written in compartment-local DEVELOPED coordinates (x along the ring at R_MID = 40.0, y = r - R_MID,
+y OUTWARD) and drawn in TRUE PLAN through PolarView, so walls along x come out as arcs and cross walls as radials.
"""
import math
from draft import View

# ------------------------------------------------------------------ envelope (build.py)
RO, RI, T = 44.0, 36.0, 0.6                   # ring pressure hull inner faces: outer wall r, hub-side wall r; shell
HW = (RO - RI) / 2                            # 4.000: plan half-depth (radial), +y outward
R_MID = (RO + RI) / 2                         # 40.000: plans are developed at this radius
SR, SRI, Z0, Z1 = 8.0, 7.4, -26.0, 30.0       # spine outer / inner radius, bottom, top
SPINE_DECKS = [-22.0, -13.5, -0.2, 10.0, 21.0]
SPOKE_W, SPOKE_H, SPOKE_T = 4.8, 3.2, 0.25    # spoke outer tangential width, height, wall
SPOKE_Z = 1.4                                 # spoke centre height: its floor is Deck A's floor and the spine hub deck
BH_T = 0.6                                    # pressure bulkhead thickness
STEP_DEG = 3.75                               # structural frame pitch (96 frames); windows sit one per frame bay

# ------------------------------------------------------------------ deck stack (z, metres; down = -Z)
ZH = 4.0                                      # pressure hull inner faces at z = +/-4.000 (skins at +/-4.600)
Z_SUB = -ZH                                   # -4.000 top of the floor hull: Deck B subfloor plenum 0.500
Z_B = -3.5                                    # Deck B finished floor
Z_B_CLG = -0.9                                # Deck B ceiling (2.600 clear)
Z_B_STR = -0.5                                # underside of the Deck A floor slab (interstitial void 0.400)
Z_A = -0.2                                    # Deck A finished floor (= spoke floor = spine hub deck)
Z_A_CLG = 3.0                                 # Deck A ceiling (3.200 clear)
Z_ROOF = ZH                                   # +4.000 underside of the roof hull (roof void 1.000: ducts, skylight wells)
# windows (z above datum) and skylights; widths measured on the wall they sit in
WIN_OUT = {"A": (Z_A + 0.8, Z_A + 2.6, 1.6), "B": (Z_B + 0.8, Z_B + 2.0, 1.4)}    # outer wall: sill, head, width
WIN_IN = {"A": (Z_A + 0.8, Z_A + 2.4, 1.2)}                                          # hub wall, Deck A only
SKY = (R_MID - 0.96, R_MID + 0.96)                                                   # roof skylights over the corridor
FFL = {"A": Z_A, "B": Z_B}
CLG = {"A": Z_A_CLG, "B": Z_B_CLG}
R_A = R_B = R_MID          # plans of both decks are developed at R_MID
R_HULL_OUT = RO + T

G_ART = 1.0
def gee(r=None): return G_ART

def L_of(r): return r * math.pi / 6     # one 30-degree compartment, developed at radius r
LA = LB = L_of(R_MID)
STEP = L_of(R_MID) / 8                 # one frame bay at R_MID: 2.618

COMP_ANGLE = lambda k: 30.0 * (k - 1)   # compartment centre (deg); C01 on Spoke 1
JUNCTION = {1: 1, 4: 2, 7: 3, 10: 4}     # compartment -> spoke number
HANGAR_DEG = {3: 65.05, 9: 245.05}       # rim hangar bays on the outer wall, floor = Deck B (build.py: 45 deg + 0.35 rad)
HANGAR_W = 12.0

PROGRAM_A = {1: "J", 2: "HYD", 3: "HYD", 4: "J", 5: "QA", 6: "QA", 7: "J", 8: "GAL", 9: "REC", 10: "J", 11: "WKS", 12: "MED"}
PROGRAM_B = {1: "JB", 2: "HYP", 3: "WAT", 4: "JB", 5: "QB", 6: "QB", 7: "JB", 8: "GST", 9: "TRB", 10: "JB", 11: "STO", 12: "LAB"}
J_ROOM = {1: "OPERATIONS OFFICE", 4: "LIBRARY / QUIET ROOM", 7: "COUNSELLING / MULTI-FAITH", 10: "DAMAGE CONTROL STATION"}
AHU_OF = {1: 1, 4: 2, 7: 3, 10: 4}

DOORS = []          # (compartment, kind) for every tagged door drawn: the door-type schedule counts these
TS = [1.0]          # annotation scale: enlarged views (1:25 / 1:50) set this > 1 while they are built
FILL_HULL = "#1e1e1e"
FILL_WALL = "#a9a9a9"
FILL_SHAFT = "#d0d0d0"
FILL_ROOM = None

# airflow design (see M-002)
ACH_A, ACH_B = 6.0, 6.0
OA_PER_PERSON = 0.012     # m3/s revitalised air exchanged with the hub ECLSS, per person


class PolarView(View):
    """Draws developed (x, y) as TRUE PLAN: x is arc length at R_MID, y = r - R_MID (+ outward). The view is set so that
    the middle of the drawn run (x = xm) lands at (xm, y) and the ring curves away (down the sheet) on both sides;
    outward is UP the sheet there. Lines are subdivided so arcs stay smooth; text turns with the ring."""

    def __init__(self, name, xm=0.0, r=R_MID):
        super().__init__(name); self.xm, self.r = xm, r

    def T(self, p):
        phi = (p[0] - self.xm) / self.r; rr = self.r + p[1]
        return (self.xm + rr * math.sin(phi), rr * math.cos(phi) - self.r)

    def turn(self, x): return -math.degrees((x - self.xm) / self.r)

    def _p(self, kind, layer, **d):
        if "pts" in d:
            pts = list(d["pts"]); closed = d.get("closed", False)
            out = []
            segs = list(zip(pts, pts[1:] + ([pts[0]] if closed else [])))
            for i, (a, b) in enumerate(segs):
                n = max(1, int(abs(b[0] - a[0]) / 0.25 + 0.999))
                out += [(a[0] + (b[0] - a[0]) * j / n, a[1] + (b[1] - a[1]) * j / n) for j in range(n)]
            if not closed: out.append(pts[-1])
            d["pts"] = [self.T(q) for q in out]
        if kind == "text": d["rot"] = d.get("rot", 0) + self.turn(d["p"][0])
        if kind == "arc": t = self.turn(d["c"][0]); d["a0"] += t; d["a1"] += t
        for k in ("c", "p", "p0", "p1"):
            if k in d: d[k] = self.T(d[k])
        View._p(self, kind, layer, **d)


class D:
    """A drawing pen offset onto a sheet: draws compartment-local geometry at x*sx + ox."""

    def __init__(self, v, ox=0.0, oy=0.0, sx=1.0):
        self.v, self.ox, self.oy, self.sx = v, ox, oy, sx
        self.rooms = []
        self.dp, self.dn = None, 0       # door-tag prefix (the compartment, e.g. A05) and counter
        self.vx = []                     # cross partitions (x, y0, y1) for the dimension strings
        self.dj = []                     # door openings in walls along x: (a, b, y)

    def door_tag(self, x, y, kind="S"):
        if self.dp is None: return
        self.dn += 1
        DOORS.append((self.dp, kind))
        self.v.symtag(self.P(x, y), f"{self.dp}.{self.dn:02d}", "ellipse", h=0.95 * TS[0])

    # ---- fixtures and furniture (plan symbols)
    def ellipse(self, cx, cy, rx, ry, layer="A-FURN", n=28):
        self.v.poly([self.P(cx + rx * math.cos(2 * math.pi * i / n), cy + ry * math.sin(2 * math.pi * i / n)) for i in range(n)], layer, closed=True)

    def bed(self, x0, y0, x1, y1, head="x0", label=None):
        """Bed: frame, pillow at the head end (x0, x1, y0 or y1), the turned-down sheet."""
        self.box(x0, y0, x1, y1, "A-FURN")
        if head in ("x0", "x1"):
            s = 1 if head == "x0" else -1; e = x0 if s > 0 else x1
            self.box(min(e + s * 0.06, e + s * 0.38), y0 + 0.08, max(e + s * 0.06, e + s * 0.38), y1 - 0.08, "A-FURN")
            self.line(e + s * 0.62, y0, e + s * 0.62, y1); self.line(e + s * 0.62, y0, e + s * 0.92, y0 + (y1 - y0) * 0.45)
        else:
            s = 1 if head == "y0" else -1; e = y0 if s > 0 else y1
            self.box(x0 + 0.08, min(e + s * 0.06, e + s * 0.38), x1 - 0.08, max(e + s * 0.06, e + s * 0.38), "A-FURN")
            self.line(x0, e + s * 0.62, x1, e + s * 0.62); self.line(x0, e + s * 0.62, x0 + (x1 - x0) * 0.45, e + s * 0.92)
        if label: self.text((x0 + x1) / 2, (y0 + y1) / 2 + (0 if head in ("x0", "x1") else (-0.3 if head == "y1" else 0.3)), label, h=0.95,
                            rot=0 if head in ("x0", "x1") else 90)

    def wardrobe(self, x0, y0, x1, y1):
        self.box(x0, y0, x1, y1, "A-FURN")
        if (x1 - x0) >= (y1 - y0):
            ym = (y0 + y1) / 2; self.line(x0 + 0.05, ym, x1 - 0.05, ym, "A-HIDN")
            for i in range(1, 5):
                x = x0 + (x1 - x0) * i / 5; self.line(x - 0.06, y0 + 0.08, x + 0.06, y1 - 0.08)
        else:
            xm = (x0 + x1) / 2; self.line(xm, y0 + 0.05, xm, y1 - 0.05, "A-HIDN")
            for i in range(1, 5):
                y = y0 + (y1 - y0) * i / 5; self.line(x0 + 0.08, y - 0.06, x1 - 0.08, y + 0.06)

    def desk(self, x0, y0, x1, y1, chair=None):
        self.box(x0, y0, x1, y1, "A-FURN")
        if chair:
            cx, cy = chair; s = 0.22
            self.box(cx - s, cy - s, cx + s, cy + s, "A-FURN")
            self.line(cx - s, cy - s, cx - s + 0.05, cy + s)

    def wc(self, x, y, facing=(0, 1)):
        """Wall-hung WC: cistern against the wall, pan projecting along `facing`."""
        fx, fy = facing
        if fy:
            self.box(x - 0.2, y - fy * 0.09 - 0.09, x + 0.2, y - fy * 0.09 + 0.09, "A-FURN")
            self.ellipse(x, y + fy * 0.28, 0.18, 0.27)
            self.ellipse(x, y + fy * 0.3, 0.11, 0.17)
        else:
            self.box(x - fx * 0.09 - 0.09, y - 0.2, x - fx * 0.09 + 0.09, y + 0.2, "A-FURN")
            self.ellipse(x + fx * 0.28, y, 0.27, 0.18)
            self.ellipse(x + fx * 0.3, y, 0.17, 0.11)

    def basin(self, x, y, facing=(0, 1), w=0.5):
        fx, fy = facing
        if fy:
            self.box(x - w / 2, y - (0.0 if fy > 0 else 0.42), x + w / 2, y + (0.42 if fy > 0 else 0.0), "A-FURN")
            self.ellipse(x, y + fy * 0.23, w * 0.36, 0.14); self.circ(x, y + fy * 0.06, 0.025)
        else:
            self.box(x - (0.0 if fx > 0 else 0.42), y - w / 2, x + (0.42 if fx > 0 else 0.0), y + w / 2, "A-FURN")
            self.ellipse(x + fx * 0.23, y, 0.14, w * 0.36); self.circ(x + fx * 0.06, y, 0.025)

    def shower(self, x0, y0, x1, y1):
        self.box(x0, y0, x1, y1, "A-FURN")
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        for px, py in ((x0, y0), (x1, y0), (x1, y1), (x0, y1)): self.line(px, py, cx, cy)
        self.circ(cx, cy, 0.06, fill="#ffffff")

    def sink(self, x0, y0, x1, y1, bowls=2):
        self.box(x0, y0, x1, y1, "A-FURN")
        w = (x1 - x0) / bowls
        for i in range(bowls):
            self.box(x0 + i * w + 0.06, y0 + 0.08, x0 + (i + 1) * w - 0.06, y1 - 0.08, "A-FURN")
            self.circ(x0 + (i + 0.5) * w, (y0 + y1) / 2, 0.03)

    def hob(self, x0, y0, x1, y1, n=4):
        self.box(x0, y0, x1, y1, "A-EQPM")
        w = (x1 - x0) / (n // 2); h = (y1 - y0) / 2
        for i in range(n // 2):
            for j in range(2): self.circ(x0 + (i + 0.5) * w, y0 + (j + 0.5) * h, min(w, h) * 0.3, "A-EQPM")

    def P(self, x, y): return (x * self.sx + self.ox, y + self.oy)

    # ---- cut walls
    def wall_rect(self, x0, y0, x1, y1, layer="A-WALL", fill=FILL_WALL):
        if x1 - x0 < 1e-4 or y1 - y0 < 1e-4: return
        self.v.rect(*self.P(x0, y0), *self.P(x1, y1), layer, fill=fill)

    def hwall(self, x0, x1, y, gaps=(), t=0.12, layer="A-WALL", fill=FILL_WALL):
        xs = [x0]
        for a, b in sorted(gaps): xs += [a, b]
        xs.append(x1)
        for a, b in zip(xs[::2], xs[1::2]): self.wall_rect(a, y - t / 2, b, y + t / 2, layer, fill)

    def vwall(self, x, y0, y1, gaps=(), t=0.12, layer="A-WALL", fill=FILL_WALL):
        self.vx.append((x * self.sx, min(y0, y1), max(y0, y1)))
        ys = [y0]
        for a, b in sorted(gaps): ys += [a, b]
        ys.append(y1)
        for a, b in zip(ys[::2], ys[1::2]): self.wall_rect(x - t / 2, a, x + t / 2, b, layer, fill)

    def box(self, x0, y0, x1, y1, layer="A-FURN", label=None, h=1.4, hatch=None, fill=None):
        self.v.rect(*self.P(x0, y0), *self.P(x1, y1), layer, hatch=hatch, fill=fill)
        if label: self.v.text(self.P((x0 + x1) / 2, (y0 + y1) / 2), label, "A-ANNO-NOTE", h=h * TS[0], rot=90 if (y1 - y0) > (x1 - x0) * 1.6 and (y1 - y0) > 1.2 else 0)

    def circ(self, x, y, r, layer="A-FURN", fill=None, label=None, h=1.3):
        self.v.circle(self.P(x, y), r, layer, fill=fill)
        if label: self.v.text(self.P(x, y), label, "A-ANNO-NOTE", h=h)

    def line(self, x0, y0, x1, y1, layer="A-FURN"): self.v.line(self.P(x0, y0), self.P(x1, y1), layer)

    def text(self, x, y, s, layer="A-ANNO-NOTE", h=1.6, **k): self.v.text(self.P(x, y), s, layer, h=h * TS[0], **k)

    def room(self, name, num, x0, y0, x1, y1, tag_at=None, show_area=True):
        a = (x1 - x0) * self.sx * (y1 - y0)
        self.rooms.append((num, name, a))
        tx, ty = tag_at or ((x0 + x1) / 2, (y0 + y1) / 2)
        self.v.tag(self.P(tx, ty), name, num, a if show_area else None, h=1.7 * TS[0])

    # ---- doors
    def slide_h(self, a, b, y, side=1, pocket=-1):
        """Sliding door in a wall along x at y, opening a..b; leaf drawn open, on `side`, toward `pocket`."""
        w = b - a
        x0 = a - w + 0.05 if pocket < 0 else b - 0.05
        self.v.rect(*self.P(x0, y + side * 0.07), *self.P(x0 + w, y + side * 0.11), "A-DOOR")
        self.line(a, y - 0.06, a, y + 0.06, "A-DOOR"); self.line(b, y - 0.06, b, y + 0.06, "A-DOOR")
        self.dj.append((a * self.sx, b * self.sx, y))
        self.door_tag((a + b) / 2, y - side * 0.42, f"S{round(w * 1000)}")

    def slide_v(self, x, a, b, side=1, pocket=-1):
        w = b - a
        y0 = a - w + 0.05 if pocket < 0 else b - 0.05
        self.v.rect(*self.P(x + side * 0.07, y0), *self.P(x + side * 0.11, y0 + w), "A-DOOR")
        self.door_tag(x - side * 0.5, (a + b) / 2, f"S{round(w * 1000)}")

    def swing(self, hinge, w, a0, a1, layer="A-DOOR"):
        """Hinged leaf: hinge point, width, from angle a0 (closed, along the wall) to a1 (open) in degrees."""
        hx, hy = hinge
        r1 = math.radians(a1)
        self.line(hx, hy, hx + w * math.cos(r1), hy + w * math.sin(r1), layer)
        lo, hi = (a0, a1) if (a1 - a0) % 360 <= 180 else (a1, a0)
        self.v.arc(self.P(hx, hy), w, lo, hi, layer)
        r0 = math.radians(a0)
        if a0 % 180 == 0: self.dj.append(tuple(sorted((hx * self.sx, (hx + w * math.cos(r0)) * self.sx))) + (hy,))
        mx, my = hx + w / 2 * math.cos(r0), hy + w / 2 * math.sin(r0)
        ox, oy = -math.cos(r1) * 0.42, -math.sin(r1) * 0.42       # the side the leaf does not swing into
        self.door_tag(mx + ox, my + oy, f"H{round(w * 1000)}")

    def pressure_door_wall(self, a, b, lo, hi):
        """A sliding pressure door in a wall along x (the hull), opening a..b."""
        self.v.rect(*self.P(a - (b - a) + 0.05, (lo + hi) / 2 - 0.05), *self.P(a + 0.05, (lo + hi) / 2 + 0.05), "A-DOOR-PRES", fill="#ffffff")
        self.line(a, lo, a, hi, "A-DOOR-PRES"); self.line(b, lo, b, hi, "A-DOOR-PRES")
        self.line(a, (lo + hi) / 2, b, (lo + hi) / 2, "A-HIDN")

    def pressure_door(self, x, a, b, pocket_to=None, deck="A"):
        """A sliding pressure door in a bulkhead at x (opening a..b along y), leaf parked in the bulkhead's pocket."""
        pb = pocket_to if pocket_to is not None else b + (b - a) * 1.05
        self.v.rect(*self.P(x - 0.05, b), *self.P(x + 0.05, pb), "A-DOOR-PRES", fill="#ffffff")
        self.line(x - BH_T / 2, a, x + BH_T / 2, a, "A-DOOR-PRES"); self.line(x - BH_T / 2, b, x + BH_T / 2, b, "A-DOOR-PRES")
        self.line(x, a, x, b, "A-HIDN")


# ------------------------------------------------------------------ ring: hull, bulkheads, windows, grids
def ang_diff(a, b): return (a - b + 180.0) % 360.0 - 180.0


def hangar_at(a_deg):
    """Is this angle inside a rim hangar's footprint on the outer wall?"""
    return any(abs(ang_diff(a_deg, h)) * math.pi / 180 * (RO + T) < HANGAR_W / 2 + 0.6 for h in HANGAR_DEG.values())


def spoke_at(a_deg, r=RI):
    return any(abs(ang_diff(a_deg, 90.0 * k)) * math.pi / 180 * r < SPOKE_W / 2 + 0.3 for k in range(4))


def openings():
    """Every opening in the ring's skin, for the exterior build: one window per frame bay on the outer wall (both decks,
    not behind the hangars), on the hub wall (Deck A, not at the spokes), skylights over the corridor, spoke portals
    and hangar doors. Angles in degrees at the bay centre; widths in metres on their wall."""
    out = {"outer": [], "inner": [], "roof": [], "spokes": [], "hangars": []}
    for s_ in range(96):
        a = (s_ + 0.5) * STEP_DEG
        if not hangar_at(a):
            for dk in ("A", "B"):
                z0, z1, w = WIN_OUT[dk]; out["outer"].append(dict(a=a, w=w, z0=z0, z1=z1, deck=dk))
        if not spoke_at(a):
            z0, z1, w = WIN_IN["A"]; out["inner"].append(dict(a=a, w=w, z0=z0, z1=z1, deck="A"))
        out["roof"].append(dict(a0=s_ * STEP_DEG + 0.3, a1=(s_ + 1) * STEP_DEG - 0.3, r0=SKY[0], r1=SKY[1]))
    for k in range(4): out["spokes"].append(dict(a=90.0 * k, z0=Z_A, z1=Z_A + SPOKE_H - 2 * SPOKE_T, w=SPOKE_W - 2 * SPOKE_T))
    for h in HANGAR_DEG.values(): out["hangars"].append(dict(a=h, z0=Z_B, door_w=2.4, door_h=2.4))
    return out


def ring_shell(d, x0c, x1c, r_floor, deck, theta0_deg):
    """The two curved walls of the deck (pressure hull, cut) from x0c to x1c, glazed one window per frame bay as in
    openings(): outer wall (+y) both decks, hub wall (-y) Deck A; open at the spoke portal, a door at a hangar."""
    win_tag = TS[0] > 1
    for sgn in (-1, 1):
        y_in, y_out = sgn * HW, sgn * (HW + T)
        lo, hi = min(y_in, y_out), max(y_in, y_out)
        r_wall = RO if sgn > 0 else RI
        glazed = sgn > 0 or deck == "A"
        if not glazed:
            d.wall_rect(x0c, lo, x1c, hi, "A-WALL-PRHU", FILL_HULL); continue
        w_wall = (WIN_OUT[deck] if sgn > 0 else WIN_IN["A"])[2]
        w_dev = w_wall * R_MID / r_wall
        k0 = math.floor((theta0_deg + x0c / R_MID * 180 / math.pi) / STEP_DEG) - 1
        k1 = math.ceil((theta0_deg + x1c / R_MID * 180 / math.pi) / STEP_DEG) + 1
        solid = []          # hull spans to draw
        for s_ in range(k0, k1):
            a_c = (s_ + 0.5) * STEP_DEG
            xa = (s_ * STEP_DEG - theta0_deg) * math.pi / 180 * R_MID
            xb = ((s_ + 1) * STEP_DEG - theta0_deg) * math.pi / 180 * R_MID
            if xb <= x0c or xa >= x1c: continue
            xm = (xa + xb) / 2
            if sgn < 0 and spoke_at(a_c):
                # the spoke portal: the hub wall is open between the spoke's walls (drawn by the junction)
                xs0 = (90.0 * round(a_c / 90.0) - theta0_deg) * math.pi / 180 * R_MID
                hw_ = (SPOKE_W / 2) * R_MID / RI
                for u0, u1 in ((xa, min(xb, xs0 - hw_)), (max(xa, xs0 + hw_), xb)):
                    if u1 > u0: solid.append((u0, u1))
                continue
            if sgn > 0 and hangar_at(a_c):
                solid.append((xa, xb)); continue
            solid.append((xa, xm - w_dev / 2)); solid.append((xm + w_dev / 2, xb))
            g0, g1 = max(xm - w_dev / 2, x0c), min(xm + w_dev / 2, x1c)
            if g1 > g0:
                d.line(g0, lo, g1, lo, "A-GLAZ"); d.line(g0, hi, g1, hi, "A-GLAZ")
                d.line(g0, (lo + hi) / 2 - 0.03, g1, (lo + hi) / 2 - 0.03, "A-GLAZ"); d.line(g0, (lo + hi) / 2 + 0.03, g1, (lo + hi) / 2 + 0.03, "A-GLAZ")
                if win_tag: d.v.symtag(d.P(xm, sgn * (HW + T + 0.55)), ("W1" if deck == "A" else "W2") if sgn > 0 else "W3", "hex", h=0.95 * TS[0])
        for u0, u1 in solid:
            u0, u1 = max(u0, x0c), min(u1, x1c)
            if u1 > u0 + 1e-4: d.wall_rect(u0, lo, u1, hi, "A-WALL-PRHU", FILL_HULL)
        # a hangar's pressure door in the outer wall (Deck B)
        if sgn > 0 and deck == "B":
            for h in HANGAR_DEG.values():
                xh = ang_diff(h, theta0_deg) * math.pi / 180 * R_MID
                if x0c - 3 < xh < x1c + 3:
                    d.wall_rect(max(x0c, xh - 3.0), lo, min(x1c, xh - 1.2), hi, "A-WALL-PRHU", FILL_HULL)
                    d.wall_rect(max(x0c, xh + 1.2), lo, min(x1c, xh + 3.0), hi, "A-WALL-PRHU", FILL_HULL)
                    d.pressure_door_wall(xh - 1.2, xh + 1.2, lo, hi)


def bulkhead(d, x, deck, num):
    """Pressure bulkhead at x: 600 thick, door opening on the centreline."""
    a, b = ((-0.6, 0.6) if deck == "A" else (-0.5, 0.5))
    for y0, y1 in ((-HW, a), (b, HW)):
        d.wall_rect(x - BH_T / 2, y0, x + BH_T / 2, y1, "A-WALL-BHD", FILL_HULL)
    d.pressure_door(x, a, b, deck=deck)
    d.v.symtag(d.P(x, b + (b - a) * 1.05 + 0.75), f"PD{num}{deck}", "hex", h=0.95 * TS[0])


# ------------------------------------------------------------------ Deck A compartment programmes (local coords)
SX_A = (LA / 2 - BH_T / 2) / 11.062          # Deck A rooms were laid out for an 11.06 m half-length: fit them


def deckA(d, k, L):
    kind = PROGRAM_A[k]
    n = f"A{k:02d}"
    d.dp = n
    if kind != "QA": d.sx = SX_A
    xs = (L / 2 - BH_T / 2) / d.sx      # usable half-length, in the pen's own units
    if kind == "J": junctionA(d, k, xs, n)
    elif kind == "HYD": hydroponics(d, k, xs, n)
    elif kind == "QA": quarters(d, k, xs, n, deck="A")
    elif kind == "GAL": galley(d, k, xs, n)
    elif kind == "REC": recreation(d, k, xs, n)
    elif kind == "WKS": workshop(d, k, xs, n)
    elif kind == "MED": medical(d, k, xs, n)


def deckB(d, k, L):
    xs = L / 2 - BH_T / 2
    kind = PROGRAM_B[k]
    n = f"B{k:02d}"
    d.dp = n
    if kind == "JB": junctionB(d, k, xs, n)
    elif kind == "QB": quarters(d, k, xs, n, deck="B")
    elif kind == "HYP": hydro_support(d, k, xs, n)
    elif kind == "WAT": water_store(d, k, xs, n)
    elif kind == "GST": galley_stores(d, k, xs, n)
    elif kind == "TRB": transient(d, k, xs, n)
    elif kind == "STO": stores(d, k, xs, n)
    elif kind == "LAB": laboratory(d, k, xs, n)


# Spoke in plan (both decks): tangential x, axial y. Clear section 4.300 x 2.700.
SPK = dict(x0=-2.15, x1=2.15, y0=-1.35, y1=1.35)
LIFT = dict(x0=-2.15, x1=-0.15, y0=-1.1, y1=1.1)          # lift shaft (inside face)
LADDER = dict(x0=0.0, x1=1.0, y0=-0.6, y1=0.6)            # ladder trunk (spoke and Deck B only)
CHASE = dict(x0=1.15, x1=2.15, y0=-1.35, y1=1.35)         # spoke service chase
RSHAFT = dict(x0=0.0, x1=1.35, y0=-1.1, y1=1.1)           # Deck A return-air + electrical riser shaft


def lift_core(d, deck, n):
    L = LIFT
    t = 0.15
    # shaft walls (rated), landing door on +y
    d.wall_rect(L["x0"] - t, L["y0"] - t, L["x1"] + t, L["y0"], "A-WALL-SHFT", FILL_SHAFT)
    d.wall_rect(L["x0"] - t, L["y0"], L["x0"], L["y1"] + t, "A-WALL-SHFT", FILL_SHAFT)
    d.wall_rect(L["x1"], L["y0"], L["x1"] + t, L["y1"] + t, "A-WALL-SHFT", FILL_SHAFT)
    d.wall_rect(L["x0"], L["y1"], -1.75, L["y1"] + t, "A-WALL-SHFT", FILL_SHAFT)
    d.wall_rect(-0.55, L["y1"], L["x1"], L["y1"] + t, "A-WALL-SHFT", FILL_SHAFT)
    d.box(-1.95, -0.95, -0.35, 0.95, "A-EQPM")                       # car 1600 x 1900
    d.line(-1.95, -0.95, -0.35, 0.95, "A-EQPM"); d.line(-1.95, 0.95, -0.35, -0.95, "A-EQPM")
    d.slide_h(-1.75, -0.55, L["y1"] + 0.075, side=1, pocket=1)
    d.text(-1.15, -1.5, "630 kg · 8 P", h=1.1)


def junctionA(d, k, xs, n):
    sp = JUNCTION[k]
    lift_core(d, "A", sp)
    # return-air and electrical riser shaft beside the lift
    R = RSHAFT
    d.wall_rect(R["x0"], R["y0"] - 0.15, R["x1"] + 0.15, R["y0"], "A-WALL-SHFT", FILL_SHAFT)
    d.wall_rect(R["x1"], R["y0"], R["x1"] + 0.15, R["y1"] + 0.15, "A-WALL-SHFT", FILL_SHAFT)
    d.wall_rect(R["x0"], R["y1"], R["x1"] + 0.15, R["y1"] + 0.15, "A-WALL-SHFT", FILL_SHAFT)
    d.box(0.05, -1.05, 1.3, 0.25, "A-EQPM-HIDN", "RA")
    d.box(0.05, 0.4, 1.3, 1.05, "A-EQPM-HIDN", "ER")
    d.text(0.67, -1.45, f"SHAFT RS-{sp}", h=1.2)
    # stair to Deck B: 21 risers x 181, 20 goings x 260, 1100 wide, rising spinward (+x)
    sx0, sw, y0 = 3.0, 1.1, 1.6
    for i in range(18):
        x = sx0 + i * 0.26
        d.line(x, y0, x, y0 + sw, "A-FLOR-STRS")
    d.box(sx0 - 1.2, y0, sx0, y0 + sw, "A-FLOR-STRS")                       # bottom landing
    d.line(sx0 + 2.6, y0, sx0 + 3.0, y0 + sw, "A-FLOR-STRS")                 # cut line at plan height
    d.line(sx0 + 2.7, y0, sx0 + 3.1, y0 + sw, "A-FLOR-STRS")
    d.line(sx0 - 0.6, y0 + sw / 2, sx0 + 2.5, y0 + sw / 2, "A-FLOR-STRS")
    d.text(sx0 + 1.2, y0 + sw / 2 + 0.25, "DN 18R", h=1.3)
    for yy in (y0 - 0.05, y0 + sw + 0.05): d.line(sx0, yy, sx0 + 5.2, yy, "A-FURN")   # guards / handrails
    # rooms: -y band
    # -y band (hub wall): safe haven, EVA store, the SPOKE PORTAL (open to the concourse), WCs, janitor
    sp_hw = (SPOKE_W / 2) * R_MID / RI / d.sx          # spoke half-width at the hub wall, in pen units
    d.hwall(-xs, xs, -1.9, gaps=[(-8.0, -7.0), (-5.6, -4.8), (-sp_hw, sp_hw), (3.9, 4.7), (5.6, 6.4), (8.3, 9.1)])
    d.vwall(-4.6, -HW, -1.9); d.vwall(-sp_hw, -HW, -1.9); d.vwall(sp_hw, -HW, -1.9)
    d.vwall(4.4, -HW, -1.9); d.vwall(6.6, -HW, -1.9)
    d.room("SAFE HAVEN · 30 P", f"{n}-01", -xs, -HW, -4.6, -1.9)
    d.box(-xs + 0.2, -3.9, -xs + 3.0, -3.3, "A-EQPM", "O₂ / LiOH SCRUBBER")
    d.box(-7.0, -3.9, -4.8, -3.3, "A-EQPM", "PBA LOCKER")
    d.slide_h(-8.0, -7.0, -1.9, side=1, pocket=1)
    d.room("EVA STORE", f"{n}-02", -4.6, -HW, -sp_hw, -1.9, show_area=False)
    d.box(-4.4, -3.95, -sp_hw - 0.2, -3.3, "A-EQPM", "SUITS")
    d.slide_h(-5.6, -4.8, -1.9, side=1, pocket=-1)
    d.room(f"SPOKE {sp} PORTAL", f"{n}-08", -sp_hw, -HW, sp_hw, -1.9, tag_at=(0.0, -2.9), show_area=False)
    d.text(0.0, -HW - T - 0.9, f"TO SPOKE {sp} · SPINE (FLOOR LEVEL) · ISOLATION DOOR SH-{sp} IN THE HUB WALL", h=1.2)
    for s_ in (-1, 1):       # the spoke tunnel's walls leaving the hub wall
        d.wall_rect(s_ * sp_hw - (SPOKE_T / d.sx if s_ > 0 else 0), -HW - T - 2.0, s_ * sp_hw + (0 if s_ > 0 else SPOKE_T / d.sx), -HW - T, "A-WALL-PRHU", FILL_HULL)
    d.pressure_door_wall(-1.2 / d.sx, 1.2 / d.sx, -HW - T, -HW)
    for i, (a, b) in enumerate(((sp_hw, 4.4), (4.4, 6.6))):
        d.room("WC" if i == 0 else "WC · ACC", f"{n}-0{3 + i}", a, -HW, b, -1.9, show_area=False)
        d.wc(a + 0.6, -HW + 0.2, (0, 1))
        d.basin(b - 0.06, -2.4, (-1, 0))
    d.swing((3.9, -1.9), 0.8, 0, -90); d.swing((5.6, -1.9), 0.8, 0, -90)
    d.room("JANITOR", f"{n}-05", 6.6, -HW, xs, -1.9, show_area=False)
    d.box(xs - 1.2, -3.9, xs - 0.2, -3.2, "A-EQPM", "SINK")
    d.swing((8.3, -1.9), 0.8, 0, -90)
    # +y band: the named room, the panel room
    d.hwall(-xs, -3.2, 1.9, gaps=[(-5.0, -4.0)]); d.vwall(-3.2, 1.9, HW)
    d.room(J_ROOM[k], f"{n}-06", -xs, 1.9, -3.2, HW)
    d.slide_h(-5.0, -4.0, 1.9, side=-1, pocket=1)
    d.hwall(8.7, xs, 1.9, gaps=[(9.2, 10.0)]); d.vwall(8.7, 1.9, HW)
    d.room("ELEC PANEL EP-A", f"{n}-07", 8.7, 1.9, xs, HW, show_area=False)
    d.box(8.85, 3.4, xs - 0.1, 3.95, "A-EQPM", "DB-A")
    d.swing((9.2, 1.9), 0.8, 0, 90)
    d.room("CONCOURSE", f"{n}-00", -xs, -1.9, xs, 1.9, tag_at=(-6.5, 0.0))
    d.text(-0.35, -1.55, f"LIFT L{sp}\nDECK A ↔ B", h=1.2)
    # benches facing the windows, fire point
    d.box(-1.6, 3.1, 1.6, 3.6, "A-FURN", "BENCH")
    d.box(xs - 0.35, -1.85, xs - 0.05, -1.25, "A-EQPM", "FP")


def hydroponics(d, k, xs, n):
    hx = None                      # REV B: the hangar airlocks are on Deck B
    segs = [(-10.6, -5.8), (-4.8, 0.0), (1.0, 5.8), (6.8, 10.6)]
    for sgn in (-1, 1):
        for a, b in segs:
            for (y0, y1, lab) in ((0.85, 1.75, "RACK · 2-SIDED · 4 TIER"), (2.95, 3.85, "RACK · 4 TIER")):
                if hx is not None and sgn > 0 and b > hx - 1.6 and a < hx + 1.6: continue
                ya, yb = sorted((sgn * y0, sgn * y1))
                d.box(a, ya, b, yb, "A-EQPM")
                for f in (0.25, 0.5, 0.75): d.line(a, ya + (yb - ya) * f, b, ya + (yb - ya) * f, "A-FURN")
                d.text((a + b) / 2, (ya + yb) / 2, lab, h=1.15)
    d.room("HYDROPONICS BAY " + ("1" if k == 2 else "2"), f"{n}-01", -xs, -HW, xs, HW, tag_at=(-8.2, 0.0))
    d.box(-xs + 0.1, 2.1, -xs + 0.6, 2.75, "A-EQPM", "NCP")      # nutrient control panel
    d.box(xs - 0.6, -2.75, xs - 0.1, -2.1, "A-EQPM", "HB")       # harvest bench
    if hx is not None:
        a, b = hx - 1.5, hx + 1.5
        d.hwall(a, b, 0.85, gaps=[(hx - 0.5, hx + 0.5)]); d.vwall(a, 0.85, HW); d.vwall(b, 0.85, HW)
        d.room("AIRLOCK AL-H" + ("1" if k == 3 else "2"), f"{n}-02", a, 0.85, b, HW, show_area=False)
        d.circ(hx, 2.75, 0.55, "A-EQPM"); d.circ(hx, 2.75, 0.45, "A-HIDN")
        d.text(hx, 2.0, "FLOOR HATCH Ø900\nTO RIM HANGAR", h=1.15)
        d.box(a + 0.1, 3.4, a + 1.0, 3.95, "A-EQPM", "SUITS")
        d.slide_h(hx - 0.5, hx + 0.5, 0.85, side=-1, pocket=-1)


def quarters(d, k, xs, n, deck):
    """Cabins one per frame bay (2.618 at R_MID; the end bays lose 300 to the bulkhead): every cabin gets one window
    (outer wall both decks, hub wall on Deck A). Hygiene modules mid-run, one each side."""
    L = 2 * (xs + BH_T / 2)
    xb_ = [-L / 2 + i * STEP for i in range(9)]
    xb_[0], xb_[-1] = -xs, xs
    hyg = {1: 3, -1: 4}
    ncell = 8
    for sgn in (1, -1):
        d.hwall(-xs, xs, sgn * 0.96, gaps=[(xb_[i] + (0.85 if i != hyg[sgn] else 0.3), xb_[i] + (1.65 if i != hyg[sgn] else 1.1)) for i in range(ncell)])
    rn = 1
    for sgn in (1, -1):
        for i in range(1, ncell):
            d.vwall(xb_[i], *sorted((sgn * 1.02, sgn * HW)))
        for i in range(ncell):
            cx0, cx1 = xb_[i], xb_[i + 1]
            ya, yb = sorted((sgn * 1.02, sgn * HW))
            if i == hyg[sgn]:
                ym = sgn * 2.3
                d.hwall(cx0, cx1, ym, gaps=[(cx0 + 0.25, cx0 + 0.95), (cx0 + 1.45, cx1 - 0.3)])
                d.vwall(cx0 + 1.25, *sorted((ym, sgn * HW)))
                d.wc(cx0 + 0.6, sgn * (HW - 0.2), (0, -sgn))
                d.shower(cx0 + 1.4, min(sgn * 2.45, sgn * 3.9), cx1 - 0.1, max(sgn * 2.45, sgn * 3.9))
                d.box(cx1 - 0.55, min(sgn * 1.1, sgn * 2.15), cx1 - 0.08, max(sgn * 1.1, sgn * 2.15), "A-FURN")
                d.basin(cx1 - 0.08, sgn * 1.4, (-1, 0), 0.45); d.basin(cx1 - 0.08, sgn * 1.88, (-1, 0), 0.45)
                d.box(cx0 + 0.08, min(sgn * 1.1, sgn * 1.5), cx0 + 0.5, max(sgn * 1.1, sgn * 1.5), "A-EQPM-HIDN", "R", h=0.9)   # riser
                d.room("HYGIENE", f"{n}-{rn:02d}", cx0, ya, cx1, yb, tag_at=((cx0 + cx1) / 2, sgn * 1.6), show_area=False)
                d.slide_h(cx0 + 0.3, cx0 + 1.1, sgn * 0.96, side=-sgn, pocket=1)
            else:
                # bed along the hull wall under the window, wardrobe by the door, fold-down desk
                d.bed(cx0 + 0.1, min(sgn * (HW - 0.95), sgn * (HW - 0.05)), cx0 + 2.1, max(sgn * (HW - 0.95), sgn * (HW - 0.05)), "x0",
                      None if deck == "A" or TS[0] > 1 else "BUNK ×2")
                d.wardrobe(cx0 + 0.08, min(sgn * 1.08, sgn * 1.68), cx0 + 0.75, max(sgn * 1.08, sgn * 1.68))
                d.desk(cx1 - 0.6, min(sgn * 1.5, sgn * 2.7), cx1 - 0.08, max(sgn * 1.5, sgn * 2.7), chair=(cx1 - 0.92, sgn * 2.1))
                if TS[0] > 1:     # enlarged views: furniture labels and the cabin's internal dimensions
                    d.text(cx0 + 1.5, sgn * (HW - 0.25), "BED 900×2000" if deck == "A" else "BUNKS 900×2000 ×2", h=0.95)
                    d.text(cx0 + 0.41, sgn * 1.38, "WR", h=0.95)
                    d.text(cx1 - 0.34, sgn * 2.1, "DESK", h=0.95, rot=90)
                    d.v.dim(d.P(cx0 + 0.06, sgn * 1.72), d.P(cx1 - 0.06, sgn * 1.72), 0.3 if sgn > 0 else -0.3)
                    d.v.dim(d.P(cx0 + 0.1, sgn * (HW - 0.95)), d.P(cx0 + 2.1, sgn * (HW - 0.95)), -4 if sgn > 0 else 4)
                cap = "CABIN · 1" if deck == "A" else "CABIN · 2"
                d.room(cap, f"{n}-{rn:02d}", cx0, ya, cx1, yb, tag_at=((cx0 + cx1) / 2 - 0.15, sgn * 2.45), show_area=False)
                d.slide_h(cx0 + 0.85, cx0 + 1.65, sgn * 0.96, side=-sgn, pocket=1)
            rn += 1
    d.room("CORRIDOR", f"{n}-00", -xs, -0.9, xs, 0.9, tag_at=(-xs + 1.6, 0.0), show_area=False)
    for xe in (-xs + 0.5, xs - 0.5):
        d.box(xe - 0.3, -0.85, xe + 0.3, -0.45, "A-EQPM", "EL", h=1.0)


def galley(d, k, xs, n):
    # galley on -y behind a servery counter; scullery on +y; wardroom the rest
    d.hwall(-xs, -2.0, -1.2, gaps=[(-9.0, -3.6)], t=0.12)
    d.vwall(-2.0, -HW, -1.2)
    d.box(-9.0, -1.45, -3.6, -1.15, "A-EQPM", "SERVERY COUNTER", h=1.1)
    d.box(-xs + 0.1, -HW + 0.05, -2.2, -HW + 0.75, "A-EQPM", "COOKING LINE · INDUCTION · COMBI OVENS · HOOD ABOVE", h=1.05)
    d.box(-8.0, -2.9, -4.5, -2.1, "A-EQPM", "PREP ISLAND", h=1.05)
    d.box(-3.6, -2.9, -2.2, -2.1, "A-EQPM", "CS", h=1.0)
    d.room("GALLEY", f"{n}-01", -xs, -HW, -2.0, -1.2, tag_at=(-6.2, -1.75))
    d.hwall(-xs, -7.5, 1.2, gaps=[(-9.8, -8.8)]); d.vwall(-7.5, 1.2, HW)
    d.room("SCULLERY", f"{n}-02", -xs, 1.2, -7.5, HW, show_area=False)
    d.box(-xs + 0.1, HW - 0.7, -7.7, HW - 0.05, "A-EQPM", "DISHWASH · SINKS", h=1.05)
    d.slide_h(-9.8, -8.8, 1.2, side=-1, pocket=1)
    tables = [(x, y) for x in (-6.0, -3.4, -0.8, 1.8, 4.4, 7.0, 9.6) for y in (2.6,)] + [(x, -2.6) for x in (-0.6, 2.0, 4.6, 7.2, 9.6)]
    for (x, y) in tables:
        if x + 0.6 > xs - 0.1: x = xs - 0.75
        d.box(x - 0.6, y - 0.4, x + 0.6, y + 0.4, "A-FURN")
        for cx in (x - 0.3, x + 0.3):
            for cy in (y - 0.65, y + 0.65): d.box(cx - 0.2, cy - 0.2, cx + 0.2, cy + 0.2, "A-FURN")
    d.room("WARDROOM · 48 SEATS", f"{n}-00", -7.5, -1.2, xs, HW, tag_at=(-4.0, 0.0))


def recreation(d, k, xs, n):
    d.hwall(-xs, 0.5, -0.9, gaps=[(-1.6, -0.6)]); d.vwall(0.5, -HW, -0.9)
    d.room("GYMNASIUM", f"{n}-01", -xs, -HW, 0.5, -0.9, tag_at=(-5.0, -1.4))
    for i in range(3):
        d.box(-10.6 + i * 1.3, -3.9, -9.7 + i * 1.3, -1.9, "A-EQPM", "TREADMILL", h=1.0)
    for i in range(3):
        d.box(-6.4 + i * 1.0, -3.9, -5.8 + i * 1.0, -2.7, "A-EQPM", "ERG", h=0.95)
    d.box(-3.2, -3.9, -0.4, -2.4, "A-EQPM", "RESISTANCE RIG", h=1.05)
    d.slide_h(-1.6, -0.6, -0.9, side=1, pocket=-1)
    for x in (-9.5, -5.5, 2.5, 7.5):
        d.box(x - 1.0, 3.0, x + 1.0, 3.85, "A-FURN", "SOFA", h=1.0)
        d.box(x - 0.5, 2.0, x + 0.5, 2.6, "A-FURN")
    d.room("LOUNGE", f"{n}-03", -xs, -0.9, xs, HW, tag_at=(-1.0, 1.5))
    d.box(9.0, -3.9, xs - 0.1, -1.0, "A-EQPM", "GAMES / VR", h=1.05)


def workshop(d, k, xs, n):
    d.hwall(-xs, -7.5, 1.5, gaps=[(-9.0, -8.0)]); d.vwall(-7.5, 1.5, HW)
    d.room("SUIT MAINT.", f"{n}-01", -xs, 1.5, -7.5, HW, show_area=False)
    d.box(-xs + 0.1, 3.3, -7.7, 3.95, "A-EQPM", "SUIT BENCH", h=1.0)
    d.slide_h(-9.0, -8.0, 1.5, side=-1, pocket=1)
    d.hwall(7.5, xs, 1.5, gaps=[(8.5, 9.5)]); d.vwall(7.5, 1.5, HW)
    d.room("TOOL CRIB", f"{n}-02", 7.5, 1.5, xs, HW, show_area=False)
    d.box(7.7, 3.3, xs - 0.1, 3.95, "A-EQPM", "RACKS", h=1.0)
    d.slide_h(8.5, 9.5, 1.5, side=-1, pocket=-1)
    d.hwall(7.5, xs, -1.5, gaps=[(8.5, 9.5)]); d.vwall(7.5, -HW, -1.5)
    d.room("WELD BOOTH", f"{n}-03", 7.5, -HW, xs, -1.5, show_area=False)
    d.box(8.0, -3.9, 10.4, -3.1, "A-EQPM", "HOOD · EXH", h=1.0)
    d.slide_h(8.5, 9.5, -1.5, side=1, pocket=-1)
    for x0, x1, lab in ((-10.5, -7.9, "LATHE"), (-6.8, -4.6, "MILL"), (-3.4, -0.4, "CNC ROUTER"), (0.8, 3.0, "PRESS BRAKE")):
        d.box(x0, -3.9, x1, -2.6, "A-EQPM", lab, h=1.05)
    for i in range(4):
        x = -6.5 + i * 2.6
        d.box(x, 2.9, x + 1.2, 3.9, "A-EQPM", "3D PRT", h=0.95)
    d.box(-2.5, -0.6, 4.5, 0.6, "A-EQPM", "LAYOUT BENCH", h=1.05)
    d.room("FABRICATION WORKSHOP", f"{n}-00", -7.5, -1.5, 7.5, 1.5, tag_at=(-4.8, 1.0))


def medical(d, k, xs, n):
    d.hwall(-xs, xs, 0.96, gaps=[(-9.2, -8.4), (-4.6, -3.8), (-1.1, -0.3), (2.6, 3.6), (7.0, 7.8)])
    d.hwall(-xs, xs, -0.96, gaps=[(-8.6, -7.8), (-3.4, -2.6), (2.4, 3.2), (5.6, 6.4), (8.4, 9.2)])
    plus = [(-xs, -6.0, "RECEPTION / WAIT"), (-6.0, -2.5, "EXAM 1"), (-2.5, 1.0, "EXAM 2"), (1.0, 6.0, "TREATMENT / TRAUMA"), (6.0, xs, "PHARMACY")]
    minus = [(-xs, -5.0, "ISOLATION (-ve P)"), (-5.0, 2.0, "SICKBAY · 4 BEDS"), (2.0, 5.0, "MO OFFICE"), (5.0, 8.0, "STERILE / UTILITY"), (8.0, xs, "STAFF WC")]
    for i, (a, b, nm) in enumerate(plus):
        if i: d.vwall(a, 1.02, HW)
        d.room(nm, f"{n}-{i + 1:02d}", a, 1.02, b, HW, show_area=nm not in ("PHARMACY",))
    for i, (a, b, nm) in enumerate(minus):
        if i: d.vwall(a, -HW, -1.02)
        d.room(nm, f"{n}-{i + 6:02d}", a, -HW, b, -1.02, show_area=nm not in ("STAFF WC",))
    d.vwall(-8.0, -HW, -1.02, gaps=[(-3.0, -2.2)])
    d.text(-9.4, -1.6, "ANTE", h=1.1)
    for x in (-4.4, -2.6, -0.8, 1.0):
        d.bed(x - 0.45, -3.9, x + 0.45, -1.9, "y0", "BED")
    d.box(-4.6, 2.6, -2.9, 3.3, "A-FURN", "EXAM", h=1.0); d.box(-1.1, 2.6, 0.6, 3.3, "A-FURN", "EXAM", h=1.0)
    d.box(2.5, 2.4, 4.5, 3.2, "A-EQPM", "TRAUMA BED", h=1.0)
    d.bed(-7.0, -3.4, -5.4, -2.6, "x0")
    d.room("MEDICAL CORRIDOR", f"{n}-00", -xs, -0.9, xs, 0.9, tag_at=(0.0, 0.0), show_area=False)


# ------------------------------------------------------------------ Deck B programmes
def junctionB(d, k, xs, n):
    sp = JUNCTION[k]
    lift_core(d, "B", sp)
    # the RS-n riser shaft continues down from Deck A (return air, OA / RH from the spoke, electrical)
    R = RSHAFT
    d.wall_rect(R["x0"], R["y0"] - 0.15, R["x1"] + 0.15, R["y0"], "A-WALL-SHFT", FILL_SHAFT)
    d.wall_rect(R["x1"], R["y0"], R["x1"] + 0.15, R["y1"] + 0.15, "A-WALL-SHFT", FILL_SHAFT)
    d.wall_rect(R["x0"], R["y1"], R["x1"] + 0.15, R["y1"] + 0.15, "A-WALL-SHFT", FILL_SHAFT)
    d.box(0.05, -1.05, 1.3, 0.25, "A-EQPM-HIDN", "RA")
    d.box(0.05, 0.4, 1.3, 1.05, "A-EQPM-HIDN", "OA/RH")
    d.text(0.67, -1.45, f"SHAFT RS-{sp}", h=1.2)
    d.text(0.0, -HW - T - 0.9, f"SPOKE {sp} IS ON DECK A ABOVE (A-21x)", h=1.1)
    # stair arriving from Deck A (bottom flight, 18 risers)
    sx0b = 3.0 * SX_A
    for i in range(18): d.line(sx0b + i * 0.26, 1.6, sx0b + i * 0.26, 2.7, "A-FLOR-STRS")
    d.box(sx0b - 1.2, 1.6, sx0b, 2.7, "A-FLOR-STRS")
    d.line(sx0b + 2.6, 1.6, sx0b + 3.0, 2.7, "A-FLOR-STRS")
    d.text(sx0b + 1.2, 2.15, "UP 18R TO DECK A", h=1.15)
    # plant room (AHU) on -y, electrical substation on +y
    d.hwall(-xs, -3.0, -1.6, gaps=[(-4.8, -3.6)]); d.vwall(-3.0, -HW, -1.6)
    d.room(f"PLANT · AHU-R{AHU_OF[k]}", f"{n}-01", -xs, -HW, -3.0, -1.6, tag_at=(-5.4, -2.0))
    d.box(-9.6, -3.85, -6.2, -2.45, "M-HVAC-EQPM", f"AHU-R{AHU_OF[k]}", h=1.3)
    d.box(-6.0, -3.85, -4.6, -2.45, "A-EQPM", "HEPA", h=1.0)
    d.slide_h(-4.8, -3.6, -1.6, side=1, pocket=1)
    d.hwall(-xs, -4.0, 1.6, gaps=[(-6.0, -5.0)]); d.vwall(-4.0, 1.6, HW)
    d.room(f"SUBSTATION ES-R{sp}", f"{n}-02", -xs, 1.6, -4.0, HW, tag_at=(-7.0, 2.5))
    d.box(-9.8, 3.4, -4.3, 3.95, "A-EQPM", "LV SWITCHBOARD · UPS", h=1.0)
    d.slide_h(-6.0, -5.0, 1.6, side=-1, pocket=1)
    d.hwall(7.6, xs, -1.6, gaps=[(8.2, 9.2)]); d.vwall(7.6, -HW, -1.6)
    d.room("WATER BOOSTER", f"{n}-03", 7.6, -HW, xs, -1.6, show_area=False)
    d.circ(8.6, -3.0, 0.5, "A-EQPM", label="PW"); d.box(9.3, -3.6, 9.9, -2.4, "A-EQPM", "P", h=1.0)
    d.slide_h(8.2, 9.2, -1.6, side=1, pocket=-1)
    d.room(f"SPOKE {sp} LOBBY", f"{n}-00", -3.0, -1.6, 7.6, 1.6, tag_at=(4.6, -0.6))


def hydro_support(d, k, xs, n):
    d.hwall(-xs, xs, -0.6, gaps=[(-6.0, -5.0), (3.0, 4.0)]); d.hwall(-xs, xs, 0.6, gaps=[(-3.0, -2.0), (5.0, 6.0)])
    d.vwall(0.0, -HW, -0.6); d.vwall(1.0, 0.6, HW)
    for i, x in enumerate((-8.4, -6.6, -4.8)):
        d.circ(x, -2.5, 0.6, "A-EQPM", label=f"T{i + 1}")
    d.text(-6.6, -3.6, "NUTRIENT TANKS 3 × 1.0 m³", h=1.15)
    d.box(-2.6, -3.9, -0.3, -3.0, "A-EQPM", "DOSING SKID", h=1.0)
    d.room("NUTRIENT MIXING", f"{n}-01", -xs, -HW, 0.0, -0.6, tag_at=(-4.5, -1.3))
    d.box(0.5, -3.9, 5.0, -3.1, "A-EQPM", "PROCESSING BENCH", h=1.0)
    d.box(6.0, -3.9, 8.2, -3.1, "A-EQPM", "WASH", h=1.0)
    d.room("HARVEST PROCESSING", f"{n}-02", 0.0, -HW, xs, -0.6, tag_at=(4.5, -1.6))
    d.box(-xs + 0.2, 3.2, -6.0, 3.9, "A-EQPM", "SEED BANK FREEZERS", h=1.0)
    d.room("SEED BANK", f"{n}-03", -xs, 0.6, 1.0, HW, tag_at=(-4.5, 2.0))
    d.box(2.0, 3.0, 9.6, 3.9, "A-EQPM", "COMPOST BIOREACTOR", h=1.0)
    d.room("BIOWASTE", f"{n}-04", 1.0, 0.6, xs, HW, tag_at=(5.5, 1.6))
    d.room("PASSAGE", f"{n}-00", -xs, -0.6, xs, 0.6, tag_at=(-8.5, 0.0), show_area=False)


def water_store(d, k, xs, n):
    d.hwall(-xs, xs, -0.6, gaps=[(-1.5, -0.5)]); d.hwall(-xs, xs, 0.6, gaps=[(0.5, 1.5)])
    d.box(-9.5, -3.8, -2.0, -1.4, "A-EQPM", "POTABLE WATER TANK PWT-1 · 12 m³", h=1.15)
    d.box(1.0, -3.8, 8.5, -1.4, "A-EQPM", "POTABLE WATER TANK PWT-2 · 12 m³", h=1.15)
    d.room("WATER STORE", f"{n}-01", -xs, -HW, xs, -0.6, tag_at=(-0.5, -1.0))
    for i in range(9):
        d.circ(-8.8 + i * 1.1, 2.6, 0.22, "A-EQPM")
    d.text(-4.4, 3.4, "ALGAE PHOTOBIOREACTOR COLUMNS · 9 × Ø400", h=1.15)
    hx = hangar_airlock(d, k, n, xs)
    d.box(hx + 1.8, 3.0, hx + 4.0, 3.9, "A-EQPM", "PUMPS P-1/2", h=1.0)
    d.box(-1.0, 3.0, hx - 1.8, 3.9, "A-EQPM", "UV / FILTERS", h=1.0)
    d.room("ALGAE & PUMP ROOM", f"{n}-02", -xs, 0.6, hx - 1.5, HW, tag_at=(-4.5, 1.4))
    d.room("PASSAGE", f"{n}-00", -xs, -0.6, xs, 0.6, tag_at=(-8.5, 0.0), show_area=False)


def hangar_airlock(d, k, n, xs):
    """Deck B: the airlock between the ring and its rim hangar, on the outer wall at the hangar's angle."""
    hx = ang_diff(HANGAR_DEG[k], COMP_ANGLE(k)) * math.pi / 180 * R_MID
    a, b = hx - 1.5, hx + 1.5
    d.hwall(a, b, 1.2, gaps=[(hx - 0.5, hx + 0.5)]); d.vwall(a, 1.2, HW); d.vwall(b, 1.2, HW)
    d.room("AIRLOCK AL-H" + ("1" if k == 3 else "2"), f"{n}-09", a, 1.2, b, HW, tag_at=(hx, 2.0), show_area=False)
    d.box(a + 0.1, 3.0, a + 0.9, 3.4, "A-EQPM", "SUITS", h=0.9)
    d.text(hx, HW + T + 0.9, "TO RIM HANGAR H" + ("1" if k == 3 else "2") + " · DOOR 2 400 × 2 400 AT DECK B FLOOR", h=1.15)
    d.slide_h(hx - 0.5, hx + 0.5, 1.2, side=-1, pocket=-1)
    return hx


def galley_stores(d, k, xs, n):
    d.hwall(-xs, xs, -0.6, gaps=[(-8.0, -7.2), (-5.0, -4.2), (3.6, 4.4), (8.0, 8.8)])
    d.hwall(-xs, xs, 0.6, gaps=[(-6.0, -5.0), (4.0, 5.0)])
    d.vwall(-6.4, -HW, -0.6); d.vwall(-3.4, -HW, -0.6); d.vwall(6.8, -HW, -0.6); d.vwall(2.0, 0.6, HW)
    d.room("COLD ROOM +3°C", f"{n}-01", -xs, -HW, -6.4, -0.6); d.box(-xs + 0.1, -3.9, -6.6, -3.3, "A-EQPM", "SHELVING", h=1.0)
    d.room("FREEZER −20°C", f"{n}-02", -6.4, -HW, -3.4, -0.6, show_area=False); d.box(-6.2, -3.9, -3.6, -3.3, "A-EQPM", "SHELF", h=1.0)
    d.room("DRY STORES", f"{n}-03", -3.4, -HW, 6.8, -0.6)
    for x in (-3.0, 0.0, 3.0): d.box(x, -3.9, x + 2.6, -3.3, "A-EQPM", "RACK", h=1.0)
    d.room("WASTE / COMPACTOR", f"{n}-04", 6.8, -HW, xs, -0.6, show_area=False); d.box(7.2, -3.8, 9.0, -2.6, "A-EQPM", "TC", h=1.0)
    d.room("DRY STORES 2", f"{n}-05", -xs, 0.6, 2.0, HW)
    for x in (-9.5, -6.5, -3.5, -0.5): d.box(x, 3.3, x + 2.6, 3.9, "A-EQPM", "RACK", h=1.0)
    d.room("LAUNDRY", f"{n}-06", 2.0, 0.6, xs, HW)
    for i in range(4): d.box(2.4 + i * 1.0, 3.2, 3.2 + i * 1.0, 3.9, "A-EQPM", "W" if i < 2 else "D", h=1.0)
    d.box(7.0, 3.2, 9.8, 3.9, "A-EQPM", "FOLD BENCH", h=1.0)
    d.room("PASSAGE", f"{n}-00", -xs, -0.6, xs, 0.6, tag_at=(-8.5, 0.0), show_area=False)


def transient(d, k, xs, n):
    cw = 2.4
    x0 = -xs + 0.1
    d.hwall(-xs, xs, 0.96, gaps=[(x0 + i * cw + 0.8, x0 + i * cw + 1.6) for i in range(4)])
    d.hwall(-xs, xs, -0.96, gaps=[(x0 + i * cw + 0.8, x0 + i * cw + 1.6) for i in range(4)])
    rn = 1
    for sgn in (1, -1):
        for i in range(5): d.vwall(x0 + i * cw, *sorted((sgn * 1.02, sgn * HW)))
        for i in range(4):
            cx0, cx1 = x0 + i * cw, x0 + (i + 1) * cw
            if i == 3:
                d.room("HYGIENE", f"{n}-{rn:02d}", cx0, min(sgn * 1.02, sgn * HW), cx1, max(sgn * 1.02, sgn * HW), show_area=False)
                d.wc(cx0 + 0.55, sgn * (HW - 0.2), (0, -sgn)); d.basin(cx0 + 0.55, sgn * 1.1, (0, sgn))
                d.shower(cx0 + 1.2, min(sgn * 2.4, sgn * 3.9), cx1 - 0.1, max(sgn * 2.4, sgn * 3.9))
            else:
                for bx in (cx0 + 0.1, cx1 - 1.0):
                    d.bed(bx, min(sgn * 1.9, sgn * 3.9), bx + 0.9, max(sgn * 1.9, sgn * 3.9), "y1" if sgn > 0 else "y0", "BUNK ×2")
                d.room("BUNK ROOM · 4", f"{n}-{rn:02d}", cx0, min(sgn * 1.02, sgn * HW), cx1, max(sgn * 1.02, sgn * HW),
                       tag_at=((cx0 + cx1) / 2, sgn * 1.45), show_area=False)
            d.slide_h(cx0 + 0.8, cx0 + 1.6, sgn * 0.96, side=-sgn, pocket=1)
            rn += 1
    lx = x0 + 4 * cw
    d.vwall(lx, -HW, -1.02); d.vwall(lx, 1.02, HW)
    d.room("TRANSIENT LOUNGE", f"{n}-{rn:02d}", lx, -HW, xs, HW, tag_at=((lx + xs) / 2 - 1.0, -1.6))
    d.box(lx + 1.0, -3.6, xs - 1.0, -2.8, "A-FURN", "SEATING", h=1.0)
    hangar_airlock(d, k, n, xs)
    d.room("CORRIDOR", f"{n}-00", -xs, -0.9, lx, 0.9, tag_at=(-xs + 1.6, 0.0), show_area=False)


def stores(d, k, xs, n):
    d.hwall(-xs, xs, -0.8, gaps=[(-4.0, -3.0)]); d.hwall(-xs, xs, 0.8, gaps=[(3.0, 4.0)])
    d.vwall(-5.0, -HW, -0.8)
    d.room("DATA / COMMS NODE", f"{n}-01", -xs, -HW, -5.0, -0.8, tag_at=(-7.5, -1.3))
    for i in range(6): d.box(-9.8 + i * 0.75, -3.9, -9.2 + i * 0.75, -2.7, "A-EQPM", "", h=1.0)
    d.text(-7.5, -2.3, "SERVER RACKS 6 × 600×1200 · UPS", h=1.1)
    d.room("ENGINEERING SPARES", f"{n}-02", -5.0, -HW, xs, -0.8, tag_at=(2.5, -1.3))
    for x in (-4.6, -1.6, 1.4, 4.4, 7.4): d.box(x, -3.9, x + 2.6, -2.9, "A-EQPM", "PALLET RACK", h=1.0)
    d.vwall(6.5, 0.8, HW)
    d.room("GENERAL STORES", f"{n}-03", -xs, 0.8, 6.5, HW, tag_at=(-2.0, 1.3))
    for x in (-9.8, -6.8, -3.8, -0.8, 2.2): d.box(x, 2.9, x + 2.6, 3.9, "A-EQPM", "PALLET RACK", h=1.0)
    d.room("HAZMAT LOCKER", f"{n}-04", 6.5, 0.8, xs, HW, show_area=False)
    d.box(7.0, 3.1, 9.6, 3.9, "A-EQPM", "VENTED CABINETS", h=1.0)
    d.room("PASSAGE", f"{n}-00", -xs, -0.8, xs, 0.8, tag_at=(-8.5, 0.0), show_area=False)


def laboratory(d, k, xs, n):
    d.hwall(-xs, xs, -0.8, gaps=[(-1.0, 0.0), (6.0, 7.0)]); d.hwall(-xs, -4.0, 0.8, gaps=[(-6.5, -5.5)])
    d.vwall(-4.0, 0.8, HW); d.vwall(5.0, -HW, -0.8)
    d.room("LAB OFFICE", f"{n}-01", -xs, 0.8, -4.0, HW)
    d.box(-9.6, 3.2, -5.0, 3.9, "A-FURN", "DESKS ×3", h=1.0)
    d.room("GENERAL LABORATORY", f"{n}-02", -4.0, -0.8, xs, HW, tag_at=(2.5, 0.0))
    d.box(-3.6, 3.2, 9.6, 3.9, "A-EQPM", "WALL BENCH · SERVICES SPINE", h=1.0)
    d.box(-1.0, 1.5, 6.0, 2.4, "A-EQPM", "ISLAND BENCH", h=1.0)
    d.box(7.0, 1.3, 8.6, 2.4, "A-EQPM", "FH-1", h=1.0); d.box(8.8, 1.3, xs - 0.1, 2.4, "A-EQPM", "FH-2", h=1.0)
    d.room("SAMPLE PREP / GLOVEBOX", f"{n}-03", -xs, -HW, 5.0, -0.8, tag_at=(-3.0, -1.3))
    d.box(-9.6, -3.9, -6.0, -3.0, "A-EQPM", "GLOVEBOX", h=1.0)
    d.box(-5.6, -3.9, -1.0, -3.2, "A-EQPM", "−80°C FREEZERS", h=1.0)
    d.box(0.0, -3.9, 4.6, -3.2, "A-EQPM", "MICROSCOPY", h=1.0)
    d.room("CHEMICAL STORE", f"{n}-04", 5.0, -HW, xs, -0.8, show_area=False)
    d.box(5.4, -3.9, 9.8, -3.2, "A-EQPM", "VENTED CABINETS", h=1.0)
    d.room("PASSAGE", f"{n}-00", -xs, -0.8, -4.0, 0.8, tag_at=(-8.5, 0.0), show_area=False)


# ------------------------------------------------------------------ assembling ring deck plans
def ring_plan(v, deck, comps, theta_start, grid=True, contents=True, rooms_out=None, scale=100, doors_out=None):
    """Developed plan of `comps` (consecutive compartment numbers) for a deck, x from 0 at theta_start (deg).
    Annotation offsets outside the hull are set for 1:`scale` so they sit the same paper distance at any scale."""
    kk = scale / 100.0
    bub = 3.0 if scale >= 100 else 3.6       # grid bubbles clear of the dimension strings
    r = R_A if deck == "A" else R_B
    L = L_of(r)
    for i, k in enumerate(comps):
        xc = r * math.radians(COMP_ANGLE(k) - theta_start) if COMP_ANGLE(k) >= theta_start - 1e-6 else r * math.radians(COMP_ANGLE(k) + 360 - theta_start)
        d = D(v, ox=xc)
        ring_shell(d, -L / 2, L / 2, r, deck, COMP_ANGLE(k))
        if contents:
            (deckA if deck == "A" else deckB)(d, k, L)
        if rooms_out is not None: rooms_out.extend((deck, k, *rm) for rm in d.rooms)
        if doors_out is not None: doors_out.extend((xc + a_, xc + b_, y_) for a_, b_, y_ in d.dj)
        if grid and contents:
            for sgn in (1, -1):
                xs_ = sorted({round(xc + x, 3) for x, y0_, y1_ in d.vx if (y0_ + y1_) / 2 * sgn > 1.0 and abs(x) < L / 2 - 0.4})
                pts = [xc - L / 2 + BH_T / 2] + xs_ + [xc + L / 2 - BH_T / 2]
                v.chain([(x, sgn * (HW + T)) for x in pts], 6 if sgn > 0 else -16, h=1.3)
            if kk < 1:    # 1:50 sheets: door jambs and partitions along each wall that has doors
                walls = {}
                for a_, b_, y_ in d.dj: walls.setdefault(round(y_, 2), []).extend((a_, b_))
                for y_, xs2 in walls.items():
                    if abs(y_) >= HW: continue
                    xs2 += [x for x, y0_, y1_ in d.vx if y0_ - 0.1 <= y_ <= y1_ + 0.1]
                    pts = sorted({round(xc + x, 3) for x in xs2 if abs(x) < L / 2 - 0.3})
                    pts = [p for i, p in enumerate(pts) if i == 0 or p - pts[i - 1] > 0.05]
                    if len(pts) > 1:
                        into = -1 if y_ > 0 else 1        # dimension line sits on the open (corridor) side
                        v.chain([(x, y_) for x in pts], 4.5 * into, h=1.05)
        # bulkheads at both ends (the next compartment draws its own; the last end is drawn here)
        bnum_lo = (k - 2) % 12 + 1
        bulkhead(D(v, ox=xc), -L / 2, deck, bnum_lo)
        if i == len(comps) - 1: bulkhead(D(v, ox=xc), L / 2, deck, k)
        if grid:
            for xb, bn in ((-L / 2, bnum_lo), (L / 2, k)):
                v.line((xc + xb, -HW - T - (bub - 0.6) * kk), (xc + xb, HW + T + (bub - 0.6) * kk), "A-GRID")
                v.bubble((xc + xb, HW + T + bub * kk), f"B{bn}", 6.5)
                v.bubble((xc + xb, -HW - T - bub * kk), f"B{bn}", 6.5)
            v.text((xc, HW + T + 1.2 * kk), f"COMPARTMENT C{k:02d}  ·  θ {COMP_ANGLE(k) - 15:.0f}°–{COMP_ANGLE(k) + 15:.0f}°  ·  DECK {deck} FFL z {FFL[deck] * 1000:+.0f}",
                   "A-ANNO-TEXT", h=1.8, bold=True)
            # structural frames every 3.75 deg (build.py STEPS = 96): ticks and numbers on the -y side
            for s in range(-4, 5):
                ang = COMP_ANGLE(k) + s * 3.75
                x = xc + r * math.radians(s * 3.75)
                v.line((x, -HW - T - 0.1 * kk), (x, -HW - T - 0.6 * kk), "A-ANNO-DIMS")
                fr = round(ang / 3.75) % 96
                if s % 2 == 0 or kk < 1: v.text((x, -HW - T - 1.1 * kk), f"FR{fr:02d}", "A-ANNO-NOTE", h=1.1)
    if grid:
        x_end = L * len(comps)
        for y, lab in ((-HW, "A"), (-2.0, "B"), (0.0, "C"), (2.0, "D"), (HW, "E")):
            if y == 0: v.line((-1.8 * kk, y), (x_end + 1.8 * kk, y), "A-CNTR")
            v.bubble((-3.0 * kk, y), lab, 5.5)


def compartment_x(k, deck, theta_start):
    r = R_A if deck == "A" else R_B
    a = COMP_ANGLE(k) - theta_start
    if a < -1e-6: a += 360
    return r * math.radians(a)


# ------------------------------------------------------------------ ventilation (M sheets)
def zone_flows():
    """Design airflow per compartment and deck (m3/s), and per AHU."""
    out = {}
    for k in range(1, 13):
        va = (LA - BH_T) * (2 * HW) * (Z_A_CLG - Z_A)
        vb = (LB - BH_T) * (2 * HW) * (Z_B_CLG - Z_B)
        out[k] = (va * ACH_A / 3600, vb * ACH_B / 3600, va, vb)
    return out


BOD = {("A", "M-DUCT-SUPP"): "BOD +3 250 AFF", ("A", "M-DUCT-EXHS"): "BOD +3 300 AFF", ("A", "M-DUCT-HIDN"): "TOD −0 350 BFF",
       ("B", "M-DUCT-SUPP"): "BOD +2 650 AFF", ("B", "M-DUCT-RETN"): "BOD +2 650 AFF", ("B", "M-DUCT-OAIR"): "BOD +2 625 AFF",
       ("B", "M-DUCT-EXHS"): "BOD +2 650 AFF"}
TAG_AT = {"M-DUCT-SUPP": 0.22, "M-DUCT-RETN": 0.55, "M-DUCT-HIDN": 0.62, "M-DUCT-EXHS": 0.8, "M-DUCT-OAIR": 0.4}


def duct(d, x0, y0, x1, y1, w, layer, label=None, h=1.2, lab_side=1, at=None):
    """A rectangular duct of width w (m, plan) along a straight run: double line to scale, joints every 1.5 m, and a
    leader tag (size, flow, bottom-of-duct height) pointing at the duct."""
    hid = layer == "M-DUCT-HIDN"
    fill = None if hid else "#ffffff"
    horiz = abs(y1 - y0) < 1e-9
    if horiz:
        a, b = sorted((x0, x1))
        d.v.rect(*d.P(a, y0 - w / 2), *d.P(b, y0 + w / 2), layer, fill=fill)
        if not hid:
            n = int((b - a) / 1.5)
            for i in range(1, n + 1): d.line(a + i * 1.5, y0 - w / 2, a + i * 1.5, y0 + w / 2, "M-DUCT-JNT")
    else:
        a, b = sorted((y0, y1))
        d.v.rect(*d.P(x0 - w / 2, a), *d.P(x0 + w / 2, b), layer, fill=fill)
        if not hid:
            n = int((b - a) / 1.5)
            for i in range(1, n + 1): d.line(x0 - w / 2, a + i * 1.5, x0 + w / 2, a + i * 1.5, "M-DUCT-JNT")
    if label:
        f = at if at is not None else TAG_AT.get(layer, 0.3)
        bod = BOD.get((getattr(d, "deck", "A"), layer))
        txt = label.replace(" · ", chr(10), 1) + (chr(10) + bod if bod else "")
        if horiz:
            px = a + (b - a) * f
            d.v.leader(d.P(px, y0), d.P(px + 0.9, y0 + lab_side * (w / 2 + 0.75)), txt, "M-ANNO-TEXT", h=h * 0.9)
        else:
            py = a + (b - a) * f
            d.v.leader(d.P(x0, py), d.P(x0 + lab_side * (w / 2 + 0.75), py + 0.6), txt, "M-ANNO-TEXT", h=h * 0.9, ha="left" if lab_side > 0 else "right")


def damper(d, x, y, w, tag):
    """Fire/smoke/pressure isolation damper in a duct at a bulkhead: a blade across the duct, actuator box, tag."""
    d.v.line(d.P(x - w * 0.35, y - w * 0.45), d.P(x + w * 0.35, y + w * 0.45), "M-HVAC-DAMP")
    d.v.rect(*d.P(x - 0.12, y + w / 2), *d.P(x + 0.12, y + w / 2 + 0.22), "M-HVAC-DAMP", fill="#000000")
    d.text(x, y + w / 2 + 0.55, tag, "M-ANNO-TEXT", h=1.05)


def neck(ls):
    return next(n for q, n in ((60, "Ø160"), (100, "Ø200"), (160, "Ø250"), (1e9, "Ø315")) if ls <= q)


def diffuser(d, x, y, s=0.3, ls=None, typ="S1", tag_side=1):
    """Square ceiling diffuser (face s x s) with its round neck; tagged TYPE / flow / neck when ls is given."""
    d.v.rect(*d.P(x - s / 2, y - s / 2), *d.P(x + s / 2, y + s / 2), "M-HVAC-DIFF", fill="#ffffff")
    d.v.line(d.P(x - s / 2, y - s / 2), d.P(x + s / 2, y + s / 2), "M-HVAC-DIFF"); d.v.line(d.P(x - s / 2, y + s / 2), d.P(x + s / 2, y - s / 2), "M-HVAC-DIFF")
    d.circ(x, y, s * 0.28, "M-HVAC-DIFF")
    if ls is not None:
        d.v.symtag(d.P(x + 0.62, y + tag_side * 0.42), f"{typ}|{ls:.0f} L/s|{neck(ls)}", "split", h=0.85, layer="M-ANNO-TEXT")


def grille(d, x, y, w=0.5, hgt=0.15, layer="M-HVAC-GRIL", ls=None, typ="R1", tag_side=-1, tag_dx=-0.6):
    d.v.rect(*d.P(x - w / 2, y - hgt / 2), *d.P(x + w / 2, y + hgt / 2), layer, fill="#ffffff")
    for f in (0.25, 0.5, 0.75): d.v.line(d.P(x - w / 2 + w * f, y - hgt / 2), d.P(x - w / 2 + w * f, y + hgt / 2), layer)
    if ls is not None:
        d.v.symtag(d.P(x + tag_dx, y + tag_side * 0.42), f"{typ}|{ls:.0f} L/s", "split", h=0.85, layer="M-ANNO-TEXT")


def vent_A(d, k, L, fl):
    """Deck A ventilation for one compartment: supply in the ceiling void, return in the subfloor, exhaust where needed."""
    xs = L / 2 - BH_T / 2
    d.deck = "A"
    ahu = AHU_OF.get(k) or AHU_OF.get(k - 1 if (k - 1) in AHU_OF else k + 1) or AHU_OF.get(k % 12 + 1)
    # which way supply flows here: from the junction compartment outward
    owner = k if k in AHU_OF else (k - 1 if (k - 1) in AHU_OF else (k + 1 if (k + 1) in AHU_OF else (1 if k == 12 else None)))
    q = fl[k][0]
    qd = q / 2
    sa_w, ra_w = 0.9, 1.0
    sy, ry = 2.25, 0.0
    if k in AHU_OF:
        # drop from the AHU above into the ceiling void; supply splits both ways, return comes up the RS shaft
        d.v.rect(*d.P(-5.2, sy - 0.45), *d.P(-4.2, sy + 0.45), "M-DUCT-SUPP", fill="#cfe0ff")
        d.text(-4.7, sy + 0.95, f"SA DROP FROM AHU-R{AHU_OF[k]}", "M-ANNO-TEXT", h=1.1)
        q_main = q / 2 + fl[k][0] * 1.0     # each way: half the local zone plus the whole neighbour
        duct(d, -xs - 0.3, sy, -5.2, sy, 0.9, "M-DUCT-SUPP", f"SA 900×250 · {q_main:.2f} m³/s", lab_side=1)
        duct(d, -4.2, sy, xs + 0.3, sy, 0.9, "M-DUCT-SUPP", f"SA 900×250 · {q_main:.2f} m³/s", lab_side=1)
        duct(d, -xs - 0.3, ry, 0.0, ry, 1.0, "M-DUCT-HIDN", "RA 1000×300 IN VOID BELOW", lab_side=-1, at=0.8)
        duct(d, 1.35, ry, xs + 0.3, ry, 1.0, "M-DUCT-HIDN", "RA 1000×300 IN VOID BELOW", lab_side=-1, at=0.35)
        d.text(0.7, -1.6, f"RA UP SHAFT RS-{JUNCTION[k]}", "M-ANNO-TEXT", h=1.1)
        qd8 = q * 1000 / 8
        for x in (-8.0, -3.6, 4.0, 8.4):
            duct(d, x, sy - 0.45, x, 0.9 + 0.15, 0.2, "M-DUCT-SUPP")
            duct(d, x, 0.9 - 0.15, x, -0.9 + 0.15, 0.2, "M-DUCT-SUPP")
            diffuser(d, x, 0.9); diffuser(d, x, -0.9, ls=qd8, tag_side=-1)
        for x in (-9.5, -5.0, 3.5, 7.0): grille(d, x, -1.4); grille(d, x, 1.4, ls=qd8, tag_side=-1)
        # exhaust from the WCs to the spoke chase
        duct(d, 3.6, -3.0, 3.6, -1.4, 0.25, "M-DUCT-EXHS", None)
        duct(d, 1.6, -1.4, 3.6, -1.4, 0.25, "M-DUCT-EXHS", "EA Ø250 TO SPOKE CHASE", lab_side=-1)
    else:
        direction = 1 if owner is not None and ((k - owner) % 12 == 1) else -1    # supply comes from -x (owner before) or +x
        size = "SA 600×250"
        duct(d, -xs - 0.3, sy, xs - 0.6, sy, 0.6, "M-DUCT-SUPP", f"{size} · {q:.2f} m³/s · FROM AHU-R{AHU_OF[owner]}", lab_side=1)
        duct(d, -xs + 0.6 if direction < 0 else -xs - 0.3, ry, xs + 0.3 if direction < 0 else xs - 0.6, ry, 0.8, "M-DUCT-HIDN", "RA 800×300 IN VOID BELOW", lab_side=-1)
        n_br = 8
        for i in range(n_br):
            x = -xs + (i + 0.5) * (2 * xs / n_br)
            qd = q * 1000 / (2 * n_br)
            duct(d, x, sy + 0.3, x, 2.9 - 0.15, 0.2, "M-DUCT-SUPP"); diffuser(d, x, 2.9, ls=qd if i % 2 == 0 else None, tag_side=1)
            duct(d, x, sy - 0.3, x, -2.6 + 0.15, 0.2, "M-DUCT-SUPP"); diffuser(d, x, -2.6, ls=qd if i % 2 == 0 else None, tag_side=-1)
            grille(d, x + 0.6, 0.6, ls=qd if i % 2 == 1 else None, tag_side=-1); grille(d, x + 0.6, -0.6)
        if PROGRAM_A[k] in ("QA", "GAL", "MED", "WKS"):
            duct(d, -xs, -3.3, xs - 0.6, -3.3, 0.3, "M-DUCT-EXHS", "EA 300×200 · WC/SHOWER/HOOD EXHAUST TO TCCS", lab_side=-1, h=1.05)
        # bulkhead crossings: isolation dampers in every duct
    for xb in (-L / 2, L / 2):
        damper(d, xb, sy, 0.9 if k in AHU_OF else 0.6, "FSD")
        damper(d, xb, ry, 1.0 if k in AHU_OF else 0.8, "FSD")


def vent_B(d, k, L, fl):
    xs = L / 2 - BH_T / 2
    q = fl[k][1]
    d.deck = "B"
    sy, ry = -1.0, 1.1
    owner = k if k in AHU_OF else (k - 1 if (k - 1) in AHU_OF else (k + 1 if (k + 1) in AHU_OF else (1 if k == 12 else None)))
    if k in AHU_OF:
        d.v.rect(*d.P(-9.6, -3.85), *d.P(-6.2, -2.45), "M-HVAC-EQPM", fill="#e8eef8")
        d.text(-7.9, -3.15, f"AHU-R{AHU_OF[k]}\n{sum(zone_flows()[j][0] + zone_flows()[j][1] for j in served(k)):.2f} m³/s", "M-ANNO-TEXT", h=1.25, bold=True)
        duct(d, -xs - 0.3, sy, xs + 0.3, sy, 0.5, "M-DUCT-SUPP", "SA 500×200 OVERHEAD", lab_side=-1)
        duct(d, -xs - 0.3, ry, xs + 0.3, ry, 0.5, "M-DUCT-RETN", "RA 500×200 OVERHEAD", lab_side=1)
        duct(d, -7.9, -2.45, -7.9, sy - 0.25, 0.5, "M-DUCT-SUPP")
        # revitalised air in from the hub, spill back to the hub: through the spoke chase
        duct(d, 1.65, 1.5, 1.65, 3.1, 0.4, "M-DUCT-OAIR", None)
        duct(d, -6.2, 3.1, 1.65, 3.1, 0.4, "M-DUCT-OAIR", f"OA 400×200 FROM HUB ECLSS · {OA_PER_PERSON * 27:.2f} m³/s", lab_side=1)
        duct(d, -6.2, 3.1, -6.2, -2.45, 0.4, "M-DUCT-OAIR")
        duct(d, 1.65, -3.2, 1.65, -1.5, 0.4, "M-DUCT-RETN")
        duct(d, -6.2, -3.4, 1.65, -3.4, 0.35, "M-DUCT-RETN", "RH 400×200 SPILL TO HUB", lab_side=-1)
        d.v.rect(*d.P(-5.4, 2.05), *d.P(-4.4, 2.45), "M-DUCT-SUPP", fill="#cfe0ff")
        d.text(-4.9, 1.75, "SA DROP TO DECK A VOID", "M-ANNO-TEXT", h=1.05)
    else:
        duct(d, -xs - 0.3, sy, xs - 0.5, sy, 0.4, "M-DUCT-SUPP", f"SA 400×200 · {q:.2f} m³/s", lab_side=-1)
        duct(d, -xs + 0.5, ry, xs + 0.3, ry, 0.4, "M-DUCT-RETN", "RA 400×200", lab_side=1)
    for i in range(6):
        x = -xs + (i + 0.5) * (2 * xs / 6)
        duct(d, x, sy - 0.2, x, -2.6 + 0.125, 0.2, "M-DUCT-SUPP"); diffuser(d, x, -2.6, 0.25, ls=q * 1000 / 6 if i % 2 == 0 else None, tag_side=-1)
        duct(d, x + 0.8, ry + 0.2, x + 0.8, 2.6 - 0.2, 0.2, "M-DUCT-RETN"); grille(d, x + 0.8, 2.6, 0.4, 0.4, ls=q * 1000 / 6 if i % 2 == 0 else None, tag_side=1)
    for xb in (-L / 2, L / 2):
        damper(d, xb, sy, 0.5, "FSD"); damper(d, xb, ry, 0.5, "FSD")


def served(j):
    """Compartments served by the AHU in junction compartment j."""
    return [(j - 2) % 12 + 1, j, j % 12 + 1]


def vent_plan(v, deck, comps, theta_start):
    fl = zone_flows()
    r = R_A if deck == "A" else R_B
    L = L_of(r)
    for k in comps:
        d = D(v, ox=compartment_x(k, deck, theta_start))
        (vent_A if deck == "A" else vent_B)(d, k, L, fl)


class Grey(View):
    """A background: everything architectural redrawn thin and grey, no dimensions."""
    def _p(self, kind, layer, **d):
        if kind == "dim": return
        if layer.startswith("A-"):
            if kind in ("tag",):
                d["h"] = d.get("h", 2.0) * 0.8
            if kind == "text" or kind == "tag" or kind == "bubble":
                if kind == "text": d["color"] = "#9a9a9a"
                super()._p(kind, "A-ANNO-NOTE" if kind != "bubble" else layer, **d)
                return
            if "fill" in d and d.get("fill"): d["fill"] = "#d9d9d9" if d["fill"] in (FILL_WALL, FILL_SHAFT) else "#bfbfbf"
            super()._p(kind, "A-FURN", **d)
            return
        super()._p(kind, layer, **d)


class PolarGrey(Grey, PolarView):
    """The greyed architecture background of the M sheets, in true plan."""
    def __init__(self, name, xm=0.0, r=R_MID):
        PolarView.__init__(self, name, xm, r)
