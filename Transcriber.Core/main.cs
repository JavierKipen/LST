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
            string wavFileName = "C:\\Users\\javier.kipen\\Downloads\\radio_sweden_radio_sweden_weekly_20251219_1621189759.mp3";
            var test = new Transcriptor();
            string result2 = await test.RunTest(wavFileName, ModelAccuracy.VeryLow, false);
            Console.Write(result2);
        }
            /*static async Task Main(string[] args)
            {
                string wavFileName = "C:\\Users\\javier.kipen\\Documents\\GitHub\\LST\\TestAudios\\Svenska\\SvRadio\\Intervju_ex2.wav";
                var test = new Transcriptor();

                Console.WriteLine("=== Testing Different Model Configurations ===\n");

                // Test 1: Default settings (Low accuracy, not quantized)
                Console.WriteLine("--- Test 1: Default Settings (Low, Normal) ---");
                var sw1 = Stopwatch.StartNew();
                string result1 = await test.RunTest(wavFileName);
                sw1.Stop();
                Console.WriteLine($"\nResult length: {result1.Length} characters");
                Console.WriteLine($"Time taken: {sw1.ElapsedMilliseconds} ms ({sw1.Elapsed.TotalSeconds:F2} seconds)\n");

                // Test 2: Very Low accuracy, normal model
                Console.WriteLine("--- Test 2: Very Low Accuracy, Normal Model ---");
                var sw2 = Stopwatch.StartNew();
                string result2 = await test.RunTest(wavFileName, ModelAccuracy.VeryLow, false);
                sw2.Stop();
                Console.WriteLine($"\nResult length: {result2.Length} characters");
                Console.WriteLine($"Time taken: {sw2.ElapsedMilliseconds} ms ({sw2.Elapsed.TotalSeconds:F2} seconds)\n");

                // Test 3: Low accuracy with speed boost (quantized)
                Console.WriteLine("--- Test 3: Low Accuracy, Quantized Model (Speed Boost) ---");
                var sw3 = Stopwatch.StartNew();
                string result3 = await test.RunTest(wavFileName, ModelAccuracy.Low, true);
                sw3.Stop();
                Console.WriteLine($"\nResult length: {result3.Length} characters");
                Console.WriteLine($"Time taken: {sw3.ElapsedMilliseconds} ms ({sw3.Elapsed.TotalSeconds:F2} seconds)\n");

                // Test 4: Medium accuracy, normal model
                Console.WriteLine("--- Test 4: Medium Accuracy, Normal Model ---");
                var sw4 = Stopwatch.StartNew();
                string result4 = await test.RunTest(wavFileName, ModelAccuracy.Medium);
                sw4.Stop();
                Console.WriteLine($"\nResult length: {result4.Length} characters");
                Console.WriteLine($"Time taken: {sw4.ElapsedMilliseconds} ms ({sw4.Elapsed.TotalSeconds:F2} seconds)\n");

                // Test 5: Medium accuracy with speed boost
                Console.WriteLine("--- Test 5: Medium Accuracy, Quantized Model (Speed Boost) ---");
                var sw5 = Stopwatch.StartNew();
                string result5 = await test.RunTest(wavFileName, ModelAccuracy.Medium, true);
                sw5.Stop();
                Console.WriteLine($"\nResult length: {result5.Length} characters");
                Console.WriteLine($"Time taken: {sw5.ElapsedMilliseconds} ms ({sw5.Elapsed.TotalSeconds:F2} seconds)\n");

                Console.WriteLine("\n=== Summary ===");
                Console.WriteLine($"Test 1 (Default - Low, Normal): {sw1.Elapsed.TotalSeconds:F2}s");
                Console.WriteLine($"Test 2 (VeryLow, Normal): {sw2.Elapsed.TotalSeconds:F2}s");
                Console.WriteLine($"Test 3 (Low, Quantized): {sw3.Elapsed.TotalSeconds:F2}s");
                Console.WriteLine($"Test 4 (Medium, Normal): {sw4.Elapsed.TotalSeconds:F2}s");
                Console.WriteLine($"Test 5 (Medium, Quantized): {sw5.Elapsed.TotalSeconds:F2}s");
            }*/
        }
}
