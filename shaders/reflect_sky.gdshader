// What metal sees: a dim deep-space sky with the band of the Milky Way across it. Used only for reflections and
// never drawn (the background stays black and the real galaxy is drawn in 3D). Static, so Godot bakes it once.
shader_type sky;
render_mode use_half_res_pass;
uniform vec3 band_normal = vec3(0.28, 0.93, 0.24);   // the galactic plane, roughly as seen from Sol
float h(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }
float n3(vec3 p) {
	vec3 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(h(i), h(i + vec3(1,0,0)), f.x), mix(h(i + vec3(0,1,0)), h(i + vec3(1,1,0)), f.x), f.y),
	           mix(mix(h(i + vec3(0,0,1)), h(i + vec3(1,0,1)), f.x), mix(h(i + vec3(0,1,1)), h(i + vec3(1,1,1)), f.x), f.y), f.z);
}
void sky() {
	vec3 d = EYEDIR;
	float b = dot(d, normalize(band_normal));
	float band = exp(-b * b * 22.0);
	float clouds = n3(d * 6.0) * 0.6 + n3(d * 15.0) * 0.4;
	vec3 col = vec3(0.012, 0.014, 0.02);
	col += vec3(0.30, 0.26, 0.21) * band * (0.35 + 0.65 * clouds) * 0.35;
	col += vec3(0.25, 0.2, 0.16) * exp(-pow(acos(clamp(dot(d, normalize(vec3(-0.45, -0.1, 0.88))), -1.0, 1.0)) * 3.0, 2.0)) * 0.25;   // the bright core
	COLOR = col;
}
