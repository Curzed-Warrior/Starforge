// Dark dust lanes: multiplicative sprites, order independent.
shader_type spatial;
render_mode unshaded, blend_mul, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }

uniform float near_fade = 1.0;
uniform float sky_k = 1e6;
uniform float strength = 1.0;
varying vec4 v_col;
varying float v_fade;
varying float v_id;

float hash(vec3 p){ p = fract(p*0.3183099+0.1); p *= 17.0; return fract(p.x*p.y*p.z*(p.x+p.y+p.z)); }
float noise(vec3 x){ vec3 i=floor(x), f=fract(x); f=f*f*(3.0-2.0*f);
	return mix(mix(mix(hash(i),hash(i+vec3(1,0,0)),f.x), mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),f.x),f.y),
	           mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),f.x), mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),f.x),f.y), f.z); }
float fbm(vec3 p){ float v=0.0, a=0.5; for(int i=0;i<4;i++){ v+=a*noise(p); p*=2.07; a*=0.5; } return v; }

void vertex() {
	vec3 center = MODEL_MATRIX[3].xyz;
	float size = length(MODEL_MATRIX[0].xyz);
	float d = length(center - CAMERA_POSITION_WORLD);
	float f = d > sky_k ? sky_k / d : 1.0;
	vec3 c2 = CAMERA_POSITION_WORLD + (center - CAMERA_POSITION_WORLD) * f;
	float s2 = size * f;
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], vec4(c2, 1.0));
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(s2,0,0,0), vec4(0,s2,0,0), vec4(0,0,s2,0), vec4(0,0,0,1));
	v_fade = sstep(size * 1.0 + near_fade, size * 3.0 + near_fade * 2.0, d);
	v_col = COLOR;
	v_id = fract(center.x * 0.0113 + center.z * 0.0191);
}

void fragment() {
	vec2 c = UV * 2.0 - 1.0;
	float r = length(c);
	float n = fbm(vec3(c * 2.5, v_id * 23.0));
	float a = sstep(1.0, 0.2, r) * sstep(0.3, 0.75, n + 0.25 - r * 0.3);
	a *= v_col.a * strength * v_fade;
	ALBEDO = mix(vec3(1.0), v_col.rgb, clamp(a, 0.0, 1.0));
}
