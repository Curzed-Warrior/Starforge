float sqr(float x){ return x * x; }
float sstep(float a, float b, float x){ float t = clamp((x - a) / (b - a), 0.0, 1.0); return t * t * (3.0 - 2.0 * t); }
float hash13(vec3 p){ p = fract(p*0.3183099+0.1); p *= 17.0; return fract(p.x*p.y*p.z*(p.x+p.y+p.z)); }
vec3 hash33(vec3 p){ p = fract(p * vec3(0.1031, 0.1030, 0.0973)); p += dot(p, p.yxz + 33.33); return fract((p.xxy + p.yxx) * p.zyx); }
float vnoise(vec3 x){ vec3 i=floor(x), f=fract(x); f=f*f*(3.0-2.0*f);
	return mix(mix(mix(hash13(i),hash13(i+vec3(1,0,0)),f.x), mix(hash13(i+vec3(0,1,0)),hash13(i+vec3(1,1,0)),f.x),f.y),
	           mix(mix(hash13(i+vec3(0,0,1)),hash13(i+vec3(1,0,1)),f.x), mix(hash13(i+vec3(0,1,1)),hash13(i+vec3(1,1,1)),f.x),f.y), f.z); }
float fbm(vec3 p){ float v=0.0, a=0.5; for(int i=0;i<6;i++){ v+=a*vnoise(p); p=p*2.03+vec3(1.7,9.2,3.1); a*=0.5; } return v; }
float fbm3(vec3 p){ float v=0.0, a=0.5; for(int i=0;i<3;i++){ v+=a*vnoise(p); p=p*2.03+vec3(1.7,9.2,3.1); a*=0.5; } return v; }
float ridged(vec3 p){ float v=0.0, a=0.5; for(int i=0;i<5;i++){ float n = 1.0-abs(vnoise(p)*2.0-1.0); v+=a*n*n; p=p*2.1+vec3(3.1,1.7,8.3); a*=0.5; } return v; }
// cellular: distance to nearest feature point, and its hash
vec2 cells(vec3 p){ vec3 i=floor(p), f=fract(p); float d=9.0, h=0.0;
	for(int x=-1;x<=1;x++) for(int y=-1;y<=1;y++) for(int z=-1;z<=1;z++){
		vec3 g=vec3(float(x),float(y),float(z)); vec3 o=hash33(i+g); vec3 r=g+o-f; float dd=dot(r,r);
		if(dd<d){ d=dd; h=o.x; } }
	return vec2(sqrt(d), h); }
// crater field: height offset (bowl + rim) over three octaves
float craters(vec3 p, float scale){
	float h = 0.0;
	for (int k = 0; k < 3; k++) {
		vec2 c = cells(p * scale);
		float r = 0.25 + 0.2 * fract(c.y * 7.0);
		float x = c.x / r;
		float exists = step(0.45, fract(c.y * 13.7));
		h += exists * (x < 1.0 ? (x * x - 1.0) * 0.6 : 0.25 * exp(-(x - 1.0) * 6.0)) / (1.0 + float(k));
		scale *= 2.3; p += 3.7;
	}
	return h;
}
vec2 rsi(vec3 ro, vec3 rd, float r){ float b = dot(ro,rd); float c = dot(ro,ro)-r*r; float d = b*b-c; if(d<0.0) return vec2(1e20,-1e20); d=sqrt(d); return vec2(-b-d,-b+d); }
