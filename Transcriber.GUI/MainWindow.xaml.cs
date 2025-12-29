using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Transcriber.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string? selectedFilePath;
        private Stopwatch? processingStopwatch;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Audio File",
                Filter = "Audio Files (*.wav;*.mp3;*.m4a)|*.wav;*.mp3;*.m4a|All Files (*.*)|*.*",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
                FilePathTextBox.Text = Path.GetFileName(selectedFilePath);
                TranscribeButton.IsEnabled = true;
            }
        }

        private async void TranscribeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                MessageBox.Show("Please select an audio file first.", "No File Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Disable controls during processing
                BrowseButton.IsEnabled = false;
                TranscribeButton.IsEnabled = false;
                AccuracyComboBox.IsEnabled = false;

                // Show progress panel
                ProgressPanel.Visibility = Visibility.Visible;
                ProgressStatusText.Text = "Loading model and audio...";
                TranscriptionProgressBar.Value = 0;
                ProgressPercentageText.Text = "0%";
                ETAText.Text = "Estimating time remaining...";

                // Get selected accuracy
                var selectedItem = (ComboBoxItem)AccuracyComboBox.SelectedItem;
                var accuracyTag = selectedItem.Tag.ToString();

                // Start timing
                processingStopwatch = Stopwatch.StartNew();

                // Call worker process WITHOUT specifying output file (result will come via stdout)
                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Transcriber.Core.exe"),
                    Arguments = $"--cli --audio \"{selectedFilePath}\" --accuracy {accuracyTag}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                ProgressStatusText.Text = "Transcribing...";

                // Read progress updates and result
                int updateCount = 0;
                string? transcriptionResult = null;
                
                while (!process.HasExited)
                {
                    string? line = await process.StandardOutput.ReadLineAsync();
                    if (line != null)
                    {
                        if (line.StartsWith("PROGRESS:"))
                        {
                            if (double.TryParse(line.Substring(9), out double progress))
                            {
                                TranscriptionProgressBar.Value = progress;
                                ProgressPercentageText.Text = $"{progress:F1}%";
                                
                                if (updateCount % 5 == 0 && progress > 5.0)
                                {
                                    UpdateETA(progress);
                                }
                                updateCount++;
                            }
                        }
                        else if (line.StartsWith("RESULT:"))
                        {
                            // Decode Base64 result
                            string encodedResult = line.Substring(7); //Loads after result!
                            byte[] resultBytes = Convert.FromBase64String(encodedResult);
                            transcriptionResult = Encoding.UTF8.GetString(resultBytes);
                        }
                    }
                }

                await process.WaitForExitAsync();
                processingStopwatch.Stop();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    throw new InvalidOperationException($"Transcription failed: {error}");
                }

                if (string.IsNullOrEmpty(transcriptionResult))
                {
                    throw new InvalidOperationException("No transcription result received from worker process");
                }

                // Update UI
                TranscriptionProgressBar.Value = 100;
                ProgressPercentageText.Text = "100%";
                ProgressStatusText.Text = "Complete!";
                ETAText.Text = $"Completed in {FormatTime(processingStopwatch.Elapsed)}";

                // POST-PROCESS THE TRANSCRIPTION RESULT HERE
                string processedResult = PostProcessTranscription(transcriptionResult);

                // Save the processed result
                string outputPath = Path.ChangeExtension(selectedFilePath, ".txt");
                await File.WriteAllTextAsync(outputPath, processedResult);

                MessageBox.Show($"Transcription saved to:\n{outputPath}\n\nOriginal length: {transcriptionResult.Length} chars\nProcessed length: {processedResult.Length} chars", 
                    "Success", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // Enhanced error logging
                var errorDetails = $"Error: {ex.Message}\n" +
                                 $"Type: {ex.GetType().FullName}\n";
                
                if (ex.InnerException != null)
                {
                    errorDetails += $"\nInner Exception: {ex.InnerException.Message}";
                }
                
                System.Diagnostics.Debug.WriteLine("=== ERROR ===");
                System.Diagnostics.Debug.WriteLine(errorDetails);
                
                MessageBox.Show(errorDetails, "Detailed Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable controls
                BrowseButton.IsEnabled = true;
                TranscribeButton.IsEnabled = true;
                AccuracyComboBox.IsEnabled = true;
                processingStopwatch = null;
            }
        }

        /// <summary>
        /// Post-process the transcription result before saving.
        /// Customize this method to add your own processing logic!
        /// </summary>
        private string PostProcessTranscription(string rawTranscription)
        {
            // Example post-processing operations:
            
            // 1. Remove extra whitespace
            var lines = rawTranscription.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            // 2. Trim each line
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Trim();
            }
            
            // 3. Add a header with metadata
            var processed = new StringBuilder();
            processed.AppendLine("=== TRANSCRIPTION ===");
            processed.AppendLine($"File: {Path.GetFileName(selectedFilePath)}");
            processed.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            processed.AppendLine($"Duration: {processingStopwatch?.Elapsed.ToString(@"hh\:mm\:ss")}");
            processed.AppendLine();
            processed.AppendLine("=== CONTENT ===");
            processed.AppendLine();
            
            // 4. Add the cleaned content
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    processed.AppendLine(line);
                }
            }
            
            // 5. Add footer
            processed.AppendLine();
            processed.AppendLine("=== END OF TRANSCRIPTION ===");
            
            // TODO: Add your custom post-processing here!
            // Examples:
            // - Remove filler words (um, uh, etc.)
            // - Fix common transcription errors
            // - Add speaker labels
            // - Format timestamps
            // - Translate text
            // - Run spell check
            // - Extract keywords
            // - Generate summary
            
            return processed.ToString();
        }

        private void UpdateETA(double currentProgress)
        {
            if (processingStopwatch == null || currentProgress <= 0)
                return;

            double elapsedSeconds = processingStopwatch.Elapsed.TotalSeconds;
            double estimatedTotalSeconds = elapsedSeconds / (currentProgress / 100.0);
            double remainingSeconds = estimatedTotalSeconds - elapsedSeconds;

            if (remainingSeconds > 0)
            {
                ETAText.Text = $"ETA: {FormatTime(TimeSpan.FromSeconds(remainingSeconds))}";
            }
        }

        private string FormatTime(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            else if (timeSpan.TotalMinutes >= 1)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            else
            {
                return $"{timeSpan.Seconds}s";
            }
        }
    }
}