// Galaxy-map marker: constant pixel size, projected onto the sky shell when far away.
// INSTANCE_CUSTOM: x = size in px, y = style (0 dot, 1 ringed dot = finished swarm, 2 ship head), z = main civ.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled, skip_vertex_transform;
float sqr(float x){ return x * x; }
uniform float sky_k = 1e6;
uniform float fade = 1.0;
varying vec4 v_col;
varying float v_style;

void vertex() {
	vec3 c = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	vec3 cw = CAMERA_POSITION_WORLD;
	float d = length(c - cw);
	if (d > sky_k) c = cw + (c - cw) * (sky_k / d);
	vec4 clip = PROJECTION_MATRIX * VIEW_MATRIX * vec4(c, 1.0);
	float px = INSTANCE_CUSTOM.x;
	POSITION = clip + vec4(VERTEX.xy * 2.0 * px / VIEWPORT_SIZE * clip.w, 0.0, 0.0);
	v_col = COLOR;
	v_style = INSTANCE_CUSTOM.y;
}

void fragment() {
	vec2 c = UV * 2.0 - 1.0;
	float r = length(c);
	float a;
	if (v_style > 1.5) a = exp(-r * r * 9.0) * 1.6;                         // ship head
	else if (v_style > 0.5) a = exp(-r * r * 30.0) * 0.6 + exp(-sqr((r - 0.6) * 9.0)) * 0.7;   // finished: ringed
	else a = exp(-r * r * 14.0);
	ALBEDO = v_col.rgb * v_col.a * a * fade;
}
