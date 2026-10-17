"""sfcmp.py OUT REF[,REF...] OURS[,OURS...] : sci-fi references above our sketches -> docs/blueprints/sketch/cmp_<OUT>.jpg"""
import os, sys
from PIL import Image, ImageDraw
D = os.path.join(os.path.dirname(__file__), "..", "..", "docs", "blueprints")


def row(files, h, W=2000):
    ims = [Image.open(f).convert("RGB") for f in files]
    ims = [i.resize((int(i.width * h / i.height), h)) for i in ims]
    tot = sum(i.width for i in ims) + 8 * (len(ims) - 1); s = min(1.0, W / tot)
    ims = [i.resize((int(i.width * s), int(h * s))) for i in ims]
    o = Image.new("RGB", (W, int(h * s)), (20, 20, 24)); x = 0
    for i in ims: o.paste(i, (x, 0)); x += i.width + 8
    return o


out, refs, ours = sys.argv[1], sys.argv[2].split(","), sys.argv[3].split(",")
a = row([os.path.join(D, "refs", f"sf_{r}.png") for r in refs], 420)
b = row([os.path.join(D, "sketch", f"{o}.png") for o in ours], 560)
o = Image.new("RGB", (2000, a.height + b.height + 56), (20, 20, 24)); d = ImageDraw.Draw(o)
d.text((8, 4), "REFERENCES", fill=(230, 230, 230)); o.paste(a, (0, 22))
d.text((8, a.height + 28), "OURS (built from the blueprints)", fill=(230, 230, 230)); o.paste(b, (0, a.height + 50))
p = os.path.join(D, "sketch", f"cmp_{out}.jpg"); o.save(p, quality=88); print(p)
