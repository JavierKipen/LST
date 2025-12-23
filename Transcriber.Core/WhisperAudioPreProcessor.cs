using System.Buffers;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Transcriber.Core
{
    public static class WhisperAudioPreprocessor
    {
        /// <summary>
        /// Decodes an audio file (mp3/wav/m4a depending on codecs), converts to mono, resamples to 16kHz,
        /// and returns 32-bit float PCM samples in memory (what Whisper.net can consume directly).
        /// </summary>
        public static float[] LoadAsMono16kFloatSamples(string inputPath)
        {
            // AudioFileReader outputs IEEE float samples and “ensures we are in PCM format” (conceptually).
            // Actual supported formats depend on available codecs (Media Foundation on Windows, etc.). :contentReference[oaicite:3]{index=3}
            using var reader = new AudioFileReader(inputPath);

            ISampleProvider sampleProvider = reader;

            // Downmix to mono if needed.
            if (sampleProvider.WaveFormat.Channels == 2)
            {
                sampleProvider = new StereoToMonoSampleProvider(sampleProvider)
                {
                    LeftVolume = 0.5f,
                    RightVolume = 0.5f
                };
            }
            else if (sampleProvider.WaveFormat.Channels > 2)
            {
                // Simple approach: you can implement a custom downmix here.
                // (Most interview recordings will be mono or stereo.)
                throw new NotSupportedException($"Unsupported channel count: {sampleProvider.WaveFormat.Channels}");
            }

            // Resample to 16kHz using WDL resampler (fully managed). :contentReference[oaicite:4]{index=4}
            if (sampleProvider.WaveFormat.SampleRate != 16000)
            {
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 16000);
            }

            return ReadAllSamples(sampleProvider);
        }

        private static float[] ReadAllSamples(ISampleProvider provider)
        {
            // Read in chunks to avoid huge allocations; then compact to exact size.
            var buffer = ArrayPool<float>.Shared.Rent(16000 * 10); // ~10 seconds buffer at 16kHz mono
            try
            {
                var samples = new List<float>(capacity: 16000 * 60); // start with ~1 minute
                int read;
                while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                        samples.Add(buffer[i]);
                }
                return samples.ToArray();
            }
            finally
            {
                ArrayPool<float>.Shared.Return(buffer);
            }
        }
    }
}
