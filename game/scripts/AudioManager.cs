using System;
using System.Collections.Generic;
using Godot;

namespace Poopsweeper;

/// <summary>
/// Klänge werden beim Start selbst synthetisiert (kleine Sinus-/Rausch-Bausteine): keine Audio-Dateien,
/// keine Lizenzfragen, winzige Paketgröße.
/// </summary>
public partial class AudioManager : Node
{
    private const int Rate = 22050;
    private const int PoolSize = 8;

    private readonly Dictionary<string, AudioStreamWav> _sounds = new();
    private readonly AudioStreamPlayer[] _pool = new AudioStreamPlayer[PoolSize];
    private readonly Random _noise = new(1234);

    /// <summary>Lautstärke 0..1 (linear).</summary>
    public double Volume { get; set; } = 0.8;

    public override void _Ready()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            _pool[i] = new AudioStreamPlayer();
            AddChild(_pool[i]);
        }
        Build();
    }

    public void Play(string name, float pitch = 1f)
    {
        if (Volume < 0.01 || !_sounds.TryGetValue(name, out var stream)) return;
        AudioStreamPlayer? player = null;
        foreach (var p in _pool)
        {
            if (!p.Playing) { player = p; break; }
        }
        player ??= _pool[0];
        player.Stream = stream;
        player.PitchScale = pitch;
        player.VolumeDb = Mathf.LinearToDb((float)Volume);
        player.Play();
    }

    // ------------------------------------------------------------ Synthese

    private void Build()
    {
        _sounds["dig"] = Make(0.14, t => Sine(Lerp(150, 70, t / 0.14) * t) * Env(t, 0.14, 8) + Noise() * 0.25 * Env(t, 0.05, 6));
        _sounds["pop"] = Make(0.06, t => Sine(760 * t) * Env(t, 0.06, 7) * 0.7);
        _sounds["flag"] = Make(0.14, t => t < 0.06 ? Sine(520 * t) * Env(t, 0.06, 5) : Sine(780 * (t - 0.06)) * Env(t - 0.06, 0.08, 5));
        _sounds["unflag"] = Make(0.08, t => Sine(Lerp(500, 330, t / 0.08) * t) * Env(t, 0.08, 5));
        _sounds["splat"] = Make(0.4, t => Noise() * Env(t, 0.4, 5) * 0.7 * Math.Min(1, (0.4 - t) * 6) + Sine(Lerp(140, 45, t / 0.4) * t) * Env(t, 0.4, 5));
        _sounds["click"] = Make(0.03, t => Sine(1100 * t) * Env(t, 0.03, 6) * 0.6);
        _sounds["hint"] = Make(0.35, t => (Sine(880 * t) + Sine(1320 * t) * 0.7) * Env(t, 0.35, 5) * 0.55);
        _sounds["star"] = Make(0.25, t => Sine(1319 * t) * Env(t, 0.25, 5) * 0.8 + Sine(2638 * t) * Env(t, 0.25, 8) * 0.2);
        _sounds["win"] = Sequence(new[] { 523.0, 659.0, 784.0, 1047.0 }, 0.13, 0.4);
        _sounds["lose"] = Sequence(new[] { 392.0, 330.0, 262.0, 196.0 }, 0.2, 0.5, saw: true);
    }

    private static double Lerp(double a, double b, double f) => a + (b - a) * Math.Clamp(f, 0, 1);
    private static double Sine(double cycles) => Math.Sin(cycles * Math.Tau);
    private double Noise() => _noise.NextDouble() * 2 - 1;

    /// <summary>Exponentielle Ausblendung mit kurzem Anschlag (gegen Knacken).</summary>
    private static double Env(double t, double dur, double decay) =>
        Math.Min(1, t * 400) * Math.Exp(-decay * t / dur) * (t < dur ? 1 : 0);

    private AudioStreamWav Sequence(double[] freqs, double noteLen, double tail, bool saw = false)
    {
        double total = freqs.Length * noteLen + tail;
        return Make(total, t =>
        {
            int i = Math.Min(freqs.Length - 1, (int)(t / noteLen));
            double local = t - i * noteLen;
            double phase = freqs[i] * local;
            double wave = saw ? (phase % 1.0) * 2 - 1 : Sine(phase) + Sine(phase * 2) * 0.25;
            double len = i == freqs.Length - 1 ? noteLen + tail : noteLen * 1.3;
            return wave * Env(local, len, 4) * (saw ? 0.35 : 0.6);
        });
    }

    private static AudioStreamWav Make(double seconds, Func<double, double> sample)
    {
        int n = (int)(seconds * Rate);
        var data = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            double v = Math.Clamp(sample(i / (double)Rate), -1, 1);
            short s = (short)(v * 0.55 * short.MaxValue);
            data[2 * i] = (byte)(s & 0xFF);
            data[2 * i + 1] = (byte)((s >> 8) & 0xFF);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Stereo = false,
            Data = data,
        };
    }
}

/// <summary>Plattform-Dinge hinter einer Tür: so bleibt der Rest des Spiels für Android/iOS/Windows gleich.</summary>
public static class Platform
{
    public static bool VibrationEnabled { get; set; } = true;

    public static void Vibrate(int ms)
    {
        if (VibrationEnabled) Input.VibrateHandheld(ms);
    }

    public static void OpenUrl(string url) => OS.ShellOpen(url);
}
