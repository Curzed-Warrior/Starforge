// Galaxy-map marker: constant pixel size, projected onto the sky shell when far away.
// INSTANCE_CUSTOM: x = size in px, y = style, z = main civ, w = style 7 only: battle brightness (1 - age / battleMapYears).
// Styles:
//   0 dot   1 ringed dot (finished swarm)   2 ship head
//   3 fleet: chevron pointing along its course, a unit world vector in the instance basis x column (zero in port: up)
//   4 army transport: small square, drawn 1.5 sizes right of its centre so it sits beside its fleet's chevron
//   5 occupied: ring in the occupier's colour, sized to sit round the owner's dot
//   6 blockade: dashed ring   7 battle: cross flare, brightness from w
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled, skip_vertex_transform;
float sqr(float x){ return x * x; }
uniform float sky_k = 1e6;
uniform float fade = 1.0;
varying vec4 v_col;
varying float v_style;
varying float v_w;

// distance from p to the segment a-b
float seg(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a, ba = b - a;
	return length(pa - ba * clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0));
}

void vertex() {
	vec3 c = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	vec3 cw = CAMERA_POSITION_WORLD;
	float d = length(c - cw);
	if (d > sky_k) c = cw + (c - cw) * (sky_k / d);
	vec4 clip = PROJECTION_MATRIX * VIEW_MATRIX * vec4(c, 1.0);
	float px = INSTANCE_CUSTOM.x;
	vec2 off = VERTEX.xy * px;
	int s = int(INSTANCE_CUSTOM.y + 0.5);
	if (s == 4) off.x += px * 1.5;                                  // army: beside the fleet, not on it
	if (s == 3) {
		// a fleet's chevron points along its course (basis x column, world units; zero in port = point up the screen)
		vec3 course = MODEL_MATRIX[0].xyz;
		vec2 up = vec2(0.0, -1.0);                                  // clip +y is down the screen
		if (dot(course, course) > 0.25) {
			vec4 clip2 = PROJECTION_MATRIX * VIEW_MATRIX * vec4(c + normalize(course) * length(c - cw) * 0.05, 1.0);
			vec2 sd = (clip2.xy / clip2.w - clip.xy / clip.w) * VIEWPORT_SIZE;
			if (dot(sd, sd) > 1e-6) up = normalize(sd);
		}
		off = vec2(-up.y, up.x) * off.x + up * off.y;
	}
	POSITION = clip + vec4(off * 2.0 / VIEWPORT_SIZE * clip.w, 0.0, 0.0);
	v_col = COLOR;
	v_style = INSTANCE_CUSTOM.y;
	v_w = INSTANCE_CUSTOM.w;
}

void fragment() {
	vec2 c = UV * 2.0 - 1.0;
	c.y = -c.y;                                                     // +y up on screen
	float r = length(c);
	int s = int(v_style + 0.5);
	float a;
	if (s == 7) {                                                   // battle: cross flare
		float arms = exp(-sqr(c.y * 14.0)) * (1.0 - abs(c.x)) + exp(-sqr(c.x * 14.0)) * (1.0 - abs(c.y));
		a = (arms * 1.2 + exp(-r * r * 16.0) * 1.4) * clamp(v_w, 0.0, 1.0);
	}
	else if (s == 6) {                                              // blockade: dashed ring
		float dash = smoothstep(-0.2, 0.2, sin(atan(c.y, c.x) * 8.0));
		a = exp(-sqr((r - 0.75) * 11.0)) * dash * 0.9;
	}
	else if (s == 5) a = exp(-sqr((r - 0.75) * 11.0)) * 0.9;       // occupied: ring
	else if (s == 4) {                                              // army transport: small square
		float m = max(abs(c.x), abs(c.y));
		a = smoothstep(0.5, 0.35, m) * 0.85;
	}
	else if (s == 3) {                                              // fleet: chevron
		float dd = min(seg(c, vec2(-0.7, -0.35), vec2(0.0, 0.4)), seg(c, vec2(0.0, 0.4), vec2(0.7, -0.35)));
		a = smoothstep(0.2, 0.07, dd) * 1.1;
	}
	else if (s == 2) a = exp(-r * r * 9.0) * 1.6;                   // ship head
	else if (s == 1) a = exp(-r * r * 30.0) * 0.6 + exp(-sqr((r - 0.6) * 9.0)) * 0.7;   // finished: ringed
	else a = exp(-r * r * 14.0);
	ALBEDO = v_col.rgb * v_col.a * a * fade;
}
