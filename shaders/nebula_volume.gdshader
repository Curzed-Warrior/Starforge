// A volumetric emission nebula: ray-marched density with hydrogen-alpha, oxygen-III and dark dust.
// kind 0: Orion-like (bright core, ionised cavity, glowing walls); 1: supernova remnant (filaments, hollow shell); 2: pillars.
shader_type spatial;
render_mode unshaded, blend_premul_alpha, depth_draw_never, depth_test_disabled, cull_front, shadows_disabled, fog_disabled;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }

uniform float radius = 1.0;      // world units
uniform float kind = 0.0;
uniform float seed = 0.0;
uniform float bright = 1.0;
uniform float time = 0.0;
uniform vec3 col_a : source_color = vec3(1.0, 0.25, 0.35);   // H-alpha
uniform vec3 col_b : source_color = vec3(0.25, 0.85, 0.8);   // O-III

float hash13(vec3 p){ p = fract(p*0.3183099+0.1); p *= 17.0; return fract(p.x*p.y*p.z*(p.x+p.y+p.z)); }
float vnoise(vec3 x){ vec3 i=floor(x), f=fract(x); f=f*f*f*(f*(f*6.0-15.0)+10.0);
	return mix(mix(mix(hash13(i),hash13(i+vec3(1,0,0)),f.x), mix(hash13(i+vec3(0,1,0)),hash13(i+vec3(1,1,0)),f.x),f.y),
	           mix(mix(hash13(i+vec3(0,0,1)),hash13(i+vec3(1,0,1)),f.x), mix(hash13(i+vec3(0,1,1)),hash13(i+vec3(1,1,1)),f.x),f.y), f.z); }
const mat3 ROT = mat3(vec3(0.0, 0.8, 0.6), vec3(-0.8, 0.36, -0.48), vec3(-0.6, -0.48, 0.64));
float fbm(vec3 p){ float v=0.0, a=0.5; for(int i=0;i<5;i++){ v+=a*vnoise(p); p=ROT*p*2.02+vec3(1.7,9.2,3.1); a*=0.5; } return v; }
float ridge(vec3 p){ float v=0.0, a=0.5; for(int i=0;i<4;i++){ float n=1.0-abs(vnoise(p)*2.0-1.0); v+=a*n*n*n; p=ROT*p*2.1+vec3(3.1,1.7,8.3); a*=0.5; } return v; }

// density (x), oxygen fraction (y), dust (z)
vec3 field(vec3 p) {
	float r = length(p);
	vec3 q = p * 1.8 + seed;
	float big = fbm(q * 0.7);
	// irregular outer boundary: no spherical silhouette
	float edge = 0.55 + 0.45 * fbm(normalize(p + 1e-4) * 2.0 + seed * 0.3);
	float mask = sstep(edge, edge * 0.6, r);
	float d, o, dust;
	if (kind < 0.5) {
		// a blister: an ionised cavity carved into the front face of a dark molecular cloud
		vec3 cav = vec3(0.1, 0.05, 0.3);
		float rc = length(p - cav);
		float wall = sstep(0.22, 0.0, abs(rc - 0.38 - (big - 0.5) * 0.4));
		float wisps = pow(ridge(q * 1.3), 3.0) * 1.6;
		d = (wall * 0.7 + wisps * sstep(0.9, 0.2, r)) * mask;
		o = sstep(0.55, 0.1, rc);
		dust = sstep(0.52, 0.68, fbm(q * 1.4 + 4.0)) * mask * sstep(-0.2, 0.3, -p.z) * 2.0;
	} else if (kind < 1.5) {
		// expanding filamentary shell around a pulsar; synchrotron blue fog inside
		float fil = pow(ridge(q * 2.2), 4.0) * 3.0;
		float shell = sstep(0.3, 0.7, r) * sstep(edge, edge * 0.8, r);
		d = fil * shell + sstep(0.45, 0.0, r) * 0.18;
		o = sstep(0.5, 0.05, r);
		dust = 0.0;
	} else {
		// pillars: dark columns rimmed with light, standing in a glowing haze
		vec2 xz = p.xz;
		float cols = 0.0;
		for (int i = 0; i < 3; i++) {
			vec2 c = vec2(-0.32 + 0.32 * float(i), 0.05 * float(i));
			float w = 0.11 - 0.018 * float(i) + (fbm(p * 6.0 + float(i)) - 0.5) * 0.05;
			float top = 0.45 - 0.18 * float(i);
			cols = max(cols, sstep(w, w * 0.5, length(xz - c)) * sstep(top + 0.05, top - 0.05, p.y) * sstep(-0.9, -0.6, p.y));
		}
		float haze = sstep(0.35, 0.7, big) * mask;
		float rim = cols * (1.0 - cols) * 4.0;
		d = haze * 0.22 + rim * 1.6;
		o = 0.25 + 0.6 * sstep(-0.2, 0.7, p.y);
		dust = cols * 14.0 + haze * 0.3;
	}
	return vec3(max(d, 0.0), clamp(o, 0.0, 1.0), max(dust, 0.0));
}

void fragment() {
	vec3 C = NODE_POSITION_WORLD;
	vec4 vp = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, 0.5, 1.0);
	vec3 rd = normalize(mat3(INV_VIEW_MATRIX) * normalize(vp.xyz / vp.w));
	mat3 inv = transpose(mat3(normalize(MODEL_MATRIX[0].xyz), normalize(MODEL_MATRIX[1].xyz), normalize(MODEL_MATRIX[2].xyz)));
	vec3 ro = inv * (CAMERA_POSITION_WORLD - C) / radius;
	rd = inv * rd;
	float b = dot(ro, rd), c = dot(ro, ro) - 1.0, disc = b * b - c;
	if (disc < 0.0) discard;
	float sq = sqrt(disc);
	float t0 = max(-b - sq, 0.0), t1 = -b + sq;
	if (t1 <= 0.0) discard;
	const int N = 96;
	float dt = (t1 - t0) / float(N);
	float jitter = hash13(vec3(FRAGCOORD.xy, time * 0.0));
	vec3 col = vec3(0.0);
	float T = 1.0;
	for (int i = 0; i < N; i++) {
		vec3 p = ro + rd * (t0 + dt * (float(i) + jitter));
		vec3 f = field(p);
		if (f.x < 0.002 && f.z < 0.002) continue;
		vec3 emi = mix(col_a, col_b, f.y) * f.x;
		// a hot white glow where the young stars burn
		emi += vec3(1.0, 0.95, 0.9) * exp(-dot(p - vec3(0.1, 0.05, 0.3) * step(kind, 0.5), p - vec3(0.1, 0.05, 0.3) * step(kind, 0.5)) * 40.0) * (kind < 0.5 ? 0.5 : kind < 1.5 ? 0.6 : 0.0);
		float absorb = (f.x * 0.5 + f.z * 2.5) * dt * 3.0;
		col += T * emi * dt * 0.9 * bright;
		T *= exp(-absorb);
		if (T < 0.02) break;
	}
	ALBEDO = col;
	ALPHA = clamp(1.0 - T, 0.0, 1.0) * 0.9;
}
