// Planetary ring: UV.x runs from the inner edge (0) to the outer edge (1).
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
#include "res://shaders/noise.gdshaderinc"
uniform sampler2D ring_tex : source_color, filter_linear_mipmap, repeat_disable;
uniform bool textured = false;
uniform vec3 col : source_color = vec3(0.8, 0.75, 0.65);
uniform float planet_r = 1.0;
uniform vec3 sun_color = vec3(1.0);
uniform float light = 1.0;
uniform float seed = 0.0;
uniform vec3 star_pos = vec3(0.0);
varying vec3 v_wpos;
varying vec3 v_wn;
void vertex() {
	v_wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_wn = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}
void fragment() {
	float u = UV.x;
	vec4 t;
	if (textured) t = texture(ring_tex, vec2(u, 0.5));
	else {
		float b = 0.5 + 0.5 * sin(u * 70.0 + seed) * sin(u * 17.0 + 1.0 + seed);
		b = mix(b, 0.5, sstep(0.2, 1.0, fwidth(u * 70.0)));
		float gap = sstep(0.0, 0.02, abs(u - 0.62));
		t = vec4(col * (0.8 + 0.3 * b), (0.25 + 0.55 * b) * gap * sstep(0.0, 0.05, u) * sstep(1.0, 0.92, u));
	}
	vec3 L = normalize(star_pos - v_wpos);
	vec3 V = normalize(CAMERA_POSITION_WORLD - v_wpos);
	// the planet's shadow across the ring
	vec2 h = rsi(v_wpos - NODE_POSITION_WORLD, L, planet_r);
	float sh = (h.x < h.y && h.y > 0.0) ? 0.04 : 1.0;
	vec3 n = normalize(v_wn);
	bool litSide = sign(dot(n, L)) == sign(dot(n, V));
	float fwd = pow(max(dot(-V, L), 0.0), 6.0);
	float bright = litSide ? 0.9 : 0.25 + 0.9 * fwd * (1.0 - t.a);
	ALBEDO = t.rgb * sun_color * light * bright * sh + t.rgb * 0.005;
	ALPHA = t.a * 0.95;
}
