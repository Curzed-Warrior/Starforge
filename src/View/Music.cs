using System;
using System.Collections.Generic;
using Godot;
using Starforge.Sim;

namespace Starforge.View;

/// <summary>
/// Generative score, synthesised live: slow pad chords whose mode follows the era, sparse bells,
/// and chimes when history happens. Runs through a reverb + delay bus.
/// </summary>
public partial class Music : Node
{
    const float Rate = 44100f;
    AudioStreamPlayer _player;
    AudioStreamGeneratorPlayback _pb;
    readonly Random _r = new(11);

    class Voice { public float F, TargetF, Ph, Ph2, Amp, TargetAmp, Lp; }
    readonly Voice[] _pad = new Voice[5];
    class Bell { public float F, T, Amp, Decay; }
    readonly List<Bell> _bells = new();
    class Noise { public float T, Dur, Amp, F0, F1, Lp, Bp; public bool Low; }
    readonly List<Noise> _noises = new();

    /// <summary>Sound effects: whoosh (travel), boom (flares, battles), shimmer (arrivals).</summary>
    public void Sfx(string kind)
    {
        switch (kind)
        {
            case "whoosh": _noises.Add(new Noise { Dur = 4.5f, Amp = 0.22f, F0 = 0.004f, F1 = 0.12f }); break;
            case "boom": _noises.Add(new Noise { Dur = 3.5f, Amp = 0.45f, F0 = 0.004f, F1 = 0.002f, Low = true }); break;
            case "shimmer": Ring(new[] { 24, 28, 31, 36, 40 }, 0.07f, 0.06f, 2.5f, 0); break;
        }
    }
    float _chordT, _bellT, _lfo, _time;
    int _chordIdx;
    public int Mood;               // 0 cradle (warm), 1 expansion (bright), 2 swarm (suspended), 3 eclipse (minor), 4 galactic (lydian, vast)
    public float Volume = 0.8f;
    float _master = 0;

    // chord progressions per mood, as semitone offsets from the root (D = 146.83 Hz)
    static readonly int[][][] Prog =
    {
        new[] { new[] { 0, 7, 14, 16, 21 }, new[] { -3, 4, 12, 16, 19 }, new[] { -7, 0, 9, 14, 16 }, new[] { -5, 2, 11, 14, 19 } },
        new[] { new[] { 0, 7, 14, 18, 23 }, new[] { 2, 9, 14, 18, 21 }, new[] { -5, 2, 9, 14, 18 }, new[] { -1, 6, 14, 18, 21 } },
        new[] { new[] { 0, 7, 12, 14, 19 }, new[] { -2, 5, 12, 14, 17 }, new[] { -5, 2, 7, 12, 14 }, new[] { 0, 5, 12, 14, 19 } },
        new[] { new[] { 0, 7, 12, 15, 19 }, new[] { -4, 3, 12, 15, 19 }, new[] { -2, 5, 10, 15, 17 }, new[] { -7, 0, 8, 12, 15 } },
        new[] { new[] { 0, 7, 16, 18, 26 }, new[] { -5, 2, 14, 18, 23 }, new[] { -8, -1, 11, 16, 23 }, new[] { -3, 4, 13, 16, 21 } },
    };
    static readonly int[] Pent = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24 };

    public override void _Ready()
    {
        int bus = AudioServer.BusCount;
        AudioServer.AddBus(bus);
        AudioServer.SetBusName(bus, "Score");
        AudioServer.AddBusEffect(bus, new AudioEffectReverb { RoomSize = 0.92f, Damping = 0.35f, Wet = 0.55f, Dry = 0.7f, Spread = 1f, Hipass = 0.05f });
        AudioServer.AddBusEffect(bus, new AudioEffectDelay { Tap1Active = true, Tap1DelayMs = 420, Tap1LevelDb = -12, Tap2Active = true, Tap2DelayMs = 830, Tap2LevelDb = -18, FeedbackActive = true, FeedbackDelayMs = 620, FeedbackLevelDb = -16 });
        var gen = new AudioStreamGenerator { MixRate = Rate, BufferLength = 0.25f };
        _player = new AudioStreamPlayer { Stream = gen, Bus = "Score", VolumeDb = -6 };
        AddChild(_player);
        _player.Play();
        _pb = (AudioStreamGeneratorPlayback)_player.GetStreamPlayback();
        for (int i = 0; i < _pad.Length; i++) _pad[i] = new Voice();
        SetChord(0, true);
    }

    static float Hz(int semis) => 146.83f * Mathf.Pow(2f, semis / 12f) * 0.5f;

    void SetChord(int idx, bool instant)
    {
        var ch = Prog[Mood][idx % Prog[Mood].Length];
        for (int i = 0; i < _pad.Length; i++)
        {
            var v = _pad[i];
            v.TargetF = Hz(ch[i]);
            if (instant || v.F == 0) v.F = v.TargetF;
            v.TargetAmp = i == 0 ? 0.16f : 0.11f - i * 0.012f;
        }
    }

    /// <summary>React to the chronicle with a short phrase.</summary>
    public void Chime(string kind)
    {
        switch (kind)
        {
            case "era": Ring(new[] { 0, 7, 12, 19 }, 0.5f, 0.22f, 2.6f, -12); break;
            case "break": Ring(new[] { 12, 16, 19, 24 }, 0.12f, 0.12f, 1.6f, 0); break;
            case "loss": Ring(new[] { 0, 3, 7 }, 0.35f, 0.16f, 3f, -24); break;
            case "god": Ring(new[] { 0, 7, 12, 16, 19, 24 }, 0.08f, 0.1f, 3.5f, 0); break;
            case "omen": Ring(new[] { 0, 1, 6 }, 0.6f, 0.14f, 4f, -12); break;
        }
    }

    void Ring(int[] notes, float gap, float amp, float decay, int octave)
    {
        int root = Prog[Mood][_chordIdx % Prog[Mood].Length][0];
        for (int i = 0; i < notes.Length; i++)
            _bells.Add(new Bell { F = Hz(root + notes[i] + octave + 24), T = -i * gap, Amp = amp, Decay = decay });
    }

    public override void _Process(double delta)
    {
        ProcessInner(delta);
    }

    void ProcessInner(double delta)
    {
        if (_pb == null) return;
        float dt = (float)delta;
        _chordT += dt;
        if (_chordT > 18f) { _chordT = 0; _chordIdx++; SetChord(_chordIdx, false); }
        _bellT -= dt;
        if (_bellT <= 0)
        {
            _bellT = 4f + (float)_r.NextDouble() * 9f;
            int root = Prog[Mood][_chordIdx % Prog[Mood].Length][0];
            _bells.Add(new Bell { F = Hz(root + Pent[_r.Next(Pent.Length)] + 24), Amp = 0.035f + (float)_r.NextDouble() * 0.03f, Decay = 5f });
        }
        _master = Mathf.MoveToward(_master, Volume, dt * 0.2f);
        int frames = _pb.GetFramesAvailable();
        if (frames <= 0) return;
        var buf = new Vector2[frames];
        float invRate = 1f / Rate;
        for (int n = 0; n < frames; n++)
        {
            _time += invRate;
            _lfo += invRate * 0.05f;
            float cutoff = 0.035f + 0.025f * Mathf.Sin(_lfo * Mathf.Tau);
            float l = 0, r = 0;
            for (int i = 0; i < _pad.Length; i++)
            {
                var v = _pad[i];
                v.F += (v.TargetF - v.F) * 0.00003f;
                v.Amp += (v.TargetAmp - v.Amp) * 0.00002f;
                v.Ph += v.F * invRate; if (v.Ph > 1) v.Ph -= 1;
                v.Ph2 += v.F * 1.0035f * invRate; if (v.Ph2 > 1) v.Ph2 -= 1;
                float saw = (v.Ph * 2 - 1) + (v.Ph2 * 2 - 1);
                v.Lp += (saw - v.Lp) * cutoff;
                float s = (v.Lp * 0.6f + Mathf.Sin(v.Ph * Mathf.Tau) * 0.5f) * v.Amp;
                float pan = (i - 2) * 0.22f;
                l += s * (1 - pan); r += s * (1 + pan);
            }
            for (int b = _bells.Count - 1; b >= 0; b--)
            {
                var bl = _bells[b];
                bl.T += invRate;
                if (bl.T < 0) continue;
                if (bl.T > bl.Decay * 2.5f) { _bells.RemoveAt(b); continue; }
                float env = Mathf.Exp(-bl.T / bl.Decay * 2.2f) * Mathf.Min(1, bl.T * 200f);
                float ph = bl.F * bl.T * Mathf.Tau;
                float s = (Mathf.Sin(ph) + 0.35f * Mathf.Sin(ph * 2.76f) * Mathf.Exp(-bl.T * 2f) + 0.2f * Mathf.Sin(ph * 5.4f) * Mathf.Exp(-bl.T * 4f)) * env * bl.Amp;
                float pan = Mathf.Sin(bl.F) * 0.5f;
                l += s * (1 - pan); r += s * (1 + pan);
            }
            for (int q = _noises.Count - 1; q >= 0; q--)
            {
                var z = _noises[q];
                z.T += invRate;
                if (z.T > z.Dur) { _noises.RemoveAt(q); continue; }
                float u = z.T / z.Dur;
                float env = Mathf.Sin(Mathf.Pi * Mathf.Pow(u, z.Low ? 0.15f : 0.6f)) * (z.Low ? Mathf.Exp(-u * 3f) : 1f);
                float cut = Mathf.Lerp(z.F0, z.F1, Mathf.Sin(Mathf.Pi * u));
                float wn = (float)_r.NextDouble() * 2f - 1f;
                z.Lp += (wn - z.Lp) * cut;
                z.Bp += (z.Lp - z.Bp) * cut;
                float sN = (z.Low ? z.Bp * 4f + Mathf.Sin(z.T * 38f * Mathf.Tau) * 0.4f * Mathf.Exp(-u * 5f) : z.Lp - z.Bp) * env * z.Amp;
                l += sN; r += sN;
            }
            buf[n] = new Vector2(Mathf.Tanh(l * _master), Mathf.Tanh(r * _master));
        }
        _pb.PushBuffer(buf);
    }

    public static int MoodFor(Simulation sim)
    {
        var h = sim.HomeColony;
        if (sim.Humanity.Colonies.Count > 30) return 4;
        if (h.Capture > 0.3) return 3;
        if (h.Capture > 1e-5) return 2;
        if (h.Power > Defs.TypeI * 0.3) return 1;
        return 0;
    }
}
