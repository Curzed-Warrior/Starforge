// Dyson swarm collectors, entirely on the GPU: each instance's Kepler orbit and sun-facing attitude
// come from per-instance constants (INSTANCE_CUSTOM = r fraction, phase, size, tilt seed; COLOR = orbit-plane quaternion).
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;

uniform float time = 0.0;          // years
uniform float r_in = 8.0;
uniform float r_out = 24.0;
uniform vec3 sun_color = vec3(1.0);
uniform float heat = 0.0;          // 0..1 waste-heat glow on the dark faces
uniform float glint = 1.0;
uniform float gap = 0.0;           // half-width in radians of the band left open for inhabited worlds (0 = closed shell)
const float AU = 23481.0;          // system units (Earth radii) per astronomical unit
varying float v_face;
varying float v_sunlit;
varying vec3 v_wpos;
varying float v_far;

vec3 qrot(vec4 q, vec3 v) { return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v); }

void vertex() {
	vec4 cu = INSTANCE_CUSTOM;
	vec4 q = COLOR * 2.0 - 1.0;
	q = normalize(q);
	float r = r_in + cu.x * (r_out - r_in);
	float a = cu.y * 6.2831853 + time * 6.2831853 * pow(r / AU, -1.5) * 0.02;   // true periods are hours: slowed to watch
	vec3 c = qrot(q, vec3(cos(a) * r, 0.0, sin(a) * r));
	// statites hold station off the planets' plane: lift any collector inside the open band to its edge
	if (gap > 0.0) {
		float lat = asin(clamp(c.y / r, -1.0, 1.0));
		if (abs(lat) < gap) {
			float l2 = sign(lat + 1e-4) * gap;
			vec2 h = normalize(c.xz);
			c = vec3(h.x * cos(l2) * r, sin(l2) * r, h.y * cos(l2) * r);
		}
	}
	vec3 n = normalize(-c);                                   // faces the star
	vec3 up = abs(n.y) > 0.95 ? vec3(1.0, 0.0, 0.0) : vec3(0.0, 1.0, 0.0);
	vec3 right = normalize(cross(up, n)), up2 = cross(n, right);
	float s = cu.z;
	float ms = length(MODEL_MATRIX[0].xyz);            // the system node is squashed when far (scaled space)
	vec3 local = c + (right * VERTEX.x + up2 * VERTEX.y) * s;
	vec3 w = (MODEL_MATRIX * vec4(local, 1.0)).xyz;
	vec3 wc = (MODEL_MATRIX * vec4(c, 1.0)).xyz;
	vec3 ws = MODEL_MATRIX[3].xyz;
	v_wpos = w;
	vec3 toCam = normalize(CAMERA_POSITION_WORLD - w);
	vec3 nw = normalize(ws - wc);
	v_face = dot(toCam, nw);
	v_sunlit = 1.0;
	// keep collectors at least a pixel wide from afar so the swarm reads as a glittering veil
	float d = length(CAMERA_POSITION_WORLD - wc);
	float minS = d * 0.0012;
	float sw = s * ms;
	if (sw < minS) {
		w = wc + (w - wc) * (minS / max(sw, 1e-9));
	}
	v_far = clamp(minS / max(sw, 1e-9), 1.0, 40.0);
	POSITION = PROJECTION_MATRIX * VIEW_MATRIX * vec4(w, 1.0);
}

void fragment() {
	vec3 col;
	if (v_face > 0.0) {
		// we see the mirrored, sunlit face: a glint when the reflection lines up
		col = vec3(0.16, 0.15, 0.14) * sun_color * (1.0 + 2.5 * glint * pow(v_face, 12.0));
	} else {
		// the dark back: black, warmed by waste heat
		col = vec3(0.012, 0.012, 0.014) + vec3(0.9, 0.18, 0.05) * heat * 0.35;
	}
	col /= v_far * 0.6 + 0.4;
	ALBEDO = col;
}
