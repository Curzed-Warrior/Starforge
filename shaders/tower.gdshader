// City towers and arcologies: lit by the star by day; by night thousands of windows burn.
// COLOR: r = facade shade, g = window density, b = accent (crown light), a = random seed.
shader_type spatial;
render_mode cull_back;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
uniform vec3 star_pos = vec3(0.0);
uniform vec3 planet_pos = vec3(0.0);
uniform vec3 light_col : source_color = vec3(1.0, 0.75, 0.45);
uniform float floors = 4000.0;     // floors per world unit
varying vec3 v_l;
varying vec3 v_w;
varying vec4 v_c;
varying vec3 v_s;

float h21(vec2 p){ return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

void vertex() {
	v_l = VERTEX;
	v_w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_c = COLOR;
	v_s = vec3(length(MODEL_MATRIX[0].xyz), length(MODEL_MATRIX[1].xyz), length(MODEL_MATRIX[2].xyz));
}

void fragment() {
	vec3 up = normalize(v_w - planet_pos);
	float sun = dot(up, normalize(star_pos - v_w));
	float night = sstep(0.12, -0.18, sun);
	float fy = v_l.y * v_s.y * floors;
	float fx = (v_l.x * v_s.x + v_l.z * v_s.z) * floors * 1.6;
	float rows = step(0.4, fract(fy));
	float cols = step(0.3, fract(fx));
	float on = step(1.0 - v_c.g, h21(floor(vec2(fy, fx)) + v_c.a * 17.0));
	// a lit crown on top of the tall ones
	float crown = v_c.b * sstep(0.9, 0.98, v_l.y);
	// pale stone and glass by day: window bands read darker and a little blue
	vec3 facade = mix(vec3(0.55, 0.56, 0.58), vec3(0.85, 0.84, 0.8), v_c.r);
	ALBEDO = mix(facade, vec3(0.22, 0.28, 0.36), rows * cols * 0.55);
	METALLIC = 0.15;
	ROUGHNESS = 0.55;
	vec3 win = light_col * rows * cols * on;
	EMISSION = win * (0.03 + night * 1.8) + light_col * crown * (0.4 + night * 2.5);
}
