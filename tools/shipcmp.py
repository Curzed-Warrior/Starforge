"""shipcmp.py CLASS ITER : the class's references (assets/ships/refs/<class>_ref*) in a row above our renders
(assets/stations/renders/ships/<class>_i<ITER>_*.png) -> assets/stations/renders/ships/cmp_<class>_i<ITER>.jpg"""
import sys, glob, os
from PIL import Image, ImageDraw
cls, it = sys.argv[1].lower(), sys.argv[2]
root = os.path.join(os.path.dirname(__file__), "..", "assets")
refs = sorted(glob.glob(os.path.join(root, "ships", "refs", f"{cls}_ref*.png")))
rens = sorted(glob.glob(os.path.join(root, "stations", "renders", "ships", f"{cls}_i{it}_*.png")))
W = 1800
def row(files, h):
    ims = [Image.open(f).convert("RGB") for f in files]
    ims = [i.resize((max(1, int(i.width * h / i.height)), h)) for i in ims]
    tot = sum(i.width for i in ims) + 8 * (len(ims) - 1)
    s = min(1.0, W / max(tot, 1))
    ims = [i.resize((max(1, int(i.width * s)), max(1, int(h * s)))) for i in ims]
    o = Image.new("RGB", (W, int(h * s)), (18, 18, 22)); x = 0
    for i in ims: o.paste(i, (x, 0)); x += i.width + 8
    return o
a, b = row(refs, 360), row(rens, 520)
o = Image.new("RGB", (W, a.height + b.height + 60), (18, 18, 22))
d = ImageDraw.Draw(o)
d.text((8, 6), f"REFERENCES: {cls}", fill=(230, 230, 230)); o.paste(a, (0, 24))
d.text((8, a.height + 32), f"OURS, iteration {it}", fill=(230, 230, 230)); o.paste(b, (0, a.height + 54))
out = os.path.join(root, "stations", "renders", "ships", f"cmp_{cls}_i{it}.jpg"); o.save(out, quality=88); print(out)
