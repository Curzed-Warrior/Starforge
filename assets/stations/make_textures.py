"""
Tiling PBR texture sets for the Outpost, generated procedurally with numpy (runs in Blender's Python or any
Python with numpy + Pillow-free PNG writing via Blender images). Each set: <name>_albedo.png, <name>_orm.png
(R occlusion, G roughness, B metallic, as glTF expects) and <name>_normal.png. One tile = 4 m x 4 m.

Sets: hull (exterior plating), hulldark (structural metal), floor (deck grating), wall (interior panels),
ceiling (interior with service panels).
"""
import numpy as np, os
try:
    import bpy
except ImportError:
    bpy = None

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:/Users/diamo/Desktop/starforge/assets/stations", "textures")
N = 1024
rng = np.random.default_rng(42)
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / N


def save(name, rgb):
    """rgb: HxWx3 float 0..1 (linear for data maps, sRGB-ish for albedo)."""
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".png")
    a = np.clip(np.asarray(rgb, np.float32), 0, 1).astype(np.float32)
    if bpy:
        img = bpy.data.images.new(name, N, N, alpha=False, float_buffer=False)
        if not name.endswith("albedo"):
            img.colorspace_settings.name = "Non-Color"
        px = np.ascontiguousarray(np.concatenate([a[::-1], np.ones((N, N, 1), np.float32)], axis=2), np.float32)   # Blender images are bottom-up
        img.pixels.foreach_set(px.ravel())
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
        bpy.data.images.remove(img)
    else:
        from PIL import Image
        Image.fromarray((a * 255).astype(np.uint8)).save(path)


def blur(a, r):
    # box blur by cumulative sums, wrapping (keeps tiling)
    for axis in (0, 1):
        c = np.cumsum(np.concatenate([a.take(range(-r, 0), axis=axis), a, a.take(range(0, r + 1), axis=axis)], axis=axis), axis=axis)
        lo = np.take(c, range(0, a.shape[axis]), axis=axis)
        hi = np.take(c, range(2 * r + 1, a.shape[axis] + 2 * r + 1), axis=axis)
        a = (hi - lo) / (2 * r + 1)
    return a


def value_noise(scale, seed):
    g = np.random.default_rng(seed).random((scale, scale)).astype(np.float32)
    x = xx * scale; y = yy * scale
    x0 = np.floor(x).astype(int) % scale; y0 = np.floor(y).astype(int) % scale
    x1 = (x0 + 1) % scale; y1 = (y0 + 1) % scale
    fx = x - np.floor(x); fy = y - np.floor(y)
    fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    a = g[y0, x0] * (1 - fx) + g[y0, x1] * fx
    b = g[y1, x0] * (1 - fx) + g[y1, x1] * fx
    return a * (1 - fy) + b * fy


def fbm(seed, base=4, oct=5):
    v = np.zeros((N, N), np.float32); amp = 0.5; s = base
    for i in range(oct):
        v += amp * value_noise(s, seed + i); amp *= 0.5; s *= 2
    return v


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    n = np.stack([-dx, dy, np.ones_like(h)], axis=2)
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5


def panels(seed, depth=7, min_size=0.04):
    """Recursive subdivision into rectangular plates. Returns per-pixel plate id map and seam mask."""
    r = np.random.default_rng(seed)
    pid = np.zeros((N, N), np.int32)
    seam = np.zeros((N, N), np.float32)
    rects = [(0, 0, N, N, 0)]
    out = []
    nid = 0
    while rects:
        x0, y0, x1, y1, d = rects.pop()
        w, h = x1 - x0, y1 - y0
        if d >= depth or (w < min_size * N * 2 and h < min_size * N * 2) or (max(w, h) < N * 0.3 and r.random() < 0.3):
            out.append((x0, y0, x1, y1)); continue
        if (w > h and r.random() < 0.8) or (w == h and r.random() < 0.5):
            cut = x0 + int(w * r.choice([0.25, 0.5, 0.5, 0.75]))
            rects += [(x0, y0, cut, y1, d + 1), (cut, y0, x1, y1, d + 1)]
        else:
            cut = y0 + int(h * r.choice([0.25, 0.5, 0.5, 0.75]))
            rects += [(x0, y0, x1, cut, d + 1), (x0, cut, x1, y1, d + 1)]
    for (x0, y0, x1, y1) in out:
        pid[y0:y1, x0:x1] = nid; nid += 1
        t = 2
        seam[y0:y0 + t, x0:x1] = 1; seam[y1 - t:y1, x0:x1] = 1
        seam[y0:y1, x0:x0 + t] = 1; seam[y0:y1, x1 - t:x1] = 1
    return pid, seam, out


def rivets(rects, spacing=18, rad=2.2):
    m = np.zeros((N, N), np.float32)
    for (x0, y0, x1, y1) in rects:
        if (x1 - x0) < 60 or (y1 - y0) < 60:
            continue
        for x in list(range(x0 + 7, x1 - 6, spacing)):
            for y in (y0 + 7, y1 - 8):
                m[max(y - 2, 0):y + 3, max(x - 2, 0):x + 3] = 1
        for y in list(range(y0 + 7, y1 - 6, spacing)):
            for x in (x0 + 7, x1 - 8):
                m[max(y - 2, 0):y + 3, max(x - 2, 0):x + 3] = 1
    return blur(m, 1)


def plating(name, base, tone, dark_frac, metal, rough, stripe_col, seed):
    pid, seam, rects = panels(seed)
    pr = np.random.default_rng(seed + 1).random(pid.max() + 1).astype(np.float32)
    plate = pr[pid]
    grime = blur(fbm(seed + 10, 4, 4), 6)
    # weathering: thin vertical streaks running down from seams
    cols = np.random.default_rng(seed + 20).random(N).astype(np.float32)
    streak = (cols > 0.93)[None, :] * np.ones((N, 1), np.float32) * value_noise(8, seed + 30)
    streak = blur(streak, 1)
    alb = np.ones((N, N, 3), np.float32) * np.array(base, np.float32)
    alb *= (1 - tone / 2 + tone * plate)[..., None]
    dark = (plate > 1 - dark_frac)[..., None]
    alb = np.where(dark, alb * 0.55, alb)
    # hazard stripes and colour on a few plates
    st = (pr[pid] < 0.03)[..., None]
    diag = (((xx + yy) * 40) % 1.0 < 0.5)[..., None]
    alb = np.where(st, np.where(diag, np.array(stripe_col), alb * 0.3), alb)
    alb *= (1 - 0.35 * seam)[..., None]
    alb *= (0.96 + 0.08 * grime)[..., None]
    alb *= (1 - 0.25 * streak)[..., None]
    riv = rivets(rects)
    height = -seam * 1.0 + riv * 0.6 + (plate > 0.85) * 0.25 + grime * 0.05
    height = blur(height, 1)
    nrm = normal_from_height(height, 6.0)
    ao = 1 - 0.45 * blur(seam, 2)
    ro = np.clip(rough + 0.25 * (grime - 0.5) + 0.15 * plate - 0.1 * riv, 0.05, 1)
    me = np.where(plate > 1 - dark_frac, metal * 0.8, metal) * np.ones((N, N), np.float32)
    save(name + "_albedo", alb)
    save(name + "_orm", np.stack([ao, ro, me], axis=2))
    save(name + "_normal", nrm)


def grating(name, seed):
    # deck plates with a diamond tread, framed by a darker border and yellow edge markings
    g = 32.0
    u = (xx * g) % 1.0; v = (yy * g) % 1.0
    du = ((xx + yy) * g * 2) % 1.0; dv = ((xx - yy) * g * 2) % 1.0
    tread = np.exp(-((du - 0.5) ** 2 + (dv - 0.5) ** 2) * 60)
    frame = ((xx * 4) % 1.0 < 0.015) | ((yy * 4) % 1.0 < 0.015)
    wear = fbm(seed, 4, 6)
    alb = np.ones((N, N, 3), np.float32) * 0.24
    alb *= (0.8 + 0.4 * wear)[..., None]
    alb += tread[..., None] * 0.08
    edge = ((yy % 1.0) > 0.93) & (((xx * 16) % 1.0) < 0.5)
    alb = np.where(edge[..., None], np.array([0.55, 0.42, 0.05]), alb)
    alb = np.where(frame[..., None], alb * 0.5, alb)
    height = tread * 0.5 - frame * 1.0
    save(name + "_albedo", alb)
    save(name + "_orm", np.stack([np.where(frame, 0.6, 1.0), np.clip(0.55 - tread * 0.25 + 0.2 * (wear - 0.5), 0.1, 1), np.full((N, N), 0.85)], axis=2))
    save(name + "_normal", normal_from_height(blur(height, 1), 4.0))


def interior(name, base, seed, vents=True):
    # big clean panels in a regular grid, with occasional vents and service hatches
    gx = (xx * 4) % 1.0; gy = (yy * 2) % 1.0
    seam = ((gx < 0.006) | (gx > 0.994) | (gy < 0.006) | (gy > 0.994)).astype(np.float32)
    cell = (np.floor(xx * 4) + np.floor(yy * 2) * 4).astype(int)
    cr = np.random.default_rng(seed).random(8)[cell]
    vent = np.zeros((N, N), np.float32)
    if vents:
        inv = (cr > 0.75) & (gx > 0.3) & (gx < 0.7) & (gy > 0.6) & (gy < 0.8)
        vent = (inv & (((yy * 160) % 1.0) < 0.5)).astype(np.float32)
    alb = np.ones((N, N, 3), np.float32) * np.array(base, np.float32)
    alb *= (0.94 + 0.08 * cr)[..., None]
    alb *= (1 - 0.5 * seam - 0.4 * vent)[..., None]
    height = -seam - vent * 0.6
    save(name + "_albedo", alb)
    save(name + "_orm", np.stack([1 - 0.4 * blur(seam, 2), np.full((N, N), 0.45) + 0.1 * cr, np.full((N, N), 0.05)], axis=2))
    save(name + "_normal", normal_from_height(blur(height, 1), 5.0))


plating("hull", (0.48, 0.49, 0.52), 0.08, 0.05, 0.55, 0.42, (0.75, 0.5, 0.08), 1)
plating("hulldark", (0.3, 0.31, 0.34), 0.1, 0.08, 0.8, 0.32, (0.6, 0.25, 0.1), 7)
grating("floor", 3)
interior("wall", (0.82, 0.83, 0.84), 5)
interior("ceiling", (0.58, 0.6, 0.63), 9)
result = {"textures": sorted(os.listdir(OUT))}


def hazard(name, seed):
    """Worn yellow and black diagonal stripes for edges, clamps and blast doors."""
    diag = (((xx + yy) * 16) % 1.0 < 0.5)
    wear = blur(fbm(seed, 4, 6), 2)
    chip = (fbm(seed + 3, 8, 4) > 0.72)
    alb = np.where(diag[..., None], np.array([0.85, 0.62, 0.06]), np.array([0.05, 0.05, 0.05]))
    alb = np.where(chip[..., None], np.array([0.42, 0.43, 0.45]), alb)
    alb *= (0.85 + 0.2 * wear)[..., None]
    height = chip * -0.4 + wear * 0.05
    save(name + "_albedo", alb)
    save(name + "_orm", np.stack([np.ones((N, N)), np.clip(0.55 + 0.3 * chip - 0.1 * wear, 0.1, 1), np.where(chip, 0.8, 0.1)], axis=2))
    save(name + "_normal", normal_from_height(blur(height, 1), 4.0))

# the overhaul's extra plating: white-painted panels, a signal-red livery, and hazard stripes
plating("hulllight", (0.8, 0.81, 0.8), 0.06, 0.04, 0.15, 0.5, (0.7, 0.12, 0.08), 21)
plating("accent", (0.62, 0.1, 0.07), 0.08, 0.06, 0.2, 0.45, (0.85, 0.75, 0.6), 33)
hazard("hazard", 41)
result = {"textures": sorted(os.listdir(OUT))}
