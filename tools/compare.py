"""compare.py REF x0,y0,x1,y1 RENDER OUT [LABEL] : reference (cropped) beside a render, same height."""
import sys
from PIL import Image, ImageDraw
ref = Image.open(sys.argv[1]).convert("RGB")
x0, y0, x1, y1 = map(int, sys.argv[2].split(","))
ref = ref.crop((x0, y0, x1, y1))
ren = Image.open(sys.argv[3]).convert("RGB")
H = 700
ref = ref.resize((int(ref.width * H / ref.height), H)); ren = ren.resize((int(ren.width * H / ren.height), H))
o = Image.new("RGB", (ref.width + ren.width + 10, H + 30), (20, 20, 24))
o.paste(ref, (0, 30)); o.paste(ren, (ref.width + 10, 30))
d = ImageDraw.Draw(o)
d.text((8, 8), "REFERENCE: " + (sys.argv[5] if len(sys.argv) > 5 else ""), fill=(230, 230, 230))
d.text((ref.width + 18, 8), "OURS", fill=(230, 230, 230))
o.save(sys.argv[4])
