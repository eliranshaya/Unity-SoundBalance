using System;

namespace SoundBalance
{
    /// <summary>Silence detection and trimming on raw interleaved PCM samples.</summary>
    public static class SilenceTrimmer
    {
        /// <summary>
        /// Finds the frame range [startFrame, endFrame) that contains audio louder
        /// than <paramref name="thresholdDb"/> (dBFS), padded by <paramref name="paddingMs"/>.
        /// Returns false if the whole clip is below the threshold (range = full clip).
        /// </summary>
        public static bool FindLoudRange(
            float[] interleaved, int channels, int sampleRate,
            float thresholdDb, float paddingMs,
            out int startFrame, out int endFrame)
        {
            int frames = interleaved.Length / channels;
            float threshold = (float)Math.Pow(10.0, thresholdDb / 20.0);

            int first = -1;
            for (int f = 0; f < frames && first < 0; f++)
                for (int c = 0; c < channels; c++)
                    if (Math.Abs(interleaved[f * channels + c]) > threshold)
                    {
                        first = f;
                        break;
                    }

            if (first < 0)
            {
                startFrame = 0;
                endFrame = frames;
                return false;
            }

            int last = first;
            for (int f = frames - 1; f > first && last == first; f--)
                for (int c = 0; c < channels; c++)
                    if (Math.Abs(interleaved[f * channels + c]) > threshold)
                    {
                        last = f;
                        break;
                    }

            int pad = (int)(paddingMs / 1000.0 * sampleRate);
            startFrame = Math.Max(0, first - pad);
            endFrame = Math.Min(frames, last + 1 + pad);
            return true;
        }

        /// <summary>
        /// Copies the frame range [startFrame, endFrame) and applies a short linear
        /// fade-in/out of <paramref name="fadeMs"/> at the edges to avoid clicks.
        /// </summary>
        public static float[] Extract(
            float[] interleaved, int channels, int sampleRate,
            int startFrame, int endFrame, float fadeMs = 3f)
        {
            int totalFrames = interleaved.Length / channels;
            startFrame = Math.Max(0, Math.Min(startFrame, totalFrames));
            endFrame = Math.Max(startFrame, Math.Min(endFrame, totalFrames));

            int frames = endFrame - startFrame;
            var result = new float[frames * channels];
            Array.Copy(interleaved, startFrame * channels, result, 0, result.Length);

            int fadeFrames = Math.Min((int)(fadeMs / 1000.0 * sampleRate), frames / 2);
            for (int f = 0; f < fadeFrames; f++)
            {
                float gainIn = (float)f / fadeFrames;
                float gainOut = (float)(fadeFrames - 1 - f) / fadeFrames;
                for (int c = 0; c < channels; c++)
                {
                    result[f * channels + c] *= gainIn;
                    result[(frames - 1 - f) * channels + c] *= 1f - gainOut;
                }
            }
            return result;
        }
    }
}
