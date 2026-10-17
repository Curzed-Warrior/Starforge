// Plating for every hull, station and yard: panels of slightly different alloys, seams between them,
// and here and there a lit porthole. Worked out in object space so it stays put as the ship turns.
shader_type spatial;
render_mode cull_back;
uniform vec3 base : source_color = vec3(0.56, 0.58, 0.62);
uniform float panel = 14.0;
uniform vec3 window_col : source_color = vec3(1.0, 0.82, 0.55);
varying vec3 v_o;
varying vec3 v_n;

float h31(vec3 p) { p = fract(p * 0.3183099 + 0.1); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }

void vertex() { v_o = VERTEX; v_n = NORMAL; }

void fragment() {
	vec3 an = abs(v_n);
	vec2 uv = an.x > an.y && an.x > an.z ? v_o.yz : (an.y > an.z ? v_o.xz : v_o.xy);
	float face = an.x > an.y && an.x > an.z ? 1.0 : (an.y > an.z ? 2.0 : 3.0);
	vec2 q = uv * panel * vec2(1.0, 0.5);
	float fw = length(fwidth(q));
	vec2 g = abs(fract(q) - 0.5);
	float seam = mix(step(0.46, max(g.x, g.y)), 0.08, clamp(fw * 1.5 - 0.3, 0.0, 1.0));
	float tone = h31(vec3(floor(q), face));
	// a finer layer of greebles: vents, plates, conduits
	vec2 q2 = q * 4.0;
	float fw2 = length(fwidth(q2));
	float greeb = mix(step(0.82, h31(vec3(floor(q2), face + 5.0))), 0.18, clamp(fw2 - 0.3, 0.0, 1.0));
	vec3 col = base * (0.78 + 0.34 * tone) * (1.0 - seam * 0.45) * (1.0 - greeb * 0.25);
	// a few panels carry the fleet's paint
	col = mix(col, vec3(0.42, 0.2, 0.14), step(0.975, tone) * 0.45);
	ALBEDO = col;
	METALLIC = 0.45 + 0.35 * tone;
	ROUGHNESS = 0.3 + 0.35 * (1.0 - tone);
	vec2 q3 = q * 6.0;
	float win = step(0.985, h31(vec3(floor(q3), face + 11.0))) * step(0.3, fract(q3.x)) * step(0.35, fract(q3.y)) * (1.0 - clamp(length(fwidth(q3)) - 0.5, 0.0, 1.0));
	EMISSION = window_col * win * 1.3 + base * 0.03;
}
