using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace Transcriber.Core
{
    internal class main
    {
        static async Task Main(string[] args)
        {
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

            // Method 4: Fire and forget with polling (simplest for GUI)
            Console.WriteLine("--- Method 4: Fire-and-Forget with StartProcessing ---");
            var test4 = new Transcriptor();
            var sw4 = Stopwatch.StartNew();

            test4.StartProcessing(audioFile, ModelAccuracy.VeryLow, false);
            
            // Wait for setup to complete
            await Task.Delay(1000);
            
            // Monitor progress
            while (!test4.IsComplete)
            {
                if (test4.IsProcessing || test4.IsComplete)
                {
                    Console.Write($"\rProgress: {test4.GetProgress():F1}%");
                }
                await Task.Delay(500);
            }
            
            sw4.Stop();
            Console.WriteLine($"\rProgress: {test4.GetProgress():F1}% - Complete!");
            string result4 = test4.GetResult();
            Console.WriteLine($"Result length: {result4.Length} characters");
            Console.WriteLine($"Time taken: {sw4.Elapsed.TotalSeconds:F2} seconds\n");

            Console.WriteLine("\n=== Summary ===");
            Console.WriteLine($"Method 3 (Progress Monitor): {sw3.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine($"Method 4 (Fire-and-Forget): {sw4.Elapsed.TotalSeconds:F2}s");
        }
    }
}
