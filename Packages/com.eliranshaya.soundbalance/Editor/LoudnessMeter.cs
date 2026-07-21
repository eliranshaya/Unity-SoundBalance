using System;

namespace SoundBalance
{
    /// <summary>
    /// Integrated loudness measurement (LUFS) per ITU-R BS.1770-4.
    /// Mirrors the pyloudnorm reference implementation (K-weighting filter class),
    /// including its block counting and gating behaviour, so results match it closely.
    /// </summary>
    public static class LoudnessMeter
    {
        private const double BlockSeconds = 0.400;   // gating block length (T_g)
        private const double Overlap = 0.75;         // block overlap
        private const double AbsoluteGate = -70.0;   // LUFS

        /// <summary>
        /// Measures integrated loudness of interleaved PCM samples.
        /// Returns double.NegativeInfinity for silence.
        /// Clips shorter than one 400ms block are zero-padded (padding does not
        /// change the loudness of the audible content).
        /// </summary>
        public static double IntegratedLoudness(float[] interleaved, int channels, int sampleRate)
        {
            if (channels < 1) throw new ArgumentException("channels must be >= 1");
            if (sampleRate < 8000) throw new ArgumentException("sampleRate too low");

            int frames = interleaved.Length / channels;
            int blockSize = (int)(BlockSeconds * sampleRate);
            int paddedFrames = Math.Max(frames, blockSize);

            var highShelf = Biquad.KWeightingHighShelf(sampleRate);
            var highPass = Biquad.KWeightingHighPass(sampleRate);

            var filtered = new double[channels][];
            for (int c = 0; c < channels; c++)
            {
                var x = new double[paddedFrames];
                for (int i = 0; i < frames; i++)
                    x[i] = interleaved[i * channels + c];
                highShelf.ApplyInPlace(x);
                highPass.ApplyInPlace(x);
                filtered[c] = x;
            }

            double step = BlockSeconds * (1.0 - Overlap); // 100ms hop
            double totalSeconds = (double)paddedFrames / sampleRate;
            int numBlocks = (int)Math.Round((totalSeconds - BlockSeconds) / step, MidpointRounding.ToEven) + 1;
            if (numBlocks < 1) numBlocks = 1;

            // Mean square of each K-weighted block, per channel
            var z = new double[channels][];
            for (int c = 0; c < channels; c++) z[c] = new double[numBlocks];
            for (int j = 0; j < numBlocks; j++)
            {
                int lower = (int)(BlockSeconds * (j * (1.0 - Overlap)) * sampleRate);
                int upper = Math.Min((int)(BlockSeconds * (j * (1.0 - Overlap) + 1.0) * sampleRate), paddedFrames);
                for (int c = 0; c < channels; c++)
                {
                    double sum = 0.0;
                    var x = filtered[c];
                    for (int i = lower; i < upper; i++) sum += x[i] * x[i];
                    z[c][j] = sum / blockSize;
                }
            }

            var blockLoudness = new double[numBlocks];
            for (int j = 0; j < numBlocks; j++)
            {
                double weighted = 0.0;
                for (int c = 0; c < channels; c++)
                    weighted += ChannelGain(c) * z[c][j];
                blockLoudness[j] = -0.691 + 10.0 * Math.Log10(weighted);
            }

            // Absolute gate (-70 LUFS)
            double relativeThreshold = GatedLoudness(z, blockLoudness, channels, numBlocks, AbsoluteGate) - 10.0;
            // Relative gate (-10 LU below the absolutely-gated level)
            double gate = Math.Max(AbsoluteGate, relativeThreshold);
            return GatedLoudness(z, blockLoudness, channels, numBlocks, gate);
        }

        private static double GatedLoudness(double[][] z, double[] blockLoudness, int channels, int numBlocks, double gate)
        {
            int count = 0;
            for (int j = 0; j < numBlocks; j++)
                if (blockLoudness[j] > gate) count++;
            if (count == 0) return double.NegativeInfinity;

            double weighted = 0.0;
            for (int c = 0; c < channels; c++)
            {
                double sum = 0.0;
                for (int j = 0; j < numBlocks; j++)
                    if (blockLoudness[j] > gate) sum += z[c][j];
                weighted += ChannelGain(c) * (sum / count);
            }
            return -0.691 + 10.0 * Math.Log10(weighted);
        }

        // BS.1770 channel weights: L/R/C = 1.0, surround = 1.41 (same table as pyloudnorm)
        private static double ChannelGain(int channel) => channel < 3 ? 1.0 : 1.41;
    }

    /// <summary>Second-order IIR filter (direct form II transposed), designed like pyloudnorm's IIRfilter.</summary>
    public readonly struct Biquad
    {
        private readonly double b0, b1, b2, a1, a2;

        private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            this.b0 = b0 / a0;
            this.b1 = b1 / a0;
            this.b2 = b2 / a0;
            this.a1 = a1 / a0;
            this.a2 = a2 / a0;
        }

        /// <summary>K-weighting stage 1: +4dB high shelf at 1500Hz, Q=1/sqrt(2).</summary>
        public static Biquad KWeightingHighShelf(int sampleRate)
        {
            const double G = 4.0, fc = 1500.0;
            double Q = 1.0 / Math.Sqrt(2.0);
            double A = Math.Pow(10.0, G / 40.0);
            double w0 = 2.0 * Math.PI * (fc / sampleRate);
            double alpha = Math.Sin(w0) / (2.0 * Q);
            double cosW0 = Math.Cos(w0);
            double sqrtA = Math.Sqrt(A);
            return new Biquad(
                A * ((A + 1) + (A - 1) * cosW0 + 2 * sqrtA * alpha),
                -2 * A * ((A - 1) + (A + 1) * cosW0),
                A * ((A + 1) + (A - 1) * cosW0 - 2 * sqrtA * alpha),
                (A + 1) - (A - 1) * cosW0 + 2 * sqrtA * alpha,
                2 * ((A - 1) - (A + 1) * cosW0),
                (A + 1) - (A - 1) * cosW0 - 2 * sqrtA * alpha);
        }

        /// <summary>K-weighting stage 2: high pass at 38Hz, Q=0.5.</summary>
        public static Biquad KWeightingHighPass(int sampleRate)
        {
            const double fc = 38.0, Q = 0.5;
            double w0 = 2.0 * Math.PI * (fc / sampleRate);
            double alpha = Math.Sin(w0) / (2.0 * Q);
            double cosW0 = Math.Cos(w0);
            return new Biquad(
                (1 + cosW0) / 2,
                -(1 + cosW0),
                (1 + cosW0) / 2,
                1 + alpha,
                -2 * cosW0,
                1 - alpha);
        }

        public void ApplyInPlace(double[] x)
        {
            double s1 = 0.0, s2 = 0.0;
            for (int i = 0; i < x.Length; i++)
            {
                double xn = x[i];
                double yn = b0 * xn + s1;
                s1 = b1 * xn - a1 * yn + s2;
                s2 = b2 * xn - a2 * yn;
                x[i] = yn;
            }
        }
    }
}
