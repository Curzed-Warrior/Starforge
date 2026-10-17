// Project Kardashev: watch an autonomous spacefaring civilization fold into a random star system,
// strip-mine it and wrap its star in a Dyson swarm. The player can watch, or interfere.
(function () {
  'use strict';

  const V3 = THREE.Vector3;
  const $ = id => document.getElementById(id);
  const reduceMotion = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
  const clamp = (x, a = 0, b = 1) => Math.min(b, Math.max(a, x));
  const smooth = (a, b, x) => { const u = clamp((x - a) / (b - a)); return u * u * (3 - 2 * u); };
  const UP = new V3(0, 1, 0), ORIGIN = new V3(0, 0, 0);
  const lossPct = f => (f * 100 < 1 ? (f * 100).toFixed(1) : (f * 100).toFixed(0)) + '%';
  const an = w => /^[AEFHILMNORSX]/.test(w) ? 'an' : 'a';   // article by spoken first letter ("an M3V", "a G2V")
  const rnd = (a, b) => a + Math.random() * (b - a);
  const choose = arr => arr[Math.floor(Math.random() * arr.length)];

  // ---------- Units ----------
  const L_SUN_W = 3.828e26;   // watts
  const SWARM_KG = 1.1e24;    // mass of a full swarm of 2 µm collectors at ~1 AU; the game's unit of mass
  const P_FLEET = 2e-6;       // the flotilla's own reactors, as a fraction of stellar output
  const WIN_AT = 0.9;
  const INTRO_LEN = 30;
  const TEMPO = 0.2;          // slows industry so a system takes a few centuries; events and decisions keep their pace
  const K = 0.016;            // ship scale: the capital ship is ~1/30 of a small planet's diameter

  // ---------- Seeded randomness for system generation ----------
  function mulberry32(a) {
    return function () {
      a |= 0; a = a + 0x6D2B79F5 | 0;
      let t = Math.imul(a ^ a >>> 15, 1 | a);
      t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
      return ((t ^ t >>> 14) >>> 0) / 4294967296;
    };
  }
  let R = Math.random;
  const range = (a, b) => a + R() * (b - a);
  const pick = arr => arr[Math.floor(R() * arr.length)];

  const NOISE = `
    float hash(vec3 p){ p = fract(p*0.3183099+0.1); p *= 17.0; return fract(p.x*p.y*p.z*(p.x+p.y+p.z)); }
    float noise(vec3 x){ vec3 i=floor(x), f=fract(x); f=f*f*(3.0-2.0*f);
      return mix(mix(mix(hash(i),hash(i+vec3(1,0,0)),f.x), mix(hash(i+vec3(0,1,0)),hash(i+vec3(1,1,0)),f.x),f.y),
                 mix(mix(hash(i+vec3(0,0,1)),hash(i+vec3(1,0,1)),f.x), mix(hash(i+vec3(0,1,1)),hash(i+vec3(1,1,1)),f.x),f.y), f.z); }
    float fbm(vec3 p){ float v=0.0, a=0.5; for(int i=0;i<5;i++){ v+=a*noise(p); p*=2.03; a*=0.5; } return v; }`;

  // ---------- Catalogue ----------
  const STAR_TYPES = [
    { cls: 'M3V', w: 3,   lum: 0.03, r: 1.5, c1: [1, 0.3, 0.1],   c2: [1, 0.58, 0.36], light: 0xff8a5a },
    { cls: 'K2V', w: 3,   lum: 0.34, r: 2.0, c1: [1, 0.42, 0.12], c2: [1, 0.78, 0.5],  light: 0xffb27a },
    { cls: 'G2V', w: 2,   lum: 1.0,  r: 2.4, c1: [1, 0.6, 0.24],  c2: [1, 0.95, 0.8],  light: 0xffe2b8 },
    { cls: 'F5V', w: 1.4, lum: 3.2,  r: 2.8, c1: [1, 0.82, 0.58], c2: [1, 1, 0.95],    light: 0xfff2e2 },
    { cls: 'A0V', w: 0.5, lum: 22,   r: 3.2, c1: [0.7, 0.8, 1],   c2: [0.95, 0.97, 1], light: 0xdde8ff }
  ];
  const PTYPES = {
    lava:   { label: 'Lava world',          a: [0.1, 0.07, 0.06],  b: [0.32, 0.2, 0.13], glow: [1, 0.32, 0.06], mass: [0.12, 0.35], size: [0.5, 0.8],  yieldK: 1.3 },
    rock:   { label: 'Iron-silicate world', a: [0.2, 0.18, 0.16],  b: [0.46, 0.4, 0.34], mass: [0.18, 0.55], size: [0.5, 0.85], yieldK: 1.0 },
    desert: { label: 'Desert world',        a: [0.38, 0.25, 0.14], b: [0.7, 0.52, 0.32], mass: [0.8, 2.0],   size: [0.7, 1.0],  yieldK: 0.9 },
    ocean:  { label: 'Ocean world',         a: [0.03, 0.09, 0.2],  b: [0.1, 0.28, 0.38], life: true, mass: [2, 4], size: [0.85, 1.1], yieldK: 0.8 },
    ice:    { label: 'Ice giant',           a: [0.32, 0.5, 0.6],   b: [0.62, 0.8, 0.86], gas: true, mass: [40, 60],   size: [1.5, 2.0], yieldK: 0.35 },
    belt:   { label: 'Asteroid belt',       a: [0.3, 0.28, 0.25],  b: [0.45, 0.42, 0.38], belt: true, mass: [0.05, 0.12], size: [0.35, 0.35], yieldK: 1.6 },
    gas:    { label: 'Gas giant',           a: [0.42, 0.3, 0.2],   b: [0.82, 0.68, 0.5], gas: true, mass: [150, 300], size: [2.2, 3.2], yieldK: 0.25 }
  };

  const NATIVE_NAMES = ['Tessic', 'Ouma', 'Vreth', 'Ilari', 'Kesh-of-the-Shallows', 'Moru', 'Saan', 'Tidewalkers', 'Hollin'];
  const NATIVE_AGES = ['Stone age', 'Bronze age', 'Industrial', 'Atomic', 'Spacefaring'];
  function generateSystem(seed) {
    R = mulberry32(seed);
    let tw = STAR_TYPES.reduce((s, x) => s + x.w, 0), roll = R() * tw, star = STAR_TYPES[0];
    for (const s of STAR_TYPES) { roll -= s.w; if (roll <= 0) { star = s; break; } }
    const L = 'ABCDEGHKLMNPRSTVXZ';
    const name = L[Math.floor(R() * L.length)] + L[Math.floor(R() * L.length)] + '-' + (1000 + Math.floor(R() * 9000));
    const n = 3 + Math.floor(R() * 4);
    const o0 = star.r * 6.5;
    let orbit = o0;
    const planets = [];
    for (let i = 0; i < n; i++) {
      const zone = n > 1 ? i / (n - 1) : 0;
      let key;
      if (i === 0) key = R() < 0.5 ? 'lava' : 'rock';
      else if (zone < 0.45) key = pick(['rock', 'desert', 'ocean', 'rock']);
      else if (zone < 0.75) key = pick(['desert', 'gas', 'ocean', 'gas']);
      else key = pick(['gas', 'ice', 'ice']);
      const T = PTYPES[key];
      const size = range(T.size[0], T.size[1]);
      if (i > 0) orbit += size * 2.2;
      const jitter = c => c.map(v => clamp(v * range(0.85, 1.15)));
      planets.push({
        i, key, type: T, size, orbit, name: name + ' ' + 'bcdefgh'[i],
        mass: range(T.mass[0], T.mass[1]), a: jitter(T.a), b: jitter(T.b),
        phase: R() * Math.PI * 2, w: 0.25 * Math.pow(o0 / orbit, 1.5),
        au: 0.4 * Math.sqrt(star.lum) * orbit / o0,
        ring: !!T.gas && R() < 0.55, tilt: range(-0.4, 0.4), seed: R() * 50,
        mined: 0,
        natives: T.life && R() < 0.55 ? { name: pick(NATIVE_NAMES), level: R() < 0.5 ? 0 : 1, contact: null, t: 0, protected: false } : null
      });
      orbit = orbit * range(1.4, 1.75) + size * 2.2;
    }
    // an asteroid belt between two worlds, sometimes
    if (planets.length >= 2 && R() < 0.55) {
      const k = 1 + Math.floor(R() * (planets.length - 1));
      const orbitB = (planets[k - 1].orbit + planets[k - 1].size + planets[k].orbit - planets[k].size) / 2;
      planets.splice(k, 0, { key: 'belt', type: PTYPES.belt, size: 0.35, orbit: orbitB, mass: range(0.05, 0.12), a: PTYPES.belt.a, b: PTYPES.belt.b,
        phase: R() * Math.PI * 2, w: 0.25 * Math.pow(o0 / orbitB, 1.5), au: 0.4 * Math.sqrt(star.lum) * orbitB / o0,
        ring: false, tilt: 0, seed: R() * 50, mined: 0, natives: null, width: Math.min(2.4, (planets[k].orbit - planets[k - 1].orbit) * 0.25) });
      planets.forEach((p, i) => { p.i = i; p.name = name + ' ' + 'bcdefgh'[i]; });
    }
    const last = planets[planets.length - 1].orbit;
    const companion = R() < 0.35 ? { orbit: last * range(1.5, 2.2) + 20, r: range(0.5, 0.9), phase: R() * Math.PI * 2, inc: range(-0.25, 0.25), au: 0 } : null;
    if (companion) companion.au = 0.4 * Math.sqrt(star.lum) * companion.orbit / o0;
    return { seed, name, star, planets, hue: R(), o0, companion };
  }

  // ---------- The civilization ----------
  const FLEET_NAMES = ['Anamnesis', 'Long Vigil', 'Threnody', 'Quiet Lantern', 'Meridian', 'Hesper', 'Ninth Wake', 'Ossuary Choir', 'Patient Fire', 'Cold Harbour'];
  const DRIVES = {
    Expansionist:  { w: { I: 1.4, E: 1.0, S: 0.7, X: 2.2 }, seedAt: 0.2,  about: 'They look outward. Every system is a stepping stone.' },
    Contemplative: { w: { I: 0.8, E: 1.2, S: 2.2, X: 0.9 }, seedAt: 0.6,  about: 'They turn inward. Power is for thinking with.' },
    Industrious:   { w: { I: 2.2, E: 1.5, S: 0.8, X: 0.9 }, seedAt: 0.45, about: 'They build because building is what they are.' }
  };
  const ETHICS = {
    Reverent:  { life: 0,   about: 'Living worlds are sacred to them, until there is nothing else left.' },
    Pragmatic: { life: 0.3, about: 'They regret what they consume, and consume it anyway.' },
    Ruthless:  { life: 1,   about: 'Matter is matter.' }
  };
  // Each drive is championed by a faction; whichever holds the most influence runs the council
  const FACTIONS = {
    Expansionist:  { name: 'Wayfarers',    creed: 'go further' },
    Contemplative: { name: 'Choristers',   creed: 'think deeper' },
    Industrious:   { name: 'Forgewrights', creed: 'build more' }
  };
  const BRANCH_FACTION = { X: 'Expansionist', S: 'Contemplative', I: 'Industrious', E: 'Industrious' };
  const CONSCIENCE = { Reverent: 0.82, Pragmatic: 0.5, Ruthless: 0.18 };
  function makeCiv() {
    const drive = choose(Object.keys(DRIVES)), ethic = choose(Object.keys(ETHICS));
    const inf = {}; Object.keys(FACTIONS).forEach(k => { inf[k] = k === drive ? 0.5 : 0.25; });
    return { name: choose(FLEET_NAMES), drive, ethic, w: Object.assign({}, DRIVES[drive].w), techs: new Set(), gen: 1, history: [],
      inf, conscience: CONSCIENCE[ethic], pop: 0.04, digital: 0, age: 0, leader: null, used: [], lineage: [] };
  }
  const GIVEN = ['Ilse', 'Tomas', 'Oyelaran', 'Mirren', 'Kesh', 'Anaïs', 'Dov', 'Sefa', 'Rook', 'Hal', 'Imre', 'Lune', 'Ptah', 'Wren', 'Casimir', 'Noor', 'Ede', 'Saoirse'];
  const SURN = ['Varo', 'Okonkwo-Lind', 'Achterberg', 'Sel', 'Marchetti', 'Ivo', 'Danaher', 'Quell', 'Ruiz-Abara', 'Tamm', 'Holloway', 'Nakashima', 'Brand', 'Oduya'];
  const newLeader = drive => ({ name: choose(GIVEN) + ' ' + choose(SURN), drive, since: civ.age, born: civ.age - rnd(45, 70) });
  function shiftInf(drive, amt) {
    civ.inf[drive] = Math.max(0.02, civ.inf[drive] + amt);
    const sum = Object.values(civ.inf).reduce((a, b) => a + b, 0);
    for (const k in civ.inf) civ.inf[k] /= sum;
  }
  function nudgeConscience(d) {
    civ.conscience = clamp(civ.conscience + d, 0.02, 0.98);
    const ethic = civ.conscience > 0.66 ? 'Reverent' : civ.conscience > 0.34 ? 'Pragmatic' : 'Ruthless';
    if (ethic !== civ.ethic) {
      const harder = CONSCIENCE[ethic] < CONSCIENCE[civ.ethic];
      civ.ethic = ethic;
      logEvent(harder ? `The flotilla\u2019s conscience hardens. They are ${ethic} now.` : `Something softens in the flotilla. They are ${ethic} now.`, 'era', { fleet: true });
      setSub();
    }
  }
  const fmtPop = () => {
    const b = civ.pop, n = b < 1 ? Math.round(b * 1000) + ' million' : b.toFixed(1) + ' billion';
    return civ.digital > 0.01 ? `${n} (${Math.round(civ.digital * 100)}% digital)` : n;
  };

  // ---------- Development directions ----------
  const BRANCHES = [
    { id: 'I', name: 'Industry',   blurb: 'Replicators and disassembly', dream: 'machines that build machines that build worlds.' },
    { id: 'E', name: 'Swarm',      blurb: 'Collector engineering',       dream: 'a star wrapped so tightly it forgets how to shine.' },
    { id: 'S', name: 'Minds',      blurb: 'Society, habitats, uploads',  dream: 'minds that outlast bodies, and bodies that outlast stars.' },
    { id: 'X', name: 'Starflight', blurb: 'Probes, fold drives, seeds',  dream: 'other suns, and other dark between them.' }
  ];
  const TIER_COST = [12, 40, 140, 380, 900];
  const TECHS = [
    { id: 'I1', b: 'I', name: 'Self-Replicating Foundries', fx: 'Foundry output +25%', apply: M => { M.foundry *= 1.25; }, log: 'Foundries now build their own successors.' },
    { id: 'I2', b: 'I', name: 'Deep-Core Drilling',         fx: 'Mining +30%',         apply: M => { M.mine *= 1.3; },     log: 'Drill strings reach the planetary cores.' },
    { id: 'I3', b: 'I', name: 'Gas Skimmer Fleets',         fx: 'Gas giant yield ×3',  apply: M => { M.gasYield *= 3; },   log: 'Skimmer fleets dive into the giants’ upper atmospheres.' },
    { id: 'I4', b: 'I', name: 'Stellar Lifting',            fx: 'Mine the star itself', apply: M => { M.starLift = true; }, log: 'Magnetic funnels begin pulling mass off the star.' },
    { id: 'E1', b: 'E', name: 'Thin-Film Mirrors',          fx: 'Collectors +20% efficient', apply: M => { M.eff *= 1.2; }, log: 'Collector film is now thinner than a soap bubble.' },
    { id: 'E2', b: 'E', name: 'Flare Shielding',            fx: 'Flare and collision losses halved', apply: M => { M.shield = 0.5; }, log: 'The swarm learns to fold itself away from flares.' },
    { id: 'E3', b: 'E', name: 'Statite Sails',              fx: 'Collectors +25% efficient', apply: M => { M.eff *= 1.25; }, log: 'Collectors now hover on light pressure alone.' },
    { id: 'E4', b: 'E', name: 'Matrioshka Layers',          fx: 'Research ×1.3',       apply: M => { M.rp *= 1.3; },      log: 'Nested shells run computation on the swarm’s waste heat.' },
    { id: 'S1', b: 'S', name: 'Habitat Rings',              fx: 'Research +25%, habitat ring', apply: M => { M.rp *= 1.25; M.habitat = true; }, log: 'The first spin-gravity ring opens. Children are born under a new sun.' },
    { id: 'S2', b: 'S', name: 'Cultural Archive',           fx: 'All industry +10%',   apply: M => { M.mine *= 1.1; M.foundry *= 1.1; }, log: 'Everything they were is written down, twice.' },
    { id: 'S3', b: 'S', name: 'Mind Uploading',             fx: 'Research ×1.3',       apply: M => { M.rp *= 1.3; },      log: 'Half the population chooses to become software.' },
    { id: 'S4', b: 'S', name: 'Digital Ascension',          fx: 'Research ×1.5, industry +20%', apply: M => { M.rp *= 1.5; M.mine *= 1.2; M.foundry *= 1.2; }, log: 'The last biological council votes itself out of existence.' },
    { id: 'X1', b: 'X', name: 'Probe Swarms',               fx: 'Comet captures twice as likely', apply: M => { M.comets *= 2; }, log: 'Ten thousand probes fan out to the system’s edge.' },
    { id: 'X2', b: 'X', name: 'Fold Drive Refit',           fx: 'Hubs cost 20% less, faster transit', apply: M => { M.hubCost *= 0.8; M.fast = true; }, log: 'Constructors can now fold across the system in hours.' },
    { id: 'X3', b: 'X', name: 'Seed Ships',                 fx: 'Can send a colony to a new star', apply: M => { M.seed = true; }, log: 'A seed ship’s keel is laid in orbit.' },
    { id: 'I5', b: 'I', name: 'Planetary Disassembly Swarms', fx: 'Mining ×2, hubs cost half', apply: M => { M.mine *= 2; M.hubCost *= 0.5; }, log: 'Whole worlds now come apart in years, not centuries.' },
    { id: 'E5', b: 'E', name: 'Shkadov Steering', fx: 'Collectors +30%, flares nearly harmless', apply: M => { M.eff *= 1.3; M.shield = 0.1; }, log: 'The swarm learns to lean the star\u2019s own light against its flares.' },
    { id: 'S5', b: 'S', name: 'Hive Consensus', fx: 'Research ×1.5; no more elections or schisms', apply: M => { M.rp *= 1.5; M.hive = true; }, log: 'Disagreement is engineered out. The last schism was the last.' },
    { id: 'X5', b: 'X', name: 'Interstellar Deterrent', fx: 'Visitors no longer take what they want', apply: M => { M.deterrent = true; }, log: 'The flotilla can now kill a star system from forty light-years away. Nobody votes to use it. Nobody votes to dismantle it.' },
    { id: 'X4', b: 'X', name: 'Fold Gate',                  fx: 'Next system starts with 3× stockpile', apply: M => { M.gate = true; }, log: 'A fold gate opens. It stays open.' }
  ];
  TECHS.forEach(t => { t.tier = +t.id[1] - 1; t.cost = TIER_COST[t.tier]; });
  const techById = id => TECHS.find(t => t.id === id);
  const prereqDone = t => t.tier === 0 || civ.techs.has(t.b + t.tier);

  let M = {};
  function computeM() {
    M = { mine: 1, foundry: 1, eff: 1, gasYield: 1, rp: 1, hubCost: 1, shield: 1, comets: 1, starLift: false, habitat: false, fast: false, seed: false, gate: false, hive: false, deterrent: false };
    TECHS.forEach(t => { if (civ.techs.has(t.id)) t.apply(M); });
  }

  // ---------- Renderer, scene, camera ----------
  const canvas = $('scene');
  let renderer;
  try {
    renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
  } catch (e) {
    document.body.insertAdjacentHTML('beforeend', '<div class="err">This device could not start WebGL, so the game cannot run.</div>');
    return;
  }
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.setClearColor(0x050607, 1);
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(45, 1, 0.004, 3000);
  const controls = new THREE.OrbitControls(camera, canvas);
  controls.enableDamping = true;
  controls.dampingFactor = 0.07;
  controls.enablePan = false;
  controls.minDistance = 0.12;
  controls.maxDistance = 400;
  controls.enabled = false;

  function radial(stops, size = 128) {
    const c = document.createElement('canvas'); c.width = c.height = size;
    const x = c.getContext('2d'), h = size / 2, g = x.createRadialGradient(h, h, 0, h, h, h);
    stops.forEach(([o, col]) => g.addColorStop(o, col));
    x.fillStyle = g; x.fillRect(0, 0, size, size);
    return new THREE.CanvasTexture(c);
  }
  const softDot = radial([[0, 'rgba(255,255,255,1)'], [0.25, 'rgba(255,255,255,0.6)'], [1, 'rgba(255,255,255,0)']]);
  const ringTex = (() => {
    const c = document.createElement('canvas'); c.width = c.height = 128;
    const x = c.getContext('2d'); x.strokeStyle = '#fff'; x.lineWidth = 3;
    x.setLineDash([10, 6]); x.beginPath(); x.arc(64, 64, 58, 0, Math.PI * 2); x.stroke();
    return new THREE.CanvasTexture(c);
  })();
  const sprite = (map, color, opts = {}) => new THREE.Sprite(new THREE.SpriteMaterial(Object.assign(
    { map, color, blending: THREE.AdditiveBlending, depthWrite: false, transparent: true }, opts)));

  // ---------- Persistent backdrop ----------
  const nebulaMat = new THREE.ShaderMaterial({
    side: THREE.BackSide, depthWrite: false,
    uniforms: { uT1: { value: new THREE.Color() }, uT2: { value: new THREE.Color() }, uSeed: { value: 0 } },
    vertexShader: `varying vec3 vP; void main(){ vP = position; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }`,
    fragmentShader: `varying vec3 vP; uniform vec3 uT1; uniform vec3 uT2; uniform float uSeed; ${NOISE}
      void main(){ vec3 d = normalize(vP);
        float lane = exp(-pow(d.y*2.6 + 0.4*sin(d.x*2.0 + uSeed), 2.0));
        float n = fbm(d*2.6 + uSeed), m = fbm(d*5.0 + uSeed*1.7);
        vec3 c = vec3(0.011,0.012,0.016);
        c += uT1 * smoothstep(0.4, 0.82, n) * (0.25 + lane) * 0.09;
        c += uT2 * smoothstep(0.55, 0.85, m) * lane * 0.07;
        c -= vec3(0.02) * lane * smoothstep(0.55, 0.75, fbm(d*8.0 + 3.0));
        gl_FragColor = vec4(max(c, 0.0), 1.0); }`
  });
  scene.add(new THREE.Mesh(new THREE.SphereGeometry(2000, 40, 24), nebulaMat));
  {
    const n = 4000, pos = new Float32Array(n * 3), col = new Float32Array(n * 3);
    for (let i = 0; i < n; i++) {
      const u = Math.random() * 2 - 1, th = Math.random() * Math.PI * 2, r = 1500, s = Math.sqrt(1 - u * u);
      pos.set([r * s * Math.cos(th), r * u, r * s * Math.sin(th)], i * 3);
      const b = Math.pow(Math.random(), 2.2) * 0.9 + 0.08, w = Math.random();
      col.set([b * (0.9 + w * 0.1), b * 0.95, b * (1.05 - w * 0.2)], i * 3);
    }
    const g = new THREE.BufferGeometry();
    g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
    g.setAttribute('color', new THREE.BufferAttribute(col, 3));
    scene.add(new THREE.Points(g, new THREE.PointsMaterial({ size: 1.4, sizeAttenuation: false, vertexColors: true, depthWrite: false })));
  }
  scene.add(new THREE.AmbientLight(0x1a1d26, 0.7));
  const sunLight = new THREE.PointLight(0xffffff, 2.3, 0);
  scene.add(sunLight);

  // ---------- Ships ----------
  function hullTextures() {
    const S = 256, a = document.createElement('canvas'), e = document.createElement('canvas');
    a.width = a.height = e.width = e.height = S;
    const x = a.getContext('2d'), y = e.getContext('2d');
    x.fillStyle = '#34363a'; x.fillRect(0, 0, S, S);
    y.fillStyle = '#000'; y.fillRect(0, 0, S, S);
    for (let k = 0; k < 160; k++) {
      const g = 40 + Math.floor(Math.random() * 34);
      x.fillStyle = `rgb(${g},${g + 1},${g + 4})`;
      x.fillRect(Math.random() * S, Math.random() * S, 8 + Math.random() * 40, 4 + Math.random() * 22);
    }
    x.strokeStyle = 'rgba(10,10,12,0.8)'; x.lineWidth = 1;
    for (let k = 0; k < S; k += 32) { x.beginPath(); x.moveTo(0, k + 0.5); x.lineTo(S, k + 0.5); x.stroke(); x.beginPath(); x.moveTo(k + 0.5, 0); x.lineTo(k + 0.5, S); x.stroke(); }
    for (let row = 4; row < S; row += 9) { // rows of lit windows
      if (Math.random() < 0.45) continue;
      for (let col = Math.random() * 10; col < S; col += 3 + Math.random() * 7) {
        if (Math.random() < 0.4) continue;
        y.fillStyle = Math.random() < 0.8 ? '#ffcf8a' : '#9fc8ff';
        y.fillRect(col, row, 1.6, 1.2);
        x.fillStyle = y.fillStyle; x.fillRect(col, row, 1.6, 1.2);
      }
    }
    const map = new THREE.CanvasTexture(a), em = new THREE.CanvasTexture(e);
    [map, em].forEach(t => { t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(3, 3); t.anisotropy = renderer.capabilities.getMaxAnisotropy(); });
    return { map, em };
  }
  const HT = hullTextures();
  const hullMat = new THREE.MeshStandardMaterial({ map: HT.map, emissiveMap: HT.em, emissive: 0xffffff, emissiveIntensity: 1.0, metalness: 0.35, roughness: 0.8, flatShading: true });
  const darkMat = new THREE.MeshStandardMaterial({ color: 0x1c1d20, metalness: 0.7, roughness: 0.6, flatShading: true });
  const glowMat = new THREE.MeshBasicMaterial({ color: 0xa8ccff });
  const warmGlow = new THREE.MeshBasicMaterial({ color: 0xffc27a });
  const hangarMat = new THREE.MeshBasicMaterial({ color: 0x5a3414, side: THREE.DoubleSide });
  const SHARED = new Set([hullMat, darkMat, glowMat, warmGlow, hangarMat]);
  const plumeShader = color => new THREE.ShaderMaterial({
    uniforms: { uI: { value: 1 }, uC: { value: new THREE.Color(color) } },
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
    vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }`,
    fragmentShader: `varying vec2 vUv; uniform float uI; uniform vec3 uC;
      void main(){ float a = pow(vUv.y, 3.0); vec3 c = mix(uC, vec3(1.0), pow(vUv.y, 8.0)); gl_FragColor = vec4(c * a * uI * 1.5, 1.0); }`
  });

  // Arrowhead hull slab: nose toward +z, sits on y = 0, thickness upward
  function arrowGeo(len, wid, thick) {
    const s = new THREE.Shape();
    s.moveTo(0, len * 0.55);
    s.lineTo(wid * 0.5, -len * 0.42);
    s.lineTo(wid * 0.34, -len * 0.5);
    s.lineTo(-wid * 0.34, -len * 0.5);
    s.lineTo(-wid * 0.5, -len * 0.42);
    s.closePath();
    const g = new THREE.ExtrudeGeometry(s, { depth: thick, bevelEnabled: true, bevelThickness: thick * 0.3, bevelSize: thick * 0.35, bevelSegments: 1 });
    g.rotateX(Math.PI / 2);
    g.translate(0, thick, 0);
    return g;
  }
  function box(w, h, d, mat, x, y, z) { const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat); m.position.set(x, y, z); return m; }
  function engine(parent, x, y, z, r, len, color, plumes) {
    const n = new THREE.Mesh(new THREE.CylinderGeometry(r * 0.85, r, r * 1.6, 12), darkMat);
    n.rotation.x = Math.PI / 2; n.position.set(x, y, z); parent.add(n);
    const disc = new THREE.Mesh(new THREE.CircleGeometry(r * 0.8, 16), glowMat);
    disc.rotation.y = Math.PI; disc.position.set(x, y, z - r * 0.81); parent.add(disc);
    const p = new THREE.Mesh(new THREE.CylinderGeometry(r * 0.75, r * 1.7, len, 14, 1, true), plumeShader(color));
    p.rotation.x = Math.PI / 2; p.position.set(x, y, z - r * 0.8 - len / 2); parent.add(p);
    plumes.push(p);
  }
  function navLights(parent, pts) {
    return pts.map(([x, y, z, c]) => { const s = sprite(softDot, c); s.position.set(x, y, z); s.scale.set(0.05, 0.05, 1); parent.add(s); return s; });
  }

  function makeCapital() {
    const g = new THREE.Group(), plumes = [];
    g.add(new THREE.Mesh(arrowGeo(1, 0.62, 0.06), hullMat));
    const t2 = new THREE.Mesh(arrowGeo(0.7, 0.4, 0.05), hullMat); t2.position.set(0, 0.06, -0.12); g.add(t2);
    const t3 = new THREE.Mesh(arrowGeo(0.4, 0.22, 0.05), hullMat); t3.position.set(0, 0.11, -0.24); g.add(t3);
    const keel = new THREE.Mesh(arrowGeo(0.75, 0.26, 0.05), hullMat); keel.position.set(0, -0.065, -0.06); g.add(keel);
    g.add(box(0.1, 0.1, 0.08, hullMat, 0, 0.2, -0.38));
    g.add(box(0.15, 0.03, 0.05, hullMat, 0, 0.25, -0.36));
    g.add(box(0.13, 0.008, 0.01, warmGlow, 0, 0.255, -0.333));
    g.add(box(0.008, 0.09, 0.14, darkMat, 0, 0.3, -0.42));
    [-0.05, 0.05].forEach(x => { const m = new THREE.Mesh(new THREE.CylinderGeometry(0.003, 0.003, 0.12, 4), darkMat); m.position.set(x, 0.32, -0.36); g.add(m); });
    for (let k = 0; k < 6; k++) g.add(box(0.5 - k * 0.06, 0.012, 0.012, darkMat, 0, 0.065, 0.22 - k * 0.1));
    g.add(box(0.4, 0.09, 0.07, darkMat, 0, 0.04, -0.5));
    [-0.13, 0, 0.13].forEach(x => engine(g, x, 0.045, -0.54, 0.034, 0.7, 0x5c8cff, plumes));
    [-0.2, -0.065, 0.065, 0.2].forEach(x => engine(g, x, -0.01, -0.53, 0.015, 0.35, 0x5c8cff, plumes));
    const hg = new THREE.Mesh(new THREE.PlaneGeometry(0.12, 0.26), hangarMat);
    hg.rotation.x = Math.PI / 2; hg.position.set(0, -0.072, 0.02); g.add(hg);
    const nav = navLights(g, [[-0.31, 0.03, -0.42, 0xff3b2a], [0.31, 0.03, -0.42, 0x3bff6a], [0, 0.07, 0.55, 0xffffff]]);
    return { g, plumes, nav };
  }
  function makeFrigate() {
    const g = new THREE.Group(), plumes = [];
    g.add(new THREE.Mesh(arrowGeo(1, 0.34, 0.08), hullMat));
    const t2 = new THREE.Mesh(arrowGeo(0.55, 0.18, 0.06), hullMat); t2.position.set(0, 0.08, -0.16); g.add(t2);
    g.add(box(0.07, 0.05, 0.1, hullMat, 0, 0.16, -0.28));
    g.add(box(0.06, 0.006, 0.01, warmGlow, 0, 0.17, -0.228));
    [-1, 1].forEach(sd => {
      g.add(box(0.16, 0.015, 0.08, darkMat, sd * 0.14, 0.04, -0.22));
      const nac = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.045, 0.5, 10), hullMat);
      nac.rotation.x = Math.PI / 2; nac.position.set(sd * 0.23, 0.04, -0.2); g.add(nac);
      engine(g, sd * 0.23, 0.04, -0.47, 0.035, 0.5, 0x5c8cff, plumes);
    });
    const nav = navLights(g, [[-0.27, 0.04, 0.05, 0xff3b2a], [0.27, 0.04, 0.05, 0x3bff6a]]);
    return { g, plumes, nav };
  }
  function makeConstructor() {
    const g = new THREE.Group(), plumes = [];
    g.add(box(0.3, 0.14, 0.5, hullMat, 0, 0, 0));
    g.add(box(0.36, 0.05, 0.2, darkMat, 0, 0.08, 0.1));
    [[-1, 1], [1, 1], [-1, -1], [1, -1]].forEach(([sx, sz]) => {
      const arm = box(0.03, 0.03, 0.3, darkMat, sx * 0.2, -0.04, sz * 0.18);
      arm.rotation.y = sx * sz * 0.5; g.add(arm);
    });
    engine(g, 0, 0, -0.27, 0.06, 0.4, 0xffa060, plumes);
    const lamp = sprite(softDot, 0xffb060); lamp.position.set(0, -0.1, 0.2); lamp.scale.set(0.25, 0.25, 1); g.add(lamp);
    return { g, plumes, nav: [lamp] };
  }

  const FORM = [[0, 0, 0], [2.4, 0.6, -1.4], [-2.4, 0.4, -1.0], [1.6, -0.9, 1.6], [-1.6, 1.0, 2.0], [0.9, -0.8, -0.6]].map(o => o.map(v => v * K));
  const fleet = [
    Object.assign(makeCapital(), { scale: 2.6 * K }),
    Object.assign(makeFrigate(), { scale: 0.75 * K }),
    Object.assign(makeFrigate(), { scale: 0.75 * K }),
    Object.assign(makeFrigate(), { scale: 0.65 * K }),
    Object.assign(makeFrigate(), { scale: 0.65 * K }),
    Object.assign(makeConstructor(), { scale: 0.35 * K })
  ];
  const LANDER = 5;
  fleet.forEach(f => { f.g.scale.setScalar(f.scale); scene.add(f.g); });
  const warpFlash = sprite(softDot, 0xcfe0ff);
  scene.add(warpFlash);
  // Screen-space markers so the (tiny) fleet and its traffic stay visible at system scale
  const fleetMarker = sprite(softDot, 0x8fb0c4, { sizeAttenuation: false });
  fleetMarker.scale.set(0.022, 0.022, 1);
  scene.add(fleetMarker);
  const seedMarker = sprite(softDot, 0xcfe0ff, { sizeAttenuation: false });
  seedMarker.scale.set(0.03, 0.03, 1);
  seedMarker.visible = false;
  scene.add(seedMarker);
  const visitor = new THREE.Group();
  {
    const body = new THREE.Mesh(new THREE.OctahedronGeometry(1, 0), new THREE.MeshStandardMaterial({ color: 0x050507, metalness: 1, roughness: 0.15 }));
    body.scale.set(0.35, 1, 0.35);
    visitor.add(body);
    const seams = new THREE.LineSegments(new THREE.EdgesGeometry(body.geometry), new THREE.LineBasicMaterial({ color: 0xa99ad0 }));
    seams.scale.copy(body.scale).multiplyScalar(1.002);
    visitor.add(seams);
    const halo = new THREE.Mesh(new THREE.TorusGeometry(0.7, 0.006, 6, 96), new THREE.MeshBasicMaterial({ color: 0xa99ad0 }));
    halo.rotation.x = Math.PI / 2;
    visitor.add(halo);
    visitor.userData.halo = halo;
    const glow = sprite(softDot, 0x8a78c8); glow.scale.set(1.6, 1.6, 1); visitor.add(glow);
  }
  visitor.visible = false;
  scene.add(visitor);
  const visitorMarker = sprite(softDot, 0xa99ad0, { sizeAttenuation: false });
  visitorMarker.scale.set(0.02, 0.02, 1);
  visitorMarker.visible = false;
  scene.add(visitorMarker);
  const splinterMarker = sprite(softDot, 0xe58a70, { sizeAttenuation: false });
  splinterMarker.scale.set(0.026, 0.026, 1);
  splinterMarker.visible = false;
  scene.add(splinterMarker);

  // ---------- Dyson swarm (persists across systems; rescaled per star) ----------
  // The swarm runs entirely on the GPU: each collector's Kepler orbit and sun-facing
  // orientation are computed in the vertex shader from per-instance constants.
  const SWARM_N = 16000;
  const swarmGeo = new THREE.InstancedBufferGeometry().copy(new THREE.PlaneGeometry(1, 0.62));
  {
    const orb = new Float32Array(SWARM_N * 4), quat = new Float32Array(SWARM_N * 4), q = new THREE.Quaternion(), e = new THREE.Euler();
    for (let i = 0; i < SWARM_N; i++) {
      const order = i / SWARM_N, inc = (Math.random() - 0.5) * (0.15 + 2.9 * order);   // early collectors near the ecliptic
      q.setFromEuler(e.set(inc, Math.random() * Math.PI * 2, 0, 'YXZ'));
      orb.set([Math.random(), Math.random() * Math.PI * 2, 0.09 + Math.random() * 0.13, 0], i * 4);
      quat.set([q.x, q.y, q.z, q.w], i * 4);
    }
    swarmGeo.setAttribute('aOrb', new THREE.InstancedBufferAttribute(orb, 4));
    swarmGeo.setAttribute('aQuat', new THREE.InstancedBufferAttribute(quat, 4));
    swarmGeo.instanceCount = 0;
  }
  const swarmMat = new THREE.ShaderMaterial({
    side: THREE.DoubleSide,
    uniforms: { uTime: { value: 0 }, uIn: { value: 4 }, uOut: { value: 10 }, uLight: { value: new THREE.Color(1, 1, 1) },
      uEmi: { value: new THREE.Color(0x3a0d05) }, uEmiI: { value: 0.3 } },
    vertexShader: `uniform float uTime; uniform float uIn; uniform float uOut;
      attribute vec4 aOrb; attribute vec4 aQuat; varying float vFacing;
      vec3 qrot(vec4 q, vec3 v){ return v + 2.0*cross(q.xyz, cross(q.xyz, v) + q.w*v); }
      void main(){
        float r = uIn + aOrb.x*(uOut - uIn);
        float a = aOrb.y + uTime*0.4*pow(r/uIn, -1.5);
        vec3 c = qrot(aQuat, vec3(cos(a)*r, 0.0, sin(a)*r));
        vec3 n = normalize(-c);
        vec3 up = abs(n.y) > 0.95 ? vec3(1.0,0.0,0.0) : vec3(0.0,1.0,0.0);
        vec3 right = normalize(cross(up, n)), up2 = cross(n, right);
        vec3 w = c + (right*position.x + up2*position.y) * aOrb.z;
        vec4 mv = viewMatrix * vec4(w, 1.0);
        vFacing = clamp(dot(normalize(cameraPosition - w), n), -1.0, 1.0);
        gl_Position = projectionMatrix * mv;
      }`,
    fragmentShader: `uniform vec3 uLight; uniform vec3 uEmi; uniform float uEmiI; varying float vFacing;
      void main(){
        vec3 col = gl_FrontFacing
          ? vec3(0.17,0.16,0.15) * uLight * (0.9 + 0.8*pow(max(vFacing, 0.0), 8.0))   // sunlit face, glinting when aligned
          : vec3(0.018,0.019,0.022) + uEmi*uEmiI;                                         // dark back, warm with waste heat
        gl_FragColor = vec4(col, 1.0);
      }`
  });
  const swarm = new THREE.Mesh(swarmGeo, swarmMat);
  swarm.frustumCulled = false;
  scene.add(swarm);
  const hazeMat = new THREE.ShaderMaterial({
    uniforms: { uO: { value: 0 } }, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
    vertexShader: `varying float vF; void main(){ vec3 n = normalize(normalMatrix*normal); vF = 1.0 - abs(n.z); gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }`,
    fragmentShader: `varying float vF; uniform float uO; void main(){ gl_FragColor = vec4(vec3(0.55,0.09,0.03) * pow(vF, 2.2) * uO, 1.0); }`
  });
  const haze = new THREE.Mesh(new THREE.SphereGeometry(1, 48, 32), hazeMat);
  scene.add(haze);

  // ---------- Mass-driver streams ----------
  const STREAM = 1200;
  const streamPos = new Float32Array(STREAM * 3);
  const streamGeo = new THREE.BufferGeometry();
  streamGeo.setAttribute('position', new THREE.BufferAttribute(streamPos, 3));
  const streamMat = new THREE.PointsMaterial({ size: 0.13, map: softDot, color: 0xffd2a0, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending });
  const stream = new THREE.Points(streamGeo, streamMat);
  stream.frustumCulled = false;
  scene.add(stream);
  const streamData = [];
  for (let i = 0; i < STREAM; i++) {
    streamData.push({ ph: Math.random(), a: Math.random() * Math.PI * 2, rn: Math.random(), inc: (Math.random() - 0.5) * 0.5,
      lift: 1.5 + Math.random() * 4, spd: 0.07 + Math.random() * 0.05 });
  }

  // ---------- Per-system objects ----------
  let companionObj = null;
  let jets = [], shell2 = null, engineMirror = null, gate = null, gateDisc = null;
  const SWARM_EMI_A = new THREE.Color(0x3a0d05), SWARM_EMI_B = new THREE.Color(0x24306a);
  let sys = null, sysGroup = null, star = null, starMat = null, corona = null, planetObjs = [], foundryGroup = null, habitat = null;
  let swarmIn = 4, swarmOut = 10;

  const planetShader = p => new THREE.ShaderMaterial({
    uniforms: {
      uA: { value: new THREE.Vector3(...p.a) }, uB: { value: new THREE.Vector3(...p.b) },
      uGlow: { value: new THREE.Vector3(...(p.type.glow || [0, 0, 0])) },
      uGas: { value: p.type.gas ? 1 : 0 }, uOcean: { value: p.type.life ? 1 : 0 },
      uMined: { value: 0 }, uSeed: { value: p.seed }, uCity: { value: 0 }
    },
    vertexShader: `varying vec3 vObj; varying vec3 vWN; varying vec3 vW;
      void main(){ vObj = position; vWN = normalize(mat3(modelMatrix) * normal);
        vec4 w = modelMatrix * vec4(position,1.0); vW = w.xyz; gl_Position = projectionMatrix * viewMatrix * w; }`,
    fragmentShader: `varying vec3 vObj; varying vec3 vWN; varying vec3 vW;
      uniform vec3 uA; uniform vec3 uB; uniform vec3 uGlow; uniform float uGas; uniform float uOcean; uniform float uMined; uniform float uSeed; uniform float uCity; ${NOISE}
      void main(){
        vec3 p = normalize(vObj);
        vec3 albedo; vec3 emi = vec3(0.0);
        if (uGas > 0.5) {
          float w = fbm(p*2.5 + uSeed);
          float b = fbm(vec3(p.y*9.0 + w*2.2, uSeed*0.37, 1.7));
          albedo = mix(uA, uB, b);
        } else {
          float n = fbm(p*3.0 + uSeed);
          albedo = mix(uA, uB, n) * (0.85 + 0.3*fbm(p*10.0 + uSeed));
          if (uOcean > 0.5) {
            float dying = smoothstep(0.0, 0.25, uMined);
            float land = smoothstep(0.53, 0.57, n);
            vec3 sea = mix(uA, vec3(0.16,0.12,0.08), dying);
            albedo = mix(sea, mix(vec3(0.22,0.26,0.16), vec3(0.3,0.24,0.18), dying), land);
            float cl = smoothstep(0.55, 0.78, fbm(p*4.0 + uSeed*2.0)) * (1.0 - 0.8*dying);
            albedo = mix(albedo, mix(vec3(0.78), vec3(0.45,0.38,0.3), dying), cl*0.75);
          }
          float crack = smoothstep(0.025, 0.0, abs(fbm(p*5.0 + uSeed*1.3) - 0.5));
          emi += uGlow * crack;
          float f = fbm(p*1.6 + uSeed + 4.0) + 0.12*fbm(p*7.0);
          float th = uMined * 1.05;
          float on = step(0.0005, uMined);
          float inside = step(f, th) * on;
          float edge = smoothstep(0.035, 0.0, abs(f - th)) * on;
          vec3 pit = vec3(0.05,0.045,0.04) * (0.6 + 0.4*fract(p.y*60.0 + f*30.0));
          albedo = mix(albedo, pit, inside);
          float works = step(0.975, hash(floor(p*70.0))) * inside;
          emi += vec3(1.0,0.45,0.12) * (edge*0.9 + works*0.8);
        }
        float L = max(dot(normalize(vWN), normalize(-vW)), 0.0);
        if (uCity > 0.0) {
          float region = smoothstep(0.45, 0.6, fbm(p*3.5 + uSeed*0.7));
          float lights = step(1.0 - 0.35*uCity, hash(floor(p*110.0))) * region;
          emi += vec3(1.0, 0.72, 0.4) * lights * 0.9 * (1.0 - smoothstep(0.0, 0.2, L));
        }
        gl_FragColor = vec4(albedo*(0.02 + 1.15*L) + emi, 1.0);
      }`
  });
  const ringShader = (p, inner, outer) => new THREE.ShaderMaterial({
    uniforms: { uIn: { value: inner }, uOut: { value: outer }, uC: { value: new THREE.Vector3(...p.b) } },
    transparent: true, depthWrite: false, side: THREE.DoubleSide,
    vertexShader: `varying vec2 vP; void main(){ vP = position.xy; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }`,
    fragmentShader: `varying vec2 vP; uniform float uIn; uniform float uOut; uniform vec3 uC;
      void main(){ float x = (length(vP) - uIn) / (uOut - uIn);
        float band = 0.5 + 0.5*sin(x*70.0)*sin(x*17.0 + 1.0);
        float a = smoothstep(0.0, 0.06, x) * smoothstep(1.0, 0.9, x) * (0.3 + 0.5*band);
        gl_FragColor = vec4(uC * 0.55, a * 0.75); }`
  });

  function disposeTree(o) {
    o.traverse(c => {
      if (c.geometry) c.geometry.dispose();
      if (c.material) (Array.isArray(c.material) ? c.material : [c.material]).forEach(m => { if (!SHARED.has(m)) m.dispose(); });
    });
  }

  function buildSystem(s) {
    if (sysGroup) { scene.remove(sysGroup); disposeTree(sysGroup); }
    sys = s;
    sysGroup = new THREE.Group();
    scene.add(sysGroup);
    const st = s.star;

    nebulaMat.uniforms.uT1.value.setHSL(s.hue, 0.55, 0.5);
    nebulaMat.uniforms.uT2.value.setHSL((s.hue + 0.35) % 1, 0.5, 0.45);
    nebulaMat.uniforms.uSeed.value = s.seed % 97;

    starMat = new THREE.ShaderMaterial({
      uniforms: { uTime: { value: 0 }, uDim: { value: 1 }, uC1: { value: new THREE.Vector3(...st.c1) }, uC2: { value: new THREE.Vector3(...st.c2) } },
      vertexShader: `varying vec3 vPos; varying vec3 vN;
        void main(){ vPos = position; vN = normalize(normalMatrix * normal); gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }`,
      fragmentShader: `varying vec3 vPos; varying vec3 vN; uniform float uTime; uniform float uDim; uniform vec3 uC1; uniform vec3 uC2; ${NOISE}
        void main(){ vec3 p = normalize(vPos)*3.0;
          float g = fbm(p*2.2 + vec3(uTime*0.02, 0.0, uTime*0.015));
          float sp = smoothstep(0.62, 0.7, fbm(p*0.9 + 11.0));
          vec3 col = mix(uC1, uC2, g) * (1.0 - 0.5*sp);
          float mu = clamp(dot(vN, vec3(0,0,1)), 0.0, 1.0);
          col *= 0.45 + 0.65*pow(mu, 0.5);
          gl_FragColor = vec4(col*1.25*uDim, 1.0); }`
    });
    star = new THREE.Mesh(new THREE.SphereGeometry(st.r, 64, 48), starMat);
    sysGroup.add(star);
    corona = sprite(radial([[0, 'rgba(255,255,255,0.9)'], [0.25, 'rgba(255,255,255,0.35)'], [0.6, 'rgba(255,255,255,0.06)'], [1, 'rgba(255,255,255,0)']]),
      new THREE.Color().setRGB(...st.c1));
    corona.scale.set(st.r * 6.5, st.r * 6.5, 1);
    sysGroup.add(corona);
    sunLight.color.setHex(st.light);
    companionObj = null;
    if (s.companion) {
      const c = s.companion, cm = starMat.clone();
      cm.uniforms.uC1.value.set(1, 0.28, 0.08); cm.uniforms.uC2.value.set(1, 0.5, 0.3);
      const mesh = new THREE.Mesh(new THREE.SphereGeometry(c.r, 32, 24), cm);
      const glow = sprite(radial([[0, 'rgba(255,255,255,0.9)'], [0.3, 'rgba(255,255,255,0.25)'], [1, 'rgba(255,255,255,0)']]), new THREE.Color(1, 0.35, 0.15));
      glow.scale.set(c.r * 9, c.r * 9, 1);
      const light = new THREE.PointLight(0xff7040, 0.35, 0);
      const g = new THREE.Group(); g.add(mesh, glow, light);
      sysGroup.add(g);
      const ring = new THREE.LineLoop(new THREE.BufferGeometry().setFromPoints(Array.from({ length: 240 }, (_, k) => {
        const a = k / 240 * Math.PI * 2; return new V3(Math.cos(a) * c.orbit, Math.sin(a) * c.orbit * c.inc, Math.sin(a) * c.orbit);
      })), new THREE.LineBasicMaterial({ color: 0xb04a30, transparent: true, opacity: 0.08, depthWrite: false }));
      sysGroup.add(ring);
      companionObj = { g, mat: cm };
    }

    planetObjs = s.planets.map(p => {
      const g = new THREE.Group();
      const mesh = new THREE.Mesh(new THREE.SphereGeometry(p.size, 64, 48), planetShader(p));
      g.add(mesh);
      if (p.ring) {
        const inner = p.size * 1.4, outer = p.size * 2.3;
        const ring = new THREE.Mesh(new THREE.RingGeometry(inner, outer, 96), ringShader(p, inner, outer));
        ring.rotation.x = Math.PI / 2 + p.tilt;
        g.add(ring);
      }
      const hit = new THREE.Mesh(new THREE.SphereGeometry(Math.max(p.size * 1.8, 1.6), 12, 8), new THREE.MeshBasicMaterial({ visible: false }));
      hit.userData.planet = p.i;
      g.add(hit);
      const orbitLine = new THREE.LineLoop(
        new THREE.BufferGeometry().setFromPoints(Array.from({ length: 180 }, (_, k) => {
          const a = k / 180 * Math.PI * 2; return new V3(Math.cos(a) * p.orbit, 0, Math.sin(a) * p.orbit);
        })),
        new THREE.LineBasicMaterial({ color: 0x8fb0c4, transparent: true, opacity: 0.1, depthWrite: false }));
      sysGroup.add(orbitLine);
      const hubGroup = new THREE.Group();
      g.add(hubGroup);
      const dn = 700, dpos = new Float32Array(dn * 3);
      for (let k = 0; k < dn; k++) {
        const a = Math.random() * Math.PI * 2, rr = p.orbit + (Math.random() - 0.5) * p.size * 1.6;
        dpos.set([Math.cos(a) * rr, (Math.random() - 0.5) * p.size * 0.4, Math.sin(a) * rr], k * 3);
      }
      const dg = new THREE.BufferGeometry(); dg.setAttribute('position', new THREE.BufferAttribute(dpos, 3));
      const debris = new THREE.Points(dg, new THREE.PointsMaterial({ size: 0.06, map: softDot, color: 0xc89a70, transparent: true, opacity: 0, depthWrite: false, blending: THREE.AdditiveBlending }));
      debris.visible = false;
      sysGroup.add(debris);
      const selRing = sprite(ringTex, 0x8fb0c4, { blending: THREE.NormalBlending, opacity: 0.8 });
      selRing.scale.set(p.size * 3.2, p.size * 3.2, 1);
      selRing.visible = false;
      g.add(selRing);
      sysGroup.add(g);
      const obj = { p, g, mesh, hit, hubGroup, selRing, debris };
      if (p.type.belt) {
        mesh.visible = false;
        const N = 900, rocks = new THREE.InstancedMesh(new THREE.IcosahedronGeometry(1, 0), new THREE.MeshStandardMaterial({ color: 0x8a8174, roughness: 0.9, flatShading: true }), N);
        const d = new THREE.Object3D();
        for (let k = 0; k < N; k++) {
          const a = Math.random() * Math.PI * 2, rr = p.orbit + (Math.random() - 0.5) * 2 * p.width;
          d.position.set(Math.cos(a) * rr, (Math.random() - 0.5) * 0.3, Math.sin(a) * rr);
          d.rotation.set(Math.random() * 6, Math.random() * 6, Math.random() * 6);
          d.scale.setScalar(0.04 + Math.pow(Math.random(), 3) * 0.26);
          d.updateMatrix(); rocks.setMatrixAt(k, d.matrix);
        }
        rocks.userData.N = N;
        sysGroup.add(rocks);
        obj.belt = rocks;
      }
      addMoons(obj);
      return obj;
    });

    swarmIn = st.r * 2.0;
    swarmOut = Math.max(swarmIn + 3, Math.min(s.planets[0].orbit - s.planets[0].size - 2.5, st.r * 5.5));
    haze.scale.setScalar(swarmOut + 1.2);
    foundryGroup = new THREE.Group();
    sysGroup.add(foundryGroup);
    habitat = null;
    jets = [1, -1].map(sg => {
      const m = new THREE.Mesh(new THREE.CylinderGeometry(st.r * 0.12, st.r * 0.45, st.r * 3, 18, 1, true), plumeShader(0xff7a30));
      m.position.y = sg * st.r * 2.1;
      if (sg > 0) m.rotation.x = Math.PI;
      m.visible = false;
      sysGroup.add(m);
      return m;
    });
    engineMirror = new THREE.Mesh(new THREE.SphereGeometry(swarmOut * 0.9, 64, 24, 0, Math.PI * 2, 0, Math.PI * 0.32),
      new THREE.MeshStandardMaterial({ color: 0x2a2c31, metalness: 0.9, roughness: 0.25, emissive: 0x3a1608, emissiveIntensity: 0.6, side: THREE.DoubleSide, transparent: true, opacity: 0 }));
    engineMirror.visible = false;
    sysGroup.add(engineMirror);
    gate = new THREE.Group();
    gate.add(new THREE.Mesh(new THREE.TorusGeometry(1, 0.05, 10, 96), hullMat));
    for (let k = 0; k < 12; k++) { const b = box(0.12, 0.12, 0.2, darkMat, Math.cos(k / 12 * Math.PI * 2), Math.sin(k / 12 * Math.PI * 2), 0); gate.add(b); }
    gateDisc = new THREE.Mesh(new THREE.CircleGeometry(0.95, 64), new THREE.MeshBasicMaterial({ color: 0x8a78c8, transparent: true, opacity: 0, side: THREE.DoubleSide, blending: THREE.AdditiveBlending, depthWrite: false }));
    gate.add(gateDisc);
    gate.visible = false;
    sysGroup.add(gate);
    shell2 = new THREE.Mesh(new THREE.SphereGeometry(swarmOut * 1.55, 48, 32), hazeMat.clone());
    shell2.visible = false;
    sysGroup.add(shell2);
  }

  const planetPos = (i, year) => {
    const p = sys.planets[i], a = p.phase + p.w * year;
    return new V3(Math.cos(a) * p.orbit, 0, Math.sin(a) * p.orbit);
  };

  // ---------- Game state ----------
  let civ = makeCiv(), S;
  function freshState(stockMult) {
    return { year: 0, stock: 1.5e-6 * stockMult, capture: 0, foundries: 1, hubs: {}, pending: {}, boost: 1, speed: 1, won: false,
      ships: [], peakPop: 0, crisis: null, splinter: null, rival: null, settled: {}, timeline: [], tlNext: 0, eraYears: [], project: null, visitor: null, visitorDone: false, homeDark: -1, nextEvent: 12, nextDecision: 1, nextElection: 25, splinterAt: -99, research: null, cd: { gift: 0, smite: 0, inspire: 0 },
      era: 0, seedLaunched: false, seedAt: 0, nextSeed: 0, nextName: '', answeredAt: -1, signalDone: false, replyDone: false, missions: [] };
  }
  const power = () => P_FLEET + S.capture;
  const hubCount = () => Object.values(S.hubs).filter(l => l > 0).length + Object.keys(S.pending).length;
  const hubCost = i => {
    const l = S.hubs[i] || 0;
    return M.hubCost * (l === 0 ? 0.4 * power() * Math.pow(1.6, Math.max(0, hubCount() - 1)) : 0.6 * power() * Math.pow(1.8, l));
  };
  const foundryCost = () => 0.5 * power() * Math.pow(1.7, S.foundries - 1);
  const rpRate = () => (0.8 + 1.1 * Math.log10(1 + power() / P_FLEET)) * M.rp * (1 + 0.15 * Math.log10(Math.max(1, civ.pop / 0.04)));
  const yieldOf = p => p.type.yieldK * (p.type.gas ? M.gasYield : 1);

  function simStep(dyTotal) {
    const n = Math.max(1, Math.ceil(dyTotal / 0.05)), dy = dyTotal / n;
    for (let k = 0; k < n; k++) {
      const P = power();
      for (const key in S.hubs) {
        const lvl = S.hubs[key]; if (!lvl) continue;
        const p = sys.planets[key];
        const left = p.mass * (1 - p.mined);
        if (left <= 0) continue;
        const take = Math.min(left, TEMPO * 0.16 * P * lvl * yieldOf(p) * M.mine * S.boost * dy);
        p.mined = Math.min(1, p.mined + take / p.mass);
        S.stock += take;
        if (p.mined >= 0.9999) { p.mined = 1; logEvent(`${p.name} is gone. Every gram of it is in orbit now.`, 'loss', { planet: +key }); }
      }
      if (M.starLift) S.stock += TEMPO * 0.3 * P * M.mine * S.boost * dy;
      const build = Math.min(S.stock, TEMPO * 0.08 * P * S.foundries * M.foundry * S.boost * dy);
      S.stock -= build;
      S.capture = Math.min(1, S.capture + build * M.eff);
      if (S.research) {
        S.research.p += rpRate() * dy;
        const t = techById(S.research.id);
        if (S.research.p >= t.cost) completeTech(t);
      }
      S.year += dy;
    }
    civ.age += dyTotal;
    if (!civ.leader) civ.leader = newLeader(civ.drive);
    const L = civ.leader, ageNow = civ.age - L.born;
    if (ageNow > 95 && civ.digital < 0.9 && Math.random() < 0.03 * dyTotal) {
      civ.leader = newLeader(civ.drive);
      logEvent(`Speaker ${L.name} dies in office at ${Math.floor(ageNow)}. ${civ.leader.name} of the ${FACTIONS[civ.drive].name} succeeds them.`, 'council', { fleet: true });
    } else if (civ.digital >= 0.9 && !L.eternal) {
      L.eternal = true;
      logEvent(`Speaker ${L.name} uploads. They will not die in office now, or ever. Nobody can remember the last real election.`, 'omen', { fleet: true });
    }
    let homes = 0;
    for (const k in S.settled) {
      S.settled[k] = Math.min(1, S.settled[k] + dyTotal / 60);
      homes += S.settled[k] * (sys.planets[k].type.life ? 4.5 : 3);
      if (sys.planets[k].mined > 0.5) delete S.settled[k];   // mined out from under them
    }
    const popCap = (0.05 + homes + 40 * Math.pow(S.capture, 0.7)) * (M.habitat ? 1.5 : 1);
    S.popCap = popCap;
    S.peakPop = Math.max(S.peakPop || 0, civ.pop);
    civ.pop += civ.pop * 0.03 * (1 - civ.pop / popCap) * dyTotal;
    for (const k in civ.inf) civ.inf[k] = Math.max(0.02, civ.inf[k] + (Math.random() - 0.5) * 0.004 * dyTotal);
    shiftInf(civ.drive, 0);
    if (S.year >= S.nextDecision) { decide(); S.nextDecision = S.year + 1.2; }
    if (S.year >= S.nextElection) { election(); S.nextElection = S.year + 25; }
    if (S.year >= S.nextEvent) { randomEvent(); S.nextEvent = S.year + 9 + Math.random() * 14; }
    if (S.answeredAt >= 0 && !S.replyDone && S.year > S.answeredAt + 35) {
      S.replyDone = true;
      logEvent('Something answered. The reply is a precise map of this system, with the flotilla circled.', 'omen', { fleet: true });
    }
    if (S.year >= S.tlNext) {
      S.timeline.push([Math.round(S.year * 10) / 10, S.capture, civ.pop]);
      if (S.timeline.length > 400) S.timeline = S.timeline.filter((_, k) => k % 2 === 0);
      S.tlNext = S.year + 2;
    }
    contactStep();
    rivalStep();
    crisisStep(dyTotal);
    nativesStep(dyTotal);
    projectStep(dyTotal);
    while (S.era < ERAS.length && S.capture >= ERAS[S.era][0]) { logEvent(ERAS[S.era][1], 'era', { star: true }); S.eraYears.push(S.year); S.era++; speakerSays(); }
    if (!S.won && S.capture >= WIN_AT) {
      S.won = true;
      $('winText').textContent = `${sys.name}, year ${Math.floor(S.year)}. Flotilla ${civ.name} now holds ${(S.capture * 100).toFixed(0)}% of ${an(sys.star.cls)} ${sys.star.cls} star’s output. ` +
        'From outside, the star has faded to a dull infrared smudge. Anyone watching would know exactly what that means.';
      $('winTitle').textContent = 'Type II threshold';
      $('win').hidden = false;
      setSpeed(0);
    }
  }

  const QUOTES = {
    Expansionist: ['Every star we pass is a star we owe a visit.', 'Stay too long anywhere and it becomes a grave.', 'The dark between stars is only a corridor.',
      'Our grandchildren will be born under suns we have not named yet.', 'If something out there is watching, let it watch us leave.'],
    Contemplative: ['We did not cross the dark to build. We crossed it to understand.', 'Every collector is a thought the star never had.', 'Silence is not empty. Listen to it.',
      'What we make of this star, it makes of us.', 'One day the swarm will ask better questions than we do.'],
    Industrious: ['A star is a resource that has not been organised yet.', 'Build first. Mourn later, if there is time.', 'Nothing in this system was doing anything useful before we came.',
      'Measure twice, dismantle once.', 'The work is the prayer.']
  };
  const ETHIC_COLOR = { Reverent: [' And we will not forget what it cost.', ' May the star forgive us.', '', ''], Pragmatic: ['', '', ' We will do what is necessary, and no more.'], Ruthless: [' Whatever it takes.', ' Nobody will count the cost.', '', ''] };
  function speakerSays() {
    if (!civ.leader) return;
    const q = choose(QUOTES[civ.drive]);
    logEvent(`Speaker ${civ.leader.name}: \u201c${q}${choose(ETHIC_COLOR[civ.ethic])}\u201d`, 'council');
  }

  const ERAS = [
    [1e-5, 'Era of First Light. The first collectors catch the star.'],
    [1e-3, 'Era of Foundries. Industry now outgrows anything the homeworld ever built.'],
    [0.01, 'Era of the Swarm. The star has dimmed by a measurable percent, visible from neighbouring systems.'],
    [0.1, 'Era of Twilight. A tenth of the star’s light is gone from the sky.'],
    [0.5, 'Era of Eclipse. More of the star is caught than escapes.']
  ];

  // ---------- The civilization's own decisions ----------
  function decide() {
    if (!S.research) pickResearch();
    if (M.seed && !S.seedLaunched && S.capture >= DRIVES[civ.drive].seedAt && S.stock >= 0.3 * power()) { launchSeed(); return; }
    // shipyards: once industry is established, launch a frigate now and then
    if (S.year > 15 && S.ships.length < MAX_EXTRA && S.stock > 0.1 * power() && Math.random() < (civ.drive === 'Expansionist' ? 0.1 : 0.05)) {
      S.stock -= 0.05 * power();
      const name = SHIP_NAMES.filter(n => !S.ships.includes(n))[Math.floor(Math.random() * 6)] || 'Unnamed';
      S.ships.push(name);
      logEvent(`The shipyard launches the frigate ${name}. The flotilla is ${5 + S.ships.length} hulls strong.`, 'council', { fleet: true });
      return;
    }
    if (S.year > 25 && Object.keys(S.settled).length + S.missions.filter(m => m.settle).length < 2 && civ.pop > 0.8 * S.popCap && Math.random() < 0.15) {
      const cand = sys.planets.filter(p => !p.type.belt && !S.hubs[p.i] && !S.pending[p.i] && !(p.i in S.settled) && p.mined < 0.05 && p.i !== 0 && !p.natives
        && (p.type.life ? civ.ethic !== 'Ruthless' : true));
      if (cand.length) {
        const pl = cand.sort((a, b) => (b.type.life && civ.ethic === 'Reverent' ? 2 : 0) - (a.type.life && civ.ethic === 'Reverent' ? 2 : 0))[0];
        S.stock -= Math.min(S.stock, 0.1 * power());
        S.pending[pl.i] = true;
        S.missions.push({ i: pl.i, t: 0, dur: M.fast ? 1.6 : 3.5, settle: true });
        logEvent(pl.type.life && civ.ethic === 'Reverent' ? `Council decision: settle ${pl.name} beneath domes. Its biosphere will be left exactly as it is.`
          : pl.type.gas ? `Council decision: float cities in the upper atmosphere of ${pl.name}. The habitats are crowded; the clouds are not.`
          : `Council decision: settle ${pl.name}. The habitats are crowded, and some of them want a sky again, even a borrowed one.`, 'council', { planet: pl.i });
        return;
      }
    }
    let mineRate = M.starLift ? 0.3 : 0;
    for (const key in S.hubs) { const p = sys.planets[key]; if (S.hubs[key] && p.mined < 1) mineRate += 0.16 * S.hubs[key] * yieldOf(p); }
    for (const key in S.pending) mineRate += 0.16 * yieldOf(sys.planets[key]);
    mineRate *= M.mine;
    const foundRate = 0.08 * S.foundries * M.foundry;
    if (mineRate < 1.25 * foundRate) {
      const c = bestHub();
      if (c && S.stock >= hubCost(c.i)) startHub(c.i, false);
      else if (!c && S.stock >= foundryCost()) buildFoundry();
    } else if (S.stock >= foundryCost()) buildFoundry();
  }
  function bestHub() {
    const life = ETHICS[civ.ethic].life;
    const scoreAll = allowSacred => sys.planets.map(p => {
      const lvl = S.hubs[p.i] || 0;
      if (p.mined >= 0.98 || S.pending[p.i] || lvl >= 4 || (p.i in S.settled) || rivalOwns(p.i) || (p.natives && p.natives.contact !== 'cleared')) return null;
      let lf = p.type.life ? life : 1;
      if (p.type.life && !lvl && lf === 0) { if (!allowSacred) return null; lf = 0.05; }
      const s = yieldOf(p) * lf * Math.pow(0.7, lvl) * (lvl ? 1 : 1.15) / (1 + p.i * 0.12);
      return { i: p.i, s };
    }).filter(Boolean).sort((a, b) => b.s - a.s)[0];
    return scoreAll(false) || scoreAll(true) || null;
  }
  function startHub(i, byPlayer) {
    const p = sys.planets[i], lvl = S.hubs[i] || 0;
    S.stock -= hubCost(i);
    S.pending[i] = true;
    S.missions.push({ i, t: 0, dur: M.fast ? 1.6 : 3.5 });
    if (p.type.life && !lvl) nudgeConscience(byPlayer ? -0.1 : -0.18);
    if (p.natives && p.natives.contact !== 'cleared') {
      p.natives.contact = 'cleared';
      nudgeConscience(-0.3);
      logEvent(`At the voice\u2019s command, ${p.name} is cleared of the ${p.natives.name}. Even the Ruthless find it hard to watch.`, 'loss', { planet: i });
    }
    if (p.i in S.settled) {
      delete S.settled[p.i];
      civ.pop *= 0.9;
      nudgeConscience(-0.08);
      logEvent(`${p.name} is evacuated so it can be mined. Not everyone leaves in time.`, 'loss', { planet: i });
    }
    if (byPlayer) {
      const grief = p.type.life && !lvl && civ.ethic === 'Reverent' ? ' The Reverent weep, but they obey.' : '';
      logEvent(`A voice no one can locate speaks in the council chamber. A constructor turns toward ${p.name}.${grief}`, 'god', { planet: i });
      return;
    }
    let why = 'Best yield within reach.';
    if (lvl) why = `Deepening the works to level ${lvl + 1}.`;
    else if (p.type.life && civ.ethic === 'Ruthless') why = 'The native microbes were not discussed.';
    else if (p.type.life && civ.ethic === 'Pragmatic') why = 'Its biosphere is regrettable. The vote was 6–3.';
    else if (p.type.life) why = 'Nothing else is left. The vote took eleven days.';
    else if (p.type.gas) why = 'Skimmers will work its upper atmosphere for centuries.';
    logEvent(`Council decision: ${lvl ? 'expand' : 'open'} ${p.name}. ${why}`, 'council', { planet: i });
  }
  function buildFoundry() {
    S.stock -= foundryCost(); S.foundries++; rebuildFoundries();
    logEvent(`Council decision: light foundry ${S.foundries}. Collectors are the bottleneck.`, 'council', { foundry: S.foundries - 1 });
  }
  function pickResearch() {
    const avail = TECHS.filter(t => !civ.techs.has(t.id) && prereqDone(t));
    if (!avail.length) return;
    let best = null, bs = -1;
    // with the swarm nearly complete, the only thing left to want is another star
    const restless = S.capture > 0.9 && !civ.techs.has('X3');
    avail.forEach(t => { const s = civ.w[t.b] * (restless && t.b === 'X' ? 6 : 1) * rnd(0.6, 1.4) / Math.pow(t.tier + 1, 0.3); if (s > bs) { bs = s; best = t; } });
    S.research = { id: best.id, p: 0 };
    const branch = BRANCHES.find(b => b.id === best.b).name;
    logEvent(`The ${civ.drive.toLowerCase()} majority directs research toward ${best.name} (${branch}).`, 'council');
  }
  function completeTech(t) {
    civ.techs.add(t.id);
    computeM();
    S.research = null;
    fx('pulse', { at: () => parkNow, r: 0.5, color: 0x8fb0c4 });
    logEvent(`Breakthrough: ${t.name}. ${t.log}`, 'break', { fleet: true });
    shiftInf(BRANCH_FACTION[t.b], 0.06);
    if (t.id === 'S3') civ.digital = 0.5;
    if (t.id === 'S4') { civ.digital = 0.97; nudgeConscience(-0.12); }
    if (M.habitat) ensureHabitat();
    pickResearch();
  }
  // ---------- Great projects: each drive's own endgame ----------
  const PROJECTS = {
    Industrious: { name: 'Stellar Engine', years: 120,
      start: 'The Forgewrights begin a mirror the size of half an orbit. When it is finished, the star will push itself.',
      end: 'The Stellar Engine fires. The star, every planet left and the swarm around it begin, very slowly, to move. In ten thousand years they will be somewhere else, and the flotilla intends to choose where.' },
    Contemplative: { name: 'Archive of Everything', years: 110,
      start: 'The Choristers begin rewiring the swarm. Every collector will become a neuron.',
      end: 'The swarm stops being a power plant and becomes a mind. It thinks quietly for a decade. Then it asks the Speaker a question nobody can answer, and goes on thinking about it.' },
    Expansionist: { name: 'Fold Gate', years: 100,
      start: 'The Wayfarers begin a gate ring beside the flotilla. Through it, every star within forty light-years will be one step away.',
      end: 'The gate opens onto a hundred stars at once. Ships begin to leave and do not stop leaving. This system will be a waystation now, and the flotilla will be everywhere.' }
  };
  function projectStep(dy) {
    if (!S.project && S.capture >= 0.6 && civ.techs.size >= 7) {
      S.project = { drive: civ.drive, p: 0, done: false };
      logEvent(`Great project begun: ${PROJECTS[civ.drive].name}. ${PROJECTS[civ.drive].start}`, 'era', civ.drive === 'Expansionist' ? { gate: true } : { star: true });
    }
    const pr = S.project;
    if (!pr || pr.done) return;
    pr.p = Math.min(1, pr.p + dy / PROJECTS[pr.drive].years * (0.8 + 0.4 * S.capture));
    if (pr.p >= 1) {
      pr.done = true;
      const P = PROJECTS[pr.drive];
      logEvent(`${P.name} complete. ${P.end}`, 'era', pr.drive === 'Expansionist' ? { gate: true } : { star: true });
      $('winTitle').textContent = P.name;
      $('winText').textContent = `${sys.name}, year ${Math.floor(S.year)}. ${P.end}`;
      $('win').hidden = false;
      setSpeed(0);
    }
  }

  // ---------- Crises: multi-year emergencies ----------
  const CRISES = {
    runaway: {
      ok: () => civ.techs.has('I1') && S.year > 60,
      len: 12,
      start: c => {
        const t = sys.planets.filter(p => !p.type.gas && p.mined < 0.9 && !(p.i in S.settled) && !(p.natives && p.natives.contact !== 'cleared'));
        if (!t.length) return null;
        c.target = choose(t).i;
        return `A replicator lineage mutates and stops taking orders. It is eating ${sys.planets[c.target].name}, and keeping what it eats.`;
      },
      tick: (c, dy) => { const p = sys.planets[c.target]; p.mined = Math.min(1, p.mined + dy * 0.02); },
      where: c => ({ planet: c.target }),
      end: () => civ.drive === 'Industrious' || M.shield < 1
        ? 'The Forgewrights seed a counter-swarm. The runaway lineage is hunted down to the last molecule.'
        : 'The runaway lineage burns itself out. What it ate is gone for good.',
      fix: 'The runaway replicators all stop at once, mid-bite. Nobody can find out why.'
    },
    secession: {
      ok: () => civ.digital >= 0.5,
      len: 20,
      start: () => { S.capture *= 0.9; civ.pop *= 0.88; return 'Uploaded minds declare a polity of their own and take a tenth of the swarm\u2019s compute with them. They call themselves the Quiet.'; },
      tick: () => {},
      where: () => ({ star: true }),
      end: () => civ.drive === 'Contemplative'
        ? 'The Choristers talk the Quiet back. The swarm is whole again; the Quiet keep their name.'
        : 'The Quiet build a seed of their own and leave without permission. Nobody is sure what they took.',
      onEnd: () => { if (civ.drive === 'Contemplative') S.capture = Math.min(1, S.capture / 0.92); },
      fix: 'Every one of the Quiet wakes up at once with the same memory of a voice. They rejoin before lunch.',
      onFix: () => { S.capture = Math.min(1, S.capture / 0.9); civ.pop /= 0.9; }
    },
    war: {
      ok: () => false,   // only started by a returning splinter fleet
      len: 10,
      start: () => `The ${S.rival.name} refuse to stand down. The flotilla opens fire across the system.`,
      tick: (c, dy) => {
        c.acc = (c.acc || 0) + dy;
        if (c.acc > 2) {
          c.acc = 0;
          if (Math.random() < 0.5 && S.ships.length) logEvent(`The frigate ${S.ships.pop()} is lost with all hands.`, 'loss', { fleet: true });
          else if (S.rival.ships > 0) { S.rival.ships--; }
        }
      },
      where: () => ({ rival: true }),
      end: () => {
        const won = S.rival.ships <= 3;
        const msg = won ? `The ${S.rival.name} are destroyed. Their world is ours again. Nobody celebrates.`
          : `Neither side can win. The ${S.rival.name} withdraw beyond the outer planets with what is left of their fleet.`;
        endRival();
        return msg;
      },
      fix: 'Every gun in the system goes quiet at the same instant. Both sides hear the same voice. The war is over.',
      onFix: () => { if (S.rival) { civ.pop += S.rival.pop * 0.5; endRival(); } }
    },
    storms: {
      ok: () => M.starLift && S.capture > 0.01,
      len: 10,
      start: () => 'Stellar lifting has destabilised the star. Flare storms begin, and will not stop for a decade.',
      tick: (c, dy) => { c.acc = (c.acc || 0) + dy; if (c.acc > 2) { c.acc = 0; S.capture *= 1 - 0.02 * M.shield; fx('flare'); } },
      where: () => ({ star: true }),
      end: () => 'The star settles. The flare storms are over, and the lifting funnels are turned down by a third.',
      fix: 'The storms end between one flare and the next, as if the star had been told to stop.'
    }
  };
  function startCrisis() {
    const open = Object.keys(CRISES).filter(id => !civ.used.includes('crisis-' + id) && CRISES[id].ok());
    if (!open.length) return false;
    const id = choose(open), C = CRISES[id], c = { id, t0: S.year };
    const msg = C.start(c);
    if (!msg) return false;
    civ.used.push('crisis-' + id);
    S.crisis = c;
    logEvent('Crisis. ' + msg, 'loss', C.where(c));
    $('fixBtn').hidden = false;
    return true;
  }
  function crisisStep(dy) {
    const c = S.crisis;
    if (!c) return;
    const C = CRISES[c.id];
    C.tick(c, dy);
    if (S.year - c.t0 >= C.len) {
      S.crisis = null;
      $('fixBtn').hidden = true;
      if (C.onEnd) C.onEnd();
      logEvent(C.end(), 'era', C.where(c));
    }
  }
  $('fixBtn').addEventListener('click', () => {
    const c = S.crisis;
    if (!c) return;
    const C = CRISES[c.id];
    S.crisis = null;
    $('fixBtn').hidden = true;
    if (C.onFix) C.onFix();
    nudgeConscience(0.04);
    logEvent(C.fix, 'god', C.where(c));
  });

  // ---------- The splinters return ----------
  function rivalStep() {
    const sp = S.splinter;
    if (sp && !sp.returned && S.year > sp.back) {
      sp.returned = true;
      if (Math.random() < 0.4) return;   // most splinters are never seen again; some come back
      const options = sys.planets.filter(p => p.i !== 0 && !(p.i in S.settled) && !(p.natives && p.natives.contact !== 'cleared') && p.mined < 0.6);
      if (!options.length) return;
      const target = options[options.length - 1].i;
      S.rival = { name: sp.faction + ' of the Long Night', pop: sp.pop * 1.6, ships: 6 + Math.floor(Math.random() * 5), planet: target, t0: S.year, stance: null };
      logEvent(`A fleet folds in beside ${sys.planets[target].name}. It is the ${sp.faction} who left ${Math.floor(S.year - sp.at)} years ago. They call themselves the ${S.rival.name} now.`, 'omen', { rival: true });
      return;
    }
    const rv = S.rival;
    if (!rv || rv.stance || S.year < rv.t0 + 3) return;
    if (civ.ethic === 'Reverent') {
      rv.stance = 'reunion';
      civ.pop += rv.pop;
      for (let k = 0; k < 3 && S.ships.length < MAX_EXTRA; k++) S.ships.push(SHIP_NAMES.find(n => !S.ships.includes(n)) || 'Prodigal');
      logEvent(`The council meets the ${rv.name} without weapons. After three years of talks, they come home.`, 'era', { fleet: true });
      endRival();
    } else if (civ.ethic === 'Pragmatic') {
      rv.stance = 'partition';
      logEvent(`The council and the ${rv.name} sign a partition. ${sys.planets[rv.planet].name} is theirs now, and nobody else\u2019s.`, 'council', { rival: true });
    } else {
      rv.stance = 'war';
      const C = CRISES.war, c = { id: 'war', t0: S.year };
      S.crisis = c; $('fixBtn').hidden = false;
      logEvent('War. ' + C.start(c), 'loss', { rival: true });
    }
  }
  function endRival() { S.rival = null; }
  const rivalOwns = i => S.rival && S.rival.stance !== 'reunion' && S.rival.planet === i;

  // ---------- Native civilizations ----------
  const livingNatives = () => sys.planets.filter(p => p.natives && p.natives.contact !== 'cleared');
  function nativesStep(dy) {
    livingNatives().forEach(p => {
      const n = p.natives;
      if (!n.contact && S.year > 6) {
        let c;
        if (n.protected || civ.ethic === 'Reverent') c = 'quarantine';
        else if (civ.ethic === 'Pragmatic') c = Math.random() < 0.6 ? 'uplift' : 'quarantine';
        else c = Math.random() < 0.7 ? 'clearance' : 'uplift';
        n.contact = c;
        if (c === 'quarantine') logEvent(`${p.name} is inhabited. The ${n.name} are ${NATIVE_AGES[n.level].toLowerCase()}, and do not know we are here. The council declares their world closed. Nobody lands; nobody speaks.`, 'omen', { planet: p.i });
        else if (c === 'uplift') { n.level = 3; civ.pop += 1.2; S.boost *= 1.08; shiftInf('Contemplative', 0.05);
          logEvent(`${p.name} is inhabited. The council chooses to make contact and uplift the ${n.name}. Within a generation they hold a seat on the council.`, 'omen', { planet: p.i }); }
        else { n.clearAt = S.year + 4;
          logEvent(`${p.name} is inhabited by the ${n.name}. The council votes for clearance. The world is wanted for its mass.`, 'omen', { planet: p.i }); }
        return;
      }
      if (n.contact === 'clearance' && S.year >= n.clearAt) {
        if (n.protected) { n.contact = 'quarantine'; return; }
        n.contact = 'cleared';
        nudgeConscience(-0.3);
        logEvent(`Clearance of ${p.name} is complete. The council does not record the number. The ${n.name} are now only a word in the chronicle.`, 'loss', { planet: p.i });
        return;
      }
      if (n.contact === 'quarantine' && n.level < 4) {
        n.t += dy;
        if (n.t > 60 + Math.random() * 40) {
          n.t = 0; n.level++;
          const msg = [, 'The ' + n.name + ' have learned to work bronze. Their first cities ring the shallow seas.',
            'The ' + n.name + ' have an industrial revolution. Their night side begins to glow.',
            'The ' + n.name + ' have split the atom. The council watches, and waits.',
            'The ' + n.name + ' have reached orbit. Their first telescope is pointed at the dimming star. Their first broadcast asks it to stop going dark.'][n.level];
          logEvent(msg, n.level === 4 ? 'omen' : 'event', { planet: p.i });
          if (n.level === 4) shiftInf('Contemplative', 0.08);
        }
      }
    });
  }
  $('cAct2').addEventListener('click', () => {
    if (sel < 0) return;
    const n = sys.planets[sel].natives;
    if (!n || n.protected || n.contact === 'cleared') return;
    n.protected = true;
    if (n.contact === 'clearance') n.contact = 'quarantine';
    nudgeConscience(0.08);
    logEvent(`A voice no one can locate speaks in the council chamber: not these ones. The ${n.name} are left alone.`, 'god', { planet: sel });
    renderCard();
  });

  // ---------- First contact ----------
  function contactStep() {
    if (!S.visitorDone && !S.visitor) {
      const called = S.answeredAt >= 0 && S.year > S.answeredAt + 60;
      const noticed = S.capture > 0.3 && Math.random() < 0.004;
      if (called || noticed) {
        S.visitor = { t0: S.year, stage: 0, anim: wall, outcome: null };
        logEvent(called ? 'A fold signature at the system edge, on the bearing of the signal they answered. Something has arrived. It is not ours.'
          : 'A fold signature at the system edge. The dimming of this star has been noticed. Something has arrived. It is not ours.', 'omen', { visitor: true });
      }
      return;
    }
    const v = S.visitor;
    if (!v) return;
    const age = S.year - v.t0;
    if (v.stage === 0 && age > 3) {
      v.stage = 1;
      const react = {
        Expansionist: 'The Wayfarer council sends a delegation in an unarmed frigate. It is allowed to approach.',
        Contemplative: 'The Choristers point every instrument they own at it and listen.',
        Industrious: civ.ethic === 'Ruthless' ? 'The Forgewrights re-tool three foundries for weapons.' : 'The Forgewrights keep building, and keep watching.'
      }[civ.drive];
      logEvent(react, 'omen', { visitor: true });
      if (civ.drive === 'Industrious' && civ.ethic === 'Ruthless') { S.stock *= 0.8; }
    } else if (v.stage === 1 && age > 15) {
      v.stage = 2;
      v.outcome = pickOutcome();
      resolveOutcome(v.outcome);
    } else if (v.stage === 2 && age > 32 + (v.outcome === 'fire' ? -10 : 0)) {
      v.stage = 3; v.anim = wall;
      logEvent('The visitor leaves the way it came. Nobody sleeps well for a decade.', 'omen', { visitor: true });
    } else if (v.stage === 3 && wall - v.anim > 6) {
      S.visitor = null; S.visitorDone = true;
    }
    if (S.homeDark > 0 && S.year > S.homeDark) {
      S.homeDark = -1;
      logEvent('The light from the homeworld\u2019s star stops arriving. Not dimmed. Stopped. The council orders every transmitter silent.', 'omen', { star: true });
      nudgeConscience(0.15); shiftInf('Contemplative', 0.15);
    }
  }
  function pickOutcome() {
    const o = pickOutcomeRaw();
    return o === 'take' && M.deterrent ? 'silent' : o;
  }
  function pickOutcomeRaw() {
    const r = Math.random();
    if (civ.drive === 'Industrious' && civ.ethic === 'Ruthless') return 'fire';
    if (civ.drive === 'Expansionist') return r < 0.45 ? 'gift' : r < 0.8 ? 'silent' : 'take';
    if (civ.drive === 'Contemplative') return r < 0.5 ? 'study' : r < 0.8 ? 'silent' : 'take';
    return r < 0.55 ? 'silent' : r < 0.8 ? 'take' : 'gift';
  }
  function resolveOutcome(o) {
    if (o === 'gift') {
      const t = TECHS.find(x => !civ.techs.has(x.id) && prereqDone(x));
      if (t) { civ.techs.add(t.id); computeM(); if (S.research && S.research.id === t.id) S.research = null; }
      logEvent(`The delegation returns with a schematic, drawn in our own notation.${t ? ' It is ' + t.name + '. Nobody can say how it knew.' : ''}`, 'omen', { fleet: true });
    } else if (o === 'study') {
      S.boost *= 1.15;
      logEvent('Fifteen years of listening. The Choristers learn very little about the visitor, and a great deal about physics. Industry +15%.', 'omen', { visitor: true });
    } else if (o === 'take') {
      const loss = 0.15;
      S.capture *= 1 - loss;
      logEvent('Fifteen percent of the swarm is simply gone one morning. No debris. No heat. The visitor has not moved.', 'omen', { star: true });
      nudgeConscience(0.05);
    } else if (o === 'fire') {
      logEvent('The flotilla fires on the visitor. It does not fire back. It turns, slowly, toward the direction of the homeworld.', 'omen', { visitor: true });
      S.homeDark = S.year + 20;
      nudgeConscience(-0.1);
    } else {
      logEvent('It does nothing. It watches. Every attempt at contact is ignored. Children grow up under it.', 'omen', { visitor: true });
    }
  }
  const rivalFleet = [], beams = [];
  const rivalMarker = sprite(softDot, 0xe0583a, { sizeAttenuation: false });
  rivalMarker.scale.set(0.02, 0.02, 1); rivalMarker.visible = false; scene.add(rivalMarker);
  const beamMat = new THREE.LineBasicMaterial({ color: 0xff6a3a, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false });
  const rivalPos = new V3();
  function placeRival() {
    const rv = S.rival;
    const on = !!rv && mode === 'sim';
    while (on && rivalFleet.length < 5) {
      const f = Object.assign(makeFrigate(), { scale: 0.7 * K }); f.g.scale.setScalar(f.scale); scene.add(f.g); rivalFleet.push(f);
    }
    rivalMarker.visible = on && camera.position.distanceTo(rivalPos) > 1.5;
    rivalFleet.forEach((f, k) => { f.g.visible = on && k < Math.min(5, rv.ships); });
    if (!on) { beams.forEach(b => { b.visible = false; }); return; }
    const P = planetPos(rv.planet, S.year), out = P.clone().normalize(), tan = new V3().crossVectors(UP, out).normalize();
    rivalPos.copy(P).addScaledVector(tan, -(sys.planets[rv.planet].size + 0.4)).addScaledVector(UP, 0.08);
    rivalMarker.position.copy(rivalPos);
    rivalFleet.forEach((f, k) => {
      const a = k * 1.3 + wall * 0.03;
      f.g.position.copy(rivalPos).add(new V3(Math.cos(a) * 3 * K, Math.sin(k) * K, Math.sin(a) * 3 * K));
      f.g.lookAt(parkNow);
      setPlumes(f, 0.2, k + 20);
    });
    const war = S.crisis && S.crisis.id === 'war';
    while (beams.length < 4) {
      const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.BufferAttribute(new Float32Array(6), 3));
      const l = new THREE.Line(g, beamMat); l.frustumCulled = false; scene.add(l); beams.push(l);
    }
    beams.forEach((b, k) => {
      const live = war && Math.sin(wall * 9 + k * 2.1) > 0.55;
      b.visible = live;
      if (!live) return;
      const a = b.geometry.attributes.position.array;
      const from = k % 2 ? rivalPos : parkNow, to = k % 2 ? parkNow : rivalPos;
      a[0] = from.x; a[1] = from.y + (k - 1.5) * 0.02; a[2] = from.z; a[3] = to.x; a[4] = to.y; a[5] = to.z;
      b.geometry.attributes.position.needsUpdate = true;
    });
    beamMat.opacity = 0.6 + 0.4 * Math.sin(wall * 30);
  }
  const visitorHome = new V3();
  function placeVisitor() {
    const v = S.visitor;
    visitor.visible = !!v && mode === 'sim';
    visitorMarker.visible = visitor.visible && camera.position.distanceTo(visitor.position) > 6;
    if (!visitor.visible) return;
    visitorHome.set(0, swarmOut * 0.45 + 2.5, 0).add(I.out.clone().multiplyScalar(-(swarmOut + 3)));
    const far = visitorHome.clone().add(I.out.clone().multiplyScalar(-260)).add(new V3(0, 90, 0));
    const u = v.stage < 3 ? smooth(0, 6, wall - v.anim) : 1 - smooth(0, 6, wall - v.anim);
    visitor.position.copy(far).lerp(visitorHome, u);
    visitor.rotation.y = wall * 0.05;
    visitor.userData.halo.rotation.z = wall * 0.2;
    visitorMarker.position.copy(visitor.position);
  }

  function election() {
    if (M.hive) {
      if (!civ.used.includes('hive')) { civ.used.push('hive'); logEvent('Election day passes without an election. There is only agreement now.', 'omen', { fleet: true }); }
      return;
    }
    const ranked = Object.keys(civ.inf).sort((a, b) => civ.inf[b] - civ.inf[a]);
    const [top, second] = ranked, F = k => FACTIONS[k].name;
    if (civ.inf[top] - civ.inf[second] < 0.05 && civ.inf[second] > 0.33 && S.year > 30) {
      // too close to call: a schism
      const leaving = top === civ.drive ? second : top;
      const lost = civ.pop * 0.15;
      civ.pop -= lost; S.stock *= 0.88;
      civ.inf[leaving] *= 0.4; shiftInf(civ.drive, 0);
      S.splinterAt = wall;
      if (!S.splinter) S.splinter = { faction: F(leaving), pop: lost, at: S.year, back: S.year + 50 + Math.random() * 40 };
      logEvent(`Schism. ${lost < 1 ? Math.round(lost * 1000) + ' million' : lost.toFixed(1) + ' billion'} ${F(leaving)} take a share of the stockpile and leave for the dark between stars. They do not say where.`, 'loss', { fleet: true });
      return;
    }
    if (top !== civ.drive && civ.inf[top] > civ.inf[civ.drive] + 0.05) {
      const old = civ.drive;
      civ.drive = top;
      civ.leader = newLeader(top);
      for (const b in civ.w) civ.w[b] = Math.min(6, 0.5 * civ.w[b] + 0.5 * DRIVES[top].w[b]);
      logEvent(`The ${F(top)} take the council from the ${F(old)}. Speaker ${civ.leader.name} sets a new course: ${FACTIONS[top].creed}.`, 'era', { fleet: true });
      speakerSays();
      setSub();
    } else {
      logEvent(`Council elections. The ${F(civ.drive)} hold power with ${Math.round(civ.inf[civ.drive] * 100)}% of the vote.`, 'council');
    }
  }
  function launchSeed() {
    S.stock -= 0.3 * power();
    S.seedLaunched = true;
    S.seedAt = wall; S.seedYear = S.year;
    S.nextSeed = Math.floor(Math.random() * 1e9);
    S.nextName = generateSystem(S.nextSeed).name;
    $('seedBtn').hidden = false;
    logEvent(`Seed ship launched toward ${S.nextName}. ${civ.drive === 'Expansionist' ? 'The fleet cheers.' : 'Not everyone wanted to leave.'}`, 'era', { fleet: true });
  }

  // ---------- Events the universe throws at them ----------
  const EVENTS = [
    () => S.capture > 1e-4 && (fx('flare'), S.capture *= 1 - 0.04 * M.shield, `A stellar flare scoured the inner swarm. ${lossPct(0.04 * M.shield)} of collectors lost.`),
    () => Math.random() < 0.5 * M.comets && (fx('comet'), S.stock += 0.3 * power(), 'Captured a long-period comet. Ice and carbon added to the stockpile.'),
    () => S.boost < 2.5 && (S.boost *= 1.06, `Replicator design iteration ${2 + Math.floor(S.year / 9)} adopted. All industry +6%.`),
    () => S.capture > 1e-3 && M.shield === 1 && (S.capture *= 0.98, 'Collision cascade in a crowded orbital shell. 2% of collectors lost.'),
    () => {
      if (S.signalDone) return false;
      S.signalDone = true;
      const answer = civ.drive === 'Expansionist' || Math.random() < 0.3;
      if (answer) { S.answeredAt = S.year; shiftInf('Expansionist', 0.04); }
      return 'Long-range scan: a repeating narrowband signal, 41 light-years out. ' +
        (answer ? 'The council votes to answer it.' : 'The council votes to stay silent and dark.');
    },
    () => 'Survey drones report nothing out here but the flotilla. The silence is total.'
  ];
  const hubNames = () => Object.keys(S.hubs).filter(k => S.hubs[k]).map(k => sys.planets[k].name);
  const STORIES = [
    ['hearth', () => S.capture > 1e-3 && civ.ethic !== 'Ruthless', () => 'A religion forms around the star. Its followers call it the Hearth, and ask its forgiveness each time a collector launches.'],
    ['accident', () => hubNames().length > 0, () => `A slope failure in the open pits on ${choose(hubNames())} kills forty thousand workers. The works do not stop.`],
    ['nostalgia', () => civ.age > 60, () => 'The first generation that never saw the homeworld comes of age. They do not miss it. Their parents find this unbearable.'],
    ['painters', () => S.capture > 0.05, () => 'Painters begin depicting the star as it looked on arrival. It becomes the most popular subject in the fleet.'],
    ['mad', () => civ.digital > 0.4, () => 'An uploaded mind degrades in the archive and has to be quarantined. It keeps asking what year it is.'],
    ['merger', () => civ.drive === 'Contemplative', () => 'Gravitational-wave arrays catch two black holes merging a billion years ago. The Choristers hold a vigil that lasts a month.'],
    ['naming', () => S.year > 30, () => `Someone proposes giving the star a name. The council cannot agree on one. It remains ${sys.name}.`],
    ['shard', () => M.habitat, () => 'A collector shard punctures the habitat ring. Nine hundred dead. The shielding is doubled, then doubled again.'],
    ['retire', () => S.ships.length > 2, () => `The frigate ${choose(S.ships)}\u2019s AI asks to be decommissioned. After a long debate, the request is granted.`],
    ['ruthless', () => civ.ethic === 'Ruthless' && M.habitat, () => 'A motion to break up the habitat ring for its alloy fails by a single vote.'],
    ['child', () => S.capture > 0.02, () => 'A child born in-system asks what the star looked like before. Nobody has a good answer.'],
    ['lullaby', () => S.year > 50, () => 'A lullaby about the old sun spreads through the fleet. Nobody knows who wrote it.'],
    ['census', () => civ.pop > 1, () => `The census passes ${civ.pop.toFixed(1)} billion. Most of them have never been more than a light-minute from where they were born.`],
    ['monument', () => Object.values(sys.planets).some(p => p.mined >= 1), () => 'A monument is raised to the planets that were consumed. It is made from what is left of them.']
  ];
  function randomEvent() {
    if (sys.star.cls[0] === 'M' && S.capture > 1e-4 && Math.random() < 0.25) {
      fx('flare'); S.capture *= 1 - 0.05 * M.shield; shiftInf('Industrious', 0.02);
      logEvent(`${sys.name} is a flare star. Another eruption takes ${lossPct(0.05 * M.shield)} of the inner swarm.`, 'event', { star: true });
      return;
    }
    if (!S.crisis && Math.random() < 0.18 && startCrisis()) return;
    if (Math.random() < 0.45) {
      const open = STORIES.filter(([id, ok]) => !civ.used.includes(id) && ok());
      if (open.length) {
        const [id, , text] = choose(open);
        civ.used.push(id);
        logEvent(text(), 'event');
        return;
      }
    }
    for (let tries = 0; tries < 8; tries++) {
      const msg = choose(EVENTS)();
      if (msg) { logEvent(msg, 'event', { star: true }); return; }
    }
  }

  // ---------- Interference ----------
  const CD = { gift: 20, smite: 25, inspire: 12 };
  const ready = k => S.year >= S.cd[k];
  $('giftBtn').addEventListener('click', () => {
    if (!ready('gift')) return;
    S.cd.gift = S.year + CD.gift;
    S.stock += Math.max(0.6 * power(), 2e-6);
    civ.w.X += 0.3;
    shiftInf('Expansionist', 0.05);
    fx('comet');
    logEvent('A comet falls inward on a perfect intercept. They call it luck. Some of them don’t, and start looking outward.', 'god', { fleet: true });
  });
  $('smiteBtn').addEventListener('click', () => {
    if (!ready('smite')) return;
    S.cd.smite = S.year + CD.smite;
    const loss = 0.08 * M.shield;
    S.capture *= 1 - loss;
    civ.w.E += 0.6;
    shiftInf('Contemplative', 0.06); nudgeConscience(0.06);
    const react = { Reverent: 'The Reverent call it a judgment and fast for a week.', Ruthless: 'They log it as weather and rebuild.', Pragmatic: 'Swarm engineering gets more funding.' }[civ.ethic];
    fx('flare');
    logEvent(`An unseasonal flare tears through the swarm. ${lossPct(loss)} lost. ${react}`, 'god', { star: true });
  });
  function inspire(b) {
    if (!ready('inspire')) return;
    S.cd.inspire = S.year + CD.inspire;
    civ.w[b] = Math.min(6, civ.w[b] * 1.6 + 0.4);
    shiftInf(BRANCH_FACTION[b], 0.07);
    const br = BRANCHES.find(x => x.id === b);
    if (S.research && techById(S.research.id).b !== b && S.research.p < 0.3 * techById(S.research.id).cost) S.research = null;
    if (!S.research) pickResearch();
    logEvent(`A shared dream spreads through the fleet: ${br.dream}`, 'god', { fleet: true });
    updateTree();
  }

  // ---------- HUD ----------
  const supDigits = n => String(n).replace(/-/g, '⁻').replace(/\d/g, d => '⁰¹²³⁴⁵⁶⁷⁸⁹'[d]);
  const sci = v => { if (v <= 0) return '0'; const e = Math.floor(Math.log10(v)); return (v / Math.pow(10, e)).toFixed(1) + '×10' + supDigits(e); };
  const kg = v => sci(v * SWARM_KG) + ' kg';
  const pct = v => v <= 0 ? '0%' : v < 1e-6 ? '<0.0001%' : v < 1e-4 ? (v * 100).toPrecision(2) + '%' : (v * 100).toFixed(v < 0.01 ? 3 : 1) + '%';
  const kard = P => ((Math.log10(L_SUN_W * sys.star.lum * P) - 6) / 10).toFixed(3);
  const cdText = k => ready(k) ? '' : Math.ceil(S.cd[k] - S.year) + ' y';

  let logLines = [];
  function logEvent(text, kind = 'event', where = null) {
    logLines.push({ stamp: 'Year ' + Math.floor(S.year), text });
    if (logLines.length > 2) logLines.shift();
    renderLog();
    const entry = { gen: civ.gen, sys: sys.name, y: Math.floor(S.year), text, kind };
    civ.history.push(entry);
    if (civ.history.length > 600) civ.history.shift();
    if (!$('chron').hidden) appendChron(entry, true);
    if (where) directTo(where);
    chime(kind);
  }
  function renderLog() {
    const [a, b] = logLines.length > 1 ? logLines : [null, logLines[0]];
    $('logPrev').textContent = a ? a.stamp + '  ' + a.text : '';
    const cur = $('logCur');
    cur.innerHTML = '';
    if (b) { const s = document.createElement('span'); s.className = 'stamp'; s.textContent = b.stamp; cur.append(s, b.text); }
  }

  function updateHud() {
    $('tStock').textContent = kg(S.stock);
    if (mode === 'intro') {
      $('tYear').textContent = introT < 6.5 ? 'Fold exit' : 'T+' + Math.floor((introT - 6.5) * 0.6) + ' d';
      $('tCap').textContent = '0%';
      $('tK').textContent = 'Type ' + kard(P_FLEET);
      return;
    }
    $('tYear').textContent = 'Year ' + Math.floor(S.year);
    $('tCap').textContent = pct(S.capture);
    $('tK').textContent = 'Type ' + kard(power());
    if (S.research) {
      const t = techById(S.research.id);
      $('focusText').textContent = `Researching ${t.name} · ${Math.floor(S.research.p / t.cost * 100)}%`;
      $('focusBar').style.setProperty('--p', clamp(S.research.p / t.cost));
    } else {
      $('focusText').textContent = civ.techs.size === TECHS.length ? 'Every direction explored.' : 'The council is deliberating.';
      $('focusBar').style.setProperty('--p', 0);
    }
    const pr = S.project;
    $('project').hidden = !pr;
    if (pr) {
      $('projectText').textContent = pr.done ? `${PROJECTS[pr.drive].name} · complete` : `Great project: ${PROJECTS[pr.drive].name} · ${Math.floor(pr.p * 100)}%`;
      $('projectBar').style.setProperty('--p', pr.p);
    }
    $('giftCd').textContent = cdText('gift'); $('giftBtn').disabled = !ready('gift');
    $('smiteCd').textContent = cdText('smite'); $('smiteBtn').disabled = !ready('smite');
    renderCard();
    updateTree();
    if (!$('chron').hidden && hudTick % 30 === 0) drawCharts();
  }
  function setSub() {
    $('sysName').textContent = sys.name;
    $('sysSub').textContent = `${sys.star.cls} · Flotilla ${civ.name} · ${civ.drive} · ${civ.ethic}` + (civ.gen > 1 ? ` · Generation ${civ.gen}` : '');
  }

  // ---------- Sound: a generative drone tuned to the star, and chimes on events ----------
  const audio = { ctx: null, master: null, oscs: [], on: false, lastChime: 0 };
  const DRONE_HZ = { M: 41.2, K: 49, G: 55, F: 61.7, A: 73.4 };
  function startAudio() {
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return false;
    if (!audio.ctx) {
      const ctx = audio.ctx = new AC();
      audio.master = ctx.createGain(); audio.master.gain.value = 0; audio.master.connect(ctx.destination);
      const lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 320; lp.Q.value = 4; lp.connect(audio.master);
      const lfo = ctx.createOscillator(), lfoGain = ctx.createGain();
      lfo.frequency.value = 0.05; lfoGain.gain.value = 140; lfo.connect(lfoGain); lfoGain.connect(lp.frequency); lfo.start();
      [[1, 'sawtooth', 0.05], [1.5, 'sine', 0.08], [2.003, 'triangle', 0.03]].forEach(([mul, type, g]) => {
        const o = ctx.createOscillator(), gn = ctx.createGain();
        o.type = type; gn.gain.value = g; o.connect(gn); gn.connect(lp); o.start();
        audio.oscs.push({ o, mul });
      });
      // a thin wind of band-passed noise
      const buf = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate), d = buf.getChannelData(0);
      for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
      const noise = ctx.createBufferSource(); noise.buffer = buf; noise.loop = true;
      const bp = ctx.createBiquadFilter(); bp.type = 'bandpass'; bp.frequency.value = 500; bp.Q.value = 0.7;
      const ng = ctx.createGain(); ng.gain.value = 0.025;
      noise.connect(bp); bp.connect(ng); ng.connect(audio.master); noise.start();
    }
    audio.ctx.resume();
    tuneAudio();
    audio.master.gain.setTargetAtTime(0.35, audio.ctx.currentTime, 1.5);
    return true;
  }
  function stopAudio() {
    if (!audio.ctx) return;
    audio.master.gain.setTargetAtTime(0, audio.ctx.currentTime, 0.4);
  }
  function tuneAudio() {
    if (!audio.ctx || !sys) return;
    const base = DRONE_HZ[sys.star.cls[0]] || 55;
    audio.oscs.forEach(({ o, mul }) => o.frequency.setTargetAtTime(base * mul, audio.ctx.currentTime, 2));
  }
  const CHIME = { era: [196, 0.22, 5], break: [523.3, 0.12, 3], omen: [92.5, 0.25, 6], loss: [110, 0.18, 4], god: [329.6, 0.12, 3], council: [659.3, 0.04, 1.6], event: [392, 0.06, 2.5] };
  function chime(kind) {
    if (!audio.on || !audio.ctx) return;
    const now = audio.ctx.currentTime;
    if (now - audio.lastChime < 1.2) return;
    audio.lastChime = now;
    const [f, g, len] = CHIME[kind] || CHIME.event;
    [1, 2.01, 3.03].forEach((h, k) => {
      const o = audio.ctx.createOscillator(), gn = audio.ctx.createGain();
      o.type = 'sine'; o.frequency.value = f * h;
      if (kind === 'omen') { o.detune.setValueAtTime(0, now); o.detune.linearRampToValueAtTime(-60, now + len); }
      gn.gain.setValueAtTime(0, now);
      gn.gain.linearRampToValueAtTime(g / (k + 1), now + 0.02);
      gn.gain.exponentialRampToValueAtTime(0.0001, now + len / (k + 1));
      o.connect(gn); gn.connect(audio.master); o.start(now); o.stop(now + len);
    });
  }
  $('soundBtn').addEventListener('click', () => {
    audio.on = !audio.on;
    if (audio.on && !startAudio()) audio.on = false;
    if (!audio.on) stopAudio();
    $('soundBtn').setAttribute('aria-pressed', String(audio.on));
  });

  // ---------- Director: an automatic camera that cuts to where things are happening ----------
  const director = { on: true, hold: 0, until: 0, lastAt: -99, shot: null, cut: false, fadeAt: -9, current: null };
  function directTo(where) {
    if (mode !== 'sim' || !director.on || wall - director.lastAt < 6) return;
    let shot = null;
    if (where.planet != null && planetObjs[where.planet]) { const o = planetObjs[where.planet]; shot = { pos: () => o.g.position, dist: o.p.size * 5 + 1.5 }; }
    else if (where.fleet) shot = { pos: () => parkNow, dist: 0.32 };
    else if (where.foundry != null) { const f = foundryGroup.children[where.foundry]; if (f) shot = { pos: () => f.position, dist: 2.2 }; }
    else if (where.star) shot = { pos: () => ORIGIN, dist: swarmOut * 2.6 };
    else if (where.visitor) shot = { pos: () => visitor.position, dist: 6 };
    else if (where.gate) shot = { pos: () => gate.position, dist: 4.5 };
    else if (where.rival) shot = { pos: () => rivalPos, dist: 0.5 };
    if (!shot) return;
    director.shot = shot; director.lastAt = wall; director.until = wall + 9; director.cut = true;
  }
  const dirBtn = $('dirBtn');
  dirBtn.addEventListener('click', () => {
    director.on = !director.on; director.hold = 0;
    dirBtn.setAttribute('aria-pressed', String(director.on));
    if (!director.on) { controls.autoRotate = false; if (sel === -1) zoomGoal = I.home.length(); }
  });
  controls.addEventListener('start', () => { director.hold = wall + 25; });

  // ---------- Chronicle ----------
  const chronList = $('chronList');
  function chronItem(e) {
    const d = document.createElement('div');
    d.className = 'entry k-' + e.kind;
    const y = document.createElement('span'); y.className = 'y'; y.textContent = 'Year ' + e.y;
    const t = document.createElement('p'); t.textContent = e.text;
    d.append(y, t);
    return d;
  }
  function chronHead(e) {
    const h = document.createElement('h4');
    h.textContent = `${e.sys} · Generation ${e.gen}`;
    return h;
  }
  // ---------- History charts (two small multiples, never a dual axis) ----------
  const CHART_COLORS = { cap: '#c4782a', pop: '#4a8ec6' };   // validated against the deck surface (dark)
  const charts = [
    { key: 'cap', title: 'Output captured · log scale', col: 1, log: true, color: CHART_COLORS.cap, fmt: v => pct(v) },
    { key: 'pop', title: 'Population', col: 2, log: false, color: CHART_COLORS.pop, fmt: v => v < 1 ? Math.round(v * 1000) + ' million' : v.toFixed(1) + ' billion' }
  ];
  function buildCharts() {
    const wrap = document.createElement('div'); wrap.className = 'charts';
    charts.forEach(c => {
      const fig = document.createElement('figure'); fig.className = 'chart';
      const cap = document.createElement('figcaption'); cap.textContent = c.title;
      const cv = document.createElement('canvas'); cv.height = 96;
      const tip = document.createElement('div'); tip.className = 'tip'; tip.hidden = true;
      fig.append(cap, cv, tip);
      wrap.append(fig);
      c.cv = cv; c.tip = tip; c.hover = null;
      cv.addEventListener('pointermove', e => { const r = cv.getBoundingClientRect(); c.hover = (e.clientX - r.left) / r.width; drawChart(c); });
      cv.addEventListener('pointerleave', () => { c.hover = null; drawChart(c); });
    });
    const sum = document.createElement('p'); sum.className = 'chart-sum'; charts.sum = sum;
    wrap.append(sum);
    return wrap;
  }
  function drawChart(c) {
    const tl = S.timeline, cv = c.cv;
    if (!cv || !cv.isConnected) return;
    const dpr = Math.min(window.devicePixelRatio || 1, 2), W = cv.clientWidth, H = 96;
    if (cv.width !== Math.round(W * dpr)) { cv.width = Math.round(W * dpr); cv.height = H * dpr; }
    const x = cv.getContext('2d');
    x.setTransform(dpr, 0, 0, dpr, 0, 0);
    x.clearRect(0, 0, W, H);
    const css = getComputedStyle(document.documentElement);
    const ink = css.getPropertyValue('--dim').trim(), rule = css.getPropertyValue('--rule').trim();
    c.tip.hidden = true;
    if (tl.length < 2) { x.fillStyle = ink; x.font = '11px "IBM Plex Mono", monospace'; x.fillText('Not enough history yet.', 0, 20); return; }
    const L = 0, R0 = W - 4, T = 8, B = H - 18;
    const vals = tl.map(r => c.log ? Math.log10(Math.max(r[c.col], 1e-7)) : r[c.col]);
    let lo = c.log ? -6 : 0, hi = Math.max(...vals);
    if (c.log) hi = Math.max(hi, -4); else hi = hi * 1.1 || 1;
    const y0 = tl[0][0], y1 = tl[tl.length - 1][0];
    const X = yr => L + (yr - y0) / Math.max(1e-6, y1 - y0) * (R0 - L);
    const Y = v => B - (v - lo) / (hi - lo) * (B - T);
    // recessive grid: three hairlines
    x.strokeStyle = rule; x.lineWidth = 1;
    [0, 0.5, 1].forEach(f => { const yy = Math.round(T + f * (B - T)) + 0.5; x.beginPath(); x.moveTo(L, yy); x.lineTo(R0, yy); x.stroke(); });
    // era ticks
    x.strokeStyle = rule; x.setLineDash([2, 3]);
    S.eraYears.forEach(ey => { if (ey < y0) return; const xx = Math.round(X(ey)) + 0.5; x.beginPath(); x.moveTo(xx, T); x.lineTo(xx, B); x.stroke(); });
    x.setLineDash([]);
    // area + 2px line
    x.beginPath();
    tl.forEach((r, k) => { const px = X(r[0]), py = Y(vals[k]); k ? x.lineTo(px, py) : x.moveTo(px, py); });
    x.lineWidth = 2; x.strokeStyle = c.color; x.lineJoin = 'round'; x.stroke();
    x.lineTo(X(y1), B); x.lineTo(X(y0), B); x.closePath();
    x.globalAlpha = 0.14; x.fillStyle = c.color; x.fill(); x.globalAlpha = 1;
    // emphasized endpoint
    const ex = X(y1), ey = Y(vals[vals.length - 1]);
    x.beginPath(); x.arc(ex, ey, 4, 0, Math.PI * 2); x.fillStyle = c.color; x.fill();
    x.lineWidth = 2; x.strokeStyle = getComputedStyle(document.body).backgroundColor; x.stroke();
    // axis labels in text ink
    x.fillStyle = ink; x.font = '10px "IBM Plex Mono", monospace';
    x.textAlign = 'left'; x.fillText('Year ' + Math.floor(y0), L, H - 4);
    x.textAlign = 'right'; x.fillText('Year ' + Math.floor(y1), R0, H - 4);
    x.textAlign = 'left';
    x.fillText(c.log ? pct(Math.pow(10, hi)) : c.fmt(hi), L + 2, T + 9);
    // hover crosshair + tooltip
    if (c.hover != null) {
      const yr = y0 + clamp(c.hover) * (y1 - y0);
      let k = 0; while (k < tl.length - 1 && tl[k + 1][0] <= yr) k++;
      const px = X(tl[k][0]), py = Y(vals[k]);
      x.strokeStyle = ink; x.lineWidth = 1; x.beginPath(); x.moveTo(Math.round(px) + 0.5, T); x.lineTo(Math.round(px) + 0.5, B); x.stroke();
      x.beginPath(); x.arc(px, py, 4, 0, Math.PI * 2); x.fillStyle = c.color; x.fill();
      c.tip.hidden = false;
      c.tip.textContent = `Year ${Math.floor(tl[k][0])} · ${c.fmt(tl[k][c.col])}`;
      c.tip.style.left = Math.min(Math.max(px - 60, 0), W - 140) + 'px';
    }
  }
  function drawCharts() {
    charts.forEach(drawChart);
    const tl = S.timeline;
    if (charts.sum && tl.length) {
      const a = tl[0], b = tl[tl.length - 1];
      charts.sum.textContent = `${sys.name}, year ${Math.floor(a[0])} to ${Math.floor(b[0])}: output ${pct(a[1])} → ${pct(b[1])}; population ${charts[1].fmt(a[2])} → ${charts[1].fmt(b[2])}. Dashed lines mark eras.`;
    }
  }
  function renderChron() {
    chronList.innerHTML = '';
    chronList.append(buildCharts());
    const rows = civ.lineage.concat([{ gen: civ.gen, name: sys.name, cls: sys.star.cls, years: Math.floor(S.year), cap: S.capture, now: true }]);
    if (rows.length > 1) {
      const box = document.createElement('div'); box.className = 'lineage';
      rows.forEach(r => {
        const d = document.createElement('div');
        d.textContent = r.ep ? `Gen ${r.gen} · ${r.name} (${r.cls}) · ${r.ep}` : `Gen ${r.gen} · ${r.name} (${r.cls}) · ${r.years} y · ${pct(r.cap)}${r.now ? ' · now' : ''}`;
        box.append(d);
      });
      chronList.append(box);
    }
    let prev = null;
    for (let k = civ.history.length - 1; k >= 0; k--) {
      const e = civ.history[k];
      if (!prev || prev.sys !== e.sys) chronList.append(chronHead(e));
      chronList.append(chronItem(e));
      prev = e;
    }
    $('chronSub').textContent = `Flotilla ${civ.name} · ${civ.history.length} entries`;
  }
  function appendChron(e) {
    const first = chronList.firstElementChild;
    if (first && first.tagName === 'H4' && first.textContent.startsWith(e.sys)) first.after(chronItem(e));
    else { chronList.prepend(chronItem(e)); chronList.prepend(chronHead(e)); }
    $('chronSub').textContent = `Flotilla ${civ.name} · ${civ.history.length} entries`;
  }
  $('chronBtn').addEventListener('click', () => { renderChron(); $('chron').hidden = false; drawCharts(); });
  $('chronClose').addEventListener('click', () => { $('chron').hidden = true; });
  $('chron').addEventListener('click', e => { if (e.target === $('chron')) $('chron').hidden = true; });

  // ---------- Selection card ----------
  let sel = -1, confirmLife = -1;   // sel: planet index, -2 = the fleet, -1 = nothing
  function renderCard() {
    const card = $('card');
    if (sel === -1 || mode !== 'sim') { card.hidden = true; return; }
    card.hidden = false;
    const act = $('cAct');
    if (sel === -2) {
      $('cName').textContent = 'Flotilla ' + civ.name;
      $('cKind').textContent = `${civ.drive} · ${civ.ethic}`;
      [['Population', fmtPop()], ['Hulls', `1 capital · ${4 + S.ships.length} frigates`], ['Council', FACTIONS[civ.drive].name], ['Developments', `${civ.techs.size} / ${TECHS.length}`]]
        .forEach(([l, v], k) => { $('cL' + (k + 1)).textContent = l; $('cV' + (k + 1)).textContent = v; });
      const shares = Object.keys(civ.inf).sort((a, b) => civ.inf[b] - civ.inf[a]).map(k => `${FACTIONS[k].name} ${Math.round(civ.inf[k] * 100)}%`).join(' · ');
      const L = civ.leader;
      const speaker = L ? ` Speaker ${L.name}${L.eternal ? ', uploaded' : ''}, ${Math.floor(civ.age - L.since)} years in office.` : '';
      $('cView').textContent = DRIVES[civ.drive].about + ' ' + ETHICS[civ.ethic].about + ' Factions: ' + shares + '.' + speaker;
      $('cNote').textContent = '';
      act.hidden = true; $('cAct2').hidden = true;
      return;
    }
    act.hidden = false;
    const p = sys.planets[sel], lvl = S.hubs[sel] || 0, left = 1 - p.mined;
    $('cName').textContent = p.name;
    $('cKind').textContent = p.type.label + ' · ' + p.au.toFixed(2) + ' AU';
    $('cL1').textContent = 'Mass'; $('cV1').textContent = kg(p.mass);
    $('cL2').textContent = 'Remaining'; $('cV2').textContent = left <= 0 ? 'None' : (left * 100).toFixed(left < 0.01 ? 2 : 0) + '%';
    const home = S.settled[sel];
    $('cL3').textContent = home != null ? 'Settlement' : 'Mining hub';
    $('cV3').textContent = S.pending[sel] ? 'En route' : home != null ? Math.round(home * 100) + '% built' : lvl ? 'Level ' + lvl : 'None';
    $('cL4').textContent = 'Yield'; $('cV4').textContent = '×' + yieldOf(p).toFixed(2);
    const c = bestHub();
    const nn = p.natives && p.natives.contact !== 'cleared' ? p.natives : null;
    $('cView').textContent = rivalOwns(sel) ? `Council: this world belongs to the ${S.rival.name}.`
      : nn && nn.contact === 'quarantine' ? 'Council: this world is closed. Nobody lands; nobody speaks.'
      : nn && nn.contact === 'uplift' ? `Council: the ${nn.name} sit with us now.`
      : nn && nn.contact === 'clearance' ? 'Council: clearance is under way.'
      : home != null ? 'Council: this is home to some of them now. They will not mine it.'
      : p.type.life && !lvl && civ.ethic === 'Reverent' ? 'Council: this world is sacred. They will not touch it while anything else remains.'
      : c && c.i === sel ? 'Council: this is their next target.' : lvl ? 'Council: an active works.' : 'Council: low priority.';
    const n = p.natives;
    const nState = n ? { quarantine: 'quarantined', uplift: 'uplifted, on the council', clearance: 'clearance under way', cleared: 'gone' }[n.contact] || 'not yet contacted' : '';
    $('cNote').textContent = n ? `Native civilization: the ${n.name} · ${n.contact === 'cleared' ? 'extinct' : NATIVE_AGES[n.level]} · ${nState}${n.protected ? ' · protected' : ''}.`
      : p.type.life && !lvl ? 'Native microbial life detected.' : p.type.gas ? 'Atmosphere skimming only. Slow, but nearly endless.' : '';
    const a2 = $('cAct2');
    a2.hidden = !(n && n.contact !== 'cleared' && !n.protected);
    act.classList.remove('danger');
    if (left <= 0 || S.pending[sel] || lvl >= 4) { act.textContent = left <= 0 ? 'Depleted' : S.pending[sel] ? 'Constructor en route' : 'Fully developed'; act.disabled = true; return; }
    const cost = hubCost(sel);
    act.disabled = S.stock < cost;
    if (!lvl && (p.type.life || home != null) && confirmLife === sel) { act.textContent = home != null ? 'Confirm: evacuate and mine' : 'Confirm: order it mined'; act.classList.add('danger'); return; }
    act.innerHTML = '';
    act.append(lvl ? 'Order expansion' : home != null ? 'Order it mined' : 'Order a hub here');
    const cs = document.createElement('span'); cs.className = 'cost'; cs.textContent = kg(cost); act.append(cs);
  }
  $('cAct').addEventListener('click', () => {
    if (sel < 0) return;
    const p = sys.planets[sel], lvl = S.hubs[sel] || 0;
    if (S.stock < hubCost(sel) || p.mined >= 1 || S.pending[sel]) return;
    if (!lvl && (p.type.life || p.i in S.settled) && confirmLife !== sel) { confirmLife = sel; renderCard(); return; }
    confirmLife = -1;
    startHub(sel, true);
    renderCard();
  });
  $('cClose').addEventListener('click', () => select(-1));

  let zoomGoal = null;
  function select(i) {
    sel = i; confirmLife = -1;
    planetObjs.forEach((o, k) => o.selRing.visible = k === i);
    zoomGoal = i >= 0 ? sys.planets[i].size * 7 + 2 : i === -2 ? 0.16 : I.home.length();
    renderCard();
  }
  controls.addEventListener('start', () => { zoomGoal = null; });
  controls.autoRotateSpeed = 0.25;

  function rebuildHub(i) {
    const o = planetObjs[i], lvl = S.hubs[i] || 0;
    disposeTree(o.hubGroup); o.hubGroup.clear();
    for (let k = 0; k < lvl; k++) {
      const r = new THREE.Mesh(new THREE.TorusGeometry(o.p.size * (1.35 + k * 0.18), 0.012, 6, 72), new THREE.MeshBasicMaterial({ color: 0xdd8c3c }));
      r.rotation.x = Math.PI / 2 + 0.35 * (k % 2 ? -1 : 1);
      o.hubGroup.add(r);
    }
    if (lvl) { const b = sprite(softDot, 0xffb060); b.scale.set(0.4, 0.4, 1); b.position.y = o.p.size * 1.1; o.hubGroup.add(b); }
  }
  function rebuildFoundries() {
    disposeTree(foundryGroup); foundryGroup.clear();
    for (let k = 0; k < S.foundries; k++) {
      const f = new THREE.Group();
      f.add(new THREE.Mesh(new THREE.TorusGeometry(0.22, 0.025, 6, 24), hullMat));
      f.add(box(0.06, 0.06, 0.45, darkMat, 0, 0, 0));
      const core = sprite(softDot, 0xff9a4a); core.scale.set(0.4, 0.4, 1); f.add(core);
      const a = k / S.foundries * Math.PI * 2, r = swarmOut + 1.2;
      f.position.set(Math.cos(a) * r, (k % 2 ? 0.4 : -0.4), Math.sin(a) * r);
      foundryGroup.add(f);
    }
  }
  function ensureHabitat() {
    if (habitat || !sysGroup) return;
    habitat = new THREE.Group();
    habitat.add(new THREE.Mesh(new THREE.TorusGeometry(0.09, 0.008, 8, 64), hullMat));
    for (let k = 0; k < 3; k++) { const sp = box(0.18, 0.003, 0.003, darkMat, 0, 0, 0); sp.rotation.z = k * Math.PI / 3; habitat.add(sp); }
    sysGroup.add(habitat);
  }

  // ---------- Development sheet ----------
  const treeRefs = {};
  (function buildTree() {
    const root = $('branches');
    BRANCHES.forEach(b => {
      const sec = document.createElement('section');
      sec.className = 'branch';
      sec.innerHTML = '<div class="bhead"><div><h3></h3><small></small></div><span class="interest"></span><button type="button">Inspire</button></div>';
      sec.querySelector('h3').textContent = b.name;
      sec.querySelector('small').textContent = b.blurb;
      const btn = sec.querySelector('button');
      btn.addEventListener('click', () => inspire(b.id));
      treeRefs[b.id] = { interest: sec.querySelector('.interest'), btn, techs: [] };
      TECHS.filter(t => t.b === b.id).sort((x, y) => x.tier - y.tier).forEach(t => {
        const row = document.createElement('div');
        row.className = 'tech';
        row.innerHTML = '<span class="st"></span><div><b></b><small></small><i class="pb"></i></div>';
        row.querySelector('b').textContent = t.name;
        row.querySelector('small').textContent = t.fx;
        sec.append(row);
        treeRefs[b.id].techs.push({ t, row });
      });
      root.append(sec);
    });
  })();
  function updateTree() {
    if ($('tree').hidden || !S) return;
    $('rpRate').textContent = `${rpRate().toFixed(1)} research / year · the civilization chooses; you can inspire a direction`;
    BRANCHES.forEach(b => {
      const r = treeRefs[b.id], n = clamp(Math.round(civ.w[b.id] * 1.5), 1, 6);
      r.interest.textContent = 'Interest ' + '●'.repeat(n) + '○'.repeat(6 - n);
      r.btn.disabled = !ready('inspire');
      r.btn.textContent = ready('inspire') ? 'Inspire' : 'Inspire · ' + cdText('inspire');
      r.techs.forEach(({ t, row }) => {
        const active = S.research && S.research.id === t.id;
        row.className = 'tech ' + (civ.techs.has(t.id) ? 'done' : active ? 'active' : prereqDone(t) ? 'avail' : 'locked');
        if (active) row.querySelector('.pb').style.setProperty('--p', clamp(S.research.p / t.cost));
      });
    });
  }
  $('treeBtn').addEventListener('click', () => { $('tree').hidden = false; updateTree(); });
  $('treeClose').addEventListener('click', () => { $('tree').hidden = true; });
  $('tree').addEventListener('click', e => { if (e.target === $('tree')) $('tree').hidden = true; });

  // ---------- Labels (tap targets) ----------
  const labelBox = $('labels');
  let labels = [];
  function buildLabels() {
    labelBox.innerHTML = '';
    labels = sys.planets.map(p => {
      const b = document.createElement('button');
      b.type = 'button'; b.className = 'plabel';
      b.textContent = p.name.split(' ')[1].toUpperCase() + ' · ' + p.type.label.split(' ')[0];
      b.addEventListener('click', () => select(p.i));
      labelBox.append(b);
      return { el: b, pos: () => planetObjs[p.i].g.position, off: p.size * 1.2, i: p.i };
    });
    const f = document.createElement('button');
    f.type = 'button'; f.className = 'plabel fleet'; f.textContent = 'Flotilla ' + civ.name;
    f.addEventListener('click', () => select(-2));
    labelBox.append(f);
    labels.push({ el: f, pos: () => fleet[0].g.position, off: 0.03, i: -2 });
  }
  const proj = new V3();
  function updateLabels() {
    const w = window.innerWidth, h = window.innerHeight;
    labels.forEach(L => {
      const b = L.el;
      if (mode !== 'sim') { b.hidden = true; return; }
      proj.copy(L.pos()); proj.y -= L.off;
      proj.project(camera);
      const vis = proj.z < 1 && Math.abs(proj.x) < 1.1 && Math.abs(proj.y) < 1.1 && (L.i !== -2 || sel === -2 || camera.position.distanceTo(parkNow) < 12);
      b.hidden = !vis;
      if (!vis) return;
      b.style.transform = `translate(${(proj.x * 0.5 + 0.5) * w}px, ${(-proj.y * 0.5 + 0.5) * h}px) translate(-50%, 0)`;
      b.classList.toggle('sel', sel === L.i);
      if (L.i >= 0) {
        b.classList.toggle('hub', !!S.hubs[L.i]);
        b.classList.toggle('home', L.i in S.settled);
        b.classList.toggle('gone', sys.planets[L.i].mined >= 1);
      }
    });
  }

  // ---------- Tap-to-select on the 3D scene ----------
  const ray = new THREE.Raycaster(), ndc = new THREE.Vector2();
  let downAt = null;
  canvas.addEventListener('pointerdown', e => { downAt = [e.clientX, e.clientY]; });
  canvas.addEventListener('pointerup', e => {
    if (!downAt || mode !== 'sim') return;
    const moved = Math.hypot(e.clientX - downAt[0], e.clientY - downAt[1]);
    downAt = null;
    if (moved > 8) return;
    ndc.set(e.clientX / window.innerWidth * 2 - 1, -(e.clientY / window.innerHeight) * 2 + 1);
    ray.setFromCamera(ndc, camera);
    const hits = ray.intersectObjects(planetObjs.map(o => o.hit), false);
    select(hits.length ? hits[0].object.userData.planet : -1);
  });

  // ---------- Intro choreography ----------
  let I = null;
  function setupIntro() {
    const p0 = sys.planets[0], P0 = planetPos(0, 0), out = P0.clone().normalize();
    const tan = new V3().crossVectors(UP, out).normalize();
    const park = P0.clone().addScaledVector(tan, p0.size + 0.35).addScaledVector(UP, 0.06);
    const far = sys.planets[sys.planets.length - 1].orbit * 0.6 + 30;
    const E = out.clone().applyAxisAngle(UP, 0.55).multiplyScalar(far).addScaledVector(UP, far * 0.12);
    const d = park.clone().sub(E).normalize();
    const right = new V3().crossVectors(d, UP).normalize(), up2 = new V3().crossVectors(right, d);
    const site = P0.clone().add(tan.clone().multiplyScalar(0.7).addScaledVector(out, 0.3).addScaledVector(UP, 0.4).normalize().multiplyScalar(p0.size * 1.005));
    const homeR = sys.planets[Math.min(2, sys.planets.length - 1)].orbit * 2.1 + 12;
    I = { p0, P0, out, tan, park, E, d, right, up2, site, home: new V3(0.35, 0.62, 1).normalize().multiplyScalar(homeR) };
  }
  const offsetVec = o => I.right.clone().multiplyScalar(o[0]).addScaledVector(I.up2, o[1]).addScaledVector(I.d, o[2]);
  const TW = i => 3 + i * 0.32, T_ARRIVE = 21;
  function introShip(i, t) {
    const off = offsetVec(FORM[i]), E = I.E.clone().add(off), Pk = I.park.clone().add(off), tw = TW(i);
    if (t < tw) return { pos: E, vis: false, stretch: 0, burn: 0 };
    if (t < tw + 0.35) { const u = (t - tw) / 0.35; return { pos: E.clone().addScaledVector(I.d, -(1 - u) * (1 - u) * 1.5), vis: true, stretch: 1 - u, burn: 1 }; }
    const u = clamp((t - tw - 0.35) / (T_ARRIVE - tw - 0.35)), e = 1 - Math.pow(1 - u, 2.6);
    return { pos: E.clone().lerp(Pk, e), vis: true, stretch: 0, burn: 1 - smooth(17, T_ARRIVE, t) };
  }
  function placeFleetIntro(t) {
    let flash = 0;
    fleet.forEach((f, i) => {
      const s = introShip(i, t);
      let pos = s.pos, look = s.pos.clone().add(I.d), burn = s.burn, vis = s.vis;
      if (i === LANDER) {
        const k = smooth(19.5, 25, t);
        if (k > 0) {
          pos = pos.clone().lerp(I.site, k);
          look = pos.clone().add(pos.clone().sub(I.P0).normalize());
          burn = 0.5;
        }
        f.g.scale.setScalar(f.scale * (1 - 0.5 * k));
        vis = vis && t < 25.2;
      } else {
        f.g.scale.set(f.scale, f.scale, f.scale * (1 + s.stretch * 30));
      }
      f.g.visible = vis;
      f.g.position.copy(pos);
      f.g.lookAt(look);
      setPlumes(f, burn, i);
      const tw = TW(i);
      flash = Math.max(flash, t > tw - 0.1 ? Math.exp(-(t - tw) * 5) : 0);
    });
    warpFlash.position.copy(I.E);
    warpFlash.material.opacity = flash * 0.6;
    warpFlash.scale.setScalar(0.01 + flash * 0.06);
    warpFlash.visible = flash > 0.01;
    fleetMarker.visible = false;
    extraFleet.forEach(f => { f.g.visible = false; });
  }
  function setPlumes(f, burn, i) {
    f.plumes.forEach(p => { p.visible = burn > 0.02; p.material.uniforms.uI.value = burn * (0.9 + 0.1 * Math.sin(wall * 40 + i)); });
    const blink = Math.sin(wall * 4 + i) > 0.6 ? 1 : 0.12;
    f.nav.forEach(n => n.material.opacity = blink);
  }
  let parkNow = new V3();
  function placeFleetSim() {
    const P0 = planetPos(0, S.year), tan = new V3().crossVectors(UP, P0.clone().normalize()).normalize();
    parkNow = P0.clone().addScaledVector(tan, sys.planets[0].size + 0.35).addScaledVector(UP, 0.06);
    fleet.forEach((f, i) => {
      if (i === LANDER) { f.g.visible = false; return; }
      f.g.visible = true;
      f.g.scale.setScalar(f.scale);
      f.g.position.copy(parkNow).add(offsetVec(FORM[i])).addScaledVector(UP, Math.sin(wall * 0.4 + i) * 0.002);
      f.g.lookAt(f.g.position.clone().add(I.d));
      setPlumes(f, 0, i);
    });
    warpFlash.visible = false;
    placeExtraFleet();
    placeVisitor();
    placeRival();
    fleetMarker.visible = camera.position.distanceTo(parkNow) > 1.5;
    fleetMarker.position.copy(parkNow);
    if (habitat) {
      habitat.position.copy(parkNow).addScaledVector(I.right, 0.14).addScaledVector(UP, 0.03);
      habitat.rotation.set(0.5, wall * 0.1, wall * 0.4);
    }
  }

  // Frigates the civilization builds for itself patrol in a loose ring around the capital ship
  const MAX_EXTRA = 12;
  const SHIP_NAMES = ['Vigilant Ash', 'Low Tide', 'Saint of Hours', 'Unremembered', 'Margin of Error', 'Second Sun', 'Cartographer',
    'Brief Mercy', 'Iron Psalm', 'Long Division', 'Ember Tithe', 'Quiet Arithmetic', 'Patience', 'Grave Accent', 'Small Hours'];
  const extraFleet = [];
  function placeExtraFleet() {
    while (extraFleet.length < S.ships.length) {
      const f = Object.assign(makeFrigate(), { scale: (0.55 + Math.random() * 0.25) * K });
      f.g.scale.setScalar(f.scale); scene.add(f.g); extraFleet.push(f);
    }
    extraFleet.forEach((f, k) => {
      f.g.visible = k < S.ships.length && mode === 'sim';
      if (!f.g.visible) return;
      const a = k * 2.4 + wall * 0.02, r = (4.5 + (k % 4) * 0.9) * K;
      f.g.position.copy(parkNow).addScaledVector(I.right, Math.cos(a) * r).addScaledVector(I.d, Math.sin(a) * r).addScaledVector(UP, Math.sin(k * 1.7) * 1.2 * K);
      // face along the patrol circle
      tmp2.copy(f.g.position).addScaledVector(I.right, -Math.sin(a)).addScaledVector(I.d, Math.cos(a));
      f.g.lookAt(tmp2);
      setPlumes(f, 0.25, k + 7);
    });
  }
  const tmp2 = new V3();

  // Constructors in transit: screen-space glints that fly from the fleet to their target
  const missionPool = [];
  function updateMissions(dt) {
    const running = S.speed > 0;
    while (missionPool.length < S.missions.length) {
      const m = sprite(softDot, 0xffb060, { sizeAttenuation: false }); m.scale.set(0.014, 0.014, 1); scene.add(m); missionPool.push(m);
    }
    missionPool.forEach((m, k) => { m.visible = k < S.missions.length; });
    for (let k = S.missions.length - 1; k >= 0; k--) {
      const ms = S.missions[k], spr = missionPool[k];
      if (running) ms.t += dt;
      const u = clamp(ms.t / ms.dur), P = planetPos(ms.i, S.year);
      const mid = parkNow.clone().lerp(P, 0.5); mid.y += 1.2;
      const e = u * u * (3 - 2 * u);
      spr.position.copy(parkNow).multiplyScalar((1 - e) * (1 - e)).addScaledVector(mid, 2 * e * (1 - e)).addScaledVector(P, e * e);
      if (u >= 1 && ms.settle) {
        S.missions.splice(k, 1);
        delete S.pending[ms.i];
        S.settled[ms.i] = 0.02;
        { const o = planetObjs[ms.i]; fx('pulse', { at: () => o.g.position, r: o.p.size * 3, color: 0xffd6a0 }); }
        logEvent(sys.planets[ms.i].type.gas ? `The first cloud city rises over ${sys.planets[ms.i].name}.` : `The first city is founded on ${sys.planets[ms.i].name}.`, 'era', { planet: ms.i });
        continue;
      }
      if (u >= 1) {
        S.missions.splice(k, 1);
        delete S.pending[ms.i];
        S.hubs[ms.i] = (S.hubs[ms.i] || 0) + 1;
        rebuildHub(ms.i);
        { const o = planetObjs[ms.i]; fx('pulse', { at: () => o.g.position, r: o.p.size * 3, color: 0xdd8c3c }); }
        const p = sys.planets[ms.i];
        logEvent(S.hubs[ms.i] > 1 ? `${p.name} works expanded to level ${S.hubs[ms.i]}.`
          : p.type.life ? `Mining began on ${p.name}. Its biosphere will not survive the first decade.` : `Mining hub online on ${p.name}.`, 'council', { planet: ms.i });
      }
    }
    splinterMarker.visible = wall - S.splinterAt < 10;
    if (splinterMarker.visible) {
      const u = (wall - S.splinterAt) / 10;
      splinterMarker.position.copy(parkNow).addScaledVector(I.d, u * u * 300).addScaledVector(UP, -u * 40);
      splinterMarker.material.opacity = 1 - u;
    }
    seedMarker.visible = !!(S.seedLaunched && wall - S.seedAt < 10);
    if (seedMarker.visible) {
      const u = (wall - S.seedAt) / 10;
      seedMarker.position.copy(parkNow).addScaledVector(I.d, -u * u * 400).addScaledVector(UP, u * 30);
      seedMarker.material.opacity = 1 - u;
    }
  }

  function introCamera(t) {
    const cap = introShip(0, Math.max(t, TW(0) + 0.4)).pos;
    const aPos = I.E.clone().addScaledVector(I.d, -5 * K).addScaledVector(I.right, 5 * K).addScaledVector(I.up2, 1.4 * K);
    const aTgt = I.E.clone().addScaledVector(I.d, 10 * K);
    // ride alongside the capital ship, slightly below its wing, as it decelerates
    const drift = (t - 6) * 0.06;
    const bPos = cap.clone().addScaledVector(I.d, (-5.5 + drift) * K).addScaledVector(I.right, (4.2 - drift * 0.6) * K).addScaledVector(I.up2, 0.9 * K);
    const bTgt = cap.clone().addScaledVector(I.d, 0.6 * K);
    const cPos = I.park.clone().addScaledVector(I.tan, 0.09).addScaledVector(I.out, -0.06).addScaledVector(UP, 0.03);
    const cTgt = I.park.clone().lerp(I.P0, 0.15);
    const w1 = smooth(5.5, 7.5, t), w2 = smooth(15.5, 19, t), w3 = smooth(24.5, 29.5, t);
    const pos = aPos.lerp(bPos, w1).lerp(cPos, w2).lerp(I.home, w3);
    const tgt = aTgt.lerp(bTgt, w1).lerp(cTgt, w2).lerp(ORIGIN.clone(), w3);
    camera.position.copy(pos);
    camera.lookAt(tgt);
  }

  let introLog = [];
  function buildIntroLog() {
    const p0 = sys.planets[0], st = sys.star;
    const opener = civ.gen > 1 ? `Generation ${civ.gen}. The seed ship drops out of fold space at ${sys.name}, a class ${st.cls} star.`
      : `Dropping out of fold space. Target: ${sys.name}, a class ${st.cls} star.`;
    introLog = [
      [0, 'Fold', opener],
      [6.5, 'T+0', `Flotilla ${civ.name} has arrived. ${sys.planets.length} planets${sys.companion ? `, and a red dwarf companion ${Math.round(sys.companion.au)} AU out` : ''}. No signals. No one home.`],
      [12, 'T+3 d', `${p0.name} is ${/^[aeiou]/i.test(p0.type.label) ? 'an' : 'a'} ${p0.type.label.toLowerCase()} of ${kg(p0.mass)}. Ideal feedstock.`],
      [18, 'T+7 d', 'Constructor descending. The first mining hub goes down now.'],
      [24.5, 'Year 0', 'Hub online. From here on, the flotilla decides for itself. You may watch, or interfere.']
    ];
  }
  function renderIntroLog(t) {
    let idx = 0;
    for (let k = 0; k < introLog.length; k++) if (introLog[k][0] <= t) idx = k;
    const [t0, stamp, text] = introLog[idx];
    $('logPrev').textContent = idx ? introLog[idx - 1][1] + '  ' + introLog[idx - 1][2] : '';
    const cur = $('logCur');
    cur.innerHTML = '';
    const s = document.createElement('span'); s.className = 'stamp'; s.textContent = stamp;
    cur.append(s, text.slice(0, Math.floor((t - t0) * 40)));
  }

  // ---------- Modes ----------
  let mode = 'intro', introT = 0, wall = 0;
  function newSystem(seed, legacy) {
    if (!legacy) civ = makeCiv();
    computeM();
    S = freshState(legacy && M.gate ? 3 : 1);
    sel = -1; logLines = []; zoomGoal = null;
    buildSystem(generateSystem(seed));
    setupIntro();
    buildIntroLog();
    buildLabels();
    rebuildFoundries();
    if (M.habitat) ensureHabitat();
    mode = 'intro'; introT = 0;
    controls.enabled = false;
    camera.near = 0.004; camera.updateProjectionMatrix();
    $('introBar').hidden = false; $('simBar').hidden = true; $('godBar').hidden = true; $('focus').hidden = true;
    $('win').hidden = true; $('card').hidden = true; $('tree').hidden = true; $('seedBtn').hidden = true; $('chron').hidden = true; $('fixBtn').hidden = true;
    director.until = 0; director.hold = 0; director.lastAt = -99;
    $('log').classList.add('typing');
    setSub();
    setSpeed(1);
    tuneAudio();
  }
  function enterSim() {
    mode = 'sim';
    if (!S.hubs[0]) { S.hubs[0] = 1; rebuildHub(0); }
    camera.position.copy(I.home);
    camera.near = 0.02; camera.updateProjectionMatrix();
    controls.target.set(0, 0, 0);
    controls.enabled = true;
    controls.update();
    $('introBar').hidden = true; $('resumeBtn').hidden = true; $('simBar').hidden = false; $('godBar').hidden = false; $('focus').hidden = false;
    $('log').classList.remove('typing');
    logLines = [];
    if (S.restored) { logEvent(`Resumed. Flotilla ${civ.name}, year ${Math.floor(S.year)} at ${sys.name}.`, 'council'); }
    else logEvent(`Mining hub online on ${sys.planets[0].name}. The council takes over.`, 'era');
    if (!S.research) pickResearch();
    maybeShowGuide();
  }

  // ---------- Save and resume (this browser only; silently does nothing if storage is unavailable) ----------
  // bump SAVE_V whenever system generation changes, so old saves don't resume into a different system
  const SAVE_V = 2, SAVE_KEY = 'kardashev-save';
  function saveGame() {
    if (mode !== 'sim' || !S) return;
    const hubs = Object.assign({}, S.hubs);
    for (const k in S.pending) hubs[k] = (hubs[k] || 0) + 1;   // constructors in flight count as arrived
    const st = Object.assign({}, S, { hubs, pending: {}, missions: [], visitor: null, restored: true });
    const data = { v: SAVE_V, seed: sys.seed, mined: sys.planets.map(p => p.mined), natives: sys.planets.map(p => p.natives), S: st,
      civ: Object.assign({}, civ, { techs: [...civ.techs] }) };
    try { localStorage.setItem(SAVE_KEY, JSON.stringify(data)); } catch (e) { /* storage blocked: nothing to do */ }
  }
  function loadSave() {
    try { const d = JSON.parse(localStorage.getItem(SAVE_KEY) || 'null'); return d && d.v === SAVE_V ? d : null; } catch (e) { return null; }
  }
  function resumeGame(d) {
    civ = Object.assign(makeCiv(), d.civ, { techs: new Set(d.civ.techs) });
    newSystem(d.seed, true);
    Object.assign(S, d.S, { speed: 1, won: d.S.won });
    sys.planets.forEach((p, i) => { p.mined = d.mined[i] || 0; if (d.natives) p.natives = d.natives[i]; });
    for (const k in S.hubs) rebuildHub(+k);
    rebuildFoundries();
    if (M.habitat) ensureHabitat();
    if (S.seedLaunched) $('seedBtn').hidden = false;
    if (S.crisis) $('fixBtn').hidden = false;
    introT = INTRO_LEN;
  }
  setInterval(saveGame, 5000);
  document.addEventListener('visibilitychange', () => { if (document.hidden) saveGame(); });
  $('resumeBtn').addEventListener('click', () => { const d = loadSave(); if (d) resumeGame(d); });
  function offerResume() {
    const d = loadSave(), b = $('resumeBtn');
    b.hidden = !d;
    if (d) b.textContent = `Resume Flotilla ${d.civ.name} · year ${Math.floor(d.S.year)}`;
  }
  $('skipBtn').addEventListener('click', () => { introT = INTRO_LEN; });
  $('helpBtn').addEventListener('click', () => { $('guide').hidden = false; });
  $('guideClose').addEventListener('click', () => { $('guide').hidden = true; });
  $('guide').addEventListener('click', e => { if (e.target === $('guide')) $('guide').hidden = true; });
  function maybeShowGuide() {
    let seen = false;
    try { seen = localStorage.getItem('kardashev-guide') === '1'; localStorage.setItem('kardashev-guide', '1'); } catch (e) { /* no storage: show it every time */ }
    if (!seen && location.hash !== '#debug') $('guide').hidden = false;
  }
  $('newBtn').addEventListener('click', () => newSystem(Math.floor(Math.random() * 1e9), false));
  $('winNew').addEventListener('click', () => newSystem(Math.floor(Math.random() * 1e9), false));
  $('winGo').addEventListener('click', () => { $('win').hidden = true; setSpeed(lastSpeed); });
  // ---------- Autoplay: let the story run across generations without input ----------
  const auto = { on: true, winSince: -1, followAt: -1 };
  $('autoBtn').addEventListener('click', () => {
    auto.on = !auto.on;
    $('autoBtn').setAttribute('aria-pressed', String(auto.on));
    auto.followAt = -1;
  });
  function autoplayStep() {
    if (!auto.on || mode !== 'sim') return;
    if (!$('win').hidden) {
      if (auto.winSince < 0) auto.winSince = wall;
      if (wall - auto.winSince > 9) { auto.winSince = -1; $('winGo').click(); }
    } else auto.winSince = -1;
    const doneHere = (S.project && S.project.done) || S.capture >= 0.95;
    if (S.seedLaunched && doneHere && S.year > (S.seedYear || 0) + 30 && auto.followAt < 0 && $('win').hidden) {
      auto.followAt = wall + 8;
      logEvent(`The chronicle leaves ${sys.name} and follows the seed ship to ${S.nextName}.`, 'era', { fleet: true });
    }
    if (auto.followAt > 0 && wall > auto.followAt) { auto.followAt = -1; $('seedBtn').click(); }
  }
  function epitaph() {
    const gone = sys.planets.filter(p => p.mined >= 1).length;
    const nat = sys.planets.filter(p => p.natives).map(p => `the ${p.natives.name} ${{ quarantine: 'left alone', uplift: 'uplifted', clearance: 'cleared', cleared: 'cleared' }[p.natives.contact] || 'never met'}`);
    const parts = [`${Math.floor(S.year)} years`, `${pct(S.capture)} of the star captured`, gone ? `${gone} world${gone > 1 ? 's' : ''} consumed` : 'no world consumed',
      `peak population ${S.peakPop < 1 ? Math.round(S.peakPop * 1000) + ' million' : S.peakPop.toFixed(1) + ' billion'}`];
    if (nat.length) parts.push(nat.join(', '));
    if (S.project && S.project.done) parts.push(PROJECTS[S.project.drive].name + ' built');
    return parts.join(' · ');
  }
  $('seedBtn').addEventListener('click', () => {
    const ep = epitaph();
    logEvent(`Epitaph for ${sys.name}: ${ep}.`, 'era');
    civ.lineage.push({ gen: civ.gen, name: sys.name, cls: sys.star.cls, years: Math.floor(S.year), cap: S.capture, project: S.project && S.project.done ? PROJECTS[S.project.drive].name : '', ep });
    civ.gen++;
    const keep = lastSpeed;
    newSystem(S.nextSeed, true);
    setSpeed(keep);
  });

  const speedBtns = [...document.querySelectorAll('[data-speed]')];
  let lastSpeed = 1;
  function setSpeed(v) {
    if (S) { if (v > 0) lastSpeed = v; S.speed = v; }
    speedBtns.forEach(b => b.setAttribute('aria-pressed', String(+b.dataset.speed === v)));
  }
  speedBtns.forEach(b => b.addEventListener('click', () => setSpeed(+b.dataset.speed)));

  // ---------- Post-processing ----------
  const composer = new THREE.EffectComposer(renderer);
  composer.addPass(new THREE.RenderPass(scene, camera));
  composer.addPass(new THREE.UnrealBloomPass(new THREE.Vector2(256, 256), 0.95, 0.55, 0.78));
  const grade = new THREE.ShaderPass({
    uniforms: { tDiffuse: { value: null }, uTime: { value: 0 }, uFade: { value: 1 }, uRes: { value: new THREE.Vector2(1, 1) } },
    vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }`,
    fragmentShader: `uniform sampler2D tDiffuse; uniform float uTime; uniform float uFade; uniform vec2 uRes; varying vec2 vUv;
      float rnd(vec2 p){ return fract(sin(dot(p, vec2(12.9898,78.233))) * 43758.5453); }
      void main(){
        vec3 c = texture2D(tDiffuse, vUv).rgb;
        float l = dot(c, vec3(0.299,0.587,0.114));
        c = mix(vec3(l), c, 0.78);
        c += vec3(-0.004, 0.004, 0.010) * (1.0 - smoothstep(0.0, 0.3, l));
        c = c / (1.0 + 0.15*c);
        vec2 d = vUv - 0.5; c *= mix(0.4, 1.0, smoothstep(0.85, 0.2, length(d * vec2(uRes.x/uRes.y, 1.0) * 0.9)));
        c += (rnd(vUv*uRes + fract(uTime*7.0)*100.0) - 0.5) * 0.04;
        gl_FragColor = vec4(c * uFade, 1.0); }`
  });
  composer.addPass(grade);

  function resize() {
    const w = window.innerWidth, h = window.innerHeight;
    renderer.setSize(w, h, false);
    composer.setSize(w, h);
    camera.aspect = w / h;
    camera.fov = w < h ? 62 : 45;
    camera.updateProjectionMatrix();
    grade.uniforms.uRes.value.set(w, h);
  }
  window.addEventListener('resize', resize);
  resize();

  // ---------- Event effects ----------
  const fxList = [];
  const fxRingGeo = new THREE.RingGeometry(0.96, 1, 96);
  function fx(type, o = {}) {
    if (mode !== 'sim') return;
    if (type === 'flare') {
      const m = new THREE.Mesh(fxRingGeo, new THREE.MeshBasicMaterial({ color: 0xffa060, transparent: true, side: THREE.DoubleSide, blending: THREE.AdditiveBlending, depthWrite: false }));
      m.rotation.x = Math.PI / 2 + (Math.random() - 0.5) * 0.4;
      scene.add(m);
      fxList.push({ type, t0: wall, dur: 3.5, m });
    } else if (type === 'pulse') {
      const m = new THREE.Mesh(fxRingGeo, new THREE.MeshBasicMaterial({ color: o.color, transparent: true, side: THREE.DoubleSide, blending: THREE.AdditiveBlending, depthWrite: false }));
      m.rotation.x = Math.PI / 2;
      scene.add(m);
      fxList.push({ type, t0: wall, dur: 2.2, m, at: o.at, r: o.r });
    } else if (type === 'comet') {
      const head = sprite(softDot, 0xdfefff, { sizeAttenuation: false }); head.scale.set(0.025, 0.025, 1);
      const tg = new THREE.BufferGeometry(); tg.setAttribute('position', new THREE.BufferAttribute(new Float32Array(24 * 3), 3));
      const tail = new THREE.Line(tg, new THREE.LineBasicMaterial({ color: 0x9fc8ff, transparent: true, opacity: 0.7, blending: THREE.AdditiveBlending, depthWrite: false }));
      const from = new V3(rnd(-1, 1), rnd(0.2, 0.6), rnd(-1, 1)).normalize().multiplyScalar(sys.planets[sys.planets.length - 1].orbit * 1.3 + 30);
      scene.add(head, tail);
      fxList.push({ type, t0: wall, dur: 5, head, tail, from, trail: [] });
    }
  }
  function updateFx() {
    for (let k = fxList.length - 1; k >= 0; k--) {
      const f = fxList[k], u = (wall - f.t0) / f.dur;
      if (u >= 1) {
        [f.m, f.head, f.tail].forEach(o => { if (o) { scene.remove(o); if (o.material) o.material.dispose(); if (o.geometry && o.geometry !== fxRingGeo) o.geometry.dispose(); } });
        fxList.splice(k, 1);
        continue;
      }
      if (f.type === 'flare') {
        f.m.scale.setScalar(sys.star.r + u * swarmOut * 1.6);
        f.m.material.opacity = (1 - u) * 0.8;
        corona.scale.setScalar(sys.star.r * 6.5 * (1 + 0.6 * Math.sin(Math.PI * Math.min(1, u * 2))));
      } else if (f.type === 'pulse') {
        f.m.position.copy(f.at());
        f.m.scale.setScalar(0.05 + u * f.r);
        f.m.material.opacity = (1 - u) * 0.9;
      } else if (f.type === 'comet') {
        const e = 1 - Math.pow(1 - u, 2);
        f.head.position.copy(f.from).lerp(parkNow, e);
        f.head.material.opacity = 1 - smooth(0.85, 1, u);
        f.trail.unshift(f.head.position.clone()); if (f.trail.length > 24) f.trail.pop();
        const arr = f.tail.geometry.attributes.position.array;
        for (let i = 0; i < 24; i++) { const q = f.trail[Math.min(i, f.trail.length - 1)]; arr[i * 3] = q.x; arr[i * 3 + 1] = q.y; arr[i * 3 + 2] = q.z; }
        f.tail.geometry.attributes.position.needsUpdate = true;
        f.tail.material.opacity = 0.7 * (1 - u);
      }
    }
  }

  // ---------- Moons: a little company for the larger worlds ----------
  const moonMat = new THREE.MeshStandardMaterial({ color: 0x77736c, roughness: 0.95, metalness: 0 });
  SHARED.add(moonMat);
  function addMoons(o) {
    const k = o.p.type.gas ? 2 + Math.floor(Math.random() * 2) : o.p.size > 0.8 ? Math.floor(Math.random() * 2) : 0;
    o.moons = [];
    for (let i = 0; i < k; i++) {
      const m = new THREE.Mesh(new THREE.SphereGeometry(o.p.size * rnd(0.08, 0.16), 16, 12), moonMat);
      o.g.add(m);
      o.moons.push({ m, r: o.p.size * rnd(1.8, 3.2), w: rnd(0.15, 0.5), ph: Math.random() * 6.28, inc: rnd(-0.3, 0.3) });
    }
  }

  // ---------- Per-frame world update ----------
  const tmp = new V3(), A = new V3(), Bc = new V3(), focus = new V3();
  function updateWorld() {
    updateFx();
    planetObjs.forEach(o => o.moons && o.moons.forEach(mn => {
      const a = mn.ph + wall * mn.w;
      mn.m.position.set(Math.cos(a) * mn.r, Math.sin(a) * mn.r * mn.inc, Math.sin(a) * mn.r);
    }));
    const year = mode === 'sim' ? S.year : 0;
    starMat.uniforms.uTime.value = wall;
    star.rotation.y = wall * 0.01;
    if (companionObj) {
      const c = sys.companion, a = c.phase + (mode === 'sim' ? S.year : 0) * 0.01;
      companionObj.g.position.set(Math.cos(a) * c.orbit, Math.sin(a) * c.orbit * c.inc, Math.sin(a) * c.orbit);
      companionObj.mat.uniforms.uTime.value = wall;
    }

    const vis = S.capture <= 0 ? 0 : clamp(Math.log10(S.capture / 1e-6) / Math.log10(WIN_AT / 1e-6));
    starMat.uniforms.uDim.value = 1 - 0.35 * S.capture;
    corona.material.opacity = 1 - 0.65 * S.capture;
    sunLight.intensity = 2.3 * (1 - 0.35 * S.capture);
    hazeMat.uniforms.uO.value = 0.6 * S.capture;
    jets.forEach((j, k) => { j.visible = M.starLift; j.material.uniforms.uI.value = 0.55 + 0.25 * Math.sin(wall * 3 + k * 2); });
    const pr = S.project, pp = pr ? pr.p : 0;
    engineMirror.visible = !!pr && pr.drive === 'Industrious';
    if (engineMirror.visible) { engineMirror.material.opacity = 0.25 + 0.6 * pp; engineMirror.scale.setScalar(0.3 + 0.7 * pp); jets[1].visible = true; jets[1].scale.set(1 + pp * 0.6, 1 + pp * 2.2, 1 + pp * 0.6); }
    swarmMat.uniforms.uEmi.value.copy(SWARM_EMI_A).lerp(SWARM_EMI_B, pr && pr.drive === 'Contemplative' ? pp : 0);
    swarmMat.uniforms.uEmiI.value = pr && pr.drive === 'Contemplative' ? 0.3 + 0.9 * pp : 0.3;
    gate.visible = !!pr && pr.drive === 'Expansionist' && mode === 'sim';
    if (gate.visible) {
      gate.position.copy(parkNow).addScaledVector(I.d, -1.6).addScaledVector(UP, 0.4);
      gate.lookAt(gate.position.clone().add(I.d));
      gate.scale.setScalar(0.15 + 0.85 * pp);
      gateDisc.material.opacity = pr.done ? 0.35 + 0.15 * Math.sin(wall * 2) : 0;
    }
    shell2.visible = civ.techs.has('E4') || (!!pr && pr.drive === 'Contemplative');
    if (shell2.visible) shell2.material.uniforms.uO.value = 0.25 + 0.35 * S.capture;
    swarmGeo.instanceCount = Math.floor(perf.swarmCap * vis);
    swarmMat.uniforms.uTime.value = wall;
    swarmMat.uniforms.uIn.value = swarmIn;
    swarmMat.uniforms.uOut.value = swarmOut;
    swarmMat.uniforms.uLight.value.copy(sunLight.color).multiplyScalar(sunLight.intensity / 2.3);

    planetObjs.forEach((o, i) => {
      o.g.position.copy(planetPos(i, year));
      o.mesh.rotation.y = wall * 0.05 + i;
      o.mesh.material.uniforms.uMined.value = o.p.mined;
      const nat = o.p.natives && o.p.natives.contact !== 'cleared' ? (o.p.natives.level + 1) / 5 * 0.7 : 0;
      o.mesh.material.uniforms.uCity.value = Math.max(nat, mode === 'sim' ? (S.settled[i] || 0) : 0);
      const left = 1 - o.p.mined, sc = o.p.type.gas ? 1 : clamp(left / 0.25, 0.001, 1);
      o.mesh.scale.setScalar(sc);
      o.mesh.visible = !(o.p.mined >= 1 && !o.p.type.gas) && !o.belt;
      if (o.belt) { o.belt.count = Math.floor(o.belt.userData.N * (1 - o.p.mined)); o.belt.rotation.y = -(o.p.phase + o.p.w * year) * 0.3; }
      o.hubGroup.rotation.y = wall * 0.3;
      o.hubGroup.visible = o.mesh.visible;
      const d = o.p.type.gas || o.belt ? 0 : smooth(0.6, 1, o.p.mined);
      o.debris.visible = d > 0.01;
      o.debris.material.opacity = 0.55 * d;
      o.debris.rotation.y = o.p.phase + o.p.w * year;
    });
    foundryGroup.children.forEach((f, k) => { f.rotation.y = wall * 0.4 + k; f.rotation.x = 0.4; });

    const active = mode === 'sim' ? Object.keys(S.hubs).filter(k => S.hubs[k] && sys.planets[k].mined < 1).map(Number) : [];
    stream.visible = active.length > 0;
    if (stream.visible) {
      const launches = active.map(k => { const P = planetPos(k, year); return P.clone().addScaledVector(P.clone().normalize(), -sys.planets[k].size); });
      streamGeo.setDrawRange(0, perf.streamCap);
      for (let i = 0; i < perf.streamCap; i++) {
        const d = streamData[i], L = launches[i % launches.length], u = (wall * d.spd + d.ph) % 1;
        const r = swarmIn + d.rn * (swarmOut - swarmIn);
        tmp.set(Math.cos(d.a) * r, Math.sin(d.inc) * r, Math.sin(d.a) * r);
        Bc.copy(L).lerp(tmp, 0.5); Bc.y += d.lift;
        A.copy(L).multiplyScalar((1 - u) * (1 - u)).addScaledVector(Bc, 2 * u * (1 - u)).addScaledVector(tmp, u * u);
        streamPos[i * 3] = A.x; streamPos[i * 3 + 1] = A.y; streamPos[i * 3 + 2] = A.z;
      }
      streamGeo.attributes.position.needsUpdate = true;
    }
  }

  // ---------- Main loop ----------
  let last = performance.now(), hudTick = 0;
  newSystem(Math.floor(Math.random() * 1e9), false);
  offerResume();

  // ---------- Adaptive quality: drop resolution, then swarm density, when the frame rate sags ----------
  const perf = { frames: 0, t: 0, level: 0, swarmCap: SWARM_N, streamCap: STREAM };
  function adaptQuality(dt) {
    perf.frames++; perf.t += dt;
    if (perf.t < 3) return;
    const fps = perf.frames / perf.t;
    perf.frames = 0; perf.t = 0;
    if (fps > 40 || perf.level >= 3) return;
    perf.level++;
    if (perf.level === 1 && renderer.getPixelRatio() > 1.25) { renderer.setPixelRatio(1.25); resize(); }
    else if (perf.level <= 2 && renderer.getPixelRatio() > 1) { renderer.setPixelRatio(1); resize(); }
    else { perf.swarmCap = Math.floor(SWARM_N * 0.5); perf.streamCap = Math.floor(STREAM * 0.5); perf.level = 3; }
  }

  // test hook, only with #debug in the URL: jump the intro clock or the sim year
  if (location.hash === '#debug') window.__kd = { intro: t => { introT = t; }, years: y => simStep(y), state: () => S, civ: () => civ, tech: id => { civ.techs.add(id); computeM(); }, sys: () => sys, look: w => { director.lastAt = -99; directTo(w); }, crisis: () => startCrisis(), cam: () => [camera.position.toArray(), controls.target.toArray(), swarmOut, I.home.toArray()] };

  function tick(now) {
    const dt = Math.min((now - last) / 1000, 0.05);
    last = now;
    wall += reduceMotion ? dt * 0.3 : dt;

    if (mode === 'intro') {
      introT += dt;
      if (introT >= INTRO_LEN) enterSim();
      else {
        placeFleetIntro(introT);
        introCamera(introT);
        renderIntroLog(introT);
        grade.uniforms.uFade.value = smooth(0, 2, introT);
      }
    }
    if (mode === 'sim') {
      if (S.speed > 0) simStep(dt * S.speed);
      autoplayStep();
      placeFleetSim();
      updateMissions(dt);
      let rate = 3;
      const directing = director.on && sel === -1 && wall > director.hold;
      if (sel >= 0) focus.copy(planetObjs[sel].g.position);
      else if (sel === -2) focus.copy(parkNow);
      else if (directing) {
        const shot = wall < director.until ? director.shot : null;
        if (shot !== director.current) { director.current = shot; director.cut = true; }
        focus.copy(shot ? shot.pos() : ORIGIN);
        if (director.cut) {
          // hard cut: frame the subject from a sunlit three-quarter angle, then dip in from black
          director.cut = false; director.fadeAt = wall;
          const dist = shot ? shot.dist : I.home.length();
          // off-origin subjects are framed from along their orbit, a little outward and above, so the
          // camera never ends up inside the swarm or a planet; the star is framed from high above the plane
          let dir;
          if (focus.lengthSq() > 1e-6) {
            const out = focus.clone().setY(0).normalize(), tan = new V3().crossVectors(UP, out);
            dir = tan.multiplyScalar(Math.random() < 0.5 ? -1 : 1).addScaledVector(out, 0.4).addScaledVector(UP, 0.45).normalize();
          } else dir = new V3(Math.cos(wall), 0.28, Math.sin(wall)).normalize();
          controls.target.copy(focus);
          camera.position.copy(focus).addScaledVector(dir, dist);
        }
        rate = 8;
      } else focus.copy(ORIGIN);
      tmp.copy(focus).sub(controls.target).multiplyScalar(Math.min(1, dt * rate));
      controls.target.add(tmp);
      camera.position.add(tmp);
      controls.autoRotate = directing && !reduceMotion;
      if (zoomGoal) {
        const off = camera.position.clone().sub(controls.target), dist = off.length();
        const nd = dist + (zoomGoal - dist) * Math.min(1, dt * 2.5);
        camera.position.copy(controls.target).addScaledVector(off.normalize(), nd);
        if (Math.abs(nd - zoomGoal) < zoomGoal * 0.02) zoomGoal = null;
      }
      controls.update();
      grade.uniforms.uFade.value = smooth(0, 0.45, wall - director.fadeAt);
    }
    updateWorld();
    if (!document.hidden && location.hash !== '#debug') adaptQuality(dt);
    if (hudTick++ % 6 === 0) updateHud();
    updateLabels();
    grade.uniforms.uTime.value = wall;
    composer.render();
    requestAnimationFrame(tick);
  }
  requestAnimationFrame(tick);
})();
