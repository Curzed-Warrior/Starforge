// Stars of the visual galaxy. Each point is an aggregate of many real stars; its on-screen energy
// falls with distance squared, so far away they integrate into a smooth glow and close up they resolve.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }

uniform float exposure = 1.0;
uniform float gscale = 1.0;       // world units per galaxy unit
uniform float hide_near = 0.0;    // galaxy units: points closer than this fade out
uniform float max_px = 6.0;
uniform float sky_k = 1e6;       // world radius of the sky shell
uniform sampler2D cap_tex : filter_linear, repeat_disable;
uniform float tex_extent = 1150.0;
varying vec3 v_col;
varying float v_px;

void vertex() {
	vec3 wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float d = max(length(wp - CAMERA_POSITION_WORLD) / gscale, 0.001);
	float lum = COLOR.a * COLOR.a * 6.0;
	float e = exposure * lum / (d * d);
	float ec = e / (1.0 + e * 0.08);
	float px = clamp(1.0 + sqrt(ec) * 1.4, 1.0, max_px);
	float fade = sstep(hide_near, hide_near * 2.5, d);
	v_col = COLOR.rgb * ec * fade / (0.3 * px * px + 0.7);
	// stars wrapped in swarms go dark, leaking only a dull infrared glow
	float cap = textureLod(cap_tex, VERTEX.xz / (2.0 * tex_extent) + 0.5, 0.0).r;
	float lumi = dot(v_col, vec3(0.3, 0.5, 0.2));
	v_col = mix(v_col, vec3(0.5, 0.09, 0.03) * lumi * 0.35, cap * 0.92);
	v_px = px;
	POINT_SIZE = px;
	vec3 cw = CAMERA_POSITION_WORLD;
	float dw = length(wp - cw);
	if (dw > sky_k) wp = cw + (wp - cw) * (sky_k / dw);
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(wp, 1.0);
}

void fragment() {
	vec2 c = POINT_COORD * 2.0 - 1.0;
	float r2 = dot(c, c);
	float a = v_px <= 1.5 ? 1.0 : exp(-r2 * 4.0) * 1.3;
	ALBEDO = v_col * a;
}
