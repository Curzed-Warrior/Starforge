"""
A small drafting kit: model-space views drawn onto ISO sheets (A1), rendered to PDF/PNG through matplotlib and to DXF
through ezdxf from the same primitives.

Conventions (ISO 128 / 129 / 3098 / 5455 / 5457 / 7200):
  - model units are metres; dimension text is in millimetres; text heights are given in paper millimetres
  - pen widths 0.13 / 0.18 / 0.25 / 0.35 / 0.5 / 0.7 / 1.0 mm
  - continuous wide: cut walls and hull; continuous narrow: visible edges, furniture; dashed narrow: hidden and
    overhead; chain narrow: centre and grid lines; chain wide: section planes
"""
import math
from dataclasses import dataclass, field
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Polygon, Circle as MCircle, Arc as MArc
from matplotlib.backends.backend_pdf import PdfPages

PT = 72 / 25.4                     # points per millimetre
plt.rcParams["font.family"] = ["Arial", "DejaVu Sans"]
plt.rcParams["hatch.linewidth"] = 0.25

# ------------------------------------------------------------------ layers
# name: (colour, pen mm, linestyle, zorder, DXF ACI colour)
LAYERS = {
    "A-WALL-PRHU": ("#000000", 0.70, "-", 6, 7),     # pressure hull, cut
    "A-WALL-BHD":  ("#000000", 0.50, "-", 6, 7),     # pressure bulkheads, cut
    "A-WALL":      ("#000000", 0.35, "-", 5, 7),     # partitions, cut
    "A-WALL-SHFT": ("#000000", 0.35, "-", 5, 7),     # shaft enclosures
    "A-DOOR":      ("#000000", 0.25, "-", 5, 3),
    "A-DOOR-PRES": ("#000000", 0.35, "-", 5, 1),
    "A-GLAZ":      ("#1a4f8a", 0.25, "-", 5, 5),
    "A-FLOR-STRS": ("#000000", 0.25, "-", 4, 2),
    "A-FURN":      ("#444444", 0.18, "-", 3, 8),
    "A-EQPM":      ("#333333", 0.25, "-", 3, 30),
    "A-EQPM-HIDN": ("#555555", 0.18, (0, (4, 2)), 3, 30),
    "A-CLNG-OVHD": ("#555555", 0.18, (0, (6, 2, 1, 2)), 3, 8),
    "A-HIDN":      ("#555555", 0.18, (0, (4, 2)), 3, 8),
    "A-GRID":      ("#7a7a7a", 0.18, (0, (12, 2, 2, 2)), 1, 9),
    "A-CNTR":      ("#7a7a7a", 0.13, (0, (12, 2, 2, 2)), 1, 9),
    "A-SECT":      ("#000000", 0.70, (0, (16, 3, 3, 3)), 9, 1),
    "A-AREA":      ("#e8e2d0", 0.00, "-", 0, 254),   # room fills
    "A-POCHE":     ("#000000", 0.50, "-", 0.5, 7),   # solid structure fill (hull), drawn under everything
    "A-VOID":      ("#ffffff", 0.00, "-", 0.8, 7),   # the interior cut out of the poche, still under the content
    "A-ANNO-DIMS": ("#000000", 0.18, "-", 8, 2),
    "A-ANNO-TEXT": ("#000000", 0.25, "-", 9, 7),
    "A-ANNO-NOTE": ("#000000", 0.18, "-", 9, 7),
    "A-ANNO-TTLB": ("#000000", 0.35, "-", 9, 7),
    "M-DUCT-SUPP": ("#0050c8", 0.35, "-", 7, 5),
    "M-DUCT-RETN": ("#c84b00", 0.35, "-", 7, 30),
    "M-DUCT-EXHS": ("#7a00a8", 0.35, "-", 7, 6),
    "M-DUCT-OAIR": ("#008a3a", 0.35, "-", 7, 3),     # revitalised air from the hub
    "M-DUCT-HIDN": ("#666666", 0.18, (0, (4, 2)), 2, 8),
    "M-DUCT-JNT":  ("#444444", 0.13, "-", 7.5, 8),    # duct joints / flanges
    "M-HVAC-EQPM": ("#000000", 0.35, "-", 7, 4),
    "M-HVAC-DAMP": ("#000000", 0.25, "-", 8, 1),
    "M-HVAC-DIFF": ("#0050c8", 0.25, "-", 8, 5),
    "M-HVAC-GRIL": ("#c84b00", 0.25, "-", 8, 30),
    "M-ANNO-TEXT": ("#000000", 0.25, "-", 9, 7),
    "S-STRC":      ("#000000", 0.25, "-", 4, 7),
    "S-STRC-HIDN": ("#666666", 0.18, (0, (4, 2)), 2, 8),
    "P-PIPE":      ("#007a7a", 0.35, "-", 6, 4),
    "E-POWR":      ("#a80000", 0.35, "-", 6, 1),
}


# ------------------------------------------------------------------ primitives (model space)
@dataclass
class Prim:
    kind: str                      # line, poly, circle, arc, text, dim, raw
    layer: str
    data: dict


class View:
    """A drawing in model space (metres). Text and annotation sizes are in paper mm, so they read the same at any scale."""

    def __init__(self, name):
        self.name = name
        self.prims = []

    def _p(self, kind, layer, **d):
        assert layer in LAYERS, layer
        self.prims.append(Prim(kind, layer, d))

    def line(self, p0, p1, layer):
        self._p("line", layer, pts=[tuple(p0), tuple(p1)])

    def poly(self, pts, layer, closed=False, fill=None, hatch=None, lw=None):
        self._p("poly", layer, pts=[tuple(p) for p in pts], closed=closed, fill=fill, hatch=hatch, lw=lw)

    def rect(self, x0, y0, x1, y1, layer, fill=None, hatch=None):
        self.poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], layer, closed=True, fill=fill, hatch=hatch)

    def circle(self, c, r, layer, fill=None, hatch=None):
        self._p("circle", layer, c=tuple(c), r=r, fill=fill, hatch=hatch)

    def arc(self, c, r, a0, a1, layer):
        """Arc from a0 to a1 degrees, counter-clockwise."""
        self._p("arc", layer, c=tuple(c), r=r, a0=a0, a1=a1)

    def text(self, p, s, layer="A-ANNO-TEXT", h=2.5, ha="center", va="center", rot=0, bold=False, color=None):
        self._p("text", layer, p=tuple(p), s=s, h=h, ha=ha, va=va, rot=rot, bold=bold, color=color)

    def dim(self, p0, p1, off, layer="A-ANNO-DIMS", text=None, h=1.8):
        """Aligned dimension between two model points, the dimension line offset `off` paper-mm to the left of p0->p1."""
        self._p("dim", layer, p0=tuple(p0), p1=tuple(p1), off=off, text=text, h=h)

    def chain(self, pts, off, layer="A-ANNO-DIMS", h=1.8):
        for a, b in zip(pts, pts[1:]):
            if abs(a[0] - b[0]) + abs(a[1] - b[1]) > 1e-6: self.dim(a, b, off, layer, h=h)

    def leader(self, p_obj, p_text, s, layer="A-ANNO-NOTE", h=1.8, ha="left"):
        self._p("leader", layer, p0=tuple(p_obj), p1=tuple(p_text), s=s, h=h, ha=ha)

    def tag(self, p, name, number, area=None, h=2.2):
        """Room tag: name, number, area."""
        self._p("tag", "A-ANNO-TEXT", p=tuple(p), name=name, number=number, area=area, h=h)

    def symtag(self, p, label, shape="ellipse", h=1.2, layer="A-ANNO-TEXT"):
        """A paper-size annotation symbol at a model point: ellipse (door), hex (window / wall type), diamond (keynote),
        box (room number), split (diffuser: 'TYPE|flow|neck')."""
        self._p("symtag", layer, p=tuple(p), label=label, shape=shape, h=h)

    def datum(self, p, name, value, side=1, length=22.0, layer="A-ANNO-TEXT"):
        """Level datum (ISO 128 / architectural target): a quartered circle at p, a line `length` paper-mm toward
        `side`, the level name above and its value below."""
        self._p("datum", layer, p=tuple(p), name=name, value=value, side=side, length=length)

    def image(self, path, x0, y0, x1, y1, layer="A-ANNO-TTLB"):
        """A raster (concept sketch) placed in paper space, framed."""
        self._p("image", layer, path=path, box=(x0, y0, x1, y1))

    def bubble(self, p, label, d=7.0, layer="A-ANNO-TEXT"):
        self._p("bubble", layer, p=tuple(p), label=label, d=d)

    def section_mark(self, p0, p1, letter, sheet, look=1, layer="A-SECT"):
        """Section cut from p0 to p1 (model), arrows looking to the left of p0->p1 (look=1) or right (-1)."""
        self._p("section", layer, p0=tuple(p0), p1=tuple(p1), letter=letter, sheet=sheet, look=look)

    def extend(self, other, dx=0.0, dy=0.0, rot=0.0):
        c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        T = lambda p: (p[0] * c - p[1] * s + dx, p[0] * s + p[1] * c + dy)
        for pr in other.prims:
            d = dict(pr.data)
            for k in ("pts",):
                if k in d: d[k] = [T(p) for p in d[k]]
            for k in ("c", "p", "p0", "p1"):
                if k in d: d[k] = T(d[k])
            if "a0" in d: d["a0"] += rot; d["a1"] += rot
            if pr.kind == "text": d["rot"] = d.get("rot", 0) + 0
            self.prims.append(Prim(pr.kind, pr.layer, d))

    def bounds(self):
        xs, ys = [], []
        for pr in self.prims:
            d = pr.data
            for p in d.get("pts", []): xs.append(p[0]); ys.append(p[1])
            if "c" in d: xs += [d["c"][0] - d["r"], d["c"][0] + d["r"]]; ys += [d["c"][1] - d["r"], d["c"][1] + d["r"]]
        return min(xs), min(ys), max(xs), max(ys)


# ------------------------------------------------------------------ sheets (paper space, millimetres)
A1 = (841.0, 594.0)


@dataclass
class Placed:
    view: View
    origin: tuple       # paper mm where model (0,0) lands
    scale: float        # paper mm per model metre (1:100 -> 10)
    title: str = None
    number: str = None
    scale_label: str = None
    title_at: tuple = None


@dataclass
class Sheet:
    number: str
    title: str
    subtitle: str = ""
    discipline: str = "Architecture"
    size: tuple = A1
    views: list = field(default_factory=list)
    paper: View = None
    status: str = "ISSUED FOR MODELLING"
    rev: str = "A"

    def __post_init__(self):
        self.paper = View("paper")

    def place(self, view, origin, scale_n, title=None, number=None, title_at=None):
        """Place a view at 1:scale_n with model (0,0) at paper `origin`."""
        self.views.append(Placed(view, origin, 1000.0 / scale_n, title, number, f"1:{scale_n}", title_at))


# ------------------------------------------------------------------ matplotlib renderer
def _lw(layer, over=None):
    return (over if over is not None else LAYERS[layer][1]) * PT


def _fs(h):
    """Font size (pt) for a cap height of h paper-mm (ISO 3098: cap height ~0.72 em)."""
    return h * PT / 0.72


class MplRenderer:
    def __init__(self, sheet):
        self.sh = sheet
        W, H = sheet.size
        self.fig = plt.figure(figsize=(W / 25.4, H / 25.4))
        self.ax = self.fig.add_axes([0, 0, 1, 1])
        self.ax.set_xlim(0, W); self.ax.set_ylim(0, H)
        self.ax.set_aspect("equal"); self.ax.axis("off")

    def draw_view(self, v, T, scale):
        ax = self.ax
        for pr in v.prims:
            col, pen, ls, z, _ = LAYERS[pr.layer]
            d = pr.data
            if pr.kind == "line":
                (x0, y0), (x1, y1) = T(d["pts"][0]), T(d["pts"][1])
                ax.plot([x0, x1], [y0, y1], color=col, lw=_lw(pr.layer), ls=ls, zorder=z, solid_capstyle="butt")
            elif pr.kind == "poly":
                pts = [T(p) for p in d["pts"]]
                if d.get("fill") or d.get("hatch"):
                    ax.add_patch(Polygon(pts, closed=True, facecolor=d.get("fill") or "none", edgecolor=col if pen > 0 else "none",
                                         lw=_lw(pr.layer, d.get("lw")), ls=ls, hatch=d.get("hatch"), zorder=z, joinstyle="miter"))
                else:
                    if d["closed"]: pts = pts + [pts[0]]
                    ax.plot([p[0] for p in pts], [p[1] for p in pts], color=col, lw=_lw(pr.layer, d.get("lw")), ls=ls, zorder=z,
                            solid_joinstyle="miter", solid_capstyle="butt")
            elif pr.kind == "circle":
                c = T(d["c"])
                ax.add_patch(MCircle(c, d["r"] * scale, facecolor=d.get("fill") or "none", edgecolor=col, lw=_lw(pr.layer), ls=ls,
                                     hatch=d.get("hatch"), zorder=z))
            elif pr.kind == "arc":
                c = T(d["c"])
                ax.add_patch(MArc(c, 2 * d["r"] * scale, 2 * d["r"] * scale, theta1=d["a0"], theta2=d["a1"], color=col, lw=_lw(pr.layer), ls=ls, zorder=z))
            elif pr.kind == "text":
                ax.text(*T(d["p"]), d["s"], fontsize=_fs(d["h"]), ha=d["ha"], va=d["va"], rotation=d["rot"], color=d.get("color") or col,
                        fontweight="bold" if d["bold"] else "normal", zorder=z, linespacing=1.25)
            elif pr.kind == "dim":
                self._dim(T(d["p0"]), T(d["p1"]), d["off"], d["text"] or f"{round(_dist(d['p0'], d['p1']) * 1000):d}", d["h"], col)
            elif pr.kind == "leader":
                p0, p1 = T(d["p0"]), T(d["p1"])
                ax.plot([p0[0], p1[0]], [p0[1], p1[1]], color=col, lw=_lw(pr.layer), zorder=z)
                ax.add_patch(MCircle(p0, 0.45, facecolor=col, edgecolor="none", zorder=z))
                ax.plot([p1[0], p1[0] + (2 if d["ha"] == "left" else -2)], [p1[1], p1[1]], color=col, lw=_lw(pr.layer), zorder=z)
                ax.text(p1[0] + (3 if d["ha"] == "left" else -3), p1[1], d["s"], fontsize=_fs(d["h"]), ha=d["ha"], va="center", zorder=z, linespacing=1.2)
            elif pr.kind == "tag":
                x, y = T(d["p"]); h = d["h"]
                ax.text(x, y + h * 0.75, d["name"], fontsize=_fs(h), ha="center", va="center", fontweight="bold", zorder=9)
                nh = h * 0.78; nw = len(d["number"]) * nh * 0.66 + nh * 1.2
                ny = y - h * 0.75
                ax.add_patch(Polygon([(x - nw / 2, ny - nh * 0.9), (x + nw / 2, ny - nh * 0.9), (x + nw / 2, ny + nh * 0.9), (x - nw / 2, ny + nh * 0.9)],
                                     closed=True, facecolor="white", edgecolor="black", lw=0.18 * PT, zorder=9))
                ax.text(x, ny, d["number"], fontsize=_fs(nh), ha="center", va="center", zorder=10)
                if d["area"]: ax.text(x, ny - nh * 2.0, f"{d['area']:.1f} m²", fontsize=_fs(nh), ha="center", va="center", zorder=9)
            elif pr.kind == "bubble":
                x, y = T(d["p"])
                ax.add_patch(MCircle((x, y), d["d"] / 2, facecolor="white", edgecolor="black", lw=0.25 * PT, zorder=9))
                ax.text(x, y, d["label"], fontsize=_fs(d["d"] * 0.33), ha="center", va="center", zorder=10)
            elif pr.kind == "image":
                x0, y0, x1, y1 = d["box"]
                ax.imshow(plt.imread(d["path"]), extent=[x0, x1, y0, y1], zorder=2, interpolation="antialiased")
                ax.add_patch(Polygon([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], closed=True, facecolor="none", edgecolor="black", lw=0.35 * PT, zorder=3))
            elif pr.kind == "symtag":
                self._symtag(T(d["p"]), d["label"], d["shape"], d["h"])
            elif pr.kind == "datum":
                self._datum(T(d["p"]), d["name"], d["value"], d["side"], d["length"])
            elif pr.kind == "section":
                self._section(T(d["p0"]), T(d["p1"]), d["letter"], d["sheet"], d["look"])

    def _dim(self, a, b, off, txt, h, col):
        ax = self.ax
        dx, dy = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dy)
        if L < 0.5: return
        ux, uy = dx / L, dy / L
        nx, ny = -uy, ux
        a2 = (a[0] + nx * off, a[1] + ny * off); b2 = (b[0] + nx * off, b[1] + ny * off)
        sgn = 1 if off >= 0 else -1
        lw = 0.18 * PT
        for p, q in ((a, a2), (b, b2)):          # extension lines: 1 mm gap, 2 mm past the dimension line
            ax.plot([p[0] + nx * sgn * 1.0, q[0] + nx * sgn * 2.0], [p[1] + ny * sgn * 1.0, q[1] + ny * sgn * 2.0], color=col, lw=lw, zorder=8)
        ax.plot([a2[0] - ux * 1.5, b2[0] + ux * 1.5], [a2[1] - uy * 1.5, b2[1] + uy * 1.5], color=col, lw=lw, zorder=8)
        for p in (a2, b2):                       # oblique strokes (ISO 129)
            tx, ty = (ux + nx) * 1.1, (uy + ny) * 1.1
            ax.plot([p[0] - tx, p[0] + tx], [p[1] - ty, p[1] + ty], color=col, lw=0.35 * PT, zorder=8)
        ang = math.degrees(math.atan2(uy, ux))
        if ang > 90.5 or ang < -89.5: ang += 180
        mx, my = (a2[0] + b2[0]) / 2, (a2[1] + b2[1]) / 2
        tw = len(txt) * h * 0.62
        if tw > L * 0.92:       # no room between the strokes: set it outside
            mx += ux * (L / 2 + tw / 2 + 2); my += uy * (L / 2 + tw / 2 + 2)
        ax.text(mx + nx * 0.9 * (1 if ang == math.degrees(math.atan2(uy, ux)) else -1) * 0 + nx * 0.9, my + ny * 0.9, txt,
                fontsize=_fs(h), ha="center", va="bottom", rotation=ang, rotation_mode="anchor", zorder=8)

    def _symtag(self, c, label, shape, h):
        ax = self.ax; x, y = c; lw = 0.18 * PT
        if shape == "split":
            parts = label.split("|")
            w = max(len(t) for t in parts) * h * 0.62 + 1.0
            ax.plot([x - w / 2, x + w / 2], [y, y], color="black", lw=lw, zorder=9)
            ax.text(x, y + 0.5, parts[0], fontsize=_fs(h), ha="center", va="bottom", zorder=9, fontweight="bold")
            ax.text(x, y - 0.5, chr(10).join(parts[1:]), fontsize=_fs(h * 0.85), ha="center", va="top", zorder=9, linespacing=1.15)
            return
        w = len(label) * h * 0.64 + h * 1.4; hh = h * 2.0
        if shape == "ellipse":
            from matplotlib.patches import Ellipse
            ax.add_patch(Ellipse((x, y), w, hh, facecolor="white", edgecolor="black", lw=lw, zorder=9))
        elif shape == "box":
            ax.add_patch(Polygon([(x - w / 2, y - hh / 2), (x + w / 2, y - hh / 2), (x + w / 2, y + hh / 2), (x - w / 2, y + hh / 2)], closed=True,
                                 facecolor="white", edgecolor="black", lw=lw, zorder=9))
        elif shape == "diamond":
            r = max(w, hh) * 0.62
            ax.add_patch(Polygon([(x - r, y), (x, y + r), (x + r, y), (x, y - r)], closed=True, facecolor="white", edgecolor="black", lw=lw, zorder=9))
        elif shape == "hex":
            r = max(w * 0.58, hh * 0.62)
            ax.add_patch(Polygon([(x + r * math.cos(math.radians(a)), y + r * math.sin(math.radians(a))) for a in range(0, 360, 60)], closed=True,
                                 facecolor="white", edgecolor="black", lw=lw, zorder=9))
        ax.text(x, y, label, fontsize=_fs(h), ha="center", va="center", zorder=10)

    def _datum(self, c, name, value, side, length):
        from matplotlib.patches import Wedge
        ax = self.ax; x, y = c; r = 1.5; lw = 0.25 * PT
        ax.add_patch(MCircle((x, y), r, facecolor="white", edgecolor="black", lw=lw, zorder=9))
        for a0 in (0, 180): ax.add_patch(Wedge((x, y), r, a0, a0 + 90, facecolor="black", edgecolor="none", zorder=10))
        x1 = x + side * length
        ax.plot([x + side * r, x1], [y, y], color="black", lw=lw, zorder=9)
        ha = "left" if side > 0 else "right"
        tx = x + side * (r + 1.0)
        ax.text(tx, y + 0.6, name, fontsize=_fs(1.5), ha=ha, va="bottom", zorder=9, fontweight="bold")
        ax.text(tx, y - 0.6, value, fontsize=_fs(1.35), ha=ha, va="top", zorder=9)

    def _section(self, a, b, letter, sheet, look):
        ax = self.ax
        dx, dy = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dy); ux, uy = dx / L, dy / L
        nx, ny = -uy * look, ux * look
        # chain-wide only at the ends and bends (ISO 128-40), the arrows, the reference bubbles
        for p, s in ((a, 1), (b, -1)):
            q = (p[0] + ux * 12 * s, p[1] + uy * 12 * s)
            ax.plot([p[0], q[0]], [p[1], q[1]], color="black", lw=0.7 * PT, zorder=9)
            ax.annotate("", xy=(p[0] + nx * 7, p[1] + ny * 7), xytext=p, arrowprops=dict(arrowstyle="-|>", lw=0.5 * PT, color="black", mutation_scale=9), zorder=9)
            c = (p[0] - ux * 6 * s, p[1] - uy * 6 * s)
            ax.add_patch(MCircle(c, 5, facecolor="white", edgecolor="black", lw=0.35 * PT, zorder=9))
            ax.plot([c[0] - 5, c[0] + 5], [c[1], c[1]], color="black", lw=0.25 * PT, zorder=10)
            ax.text(c[0], c[1] + 2.4, letter, fontsize=_fs(2.6), ha="center", va="center", fontweight="bold", zorder=10)
            ax.text(c[0], c[1] - 2.4, sheet, fontsize=_fs(1.6), ha="center", va="center", zorder=10)
        ax.plot([a[0], b[0]], [a[1], b[1]], color="black", lw=0.18 * PT, ls=(0, (16, 3, 3, 3)), zorder=8)

    def render(self):
        sh = self.sh
        for pv in sh.views:
            ox, oy = pv.origin; s = pv.scale
            self.draw_view(pv.view, lambda p, ox=ox, oy=oy, s=s: (ox + p[0] * s, oy + p[1] * s), s)
            if pv.title:
                x0, y0, x1, y1 = pv.view.bounds()
                tx, ty = pv.title_at or (ox + x0 * s, oy + y0 * s - 14)
                view_title(self.ax, (tx, ty), pv.number, pv.title, pv.scale_label)
        self.draw_view(sh.paper, lambda p: p, 1.0)
        frame(self.ax, sh)
        return self.fig


def _dist(a, b): return math.hypot(b[0] - a[0], b[1] - a[1])


def view_title(ax, p, number, title, scale):
    """Drawing title under a view: a numbered bubble, the title, the scale, and a wide underline."""
    x, y = p
    ax.add_patch(MCircle((x + 5, y), 5, facecolor="white", edgecolor="black", lw=0.35 * PT, zorder=9))
    ax.text(x + 5, y, number or "", fontsize=_fs(2.8), ha="center", va="center", fontweight="bold", zorder=10)
    ax.text(x + 13, y + 1.0, title.upper(), fontsize=_fs(3.2), ha="left", va="bottom", fontweight="bold", zorder=9)
    tw = max(len(title) * 3.2 * 0.72, 60)
    ax.plot([x + 12, x + 12 + tw], [y, y], color="black", lw=0.7 * PT, zorder=9)
    ax.text(x + 13, y - 1.2, f"SCALE {scale}" if scale else "NOT TO SCALE", fontsize=_fs(2.0), ha="left", va="top", zorder=9)


def scale_bar(v, p, n, length_m, step_m):
    """Graphic scale (ISO 5455) drawn into a paper-space view: alternating blocks, labelled in metres."""
    x, y = p; s = 1000.0 / n
    k, m = 0, 0.0
    while m < length_m - 1e-9:
        v.rect(x + m * s, y, x + (m + step_m) * s, y + 2, "A-ANNO-TTLB", fill="#000000" if k % 2 == 0 else "#ffffff")
        v.text((x + m * s, y - 1.6), f"{m:g}", h=1.5)
        m += step_m; k += 1
    v.text((x + m * s + 2, y - 1.6), f"{m:g} m", h=1.5)


def frame(ax, sh):
    """ISO 5457 border with grid reference zones and centring marks, and an ISO 7200 title block."""
    W, H = sh.size
    L, R, B, Tm = 20.0, 10.0, 10.0, 10.0
    ax.add_patch(Polygon([(L, B), (W - R, B), (W - R, H - Tm), (L, H - Tm)], closed=True, fill=False, lw=0.7 * PT, zorder=10))
    ax.add_patch(Polygon([(L - 5, B - 5), (W - R + 5, B - 5), (W - R + 5, H - Tm + 5), (L - 5, H - Tm + 5)], closed=True, fill=False, lw=0.25 * PT, zorder=10))
    nx, ny = 16, 12
    for i in range(nx + 1):
        x = L + (W - R - L) * i / nx
        for y0, y1 in ((B - 5, B), (H - Tm, H - Tm + 5)): ax.plot([x, x], [y0, y1], color="black", lw=0.25 * PT, zorder=10)
        if i < nx:
            xm = L + (W - R - L) * (i + 0.5) / nx
            for yy in (B - 2.5, H - Tm + 2.5): ax.text(xm, yy, str(i + 1), fontsize=_fs(2.0), ha="center", va="center", zorder=10)
    letters = "ABCDEFGHJKLM"
    for j in range(ny + 1):
        y = B + (H - Tm - B) * j / ny
        for x0, x1 in ((L - 5, L), (W - R, W - R + 5)): ax.plot([x0, x1], [y, y], color="black", lw=0.25 * PT, zorder=10)
        if j < ny:
            ym = H - Tm - (H - Tm - B) * (j + 0.5) / ny
            for xx in (L - 2.5, W - R + 2.5): ax.text(xx, ym, letters[j], fontsize=_fs(2.0), ha="center", va="center", zorder=10)
    for (x0, y0, x1, y1) in ((W / 2, B - 5, W / 2, B + 5), (W / 2, H - Tm - 5, W / 2, H - Tm + 5), (L - 5, H / 2, L + 5, H / 2), (W - R - 5, H / 2, W - R + 5, H / 2)):
        ax.plot([x0, x1], [y0, y1], color="black", lw=0.7 * PT, zorder=10)
    title_block(ax, (W - R - 180, B), sh)


def title_block(ax, p, sh):
    x, y = p; w, h = 180.0, 62.0
    lw = 0.35 * PT
    def box(x0, y0, x1, y1, lab=None, val=None, vh=2.4, bold=False):
        ax.add_patch(Polygon([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], closed=True, facecolor="white", edgecolor="black", lw=0.25 * PT, zorder=10))
        if lab: ax.text(x0 + 1.2, y1 - 1.2, lab, fontsize=_fs(1.3), ha="left", va="top", color="#444", zorder=11)
        if val: ax.text(x0 + 1.5, (y0 + y1) / 2 - 1.0, val, fontsize=_fs(vh), ha="left", va="center", fontweight="bold" if bold else "normal", zorder=11)
    ax.add_patch(Polygon([(x, y), (x + w, y), (x + w, y + h), (x, y + h)], closed=True, facecolor="white", edgecolor="black", lw=lw, zorder=10))
    # row 1 (top): legal owner / project
    box(x, y + 50, x + w, y + h, "LEGAL OWNER / PROJECT", "STARFORGE  ·  OUTPOST STARBASE (TIER 0)  ·  INTERIOR FIT-OUT", 2.6, True)
    # row 2: title
    box(x, y + 34, x + w, y + 50, "TITLE", sh.title.upper(), 3.4, True)
    ax.text(x + 1.5, y + 36.2, sh.subtitle, fontsize=_fs(2.0), ha="left", va="bottom", zorder=11)
    # row 3: document data
    box(x, y + 22, x + 45, y + 34, "DISCIPLINE", sh.discipline)
    box(x + 45, y + 22, x + 90, y + 34, "DOCUMENT STATUS", sh.status, 1.9)
    box(x + 90, y + 22, x + 130, y + 34, "SCALE", "AS SHOWN" if sh.views else "NTS")
    box(x + 130, y + 22, x + w, y + 34, "SIZE / UNITS", "A1 · mm")
    # row 4
    box(x, y + 10, x + 45, y + 22, "CREATED BY", "Claude (AI)")
    box(x + 45, y + 10, x + 90, y + 22, "APPROVED BY", "—")
    box(x + 90, y + 10, x + 130, y + 22, "DATE OF ISSUE", "2026-10-17")
    box(x + 130, y + 10, x + w, y + 22, "PROJECTION", "")
    _projection_symbol(ax, (x + 160, y + 15.5))
    # row 5: identification
    box(x, y, x + 90, y + 10, "IDENTIFICATION NUMBER", f"SF-OUT-{sh.number}", 3.0, True)
    box(x + 90, y, x + 130, y + 10, "REV", sh.rev, 3.0, True)
    box(x + 130, y, x + w, y + 10, "SHEET", sh.number, 3.0, True)


def _projection_symbol(ax, c):
    """First-angle projection symbol (ISO 5456-2)."""
    x, y = c
    ax.add_patch(Polygon([(x - 12, y - 3), (x - 4, y - 5), (x - 4, y + 5), (x - 12, y + 3)], closed=True, fill=False, lw=0.25 * PT, zorder=11))
    ax.plot([x - 13, x - 3], [y, y], color="black", lw=0.13 * PT, ls=(0, (6, 1, 1, 1)), zorder=11)
    ax.add_patch(MCircle((x + 6, y), 5, fill=False, lw=0.25 * PT, zorder=11))
    ax.add_patch(MCircle((x + 6, y), 3, fill=False, lw=0.25 * PT, zorder=11))
    ax.plot([x + 0, x + 12], [y, y], color="black", lw=0.13 * PT, ls=(0, (6, 1, 1, 1)), zorder=11)


# ------------------------------------------------------------------ DXF export (model space, millimetres)
def to_dxf(views, path, gap=5.0):
    """Write views side by side into one DXF, in millimetres, on their layers. Annotations become text and lines."""
    import ezdxf
    doc = ezdxf.new("R2018", setup=True)
    doc.units = ezdxf.units.MM
    msp = doc.modelspace()
    for name, (col, pen, ls, z, aci) in LAYERS.items():
        if name not in doc.layers:
            lt = "CONTINUOUS" if ls == "-" else ("DASHED" if name.endswith("HIDN") or name.endswith("OVHD") else "CENTER")
            doc.layers.add(name, color=aci, lineweight=int(round(pen * 100)) if pen > 0 else -3, linetype=lt)
    xoff = 0.0
    for v in views:
        x0, y0, x1, y1 = v.bounds()
        T = lambda p, xo=xoff - x0 * 1000: ((p[0] * 1000) + xo, p[1] * 1000)
        for pr in v.prims:
            d = pr.data; L = {"layer": pr.layer}
            if pr.kind == "line": msp.add_line(T(d["pts"][0]), T(d["pts"][1]), dxfattribs=L)
            elif pr.kind == "poly":
                msp.add_lwpolyline([T(p) for p in d["pts"]], close=d["closed"], dxfattribs=L)
            elif pr.kind == "circle": msp.add_circle(T(d["c"]), d["r"] * 1000, dxfattribs=L)
            elif pr.kind == "arc": msp.add_arc(T(d["c"]), d["r"] * 1000, d["a0"], d["a1"], dxfattribs=L)
            elif pr.kind == "text":
                msp.add_text(d["s"].replace("\n", " "), height=d["h"] * 50, rotation=d["rot"], dxfattribs=L).set_placement(T(d["p"]), align=ezdxf.enums.TextEntityAlignment.MIDDLE_CENTER)
            elif pr.kind == "tag":
                msp.add_text(f"{d['name']} {d['number']}", height=d["h"] * 50, dxfattribs=L).set_placement(T(d["p"]), align=ezdxf.enums.TextEntityAlignment.MIDDLE_CENTER)
            elif pr.kind in ("symtag", "datum"):
                lab = d.get("label") or f"{d.get('name')} {d.get('value')}"
                msp.add_text(lab.replace("|", " / "), height=60, dxfattribs=L).set_placement(T(d["p"]), align=ezdxf.enums.TextEntityAlignment.MIDDLE_CENTER)
            elif pr.kind == "dim":
                a, b = T(d["p0"]), T(d["p1"])
                msp.add_aligned_dim(p1=a, p2=b, distance=d["off"] * 50, dxfattribs=L).render()
        msp.add_text(v.name.upper(), height=600, dxfattribs={"layer": "A-ANNO-TEXT"}).set_placement((xoff, -3000))
        xoff += (x1 - x0) * 1000 + gap * 1000
    doc.saveas(path)


def save_set(sheets, pdf_path, png_dir=None, dpi=110):
    import os
    with PdfPages(pdf_path) as pdf:
        for sh in sheets:
            fig = MplRenderer(sh).render()
            pdf.savefig(fig)
            if png_dir:
                fig.savefig(os.path.join(png_dir, f"{sh.number}.png"), dpi=dpi)
            plt.close(fig)
