using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Transcriber.Core;

namespace Transcriber.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string? selectedFilePath;
        private Transcriptor? transcriptor;
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
                ModelAccuracy accuracy = accuracyTag switch
                {
                    "VeryLow" => ModelAccuracy.VeryLow,
                    "Low" => ModelAccuracy.Low,
                    "Medium" => ModelAccuracy.Medium,
                    "Good" => ModelAccuracy.Good,
                    "VeryGood" => ModelAccuracy.VeryGood,
                    _ => ModelAccuracy.Low
                };

                // Create transcriptor and setup
                transcriptor = new Transcriptor();
                await transcriptor.SetupRun(selectedFilePath, accuracy, false);

                ProgressStatusText.Text = "Transcribing...";
                
                // Start timing
                processingStopwatch = Stopwatch.StartNew();

                // Start processing in background
                var processingTask = transcriptor.ProcessAsync();

                // Update progress in a loop
                int updateCount = 0;
                while (!transcriptor.IsComplete)
                {
                    double progress = transcriptor.Progress;
                    TranscriptionProgressBar.Value = progress;
                    ProgressPercentageText.Text = $"{progress:F1}%";
                    
                    // Update ETA every 5 iterations (every 500ms) and only after 5% progress
                    if (updateCount % 5 == 0 && progress > 5.0)
                    {
                        UpdateETA(progress);
                    }
                    
                    updateCount++;
                    await Task.Delay(100); // Update every 100ms
                }

                // Wait for completion
                await processingTask;
                processingStopwatch.Stop();

                // Get result
                string result = transcriptor.GetResult();

                // Save to file
                string outputPath = Path.ChangeExtension(selectedFilePath, ".txt");
                await File.WriteAllTextAsync(outputPath, result);

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
                MessageBox.Show($"An error occurred:\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable controls
                BrowseButton.IsEnabled = true;
                TranscribeButton.IsEnabled = true;
                AccuracyComboBox.IsEnabled = true;

                // Clean up
                transcriptor?.Dispose();
                transcriptor = null;
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