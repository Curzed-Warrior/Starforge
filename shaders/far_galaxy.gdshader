// A distant galaxy on a fixed-orientation quad: procedural spiral or irregular cloud. Projected onto the sky shell.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled, skip_vertex_transform;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
uniform float sky_k = 1e6;
uniform float kind = 0.0;      // 0 spiral, 1 irregular
uniform float seed = 0.0;
uniform float bright = 1.0;
uniform float arms = 2.0;
uniform float exposure = 1.0;

float hash(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p){ vec2 i = floor(p), f = fract(p); f = f*f*(3.0-2.0*f);
	return mix(mix(hash(i), hash(i+vec2(1,0)), f.x), mix(hash(i+vec2(0,1)), hash(i+vec2(1,1)), f.x), f.y); }
float fbm(vec2 p){ float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p *= 2.1; a *= 0.5; } return v; }

void vertex() {
	vec3 w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 c = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	vec3 cw = CAMERA_POSITION_WORLD;
	float d = length(c - cw);
	if (d > sky_k) w = cw + (w - cw) * (sky_k / d);
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(w, 1.0);
}

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	if (r > 1.0) discard;
	vec3 col;
	if (kind < 0.5) {
		float ang = atan(p.y, p.x);
		float spiral = 0.0;
		for (float k = 0.0; k < 2.0; k += 1.0) {
			float th = ang - log(max(r, 0.02)) * 3.2 + k * 3.14159 + seed;
			spiral += pow(0.5 + 0.5 * cos(th * arms * 0.5 * 2.0), 3.0);
		}
		float disk = exp(-r * 3.2);
		float bulge = exp(-r * r * 60.0) * 3.0;
		float n = fbm(p * 9.0 + seed);
		float dust = sstep(0.55, 0.75, n) * spiral * 0.5;
		vec3 armCol = vec3(0.6, 0.72, 1.0);
		vec3 coreCol = vec3(1.0, 0.82, 0.6);
		col = coreCol * (bulge + disk * 0.6) + armCol * spiral * disk * (0.6 + 0.6 * n);
		col *= 1.0 - dust * 0.6;
	} else {
		float n = fbm(p * 3.0 + seed) * fbm(p * 7.0 - seed);
		float stars = step(0.985, hash(floor(p * 140.0))) * 2.0;
		col = vec3(0.75, 0.8, 1.0) * (sstep(0.1, 0.35, n) * 0.7 + stars * n) * exp(-r * r * 3.5);
	}
	ALBEDO = col * bright * exposure * (1.0 - sstep(0.7, 1.0, r));
}
