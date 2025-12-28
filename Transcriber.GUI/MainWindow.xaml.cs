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

                // WORKAROUND: Shell out to console app which doesn't have WPF native library loading issues
                string outputPath = Path.ChangeExtension(selectedFilePath, ".txt");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Transcriber.Core.exe"),
                    Arguments = $"--cli --audio \"{selectedFilePath}\" --accuracy {accuracyTag} --output \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                ProgressStatusText.Text = "Transcribing...";

                // Read progress updates
                int updateCount = 0;
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
                        else if (line.StartsWith("OUTPUT:"))
                        {
                            outputPath = line.Substring(7);
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

                // Update UI
                TranscriptionProgressBar.Value = 100;
                ProgressPercentageText.Text = "100%";
                ProgressStatusText.Text = "Complete!";
                ETAText.Text = $"Completed in {FormatTime(processingStopwatch.Elapsed)}";

                MessageBox.Show($"Transcription saved to:\n{outputPath}", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
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