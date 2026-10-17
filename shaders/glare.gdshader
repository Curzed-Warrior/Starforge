// Constant-pixel-size glare with diffraction spikes, so a star stays a brilliant point at any distance.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled, skip_vertex_transform;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
uniform vec3 color : source_color = vec3(1.0, 0.9, 0.8);
uniform float px = 160.0;            // sprite size in pixels
uniform float strength = 1.0;
uniform float sky_k = 1e6;
void vertex() {
	vec3 c = MODEL_MATRIX[3].xyz;
	float d = length(c - CAMERA_POSITION_WORLD);
	if (d > sky_k) c = CAMERA_POSITION_WORLD + (c - CAMERA_POSITION_WORLD) * (sky_k / d);
	vec4 clip = PROJECTION_MATRIX * VIEW_MATRIX * vec4(c, 1.0);
	vec2 off = VERTEX.xy * 2.0 * px / VIEWPORT_SIZE * clip.w;
	POSITION = clip + vec4(off, 0.0, 0.0);
}
void fragment() {
	vec2 c = UV * 2.0 - 1.0;
	float r = length(c);
	float core = exp(-r * r * 80.0) * 5.0 + exp(-r * 10.0) * 0.5;
	float fall = pow(max(1.0 - r, 0.0), 2.5);
	float spikes = (exp(-abs(c.x) * 180.0) + exp(-abs(c.y) * 180.0)) * fall * 0.9;
	spikes += (exp(-abs(c.x + c.y) * 160.0) + exp(-abs(c.x - c.y) * 160.0)) * pow(max(1.0 - r, 0.0), 4.0) * 0.2;
	ALBEDO = color * (core + spikes) * strength * sstep(1.0, 0.85, r);
}
