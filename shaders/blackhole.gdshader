// Sagittarius A*: rays are bent around the hole (Schwarzschild photon paths integrated per pixel),
// so the far side of the accretion disk is lensed up over the shadow, and the near side Doppler-brightens.
shader_type spatial;
render_mode unshaded, blend_premul_alpha, depth_draw_never, depth_test_disabled, cull_front, shadows_disabled, fog_disabled;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }

uniform float rs = 1.0;          // Schwarzschild radius, world units
uniform float bound = 30.0;      // bounding sphere radius, world units
uniform float disk_in = 3.0;     // in units of rs
uniform float disk_out = 14.0;
uniform float time = 0.0;
uniform float tap = 0.0;         // the core is harnessed: a bright ring of collectors
uniform float fade = 1.0;
varying vec3 v_wpos;

float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
// gradient noise with quintic fade and rotated octaves: no axis-aligned creases
vec2 g2(vec2 i) { float a = h21(i) * 6.2831853; return vec2(cos(a), sin(a)); }
float n21(vec2 p) { vec2 i = floor(p), f = fract(p); vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
	float a = dot(g2(i), f), b = dot(g2(i + vec2(1, 0)), f - vec2(1, 0)), c = dot(g2(i + vec2(0, 1)), f - vec2(0, 1)), d = dot(g2(i + vec2(1, 1)), f - vec2(1, 1));
	return 0.5 + 0.7 * mix(mix(a, b, u.x), mix(c, d, u.x), u.y); }
float fbm2(vec2 p) { float v = 0.0, a = 0.5; mat2 rot = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
	for (int i = 0; i < 4; i++) { v += a * n21(p); p = rot * p * 2.03 + vec2(1.7, 9.2); a *= 0.5; } return v; }

void vertex() { v_wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }

vec3 diskColor(float r) {
	// hotter inside: blue-white at the inner edge, through gold, to deep red at the rim
	float t = clamp((r - disk_in) / (disk_out - disk_in), 0.0, 1.0);
	vec3 a = vec3(0.8, 0.88, 1.0), b = vec3(1.0, 0.62, 0.22), c = vec3(0.75, 0.16, 0.04);
	return t < 0.35 ? mix(a, b, t / 0.35) : mix(b, c, (t - 0.35) / 0.65);
}

void fragment() {
	vec3 C = NODE_POSITION_WORLD;
	mat3 basis = mat3(normalize(MODEL_MATRIX[0].xyz), normalize(MODEL_MATRIX[1].xyz), normalize(MODEL_MATRIX[2].xyz));
	mat3 inv = transpose(basis);
	// work in units of rs, in the hole's own frame (disk in the local xz plane)
	vec3 ro = inv * (CAMERA_POSITION_WORLD - C) / rs;
	// ray direction from the pixel itself: interpolated world positions this far from the origin lose precision
	vec4 vp = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, 0.5, 1.0);
	vec3 dirView = normalize(vp.xyz / vp.w);
	vec3 rd = normalize(inv * (mat3(INV_VIEW_MATRIX) * dirView));
	float R = bound / rs;
	float b = dot(ro, rd), c = dot(ro, ro) - R * R, disc = b * b - c;
	if (disc < 0.0) discard;
	float t0 = max(-b - sqrt(disc), 0.0);
	vec3 p = ro + rd * t0;
	vec3 v = rd;
	vec3 col = vec3(0.0);
	float T = 1.0;
	for (int i = 0; i < 320; i++) {
		float r = length(p);
		if (r < 1.0) { T = 0.0; break; }
		if (r > R && dot(p, v) > 0.0) break;
		// small steps near the hole and near the disk plane, so crossings land exactly
		float dt = clamp(min(0.08 * r, max(abs(p.y) * 0.6, 0.04)), 0.02, 2.5);
		vec3 h = cross(p, v);
		float h2 = dot(h, h);
		vec3 acc = -1.5 * h2 * p / pow(r, 5.0);
		vec3 pn = p + v * dt;
		v = normalize(v + acc * dt);
		if (p.y * pn.y < 0.0) {
			vec3 q = mix(p, pn, p.y / (p.y - pn.y));
			float rq = length(q.xz);
			if (rq > disk_in && rq < disk_out) {
				float ang = atan(q.z, q.x);
				float omega = 0.6 * pow(rq / disk_in, -1.5);
				float swirl = fbm2(vec2(ang * 3.0 + time * omega * 2.0 + log(rq) * 6.0, rq * 0.8));
				float streak = fbm2(vec2(ang * 18.0 + time * omega * 6.0, rq * 4.0));
				float dens = (0.35 + 0.9 * swirl * swirl + 0.35 * streak) * sstep(disk_in, disk_in * 1.25, rq) * (1.0 - sstep(disk_out * 0.7, disk_out, rq));
				// Doppler beaming: the side turning toward us is far brighter
				vec3 vel = normalize(vec3(-q.z, 0.0, q.x));
				float beta = clamp(sqrt(0.5 / rq), 0.0, 0.7);
				float g = 1.0 / (1.0 - beta * dot(vel, -v));
				float grav = sqrt(max(1.0 - 1.0 / rq, 0.0));
				float boost = pow(g * grav, 3.0);
				vec3 emi = diskColor(rq) * dens * boost * 3.6 * pow(disk_in / rq, 1.25);
				emi = mix(emi, emi * vec3(0.7, 0.85, 1.3), clamp(g - 1.0, 0.0, 1.0));
				float a = clamp(dens * 0.85, 0.0, 0.95);
				col += T * emi * a;
				T *= 1.0 - a;
				// the harnessed core: a thin ring of collectors at the disk's outer edge
				if (tap > 0.0 && rq > disk_out * 0.9) col += T * vec3(0.6, 0.9, 1.0) * tap * step(0.6, fract(ang * 40.0 / 6.2831)) * 2.0;
			}
		}
		p = pn;
		if (T < 0.01) break;
	}
	// photon ring glow just outside the shadow
	ALBEDO = col * fade;
	ALPHA = (1.0 - T) * fade;
}
