using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Wave;

namespace Transcriptor
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
        public bool IsProcessing => isProcessing; // Indicates if processing is ongoing
        public bool IsComplete => isComplete; // Indicates if processing is complete
        public double Progress => progressPercentage; // Indicates the progress percentage



        private Dictionary<ModelAccuracy, string> normalModels; // Model file names for normal models

        private float[] audioSamples; // Loaded audio samples
        private StringBuilder transcriptionResult;  // Accumulated transcription result

        private bool isProcessing; //Progress tracking
        private bool isComplete;
        private double progressPercentage;
        private TimeSpan totalAudioDuration;
        private TimeSpan processedAudioDuration;
        

        //Whisper settings
        private WhisperFactory whisperFactory;
        private WhisperProcessor processor;
        private string currentAudioFilePath;
        

        

        public Transcriptor()
        {
            initTranscriptor();
            SwModelsFolder = "C:\\Users\\javier.kipen\\Documents\\GitHub\\LST\\Models\\KBLab\\";
            modelFileName = SwModelsFolder + normalModels[DefaultAccuracy]; //Default path of models init initialization
        }
        public Transcriptor(string customModelPath)
        {
            initTranscriptor();
            SwModelsFolder = Path.GetDirectoryName(customModelPath) + "\\";
            modelFileName = customModelPath;
        }
        public void initTranscriptor()
        {
            ggmlType = GgmlType.Base;
            DefaultAccuracy = ModelAccuracy.Low;
            normalModels = new Dictionary<ModelAccuracy, string>
            {
                { ModelAccuracy.VeryLow, "kb-ggml-tiny.bin" },
                { ModelAccuracy.Low, "kb-ggml-base.bin" },
                { ModelAccuracy.Medium, "kb-ggml-small.bin" },
                { ModelAccuracy.Good, "kb-ggml-medium.bin" },
                { ModelAccuracy.VeryGood, "kb-ggml-large.bin" }
            };
            transcriptionResult = new StringBuilder();
            isProcessing = false;
            isComplete = false;
            progressPercentage = 0.0;
        }
        public async Task SetupRun(string audioFilePath, ModelAccuracy? accuracy = null)
        {
            if (isProcessing)
                throw new InvalidOperationException("Cannot setup while processing is in progress");

            Reset();

            var selectedAccuracy = accuracy ?? DefaultAccuracy;
            var selectedModelPath = SwModelsFolder + normalModels[selectedAccuracy];
            currentAudioFilePath = audioFilePath;

            await LoadModel(selectedModelPath); //Load the model into whisperFactory

            processor = whisperFactory.CreateBuilder()
                .WithLanguage("sv")
                .Build();

            audioSamples = audioFilePath.EndsWith(".wav") 
                ? await GetAvgSamplesWav(audioFilePath) 
                : WhisperAudioPreprocessor.LoadAsMono16kFloatSamples(audioFilePath);

            totalAudioDuration = TimeSpan.FromSeconds(audioSamples.Length / 16000.0);
            progressPercentage = 0.0;
        }
        public async Task LoadModel(string selectedModelPath)
        {
            // Validate model file exists and is accessible
            if (!File.Exists(selectedModelPath))
                throw new FileNotFoundException($"Whisper model file not found at: {selectedModelPath}");

            // Check if file is readable
            try
            {
                using var testStream = File.OpenRead(selectedModelPath);
                if (testStream.Length == 0)
                    throw new InvalidOperationException($"Model file is empty: {selectedModelPath}");
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
        }

        public async Task ProcessAsync()
        {
            // Validating steps
            if (audioSamples == null || processor == null)
                throw new InvalidOperationException("Must call SetupRun before ProcessAsync");
            if (isProcessing)
                throw new InvalidOperationException("Processing is already in progress");

            //Starts processing
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

        public string GetResult()
        {
            if (isProcessing)
                throw new InvalidOperationException("Processing is still in progress. Wait until IsComplete is true.");
            return transcriptionResult.ToString();
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
