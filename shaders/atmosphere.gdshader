// Single-scattering atmosphere (Rayleigh + Mie), ray-marched against the planet sphere.
shader_type spatial;
render_mode unshaded, blend_premul_alpha, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
#include "res://shaders/noise.gdshaderinc"

uniform float planet_r = 1.0;
uniform float atmo_r = 1.025;
uniform vec3 tint = vec3(0.35, 0.55, 1.0);   // biases the Rayleigh colour per world
uniform float density = 1.0;
uniform vec3 sun_color = vec3(1.0);
uniform float light = 1.0;
uniform float intensity = 10.0;
uniform vec3 star_pos = vec3(0.0);
varying vec3 v_wpos;

void vertex() { v_wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }

void fragment() {
	vec3 center = NODE_POSITION_WORLD;
	vec3 ro = CAMERA_POSITION_WORLD - center;
	bool inside = length(ro) < atmo_r;
	if (inside == FRONT_FACING) discard;
	vec3 rd = normalize(v_wpos - CAMERA_POSITION_WORLD);
	vec2 a = rsi(ro, rd, atmo_r);
	if (a.x > a.y) discard;
	vec2 pl = rsi(ro, rd, planet_r);
	float t0 = max(a.x, 0.0);
	float t1 = a.y;
	if (pl.x < pl.y && pl.x > 0.0) t1 = min(t1, pl.x);
	vec3 L = normalize(star_pos - center);
	float thick = atmo_r - planet_r;
	float Hr = thick * 0.22, Hm = thick * 0.08;
	// Earth-like coefficients per unit length, scaled to this world and nudged toward its tint
	// tint is the relative scattering per channel: Earth ~ (0.18, 0.41, 1.0); vertical blue depth ~0.3
	vec3 tn = tint / max(max(tint.r, tint.g), tint.b);
	vec3 bR = tn * 33.1 * density * 0.026 / thick;
	float bM = 21.0 * density * 0.026 / thick;
	const int STEPS = 14;
	const int LSTEPS = 5;
	float ds = (t1 - t0) / float(STEPS);
	vec3 sumR = vec3(0.0), sumM = vec3(0.0);
	float odR = 0.0, odM = 0.0;
	for (int i = 0; i < STEPS; i++) {
		vec3 x = ro + rd * (t0 + ds * (float(i) + 0.5));
		float h = length(x) - planet_r;
		float hr = exp(-h / Hr) * ds, hm = exp(-h / Hm) * ds;
		odR += hr; odM += hm;
		vec2 lt = rsi(x, L, atmo_r);
		float ls = lt.y / float(LSTEPS);
		float lR = 0.0, lM = 0.0;
		bool blocked = false;
		for (int j = 0; j < LSTEPS; j++) {
			vec3 y = x + L * ls * (float(j) + 0.5);
			float hy = length(y) - planet_r;
			if (hy < 0.0) { blocked = true; break; }
			lR += exp(-hy / Hr) * ls; lM += exp(-hy / Hm) * ls;
		}
		if (blocked) continue;
		vec3 att = exp(-(bR * (odR + lR) + bM * 1.1 * (odM + lM)));
		sumR += hr * att; sumM += hm * att;
	}
	float mu = dot(rd, L);
	float phR = 3.0 / (16.0 * 3.14159) * (1.0 + mu * mu);
	float g = 0.76;
	float phM = 3.0 / (8.0 * 3.14159) * ((1.0 - g * g) * (1.0 + mu * mu)) / ((2.0 + g * g) * pow(1.0 + g * g - 2.0 * g * mu, 1.5));
	vec3 col = (sumR * bR * phR + sumM * bM * phM) * intensity * sun_color * light;
	vec3 trans = exp(-(bR * odR + bM * 1.1 * odM));
	ALBEDO = col;
	ALPHA = clamp(1.0 - dot(trans, vec3(0.333)), 0.0, 1.0);
}
