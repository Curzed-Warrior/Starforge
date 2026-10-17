// Planet and moon surfaces. Custom lighting: the star sits at the world origin.
shader_type spatial;
render_mode unshaded, cull_back, shadows_disabled, fog_disabled;
#include "res://shaders/noise.gdshaderinc"

uniform int mode = 0;          // 0 textured, 1 rocky, 2 gas, 3 ocean, 4 lava, 5 ice, 6 terran
uniform sampler2D day_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D night_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D cloud_tex : filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D spec_tex : filter_linear_mipmap, repeat_enable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D ring_tex : source_color, filter_linear_mipmap, repeat_disable;
uniform bool has_night = false;
uniform bool has_clouds = false;
uniform bool has_spec = false;
uniform bool has_normal = false;
uniform bool has_ring = false;
uniform bool ring_textured = false;
uniform float cloud_off = 0.0;
uniform vec3 col_a : source_color = vec3(0.3);
uniform vec3 col_b : source_color = vec3(0.6);
uniform float seed = 0.0;
uniform vec3 sun_color = vec3(1.0);
uniform float light = 1.0;
uniform float city = 0.0;           // night lights 0..1 (grows with the civilisation)
uniform vec3 city_col = vec3(1.0, 0.7, 0.38);
uniform float mined = 0.0;          // 0..1 disassembled
uniform float terraform = 0.0;      // 0..1 a dead world brought to life
uniform float ignite = 0.0;         // 0..1 a gas giant burning as a small star
uniform float dead = 0.0;           // biosphere dying 0..1
uniform float ring_in = 0.0;
uniform float ring_out = 0.0;
uniform vec3 ring_normal = vec3(0, 1, 0);
uniform vec3 atmo_tint = vec3(0.0);
uniform float time = 0.0;
// settlements painted by the close-up view: xyz = direction (object space), w = radius (chord); w < 0 marks a mine
uniform vec4 sites[48];
uniform int site_n = 0;
uniform float garden = 0.0;      // parks between the blocks (worlds you can breathe on)

varying vec3 v_obj;
uniform vec3 star_pos = vec3(0.0);
varying vec3 v_wpos;
varying vec3 v_wn;
varying vec3 v_wt;
varying vec3 v_wb;
varying vec3 v_center;

void vertex() {
	v_obj = VERTEX;
	v_center = MODEL_MATRIX[3].xyz;
	v_wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_wn = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	v_wt = normalize((MODEL_MATRIX * vec4(TANGENT, 0.0)).xyz);
	v_wb = normalize((MODEL_MATRIX * vec4(BINORMAL, 0.0)).xyz);
}

// ---- procedural worlds ----
float terrain(vec3 p) {
	float c = fbm(p * 1.7 + seed);
	float m = ridged(p * 3.6 + seed * 1.7);
	float d = fbm3(p * 15.0 + seed * 3.1);
	return c + 0.32 * m * sstep(0.42, 0.62, c) + 0.06 * d;
}
float desert_h(vec3 p) {
	float base = fbm(p * 2.2 + seed);
	vec3 dir = normalize(vec3(0.8, 0.15, 0.55));
	float dunes = sin(dot(p, dir) * 140.0 + fbm3(p * 6.0 + seed) * 9.0) * 0.5 + 0.5;
	float canyon = 1.0 - sstep(0.0, 0.035, abs(fbm(p * 3.0 + seed * 2.3) - 0.52));
	return base + 0.012 * dunes * sstep(0.45, 0.6, base) - 0.09 * canyon + 0.05 * ridged(p * 5.0 + seed);
}
float world_h(vec3 p) {
	if (mode == 7) return desert_h(p);
	if (mode == 1 || mode == 5) return fbm(p * 2.4 + seed) + craters(p + seed, 3.0) * 0.09;
	return terrain(p);
}
vec3 bump_normal(vec3 p, float h, float k) {
	vec3 t = normalize(cross(abs(p.y) > 0.99 ? vec3(1.0, 0.0, 0.0) : vec3(0.0, 1.0, 0.0), p));
	vec3 b = cross(p, t);
	float e = 0.0025;
	float hx = world_h(normalize(p + t * e)) - h;
	float hz = world_h(normalize(p + b * e)) - h;
	return normalize(p - (t * hx + b * hz) * (k / e));
}

float ring_shadow(vec3 pos, vec3 L) {
	if (!has_ring) return 1.0;
	vec3 c = v_center;
	float den = dot(L, ring_normal);
	if (abs(den) < 1e-4) return 1.0;
	float t = dot(c - pos, ring_normal) / den;
	if (t <= 0.0) return 1.0;
	float d = length(pos + L * t - c);
	float u = (d - ring_in) / (ring_out - ring_in);
	if (u < 0.0 || u > 1.0) return 1.0;
	float a = ring_textured ? texture(ring_tex, vec2(u, 0.5)).a : 0.5 + 0.3 * sin(u * 40.0);
	return 1.0 - a * 0.85;
}

void fragment() {
	vec3 Ng = normalize(v_wn);
	vec3 L = normalize(star_pos - v_wpos);
	vec3 V = normalize(CAMERA_POSITION_WORLD - v_wpos);
	vec3 p = normalize(v_obj);
	vec3 N = Ng;
	vec3 albedo;
	vec3 emi = vec3(0.0);
	float spec_mask = 0.0;
	float cloud = 0.0;

	if (mode == 0) {
		albedo = texture(day_tex, UV).rgb;
		if (has_normal) {
			vec3 nm = texture(normal_tex, UV).xyz * 2.0 - 1.0;
			N = normalize(v_wt * nm.x * 1.5 - v_wb * nm.y * 1.5 + Ng * nm.z);
		}
		if (has_spec) spec_mask = texture(spec_tex, UV).r;
		if (has_clouds) cloud = texture(cloud_tex, UV + vec2(cloud_off, 0.0)).r;
		if (terraform > 0.0) {
			// seas fill the lowlands first (darker albedo ~ lower ground), then green spreads up from the shores
			float low = 1.0 - dot(albedo, vec3(0.45, 0.4, 0.15)) * 2.2 + (fbm3(p * 6.0 + 3.0) - 0.5) * 0.3;
			float sea = sstep(1.0 - terraform * 0.55, 1.0 - terraform * 0.55 + 0.04, low);
			float green = sstep(0.25, 0.7, terraform) * sstep(0.35, 0.7, low + fbm3(p * 9.0) * 0.3) * (1.0 - sea);
			albedo = mix(albedo, albedo * vec3(0.45, 0.7, 0.35) + vec3(0.02, 0.06, 0.01), green * 0.8);
			albedo = mix(albedo, vec3(0.03, 0.08, 0.16), sea);
			spec_mask = max(spec_mask, sea);
			float tcl = sstep(0.55, 0.8, fbm(p * 3.5 + vec3(time * 0.01, 0.0, 0.0) + 7.0)) * sstep(0.2, 0.6, terraform);
			cloud = max(cloud, tcl);
			albedo = mix(albedo, vec3(0.9), tcl * 0.8);
		}
	} else if (mode == 2) {
		// gas giant: zonal bands sheared by turbulence stretched along longitude, oval storms, polar haze
		bool icy = col_a.b > col_a.r;
		float bandF = icy ? 4.0 + fract(seed * 0.73) * 4.0 : 9.0 + fract(seed * 0.73) * 9.0;
		float lat = p.y;
		float w1 = fbm(p * 2.5 + seed);
		float w2 = fbm(vec3(p.x * 5.0, p.y * 22.0, p.z * 5.0) + w1 * 2.2 + seed * 1.3);
		float w3 = fbm3(vec3(p.x * 14.0, p.y * 60.0, p.z * 14.0) + w2 * 1.5 + seed);
		float latw = lat + (w2 - 0.5) * (icy ? 0.03 : 0.07) + (w3 - 0.5) * 0.015;
		float belts = sin(latw * bandF * 3.14159) * 0.5 + 0.5;
		float fine = fbm3(vec3(latw * bandF * 3.0, seed, 0.3));
		float t = clamp(mix(belts, fine, 0.45) + (w3 - 0.5) * 0.25, 0.0, 1.0);
		vec3 mid = mix(col_a, col_b, 0.55) * vec3(1.05, 1.0, 0.95);
		albedo = t < 0.5 ? mix(col_a * 0.8, mid, t * 2.0) : mix(mid, col_b * 1.05, (t - 0.5) * 2.0);
		// oval storms riding the bands
		float lon = atan(p.z, p.x);
		vec2 sc = cells(vec3(lon * 3.0, lat * 9.0, seed));
		float storm = step(0.86, fract(sc.y * 5.3)) * sstep(0.32, 0.05, sc.x);
		vec3 stormCol = icy ? vec3(0.08, 0.12, 0.3) : mix(vec3(0.75, 0.38, 0.22), vec3(0.95, 0.92, 0.85), fract(sc.y * 17.0));
		albedo = mix(albedo, stormCol, storm * 0.75);
		albedo *= mix(1.0, icy ? 0.85 : 0.72, sstep(0.72, 0.96, abs(lat)));
		if (icy) albedo = mix(albedo, col_b, 0.35);
	} else {
		mat3 mrot = mat3(normalize(MODEL_MATRIX[0].xyz), normalize(MODEL_MATRIX[1].xyz), normalize(MODEL_MATRIX[2].xyz));
		vec3 Lo = normalize(transpose(mrot) * L);
		float h = world_h(p);
		float detail = fbm3(p * 30.0 + seed * 2.0);
		float bumpk = mode == 7 ? 1.0 : mode == 1 ? 0.5 : mode == 5 ? 0.4 : 0.3;
		vec3 No = bump_normal(p, h, bumpk);
		N = normalize(mrot * No);
		if (mode == 1) {
			// airless rock: highlands and dark maria, craters
			float maria = sstep(0.42, 0.36, fbm(p * 1.3 + seed * 0.7));
			albedo = mix(col_b, col_a, maria * 0.8) * (0.8 + 0.35 * detail);
			albedo = mix(albedo, col_b * 1.15, sstep(0.0, 0.08, h - 0.62) * 0.4);
		} else if (mode == 5) {
			// ice shell scored by reddish fractures
			float lines = 1.0 - sstep(0.0, 0.025, abs(fbm(p * 3.0 + seed) - 0.5));
			float lines2 = 1.0 - sstep(0.0, 0.015, abs(fbm(p * 7.0 + seed * 2.0) - 0.5));
			albedo = mix(vec3(0.82, 0.88, 0.93), col_b, 0.3) * (0.92 + 0.12 * detail);
			albedo = mix(albedo, vec3(0.55, 0.32, 0.2), max(lines, lines2 * 0.6) * 0.75);
		} else if (mode == 4) {
			albedo = mix(col_a, col_b, sstep(0.3, 0.9, h)) * (0.7 + 0.4 * detail);
			float crack = 1.0 - sstep(0.0, 0.012, abs(fbm(p * 5.0 + seed * 1.3) - 0.5));
			float pool = sstep(0.36, 0.33, h);
			emi += vec3(1.0, 0.3, 0.05) * (crack * 1.6 + pool * 2.2) * (0.8 + 0.2 * sin(time * 2.0 + h * 20.0));
		} else if (mode == 7) {
			// desert: dune seas, dark canyon floors, frost at the poles
			float tone = fbm(p * 4.0 + seed * 1.9);
			albedo = mix(col_a, col_b, sstep(0.3, 0.75, tone + (h - 0.5) * 0.8)) * (0.85 + 0.25 * detail);
			albedo = mix(albedo, col_a * 0.55, sstep(0.44, 0.38, h));
			float frost = sstep(0.86, 0.92, abs(p.y) + 0.05 * detail);
			albedo = mix(albedo, vec3(0.9, 0.88, 0.85), frost);
		} else {
			// living or water worlds: oceans, coasts, biomes by temperature and moisture, clouds
			float sea = mode == 3 ? 0.7 : 0.58;
			float lat = abs(p.y);
			float moist = fbm(p * 2.3 + seed * 5.0);
			float temp = 1.0 - lat * 1.1 - max(h - sea, 0.0) * 1.3 + (fbm3(p * 4.0 + seed) - 0.5) * 0.25;
			bool alienVeg = fract(seed * 0.371) > 0.55;
			vec3 veg = alienVeg ? mix(vec3(0.28, 0.1, 0.22), col_b * vec3(0.8, 0.4, 0.6), 0.4) : vec3(0.1, 0.22, 0.07);
			vec3 grass = alienVeg ? vec3(0.42, 0.24, 0.2) : vec3(0.3, 0.36, 0.14);
			vec3 sand = vec3(0.7, 0.62, 0.45);
			vec3 desert = vec3(0.62, 0.48, 0.3);
			vec3 rock = vec3(0.38, 0.34, 0.3);
			vec3 land = mix(desert, grass, sstep(0.35, 0.5, moist));
			land = mix(land, veg, sstep(0.5, 0.65, moist) * sstep(0.25, 0.5, temp));
			land = mix(land, rock, sstep(sea + 0.22, sea + 0.34, h));
			land = mix(land, vec3(0.92, 0.94, 0.97), sstep(0.18, 0.08, temp));
			land = mix(land, sand, sstep(sea + 0.02, sea + 0.005, h));
			land *= 0.85 + 0.25 * detail;
			float depth = sstep(sea, sea - 0.18, h);
			vec3 ocean = mix(col_a * 1.3 + vec3(0.0, 0.08, 0.06), col_a * 0.55, depth);
			float isLand = sstep(sea, sea + 0.004, h);
			ocean = mix(ocean, vec3(0.16, 0.12, 0.08), dead);
			land = mix(land, vec3(0.3, 0.25, 0.18), dead);
			albedo = mix(ocean, land, isLand);
			float ice = sstep(0.83, 0.9, lat + 0.05 * detail - (isLand > 0.5 ? 0.05 : 0.0));
			albedo = mix(albedo, vec3(0.9, 0.94, 0.98), ice);
			spec_mask = (1.0 - isLand) * (1.0 - ice);
			N = normalize(mix(Ng, N, isLand));
			// weather: banded by latitude, swirled, drifting; shadows cast along the light
			vec3 q = p + vec3(time * 0.004, 0.0, 0.0);
			float sw = fbm3(q * 2.0 + seed);
			float cl = fbm(q * 3.2 + vec3(sw * 1.6) + seed * 2.0);
			cl += 0.12 * cos(lat * 16.0);
			cloud = sstep(0.52, 0.78, cl) * (1.0 - 0.8 * dead);
			float cls = sstep(0.52, 0.78, fbm(normalize(q + Lo * 0.012) * 3.2 + vec3(fbm3(normalize(q + Lo * 0.012) * 2.0 + seed) * 1.6) + seed * 2.0) + 0.12 * cos(lat * 16.0));
			albedo *= 1.0 - cls * 0.45 * (1.0 - cloud);
			albedo = mix(albedo, vec3(0.92), cloud * 0.85);
		}
	}

	// seeded life on procedural worlds: seas fill the low ground, green spreads up from the shores, clouds form
	if (terraform > 0.0 && mode != 0 && mode != 2) {
		float low = 1.0 - fbm(p * 2.2 + seed) * 1.6 + (fbm3(p * 6.0 + 3.0) - 0.5) * 0.3;
		float sea = sstep(1.0 - terraform * 0.55, 1.0 - terraform * 0.55 + 0.04, low);
		float green = sstep(0.25, 0.7, terraform) * sstep(0.3, 0.75, low + fbm3(p * 9.0) * 0.4) * (1.0 - sea);
		albedo = mix(albedo, vec3(0.12, 0.26, 0.08) + albedo * 0.2, green * 0.85);
		albedo = mix(albedo, vec3(0.03, 0.08, 0.16), sea);
		spec_mask = max(spec_mask, sea);
		float tcl = sstep(0.55, 0.8, fbm(p * 3.5 + vec3(time * 0.01, 0.0, 0.0) + 7.0)) * sstep(0.2, 0.6, terraform);
		cloud = max(cloud, tcl);
		albedo = mix(albedo, vec3(0.9), tcl * 0.8);
	}

	// disassembly: pits eat in from noise thresholds, glowing edges and lit works
	if (mined > 0.0005) {
		float f = fbm(p * 1.6 + seed + 4.0) + 0.12 * fbm(p * 7.0);
		float th = mined * 1.05;
		float inside = step(f, th);
		float edge = sstep(0.03, 0.0, abs(f - th));
		vec3 pit = vec3(0.05, 0.045, 0.04) * (0.6 + 0.4 * fract(p.y * 60.0 + f * 30.0));
		albedo = mix(albedo, pit, inside);
		float works = step(0.975, hash13(floor(p * 90.0))) * inside;
		emi += vec3(1.0, 0.45, 0.12) * (edge * 1.4 + works * 1.2);
		spec_mask *= 1.0 - inside;
		cloud *= 1.0 - inside;
	}

	// ---- settlements: street grids, dark city blocks, terraced mine pits ----
	float urb = 0.0, mine = 0.0, mrad = 1.0, mdist = 0.0;
	vec3 sdir = vec3(0.0, 1.0, 0.0);
	for (int i = 0; i < site_n; i++) {
		vec4 st = sites[i];
		float r = abs(st.w);
		float d = length(p - st.xyz);
		if (d > r * 1.5) continue;
		float f = sstep(r, r * 0.35, d + (fbm3(p * 60.0 + float(i)) - 0.5) * r * 0.6);
		if (st.w > 0.0) { if (f > urb) { urb = f; sdir = st.xyz; } }
		else if (f > mine) { mine = f; mrad = r; mdist = d; }
	}
	urb *= 1.0 - spec_mask;     // no streets on the sea
	float urb_lights = 0.0;
	if (urb > 0.0) {
		// each city lays out its own grid in its own tangent plane, bent a little by the land
		vec3 ea = normalize(cross(abs(sdir.y) > 0.9 ? vec3(1.0, 0.0, 0.0) : vec3(0.0, 1.0, 0.0), sdir));
		vec3 eb = cross(sdir, ea);
		float ang = hash13(floor(sdir * 97.0)) * 6.2831;
		vec2 uv = vec2(dot(p, ea), dot(p, eb));
		uv = vec2(uv.x * cos(ang) - uv.y * sin(ang), uv.x * sin(ang) + uv.y * cos(ang));
		uv += (vec2(fbm3(p * 25.0 + seed), fbm3(p * 25.0 + seed + 7.0)) - 0.5) * 0.006;
		vec2 q1 = uv * 320.0, q2 = uv * 2400.0;
		float fw1 = length(fwidth(q1)), fw2 = length(fwidth(q2));
		vec2 g1 = abs(fract(q1) - 0.5), g2 = abs(fract(q2) - 0.5);
		float av = mix(step(0.465, max(g1.x, g1.y)), 0.13, sstep(0.25, 0.7, fw1));
		float stt = mix(step(0.42, max(g2.x, g2.y)), 0.3, sstep(0.25, 0.7, fw2));
		float blk = hash13(vec3(floor(q2), 1.0));
		vec3 roof = mix(vec3(0.2, 0.2, 0.22), vec3(0.46, 0.46, 0.48), blk);
		roof = mix(roof, vec3(0.14, 0.26, 0.1), garden * step(0.75, hash13(vec3(floor(q2 * 0.25), 3.0))));
		vec3 fabric = mix(roof, vec3(0.58, 0.56, 0.52), max(av, stt * 0.5));
		fabric = mix(fabric * albedo * 2.2, fabric, 0.4 + 0.6 * garden);   // dust of the land settles on every roof
		albedo = mix(albedo, fabric, urb * 0.7);
		cloud *= 1.0 - urb * 0.3;
		// lit avenues come and go in segments; blocks glitter
		float seg = step(0.35, hash13(vec3(floor(q1 * vec2(1.0, 4.0)), 5.0))) * step(0.35, hash13(vec3(floor(q1 * vec2(4.0, 1.0)), 6.0)));
		float glit = step(0.8, hash13(vec3(floor(q2 * 3.0), 2.0)));
		urb_lights = urb * (av * 0.7 * seg + stt * 0.25 * glit + step(0.95, blk) * 0.4);
	}
	if (mine > 0.0) {
		// terraces stepping down into the pit, and the work lights of the machines
		float u = mdist / mrad + (fbm3(p * 30.0) - 0.5) * 0.25;
		float terr = step(0.5, fract(u * 5.0));
		vec3 rock = mix(vec3(0.16, 0.13, 0.11), vec3(0.36, 0.31, 0.26), terr) * (0.5 + 0.5 * clamp(u, 0.0, 1.0));
		albedo = mix(albedo, rock, mine * 0.9);
		emi += vec3(1.0, 0.5, 0.15) * step(0.985, hash13(floor(p * 700.0))) * mine * 1.5;
		cloud *= 1.0 - mine;
	}

	float ndl = dot(N, L);
	float ndlg = dot(Ng, L);
	float lit = max(ndl, 0.0) * sstep(-0.05, 0.1, ndlg);
	if (mode == 2 || (mode == 0 && has_ring)) lit = sstep(-0.12, 0.6, ndlg) * 0.9 + 0.1 * max(ndl, 0.0);
	float shadow = ring_shadow(v_wpos, L);
	vec3 sun = sun_color * light * shadow;
	vec3 col = albedo * sun * lit * (1.0 - cloud * 0.35);
	// thin warm light at the terminator from the atmosphere
	col += albedo * atmo_tint * sun * sstep(0.2, 0.0, abs(ndlg)) * 0.15;
	// ocean glint
	if (spec_mask > 0.01) {
		vec3 H = normalize(L + V);
		float s = pow(max(dot(Ng, H), 0.0), 90.0) * 1.6 + pow(max(dot(Ng, H), 0.0), 10.0) * 0.05;
		float fres = 0.3 + 0.7 * pow(1.0 - max(dot(Ng, V), 0.0), 5.0);
		col += sun * s * fres * spec_mask * sstep(0.0, 0.1, ndlg) * (1.0 - cloud);
	}
	// night side
	float night = sstep(0.08, -0.12, ndlg);
	col += city_col * urb_lights * night * 1.2 * (1.0 - cloud * 0.6);
	if (has_night) {
		vec3 nl = texture(night_tex, UV).rgb;
		nl = pow(nl, vec3(1.4)) * 2.2;
		col += nl * night * city * (1.0 - cloud * 0.7);
	} else if (city > 0.0) {
		// metropolitan clusters around scattered centres, small towns between, resolved into fine lights
		vec2 c1 = cells(p * 3.2 + seed * 1.7);
		float big = fract(c1.y * 91.7);
		float on1 = step(1.0 - min(city, 1.0) * 0.85, fract(c1.y * 7.31));
		float metro = sstep(0.1 + 0.22 * big * min(city, 1.5), 0.0, c1.x) * on1;
		vec2 c2 = cells(p * 19.0 + seed);
		float towns = sstep(0.12, 0.0, c2.x) * step(1.0 - min(city, 1.0) * 0.4, fract(c2.y * 5.7)) * 0.5;
		float density = max(metro, towns);
		vec3 q = p * 900.0;
		float fw = length(fwidth(q));
		float grain = step(1.0 - density * 0.8, hash13(floor(q)));
		float lights = mix(grain * density, density * density * 0.3, sstep(0.4, 1.5, fw));
		col += city_col * lights * night * 1.8 * (1.0 - cloud * 0.6);
	}
	// aurorae: curtains of green and violet over the night-side poles
	if (has_night) {
		vec3 po = normalize(v_obj);
		float lat = abs(po.y);
		float lon = atan(po.z, po.x);
		float band = sstep(0.022, 0.0, abs(lat - 0.9 - 0.02 * sin(lon * 3.0 + TIME * 0.05)));
		float curtain = pow(fbm3(vec3(lon * 40.0, lat * 12.0, TIME * 0.08)), 3.0) * 3.0;
		float rim = pow(clamp(1.0 - max(dot(Ng, V), 0.0), 0.0, 1.0), 1.5);
		vec3 aur = mix(vec3(0.15, 1.0, 0.45), vec3(0.6, 0.3, 1.0), sstep(0.5, 0.95, curtain * 0.5));
		col += aur * band * curtain * night * (0.3 + 1.2 * rim) * 0.3;
	}
	col += emi;
	// ignition: the bands become rivers of fire, the whole disk self-lit like a small star
	if (ignite > 0.0) {
		float turb = fbm(p * 3.0 + vec3(time * 0.02, 0.0, time * 0.015)) * 0.6 + fbm3(p * 11.0 + time * 0.03) * 0.4;
		vec3 fire = mix(vec3(1.0, 0.32, 0.06), vec3(1.0, 0.85, 0.55), sstep(0.35, 0.75, turb));
		float limb = pow(clamp(dot(Ng, V), 0.0, 1.0), 0.4);
		// the giant's own belts become lanes of fire: dark between, white-hot along the storms
		float band = sstep(0.3, 0.75, dot(albedo, vec3(0.45, 0.4, 0.15)) * 1.4);
		float cells = sstep(0.45, 0.8, fbm3(p * 22.0 + time * 0.05));
		vec3 hot = mix(vec3(0.55, 0.08, 0.01), vec3(1.0, 0.45, 0.08), band);
		hot = mix(hot, vec3(1.0, 0.85, 0.5), cells * band);
		col = mix(col, hot * hot * (0.3 + 0.7 * turb) * (0.3 + 0.7 * limb) * 1.1, ignite);
		cloud = 0.0;
	}
	col += albedo * 0.004;  // starlight
	ALBEDO = col;
}
