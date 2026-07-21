using System;
using System.IO;
using OggVorbisEncoder;

namespace SoundBalance
{
    /// <summary>Encodes interleaved PCM float samples to an Ogg Vorbis byte stream.</summary>
    public static class OggFileWriter
    {
        private const int ChunkFrames = 4096;

        /// <param name="quality">Vorbis VBR quality, 0..1 (0.5 is roughly 160kbps stereo).</param>
        public static byte[] Encode(float[] interleaved, int channels, int sampleRate, float quality = 0.5f)
        {
            int frames = interleaved.Length / channels;

            // De-interleave into the jagged [channel][frame] layout the encoder expects
            var samples = new float[channels][];
            for (int c = 0; c < channels; c++)
            {
                samples[c] = new float[frames];
                for (int i = 0; i < frames; i++)
                    samples[c][i] = interleaved[i * channels + c];
            }

            var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, quality);
            var oggStream = new OggStream(Guid.NewGuid().GetHashCode());

            using (var output = new MemoryStream())
            {
                oggStream.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
                oggStream.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(new Comments()));
                oggStream.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));
                FlushPages(oggStream, output, true);

                var processingState = ProcessingState.Create(info);
                var chunk = new float[channels][];
                for (int c = 0; c < channels; c++) chunk[c] = new float[ChunkFrames];

                for (int offset = 0; offset < frames; offset += ChunkFrames)
                {
                    int count = Math.Min(ChunkFrames, frames - offset);
                    for (int c = 0; c < channels; c++)
                        Array.Copy(samples[c], offset, chunk[c], 0, count);

                    processingState.WriteData(chunk, count, 0);
                    WritePendingPackets(oggStream, processingState, output);
                }

                processingState.WriteEndOfStream();
                WritePendingPackets(oggStream, processingState, output);
                FlushPages(oggStream, output, true);

                return output.ToArray();
            }
        }

        private static void WritePendingPackets(OggStream oggStream, ProcessingState processingState, Stream output)
        {
            while (!oggStream.Finished && processingState.PacketOut(out OggPacket packet))
            {
                oggStream.PacketIn(packet);
                FlushPages(oggStream, output, false);
            }
        }

        private static void FlushPages(OggStream oggStream, Stream output, bool force)
        {
            while (oggStream.PageOut(out OggPage page, force))
            {
                output.Write(page.Header, 0, page.Header.Length);
                output.Write(page.Body, 0, page.Body.Length);
            }
        }
    }
}
