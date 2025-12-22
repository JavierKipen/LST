using System;
using System.Collections.Generic;
using System.Text;
using Whisper.net.Ggml;
using Whisper.net;
using Whisper.net.Wave;

namespace Transcriber.Core
{
    internal class TestKBLibTranscription
    {
        async public Task RunTest()
        {
            var ggmlType = GgmlType.Base;
            var modelFileName = "C:\\Users\\javier.kipen\\Documents\\GitHub\\LST\\Models\\KBLab\\kb-ggml-base.bin";
            var wavFileName = "C:\\Users\\javier.kipen\\Documents\\GitHub\\LST\\TestAudios\\Svenska\\SvRadio\\Intervju_ex2.wav";


            using var whisperFactory = WhisperFactory.FromPath(modelFileName);

            //if (!File.Exists(modelFileName))
            //{
            //    await DownloadModel(modelFileName, ggmlType);
            //}

            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            using var fileStream = System.IO.File.OpenRead(wavFileName);

            var waveParser = new WaveParser(fileStream);
            await waveParser.InitializeAsync();
            var channels = waveParser.Channels;
            var sampleRate = waveParser.SampleRate;
            var bitsPerSample = waveParser.BitsPerSample;
            var headerSize = waveParser.DataChunkPosition;
            var frameSize = bitsPerSample / 8 * channels;

            var samples = await waveParser.GetAvgSamplesAsync(CancellationToken.None);
            await foreach (var result in processor.ProcessAsync(samples))
            {
                Console.WriteLine($"{result.Start}->{result.End}: {result.Text}.\n");
            }
        }
        static async Task DownloadModel(string fileName, GgmlType ggmlType)
        {
            using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(ggmlType);
            using var fileWriter = File.OpenWrite(fileName);
            await modelStream.CopyToAsync(fileWriter);
        }
    }
    
}
