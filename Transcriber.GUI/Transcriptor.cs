using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Wave;

namespace Transcriber.GUI
{
    public class Transcriptor
    {
        static Transcriptor()
        {
            // Set up native library resolver BEFORE any Whisper.net calls
            NativeLibrary.SetDllImportResolver(typeof(WhisperFactory).Assembly, DllImportResolver);
        }

        private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            // Only handle whisper and ggml libraries
            if (!libraryName.Contains("whisper") && !libraryName.Contains("ggml"))
            {
                return IntPtr.Zero; // Let default resolution handle it
            }

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Try multiple paths
            string[] possiblePaths = new[]
            {
                Path.Combine(exeDir, libraryName),
                Path.Combine(exeDir, "runtimes", "win-x64", libraryName),
                Path.Combine(exeDir, "runtimes", "win-x64", $"{libraryName}.dll")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path) && NativeLibrary.TryLoad(path, out IntPtr handle))
                {
                    return handle;
                }
            }

            return IntPtr.Zero;
        }

        public GgmlType ggmlType { get; set; }
        public string modelFileName { get; set; }
        public string SwModelsFolder { get; set; }
        public ModelAccuracy DefaultAccuracy { get; set; }
        public bool UseQuantized { get; set; }

        private Dictionary<ModelAccuracy, string> normalModels;
        private Dictionary<ModelAccuracy, string> quantizedModels;

        private float[]? audioSamples;
        private WhisperFactory? whisperFactory;
        private WhisperProcessor? processor;
        private string? currentAudioFilePath;
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
            
            // Use application-relative path for installed apps
            SwModelsFolder = GetModelsFolder();
            
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
            
            modelFileName = Path.Combine(SwModelsFolder, normalModels[DefaultAccuracy]);
            
            transcriptionResult = new StringBuilder();
            isProcessing = false;
            isComplete = false;
            progressPercentage = 0.0;
        }

        /// <summary>
        /// Gets the models folder path, checking multiple locations for installed apps
        /// </summary>
        private static string GetModelsFolder()
        {
            // Check locations in order of preference:
            // 1. Executable directory\Models (for installed apps - installer puts models here)
            // 2. Development path (for debugging)
            // 3. AppData folder (alternative location)
            
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string exeModelsPath = Path.Combine(exeDir, "Models");
            
            if (Directory.Exists(exeModelsPath))
            {
                return exeModelsPath;
            }

            // Development path
            string devPath = "C:\\Users\\javier.kipen\\Documents\\GitHub\\LST\\Models\\KBLab\\";
            if (Directory.Exists(devPath))
            {
                return devPath;
            }

            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Transcriber",
                "Models");
            
            if (Directory.Exists(appDataPath))
            {
                return appDataPath;
            }

            // If none exist, use exe Models path (will be created if needed)
            return exeModelsPath;
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
            var selectedModelPath = Path.Combine(SwModelsFolder, selectedModelDict[selectedAccuracy]);

            currentAudioFilePath = audioFilePath;

            // Validate model file exists and is accessible
            if (!File.Exists(selectedModelPath))
            {
                var exeDir = AppDomain.CurrentDomain.BaseDirectory;
                var debugInfo = $"Model file not found at: {selectedModelPath}\n\n" +
                    $"Debug Info:\n" +
                    $"- Exe Directory: {exeDir}\n" +
                    $"- Models Folder: {SwModelsFolder}\n" +
                    $"- Looking for: {selectedModelDict[selectedAccuracy]}\n\n" +
                    $"Please ensure models are installed correctly.";
                
                throw new FileNotFoundException(debugInfo);
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

        private async Task<float[]> GetAvgSamplesWav(string wavFileName)
        {
            using var fileStream = File.OpenRead(wavFileName);

            var waveParser = new WaveParser(fileStream);
            await waveParser.InitializeAsync();
            var samples = await waveParser.GetAvgSamplesAsync(CancellationToken.None);
            return samples;
        }
    }
}
