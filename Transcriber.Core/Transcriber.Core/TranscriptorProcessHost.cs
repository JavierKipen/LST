using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Transcriber.Core
{
    /// <summary>
    /// Runs transcription in a separate console process to avoid WPF native library loading issues
    /// </summary>
    public class TranscriptorProcessHost
    {
        private Process? workerProcess;
        private string pipeName;
        
        public bool IsProcessing { get; private set; }
        public bool IsComplete { get; private set; }
        public double Progress { get; private set; }
        
        private string result = string.Empty;

        public TranscriptorProcessHost()
        {
            pipeName = $"Transcriptor_{Guid.NewGuid():N}";
        }

        public async Task<string> RunTranscription(string audioFilePath, ModelAccuracy accuracy)
        {
            IsProcessing = true;
            IsComplete = false;
            Progress = 0.0;
            result = string.Empty;

            // Find the Transcriber.Core.exe
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var coreExePath = Path.Combine(currentDir, "Transcriber.Core.exe");
            
            if (!File.Exists(coreExePath))
            {
                throw new FileNotFoundException($"Transcriber.Core.exe not found at: {coreExePath}");
            }

            // Start the worker process
            var startInfo = new ProcessStartInfo
            {
                FileName = coreExePath,
                Arguments = $"--pipe {pipeName} --audio \"{audioFilePath}\" --accuracy {accuracy}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            workerProcess = Process.Start(startInfo);
            
            if (workerProcess == null)
            {
                throw new InvalidOperationException("Failed to start worker process");
            }

            // Read progress and result from named pipe
            await Task.Run(async () =>
            {
                using var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1);
                await pipeServer.WaitForConnectionAsync();
                
                using var reader = new StreamReader(pipeServer, Encoding.UTF8);
                
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("PROGRESS:"))
                    {
                        if (double.TryParse(line.Substring(9), out double progress))
                        {
                            Progress = progress;
                        }
                    }
                    else if (line.StartsWith("RESULT:"))
                    {
                        result = line.Substring(7);
                    }
                    else if (line == "COMPLETE")
                    {
                        break;
                    }
                }
            });

            await workerProcess.WaitForExitAsync();
            
            IsProcessing = false;
            IsComplete = true;
            Progress = 100.0;

            if (workerProcess.ExitCode != 0)
            {
                var error = await workerProcess.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Worker process failed: {error}");
            }

            return result;
        }

        public void Dispose()
        {
            if (workerProcess != null && !workerProcess.HasExited)
            {
                workerProcess.Kill();
                workerProcess.Dispose();
            }
        }
    }
}
