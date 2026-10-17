// The star's photosphere: textured or procedural granulation, limb darkening, HDR for bloom.
shader_type spatial;
render_mode unshaded, cull_back, shadows_disabled, fog_disabled;
#include "res://shaders/noise.gdshaderinc"
uniform sampler2D tex : source_color, filter_linear_mipmap, repeat_enable;
uniform bool textured = false;
uniform vec3 color : source_color = vec3(1.0, 0.9, 0.75);
uniform float brightness = 6.0;
uniform float time = 0.0;
uniform float dim = 1.0;          // 1 = bare star, toward 0 as a swarm captures it
uniform float cell_scale = 9.0;   // granulation scale: giants have a few huge convection cells
varying vec3 v_obj;
varying vec3 v_wn;
varying vec3 v_wpos;
void vertex() {
	v_obj = VERTEX;
	v_wn = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	v_wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}
void fragment() {
	vec3 p = normalize(v_obj);
	float g = fbm(p * cell_scale + vec3(time * 0.03, time * 0.02, 0.0));
	vec2 cel = cells(p * cell_scale * 0.6 + time * 0.01);
	float gran = sstep(0.0, 0.6, cel.x) * 0.35;
	float g2 = fbm(p * 40.0 + vec3(0.0, time * 0.08, time * 0.05));
	vec3 base;
	if (textured) {
		vec2 uv = UV + vec2(time * 0.002, 0.0) + (vec2(g, g2) - 0.5) * 0.01;
		base = texture(tex, uv).rgb;
		base = base / max(max(base.r, base.g), max(base.b, 0.05));
		base = mix(color, color * base, 0.6);
		base *= 0.85 + 0.3 * g2;
	} else {
		float spots = sstep(0.66, 0.72, fbm(p * 2.0 + 11.0));
		base = color * (0.75 + 0.5 * g) * (0.85 + 0.3 * g2) * (1.0 - 0.6 * spots) * (1.0 - gran);
	}
	vec3 V = normalize(CAMERA_POSITION_WORLD - v_wpos);
	float mu = clamp(dot(normalize(v_wn), V), 0.0, 1.0);
	float limb = 0.35 + 0.65 * pow(mu, 0.45);
	vec3 limbTint = mix(vec3(1.0, 0.55, 0.3), vec3(1.0), pow(mu, 0.3));
	// seen through a closing swarm the star reddens: only gaps and waste heat get out
	vec3 veil = mix(vec3(1.0, 0.32, 0.12), vec3(1.0), sstep(0.15, 0.9, dim));
	ALBEDO = base * limb * limbTint * veil * brightness * dim;
}
