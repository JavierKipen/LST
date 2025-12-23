using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Wave;

namespace Transcriber.Core
{
    public enum ModelAccuracy
    {
        VeryLow,
        Low,
        Medium,
        Good,
        VeryGood
    }

    internal class Transcriptor
    {
        public GgmlType ggmlType { get; set; }
        public string modelFileName { get; set; }
        public string SwModelsFolder { get; set; }
        public ModelAccuracy DefaultAccuracy { get; set; }
        public bool UseQuantized { get; set; }

        private Dictionary<ModelAccuracy, string> normalModels;
        private Dictionary<ModelAccuracy, string> quantizedModels;

        public Transcriptor()
        {
            ggmlType = GgmlType.Base;
            SwModelsFolder = "C:\\Users\\javier.kipen\\Documents\\GitHub\\LST\\Models\\KBLab\\";
            
            normalModels = new Dictionary<ModelAccuracy, string>
            {
                { ModelAccuracy.VeryLow, "kb-ggml-tiny.bin" },
                { ModelAccuracy.Low, "kb-ggml-base.bin" },
                { ModelAccuracy.Medium, "kb-ggml-small.bin" },
                { ModelAccuracy.Good, "kb-ggml-medium.bin" },
                { ModelAccuracy.VeryGood, "kb-ggml-large.bin" }
            };

            quantizedModels = new Dictionary<ModelAccuracy, string>
            {
                { ModelAccuracy.VeryLow, "kb-ggml-tiny-q5_0.bin" },
                { ModelAccuracy.Low, "kb-ggml-base-q5_0.bin" },
                { ModelAccuracy.Medium, "kb-ggml-small-q5_0.bin" },
                { ModelAccuracy.Good, "kb-ggml-medium-q5_0.bin" },
                { ModelAccuracy.VeryGood, "kb-ggml-large-q5_0.bin" }
            };

            DefaultAccuracy = ModelAccuracy.Low;
            UseQuantized = false;
            
            modelFileName = SwModelsFolder + normalModels[DefaultAccuracy];
        }

        async public Task<string> RunTest(string audioFilePath, ModelAccuracy? accuracy = null, bool? speedBoost = null)
        {
            var selectedAccuracy = accuracy ?? DefaultAccuracy;
            var useQuantizedModel = speedBoost ?? UseQuantized;

            var selectedModelDict = useQuantizedModel ? quantizedModels : normalModels;
            var selectedModelPath = SwModelsFolder + selectedModelDict[selectedAccuracy];

            using var whisperFactory = WhisperFactory.FromPath(selectedModelPath);

            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage("sv")
                .Build();

            var samples = (audioFilePath.EndsWith(".wav")) ? (await GetAvgSamplesWav(audioFilePath)) : (WhisperAudioPreprocessor.LoadAsMono16kFloatSamples(audioFilePath));

            StringBuilder result = new StringBuilder();
            await foreach (var segment in processor.ProcessAsync(samples))
            {
                string line = $"{segment.Start}->{segment.End}: {segment.Text}.\n";
                //Console.WriteLine(line);
                result.Append(line);
            }

            return result.ToString();
        }

        async public Task<float[]> GetAvgSamplesWav(string wavFileName)
        {
            using var fileStream = System.IO.File.OpenRead(wavFileName);

            var waveParser = new WaveParser(fileStream);
            await waveParser.InitializeAsync();
            var channels = waveParser.Channels;
            var sampleRate = waveParser.SampleRate;
            var bitsPerSample = waveParser.BitsPerSample;
            var headerSize = waveParser.DataChunkPosition;
            var frameSize = bitsPerSample / 8 * channels;
            var samples = await waveParser.GetAvgSamplesAsync(CancellationToken.None);
            return samples;
        }
    }

    


}
