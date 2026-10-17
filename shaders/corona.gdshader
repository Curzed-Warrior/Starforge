// Corona and streamers on a camera-facing quad around the star.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
#include "res://shaders/noise.gdshaderinc"
uniform vec3 color : source_color = vec3(1.0, 0.85, 0.6);
uniform float star_r = 1.0;      // world radius of the photosphere
uniform float time = 0.0;
uniform float strength = 1.0;
uniform float dim = 1.0;
varying float v_size;
void vertex() {
	v_size = length(MODEL_MATRIX[0].xyz);
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(v_size,0,0,0), vec4(0,v_size,0,0), vec4(0,0,v_size,0), vec4(0,0,0,1));
}
void fragment() {
	vec2 c = (UV * 2.0 - 1.0) * v_size;          // world units from centre, in the quad plane
	float r = length(c) / star_r;
	float ang = atan(c.y, c.x);
	float streak = fbm(vec3(cos(ang) * 3.0, sin(ang) * 3.0, time * 0.02));
	float streak2 = fbm(vec3(cos(ang) * 11.0, sin(ang) * 11.0, r * 0.4 - time * 0.05));
	float rr = max(r, 1.0);
	float fall = pow(1.0 / rr, 3.0);
	float rays = fall * (0.4 + 1.4 * sqr(streak) + 0.6 * streak2);
	float halo = 0.4 * pow(1.0 / rr, 1.5);
	vec3 col = color * (rays * 2.0 + halo * 0.5);
	col *= sstep(1.0, 0.6, length(UV * 2.0 - 1.0));
	ALBEDO = col * strength * dim;
}
