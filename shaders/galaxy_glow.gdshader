// Unresolved starlight and emission nebulae: big soft camera-facing sprites, additive.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }

uniform float exposure = 1.0;
uniform float near_fade = 1.0;
uniform float sky_k = 1e6;
uniform sampler2D cap_tex : filter_linear, repeat_disable;
uniform float tex_extent = 1150.0;
uniform vec3 gal_origin = vec3(0.0);
uniform float gscale = 1.0;
varying float v_cap;     // world units: sprites whose centre is closer than ~this fade
uniform float nebula = 0.0;        // 1 = structured emission nebula, 0 = smooth glow
uniform float seed = 0.0;
varying vec4 v_col;
varying float v_fade;
varying float v_id;

float hash(vec3 p){ p = fract(p*0.3183099+0.1); p *= 17.0; return fract(p.x*p.y*p.z*(p.x+p.y+p.z)); }
float noise(vec3 x){ vec3 i=floor(x), f=fract(x); f=f*f*(3.0-2.0*f);
	return mix(mix(mix(hash(i),hash(i+vec3(1,0,0)),f.x), mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),f.x),f.y),
	           mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),f.x), mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),f.x),f.y), f.z); }
float fbm(vec3 p){ float v=0.0, a=0.5; for(int i=0;i<5;i++){ v+=a*noise(p); p*=2.03; a*=0.5; } return v; }

void vertex() {
	vec3 center = MODEL_MATRIX[3].xyz;
	float size = length(MODEL_MATRIX[0].xyz);
	float d = length(center - CAMERA_POSITION_WORLD);
	// billboard
	float f = d > sky_k ? sky_k / d : 1.0;
	vec3 c2 = CAMERA_POSITION_WORLD + (center - CAMERA_POSITION_WORLD) * f;
	float s2 = size * f;
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], vec4(c2, 1.0));
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(s2,0,0,0), vec4(0,s2,0,0), vec4(0,0,s2,0), vec4(0,0,0,1));
	// fade when the camera is inside or very near the sprite; it would only be a flat wash
	v_fade = sstep(size * 1.2 + near_fade, size * 4.0 + near_fade * 2.0, d);
	v_col = COLOR;
	vec3 lc = (center - gal_origin) / gscale;
	v_cap = textureLod(cap_tex, lc.xz / (2.0 * tex_extent) + 0.5, 0.0).r;
	v_id = fract(center.x * 0.0137 + center.z * 0.0071);
}

void fragment() {
	vec2 c = UV * 2.0 - 1.0;
	float r = length(c);
	float a;
	if (nebula > 0.5) {
		float n = fbm(vec3(c * 2.2, v_id * 31.0 + seed));
		float m = fbm(vec3(c * 5.0 + n, v_id * 17.0));
		a = sstep(1.0, 0.1, r) * sstep(0.35, 0.8, n * 0.8 + m * 0.5 - r * 0.25);
	} else {
		a = exp(-r * r * 3.5) * sstep(1.0, 0.8, r);
	}
	vec3 cc = mix(v_col.rgb, vec3(0.55, 0.12, 0.04) * 0.4, v_cap * 0.9);
	ALBEDO = cc * v_col.a * a * exposure * v_fade;
}
