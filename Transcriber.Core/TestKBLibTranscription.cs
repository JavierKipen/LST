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

    public class Transcriptor
    {
        public GgmlType ggmlType { get; set; }
        public string modelFileName { get; set; }
        public string SwModelsFolder { get; set; }
        public ModelAccuracy DefaultAccuracy { get; set; }
        public bool UseQuantized { get; set; }

        private Dictionary<ModelAccuracy, string> normalModels;
        private Dictionary<ModelAccuracy, string> quantizedModels;

        private float[] audioSamples;
        private WhisperFactory whisperFactory;
        private WhisperProcessor processor;
        private string currentAudioFilePath;
        private StringBuilder transcriptionResult;
        private bool isProcessing;
        private bool isComplete;
        private double progressPercentage;
        private TimeSpan totalAudioDuration;
        private TimeSpan processedAudioDuration;

        public bool IsProcessing => isProcessing;
        public bool IsComplete => isComplete;
        public double Progress => progressPercentage;

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
            
            transcriptionResult = new StringBuilder();
            isProcessing = false;
            isComplete = false;
            progressPercentage = 0.0;
        }

        public Transcriptor(string customModelPath)
        {
            ggmlType = GgmlType.Base;
            SwModelsFolder = Path.GetDirectoryName(customModelPath) + "\\";
            
            // Use only the custom model file
            var modelFile = Path.GetFileName(customModelPath);
            normalModels = new Dictionary<ModelAccuracy, string>
            {
                { ModelAccuracy.VeryLow, modelFile },
                { ModelAccuracy.Low, modelFile },
                { ModelAccuracy.Medium, modelFile },
                { ModelAccuracy.Good, modelFile },
                { ModelAccuracy.VeryGood, modelFile }
            };

            quantizedModels = normalModels;

            DefaultAccuracy = ModelAccuracy.Low;
            UseQuantized = false;
            
            modelFileName = customModelPath;
            
            transcriptionResult = new StringBuilder();
            isProcessing = false;
            isComplete = false;
            progressPercentage = 0.0;
        }

        public async Task SetupRun(string audioFilePath, ModelAccuracy? accuracy = null, bool? speedBoost = null)
        {
            if (isProcessing)
            {
                throw new InvalidOperationException("Cannot setup while processing is in progress");
            }

            Reset();

            var selectedAccuracy = accuracy ?? DefaultAccuracy;
            var useQuantizedModel = speedBoost ?? UseQuantized;

            var selectedModelDict = useQuantizedModel ? quantizedModels : normalModels;
            var selectedModelPath = SwModelsFolder + selectedModelDict[selectedAccuracy];

            currentAudioFilePath = audioFilePath;

            // Validate model file exists and is accessible
            if (!File.Exists(selectedModelPath))
            {
                throw new FileNotFoundException($"Whisper model file not found at: {selectedModelPath}");
            }

            // Check if file is readable
            try
            {
                using var testStream = File.OpenRead(selectedModelPath);
                if (testStream.Length == 0)
                {
                    throw new InvalidOperationException($"Model file is empty: {selectedModelPath}");
                }
            }
            catch (Exception ex) when (ex is not FileNotFoundException)
            {
                throw new InvalidOperationException($"Cannot access model file: {selectedModelPath}. Error: {ex.Message}", ex);
            }

            // Try loading with better error context
            try
            {
                whisperFactory = WhisperFactory.FromPath(selectedModelPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load Whisper model from: {selectedModelPath}\n" +
                    $"File size: {new FileInfo(selectedModelPath).Length} bytes\n" +
                    $"Error: {ex.Message}",
                    ex);
            }

            processor = whisperFactory.CreateBuilder()
                .WithLanguage("sv")
                .Build();

            audioSamples = audioFilePath.EndsWith(".wav") 
                ? await GetAvgSamplesWav(audioFilePath) 
                : WhisperAudioPreprocessor.LoadAsMono16kFloatSamples(audioFilePath);

            totalAudioDuration = TimeSpan.FromSeconds(audioSamples.Length / 16000.0);
            progressPercentage = 0.0;
        }

        public async Task ProcessAsync()
        {
            if (audioSamples == null || processor == null)
            {
                throw new InvalidOperationException("Must call SetupRun before ProcessAsync");
            }

            if (isProcessing)
            {
                throw new InvalidOperationException("Processing is already in progress");
            }

            isProcessing = true;
            isComplete = false;
            transcriptionResult.Clear();
            progressPercentage = 0.0;
            processedAudioDuration = TimeSpan.Zero;

            try
            {
                await foreach (var segment in processor.ProcessAsync(audioSamples))
                {
                    string line = $"{segment.Start}->{segment.End}: {segment.Text}.\n";
                    transcriptionResult.Append(line);

                    processedAudioDuration = segment.End;
                    progressPercentage = Math.Min(100.0, (processedAudioDuration.TotalSeconds / totalAudioDuration.TotalSeconds) * 100.0);
                }

                progressPercentage = 100.0;
                isComplete = true;
            }
            finally
            {
                isProcessing = false;
            }
        }

        public void StartProcessing(string audioFilePath, ModelAccuracy? accuracy = null, bool? speedBoost = null)
        {
            Task.Run(async () =>
            {
                await SetupRun(audioFilePath, accuracy, speedBoost);
                await ProcessAsync();
            });
        }

        public string GetResult()
        {
            if (isProcessing)
            {
                throw new InvalidOperationException("Processing is still in progress. Wait until IsComplete is true.");
            }

            return transcriptionResult.ToString();
        }

        public double GetProgress()
        {
            return progressPercentage;
        }

        private void Reset()
        {
            transcriptionResult?.Clear();
            isProcessing = false;
            isComplete = false;
            progressPercentage = 0.0;
            processedAudioDuration = TimeSpan.Zero;
            totalAudioDuration = TimeSpan.Zero;
            
            processor?.Dispose();
            processor = null;
            
            whisperFactory?.Dispose();
            whisperFactory = null;
            
            audioSamples = null;
        }

        public void Dispose()
        {
            Reset();
        }

        async public Task<string> RunTest(string audioFilePath, ModelAccuracy? accuracy = null, bool? speedBoost = null)
        {
            await SetupRun(audioFilePath, accuracy, speedBoost);
            await ProcessAsync();
            return GetResult();
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
