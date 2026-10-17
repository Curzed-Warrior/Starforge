// Thousands of things in orbit, moved entirely on the GPU: satellites, tugs, freighters, debris of a busy sky.
// INSTANCE_CUSTOM: x = orbit radius (model units), y = inclination, z = ascending node, w = phase.
// COLOR: rgb tint, a = brightness. Glints only in sunlight; the planet's shadow swallows them.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled, skip_vertex_transform;
uniform float time = 0.0;
uniform float planet_r = 1.0;      // model units
uniform float speed = 1.0;         // angular speed at r = planet_r
uniform vec3 sun_dir = vec3(1.0, 0.0, 0.0);   // model space, toward the star
uniform float size = 0.004;        // model units
uniform float min_px = 1.7;
uniform float fade = 1.0;
varying vec4 v_col;
varying float v_boost;

void vertex() {
	float r = INSTANCE_CUSTOM.x;
	float inc = INSTANCE_CUSTOM.y, node = INSTANCE_CUSTOM.z;
	float w = speed * pow(planet_r / r, 1.5) * sign(INSTANCE_CUSTOM.w + 0.0001);
	float ph = abs(INSTANCE_CUSTOM.w) + time * w;
	vec3 p = vec3(cos(ph) * r, 0.0, sin(ph) * r);
	p = vec3(p.x, p.y * cos(inc) - p.z * sin(inc), p.y * sin(inc) + p.z * cos(inc));
	p = vec3(p.x * cos(node) + p.z * sin(node), p.y, -p.x * sin(node) + p.z * cos(node));
	// in the planet's shadow cylinder?
	float along = dot(p, sun_dir);
	float off = length(p - sun_dir * along);
	float lit = (along < 0.0 && off < planet_r) ? 0.04 : 1.0;
	vec3 c = (MODEL_MATRIX * vec4(p, 1.0)).xyz;
	float s = size * length(MODEL_MATRIX[0].xyz);
	vec4 vc = VIEW_MATRIX * vec4(c, 1.0);
	float d = max(-vc.z, 1e-4);
	float px = s / d * VIEWPORT_SIZE.y * 0.5 * abs(PROJECTION_MATRIX[1][1]);
	float k = px < min_px ? min_px / max(px, 1e-4) : 1.0;
	v_boost = 1.0 / (k * k * 0.5 + 0.5);
	POSITION = PROJECTION_MATRIX * (vc + vec4(VERTEX.xy * s * k, 0.0, 0.0));
	v_col = vec4(COLOR.rgb, COLOR.a * lit);
}

void fragment() {
	vec2 c = UV * 2.0 - 1.0; float r = dot(c, c);
	float a = exp(-r * 7.0) + 0.5 * exp(-r * 40.0);
	ALBEDO = v_col.rgb * v_col.a * a * max(v_boost, 0.1) * 1.6 * fade;
}
