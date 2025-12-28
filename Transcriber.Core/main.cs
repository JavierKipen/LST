using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace Transcriber.Core
{
    internal class main
    {
        static async Task Main(string[] args)
        {
            // Check if running in CLI mode (called from GUI)
            if (args.Length > 0 && args[0] == "--cli")
            {
                await RunCliMode(args);
                return;
            }

            // Original test code
            string audioFile = "C:\\Users\\javier.kipen\\Downloads\\radio_sweden_radio_sweden_weekly_20251219_1621189759.mp3";

            Console.WriteLine("=== Testing Different Usage Patterns ===\n");

            // Method 3: Background processing with progress monitoring (GUI-style)
            Console.WriteLine("--- Method 3: Background Processing with Progress Monitoring ---");
            var test3 = new Transcriptor();
            var sw3 = Stopwatch.StartNew();

            await test3.SetupRun(audioFile, ModelAccuracy.VeryLow, false);
            
            // Start processing in background
            var processingTask = test3.ProcessAsync();
            
            // Monitor progress (this is what you'd do in a GUI)
            while (!test3.IsComplete)
            {
                Console.Write($"\rProgress: {test3.Progress:F1}%");
                await Task.Delay(500); // Update every 500ms
            }
            
            await processingTask; // Ensure it's complete
            sw3.Stop();
            
            Console.WriteLine($"\rProgress: {test3.Progress:F1}% - Complete!");
            string result3 = test3.GetResult();
            Console.WriteLine($"Result length: {result3.Length} characters");
            Console.WriteLine($"Time taken: {sw3.Elapsed.TotalSeconds:F2} seconds\n");
        }

        static async Task RunCliMode(string[] args)
        {
            try
            {
                string audioFile = "";
                string accuracy = "Low";
                string outputFile = "";

                // Parse arguments
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--audio" && i + 1 < args.Length)
                        audioFile = args[i + 1];
                    else if (args[i] == "--accuracy" && i + 1 < args.Length)
                        accuracy = args[i + 1];
                    else if (args[i] == "--output" && i + 1 < args.Length)
                        outputFile = args[i + 1];
                }

                if (string.IsNullOrEmpty(audioFile))
                {
                    Console.Error.WriteLine("ERROR: --audio parameter required");
                    Environment.Exit(1);
                    return;
                }

                ModelAccuracy modelAccuracy = accuracy switch
                {
                    "VeryLow" => ModelAccuracy.VeryLow,
                    "Low" => ModelAccuracy.Low,
                    "Medium" => ModelAccuracy.Medium,
                    "Good" => ModelAccuracy.Good,
                    "VeryGood" => ModelAccuracy.VeryGood,
                    _ => ModelAccuracy.Low
                };

                var transcriptor = new Transcriptor();
                
                Console.WriteLine($"PROGRESS:0");
                
                await transcriptor.SetupRun(audioFile, modelAccuracy, false);
                
                var processingTask = transcriptor.ProcessAsync();
                
                double lastProgress = 0;
                while (!transcriptor.IsComplete)
                {
                    double currentProgress = transcriptor.Progress;
                    if (Math.Abs(currentProgress - lastProgress) > 1.0)
                    {
                        Console.WriteLine($"PROGRESS:{currentProgress:F1}");
                        lastProgress = currentProgress;
                    }
                    await Task.Delay(500);
                }
                
                await processingTask;
                
                string result = transcriptor.GetResult();
                
                Console.WriteLine($"PROGRESS:100");
                
                if (string.IsNullOrEmpty(outputFile))
                {
                    outputFile = Path.ChangeExtension(audioFile, ".txt");
                }
                
                await File.WriteAllTextAsync(outputFile, result);
                
                Console.WriteLine($"OUTPUT:{outputFile}");
                Console.WriteLine("COMPLETE");
                
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
