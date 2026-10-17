// Final grade over the 3D frame: lens ghosts from the star, edge fringing, vignette, grain.
shader_type canvas_item;
render_mode unshaded;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
float sqr(float x){ return x * x; }

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap;
uniform vec2 sun_uv = vec2(-10.0);      // star position on screen (0..1), off-screen when hidden
uniform float sun_vis = 0.0;            // 0..1 how much of the star is visible
uniform vec3 sun_col = vec3(1.0, 0.9, 0.8);
uniform float time = 0.0;
uniform float grain = 0.035;
uniform float vignette = 0.35;
uniform float fringe = 0.0009;
uniform float warp = 0.0;           // 0..1 radial streaking while crossing interstellar distances

float h(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 c = uv - 0.5;
	float r2 = dot(c, c);
	// chromatic fringing grows toward the frame edge
	vec2 off = c * r2 * fringe * 18.0;
	vec3 col;
	col.r = texture(screen, uv + off).r;
	col.g = texture(screen, uv).g;
	col.b = texture(screen, uv - off).b;
	if (warp > 0.001) {
		// zoom streaks: smear outward from the centre, brightest at the edges, tinted blue ahead and red behind
		vec3 acc = vec3(0.0);
		for (int i = 1; i <= 10; i++) {
			float k = float(i) / 10.0;
			acc += texture(screen, 0.5 + c * (1.0 - warp * 0.18 * k)).rgb * (1.0 - k * 0.5);
		}
		acc /= 7.75;
		float edge = sstep(0.02, 0.35, r2 * 2.0);
		col = mix(col, acc * vec3(0.85, 0.95, 1.2), warp * edge);
	}
	// lens ghosts: reflections of the star mirrored through the frame centre
	if (sun_vis > 0.001) {
		vec2 aspect = vec2(SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x, 1.0);
		vec2 axis = 0.5 - sun_uv;
		float g = 0.0;
		vec3 gc = vec3(0.0);
		for (int i = 0; i < 5; i++) {
			float t = 0.35 + float(i) * 0.38;
			vec2 gp = sun_uv + axis * t * 2.0;
			float sz = 0.02 + 0.035 * fract(float(i) * 0.618);
			float d = length((uv - gp) * aspect);
			float ring = exp(-sqr((d - sz) / (sz * 0.25))) * 0.5 + exp(-d * d / (sz * sz)) * 0.25;
			vec3 tint = mix(vec3(0.4, 0.7, 1.0), vec3(1.0, 0.6, 0.3), fract(float(i) * 0.37));
			gc += tint * ring;
		}
		// a soft horizontal streak through the star
		vec2 ds = (uv - sun_uv) * aspect;
		float streak = exp(-abs(ds.y) * 400.0) * exp(-abs(ds.x) * 3.0) * 0.35;
		col += (gc * 0.06 + sun_col * streak) * sun_vis;
	}
	col *= 1.0 - vignette * sstep(0.15, 0.75, r2 * 1.6);
	col += (h(uv * 1000.0 + time) - 0.5) * grain * (0.4 + 0.6 * (1.0 - dot(col, vec3(0.33))));
	COLOR = vec4(col, 1.0);
}
