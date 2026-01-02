using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace LST.Terminal
{
    internal class main
    {
        static async Task Main(string[] args)
        {

            // Original test code
            string audioFile = "C:\\Users\\javier.kipen\\Documents\\GitHub\\LST\\TestAudios\\Svenska\\SvRadio\\Intervju_ex4.wav";

            Console.WriteLine("=== Testing Async processing ===\n");

            // Method 3: Background processing with progress monitoring (GUI-style)
            Console.WriteLine("--- Method 3: Background Processing with Progress Monitoring ---");
            var transcriptor = new Transcriptor.Transcriptor();
            var sw3 = Stopwatch.StartNew();

            await transcriptor.SetupRun(audioFile, Transcriptor.ModelAccuracy.VeryLow);
            
            // Start processing in background
            var processingTask = transcriptor.ProcessAsync();
            
            // Monitor progress (this is what you'd do in a GUI)
            while (!transcriptor.IsComplete)
            {
                Console.Write($"\rProgress: {transcriptor.Progress:F1}%");
                await Task.Delay(500); // Update every 500ms
            }
            
            await processingTask; // Ensure it's complete
            sw3.Stop();
            
            Console.WriteLine($"\rProgress: {transcriptor.Progress:F1}% - Complete!");
            string result3 = transcriptor.GetResult();
            Console.WriteLine($"Transcription Result: {result3}");
            Console.WriteLine($"Result length: {result3.Length} characters");
            Console.WriteLine($"Time taken: {sw3.Elapsed.TotalSeconds:F2} seconds\n");

        }
    }
}
