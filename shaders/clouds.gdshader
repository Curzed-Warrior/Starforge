// Cloud shell for textured worlds.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_back, shadows_disabled, fog_disabled;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
uniform sampler2D cloud_tex : filter_linear_mipmap_anisotropic, repeat_enable;
uniform float cloud_off = 0.0;
uniform vec3 sun_color = vec3(1.0);
uniform float light = 1.0;
uniform vec3 star_pos = vec3(0.0);
varying vec3 v_wpos;
varying vec3 v_wn;
void vertex() {
	v_wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_wn = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}
void fragment() {
	vec3 L = normalize(star_pos - v_wpos);
	float ndl = dot(normalize(v_wn), L);
	float c = texture(cloud_tex, UV + vec2(cloud_off, 0.0)).r;
	float lit = sstep(-0.15, 0.35, ndl);
	vec3 warm = mix(vec3(1.0, 0.55, 0.3), vec3(1.0), sstep(0.0, 0.3, ndl));
	ALBEDO = sun_color * light * warm * lit * 0.95 + vec3(0.003);
	ALPHA = c * mix(0.4, 0.92, lit);       // at night city light glows through the cloud deck
}
