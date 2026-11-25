using System;
using System.IO;
using System.Media;
using System.Text;
using System.Threading;

namespace FirewallControl.App.Services;

/// <summary>
/// Sintetiza y reproduce en memoria el sonido orgánico suave y relajante de inicio de Sefiros POS:
/// colchón tibio envolvente (Fmaj9 -> Cmaj9), arpegio aterciopelado tipo piano con fieltro / arpa dulce
/// y un susurro armónico final en reverberación de sala cálida al brillar la estrella.
/// </summary>
public static class SefirosStartupSoundService
{
    private const int SampleRate = 44100;
    private const short Channels = 2;
    private const short BitsPerSample = 16;
    private const double DurationSeconds = 2.85;

    private static readonly Lazy<byte[]> CachedWavBytes = new(GenerateWavBytesInternal, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Permite silenciar la reproducción real durante pruebas automatizadas.
    /// </summary>
    public static bool SuppressPlayback { get; set; }

    public static byte[] BuildStartupChimeWavBytes() => CachedWavBytes.Value;

    public static IDisposable? PlayStartupChime()
    {
        if (SuppressPlayback)
            return null;

        MemoryStream? ms = null;
        SoundPlayer? player = null;
        try
        {
            byte[] wav = CachedWavBytes.Value;
            ms = new MemoryStream(wav, writable: false);
            player = new SoundPlayer(ms);
            player.Play();
            return new PlaybackHandle(player, ms);
        }
        catch
        {
            try { player?.Dispose(); } catch { }
            try { ms?.Dispose(); } catch { }
            return null;
        }
    }

    private static byte[] GenerateWavBytesInternal()
    {
        int totalFrames = (int)Math.Round(SampleRate * DurationSeconds);
        int dataBytes = totalFrames * Channels * (BitsPerSample / 8);
        byte[] buffer = new byte[44 + dataBytes];

        // Pad cálido envolvente (Fmaj9 -> Cmaj9): (inicio, dur, freqHz, amplitud, paneo -1..1)
        var pads = new (double StartSec, double DurSec, double FreqHz, double Amp, double Pan)[]
        {
            (0.00, 1.55,  87.307, 0.24, -0.15), // F2 cálido
            (0.02, 1.55, 130.813, 0.16,  0.15), // C3
            (0.06, 1.50, 195.998, 0.11,  0.00), // G3
            (1.05, 1.80,  65.406, 0.23,  0.00), // C2 profundo
            (1.08, 1.77, 130.813, 0.14, -0.18), // C3
            (1.12, 1.73, 246.942, 0.08,  0.20)  // B3 novena/séptima mayor suave
        };

        // Notas dulces tipo piano con fieltro / arpa suave (sin estridencias metálicas)
        var feltNotes = new (double StartSec, double DurSec, double FreqHz, double Amp, double Pan, double DecayRate)[]
        {
            (0.16, 0.95, 261.626, 0.13, -0.22, 3.0), // C4
            (0.44, 0.95, 329.628, 0.13,  0.18, 3.0), // E4
            (0.72, 1.05, 391.995, 0.14, -0.14, 2.8), // G4
            (1.00, 1.15, 493.883, 0.13,  0.18, 2.7), // B4
            // Cierre relajante al encenderse la estrella al final de la S
            (1.32, 1.50, 523.251, 0.15,  0.00, 2.2), // C5 tibio principal
            (1.41, 1.44, 587.330, 0.10, -0.18, 2.3), // D5 novena relajante
            (1.50, 1.35, 783.991, 0.085, 0.18, 2.5), // G5 susurro cristalino suave
            (1.60, 1.25, 1046.50, 0.042, 0.00, 2.9)  // C6 halo muy sutil
        };

        double[] dryL = new double[totalFrames];
        double[] dryR = new double[totalFrames];
        const double twoPi = Math.PI * 2.0;

        for (int i = 0; i < totalFrames; i++)
        {
            double t = (double)i / SampleRate;
            double sL = 0.0;
            double sR = 0.0;

            // Capa 1: Colchón grave cálido respirando lentamente
            for (int p = 0; p < pads.Length; p++)
            {
                var (start, dur, freq, amp, pan) = pads[p];
                double dt = t - start;
                if (dt <= 0.0 || dt >= dur)
                    continue;

                double attack = dt < 0.52 ? 0.5 * (1.0 - Math.Cos(Math.PI * dt / 0.52)) : 1.0;
                double tailT = dt / dur;
                double decay = tailT < 0.50 ? 1.0 : Math.Cos(Math.PI * (tailT - 0.50));
                decay = Math.Max(0.0, decay * decay);

                double wave = 0.84 * Math.Sin(twoPi * freq * dt)
                            + 0.16 * Math.Sin(twoPi * (freq * 2.002) * dt);

                double v = wave * amp * attack * decay;
                double panL = Math.Sqrt(0.5 * (1.0 - pan));
                double panR = Math.Sqrt(0.5 * (1.0 + pan));
                sL += v * panL;
                sR += v * panR;
            }

            // Capa 2: Piano con fieltro / arpa dulce con caída suave del segundo armónico
            for (int f = 0; f < feltNotes.Length; f++)
            {
                var (start, dur, freq, amp, pan, decayRate) = feltNotes[f];
                double dt = t - start;
                if (dt <= 0.0 || dt >= dur)
                    continue;

                double attack = dt < 0.036 ? 0.5 * (1.0 - Math.Cos(Math.PI * dt / 0.036)) : 1.0;
                double env = amp * attack * Math.Exp(-dt * decayRate);

                // Micro-asentamiento orgánico inicial del martillo/cuerda (suave, nada robótico)
                double phase = twoPi * freq * (dt + 0.00018 * (1.0 - Math.Exp(-dt * 45.0)));
                double harmEnv = Math.Exp(-dt * (decayRate * 1.9));
                double tone = 0.88 * Math.Sin(phase) + 0.12 * harmEnv * Math.Sin(2.0 * phase);

                double v = tone * env;
                double panL = Math.Sqrt(0.5 * (1.0 - pan));
                double panR = Math.Sqrt(0.5 * (1.0 + pan));
                sL += v * panL;
                sR += v * panR;
            }

            dryL[i] = sL;
            dryR[i] = sR;
        }

        // Capa 3: Reverberación estéreo acústica de sala cálida (4 líneas comb amortiguadas en pasa-bajos)
        int[] delaysL = { 1307, 1613, 1987, 2411 };
        int[] delaysR = { 1361, 1667, 2029, 2459 };
        double[] fbGains = { 0.76, 0.74, 0.72, 0.70 };

        double[] wetL = ApplyDampedCombReverb(dryL, delaysL, fbGains, dampFactor: 0.42);
        double[] wetR = ApplyDampedCombReverb(dryR, delaysR, fbGains, dampFactor: 0.42);

        using var ms = new MemoryStream(buffer);
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataBytes);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write(Channels);
        bw.Write(SampleRate);
        bw.Write(SampleRate * Channels * (BitsPerSample / 8));
        bw.Write((short)(Channels * (BitsPerSample / 8)));
        bw.Write(BitsPerSample);
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataBytes);

        // Pasa-bajos maestro suave (~3.4 kHz) para redondear el carácter orgánico final
        double lpL = 0.0;
        double lpR = 0.0;
        const double alpha = 0.36;

        for (int i = 0; i < totalFrames; i++)
        {
            double t = (double)i / SampleRate;
            double mixL = dryL[i] * 0.72 + wetL[i] * 0.30;
            double mixR = dryR[i] * 0.72 + wetR[i] * 0.30;

            lpL += alpha * (mixL - lpL);
            lpR += alpha * (mixR - lpR);

            double edgeEnv = 1.0;
            if (t < 0.020)
                edgeEnv *= t / 0.020;
            double remaining = DurationSeconds - t;
            if (remaining < 0.140)
                edgeEnv *= Math.Max(0.0, remaining / 0.140);

            double outL = 0.78 * Math.Tanh(lpL * edgeEnv);
            double outR = 0.78 * Math.Tanh(lpR * edgeEnv);

            short pcmL = (short)Math.Clamp(Math.Round(outL * short.MaxValue), short.MinValue, short.MaxValue);
            short pcmR = (short)Math.Clamp(Math.Round(outR * short.MaxValue), short.MinValue, short.MaxValue);
            bw.Write(pcmL);
            bw.Write(pcmR);
        }

        return buffer;
    }

    private static double[] ApplyDampedCombReverb(double[] input, int[] delays, double[] gains, double dampFactor)
    {
        int n = input.Length;
        double[] output = new double[n];

        for (int c = 0; c < delays.Length; c++)
        {
            int delay = delays[c];
            double g = gains[c];
            double[] ring = new double[delay];
            int idx = 0;
            double lp = 0.0;

            for (int i = 0; i < n; i++)
            {
                double delayed = ring[idx];
                // Amortiguación pasa-bajos en cada rebote de sala para sonido sedoso, nada metálico
                lp = delayed * (1.0 - dampFactor) + lp * dampFactor;
                ring[idx] = input[i] + lp * g;
                output[i] += lp * 0.25;

                idx++;
                if (idx >= delay)
                    idx = 0;
            }
        }

        return output;
    }

    private sealed class PlaybackHandle : IDisposable
    {
        private SoundPlayer? _player;
        private MemoryStream? _stream;

        public PlaybackHandle(SoundPlayer player, MemoryStream stream)
        {
            _player = player;
            _stream = stream;
        }

        public void Dispose()
        {
            var player = Interlocked.Exchange(ref _player, null);
            var stream = Interlocked.Exchange(ref _stream, null);

            if (player != null)
            {
                try { player.Stop(); } catch { }
                try { player.Dispose(); } catch { }
            }
            if (stream != null)
            {
                try { stream.Dispose(); } catch { }
            }
        }
    }
}
