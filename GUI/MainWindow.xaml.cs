using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Transcriptor.Transcriptor? transcriptor;
        private string? selectedAudioFilePath;
        private Transcriptor.ModelAccuracy selectedAccuracy;
        private Stopwatch? processingStopwatch;
        private System.Windows.Threading.DispatcherTimer progressTimer;
        private uint secondsPerBlock = 30; // Default: 30 seconds per block

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                transcriptor = new Transcriptor.Transcriptor();
                selectedAccuracy = Transcriptor.ModelAccuracy.Low;
                
                progressTimer = new System.Windows.Threading.DispatcherTimer();
                progressTimer.Interval = TimeSpan.FromMilliseconds(1000);
                progressTimer.Tick += ProgressTimer_Tick;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to initialize application:\n\n{ex.GetType().Name}\n{ex.Message}\n\n{ex.StackTrace}",
                    "Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                throw;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Audio Files|*.mp3;*.wav;*.m4a;*.flac;*.ogg|All Files|*.*",
                Title = "Select Audio File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedAudioFilePath = openFileDialog.FileName;
                
                // Format display: ".../directory_name/filename"
                var fileInfo = new FileInfo(selectedAudioFilePath);
                var directoryName = fileInfo.Directory?.Name ?? "";
                var fileName = fileInfo.Name;
                FilePathTextBox.Text = $".../{directoryName}/{fileName}";
                FilePathTextBox.ToolTip = selectedAudioFilePath; // Full path on hover

                UpdateStartButtonState();
            }
        }

        private void AccuracyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AccuracyComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var tag = selectedItem.Tag.ToString();
                selectedAccuracy = tag switch
                {
                    "VeryLow" => Transcriptor.ModelAccuracy.VeryLow,
                    "Low" => Transcriptor.ModelAccuracy.Low,
                    "Medium" => Transcriptor.ModelAccuracy.Medium,
                    "Good" => Transcriptor.ModelAccuracy.Good,
                    "VeryGood" => Transcriptor.ModelAccuracy.VeryGood,
                    _ => Transcriptor.ModelAccuracy.Low
                };
                
                UpdateStartButtonState();
            }
        }

        private void UpdateStartButtonState()
        {
            if (transcriptor != null)
            {
                StartTranscriptionButton.IsEnabled = 
                    !string.IsNullOrEmpty(selectedAudioFilePath) && 
                    !transcriptor.IsProcessing;
            }
        }

        private async void StartTranscriptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (transcriptor == null) return;

            try
            {
                // Disable controls during processing
                StartTranscriptionButton.IsEnabled = false;
                BrowseButton.IsEnabled = false;
                AccuracyComboBox.IsEnabled = false;
                

                // Change button text to "Setting up..."
                StartTranscriptionButton.Content = "Setting up...";
                
                // Force UI to update before starting setup
                await Task.Delay(50); // Small delay to allow UI to render

                await transcriptor.SetupRun(selectedAudioFilePath!, selectedAccuracy);
                
                // Change button text to "Processing..."
                StartTranscriptionButton.Content = "Processing...";

                // Show progress panel
                ProgressPanel.Visibility = Visibility.Visible;
                TranscriptionProgressBar.Value = 0;
                ProgressPercentageText.Text = "0%";
                EstimatedTimeText.Text = "Estimated time remaining: calculating...";

                // Start processing
                processingStopwatch = Stopwatch.StartNew();
                
                // Start progress monitoring
                progressTimer.Start();
                var processingTask = transcriptor.ProcessAsync();
                
                await processingTask;
                
                // Stop progress monitoring
                progressTimer.Stop();
                processingStopwatch.Stop();
                
                // Update final progress
                TranscriptionProgressBar.Value = 100;
                ProgressPercentageText.Text = "100%";
                EstimatedTimeText.Text = $"Completed in {processingStopwatch.Elapsed.TotalSeconds:F1} seconds";
                ProgressStatusText.Text = "Complete!";

                // Show result
                string result = transcriptor.GetResult();

                PostProcessTranscription(result);

                MessageBox.Show(
                    $"Transcription completed!\n\nLength: {result.Length} characters\nTime: {processingStopwatch.Elapsed.TotalSeconds:F2} seconds",
                    "Transcription Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);


            }
            catch (Exception ex)
            {
                progressTimer.Stop();
                MessageBox.Show(
                    $"Error during transcription:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                StartTranscriptionButton.Content = "Start Transcription";
                StartTranscriptionButton.IsEnabled = true;
                BrowseButton.IsEnabled = true;
                AccuracyComboBox.IsEnabled = true;
                
            }
        }

        private void PostProcessTranscription(string result)
        {   
            // Process the transcription into time blocks
            string processedResult = SetTimeStampPerBlock(result);
            
            // Get the directory and filename of the audio file
            var audioFileInfo = new FileInfo(selectedAudioFilePath);
            var audioDirectory = audioFileInfo.DirectoryName;
            var audioFileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(selectedAudioFilePath);
                
            // Create the output .txt file path
            var txtFilePath = System.IO.Path.Combine(audioDirectory!, $"{audioFileNameWithoutExtension}.txt");
                
            // Write the transcription result to the file
            File.WriteAllText(txtFilePath, processedResult, Encoding.UTF8);
           
        }

        private string SetTimeStampPerBlock(string transcription)
        {
            if (string.IsNullOrWhiteSpace(transcription))
                return transcription;

            var lines = transcription.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var blocks = new List<(TimeSpan blockStart, TimeSpan blockEnd, List<string> texts)>();
            
            foreach (var line in lines)
            {
                // Parse line format: "00:00:00->00:00:10: Text."
                // Split by first occurrence of ": " to separate timestamp from text
                var colonIndex = line.IndexOf(": ");
                if (colonIndex == -1) continue;

                var timestampPart = line.Substring(0, colonIndex);
                var text = line.Substring(colonIndex + 2).Trim();

                // Remove trailing period if exists
                if (text.EndsWith('.'))
                    text = text.Substring(0, text.Length - 1).Trim();

                // Parse timestamps: "00:00:00->00:00:10"
                var parts = timestampPart.Split(new[] { "->" }, StringSplitOptions.None);
                if (parts.Length != 2) continue;

                var startTimeStr = parts[0].Trim();
                var endTimeStr = parts[1].Trim();

                if (!TimeSpan.TryParse(startTimeStr, out TimeSpan startTime))
                    continue;
                if (!TimeSpan.TryParse(endTimeStr, out TimeSpan endTime))
                    continue;

                // Determine which block this segment belongs to
                int blockIndex = (int)(startTime.TotalSeconds / secondsPerBlock);
                TimeSpan blockStart = TimeSpan.FromSeconds(blockIndex * secondsPerBlock);
                
                // Find existing block or create new one
                var existingBlockIndex = blocks.FindIndex(b => b.blockStart == blockStart);
                if (existingBlockIndex == -1)
                {
                    // Create new block
                    blocks.Add((blockStart, endTime, new List<string> { text }));
                }
                else
                {
                    // Update existing block - keep the latest end time
                    var existingBlock = blocks[existingBlockIndex];
                    if (endTime > existingBlock.blockEnd)
                    {
                        blocks[existingBlockIndex] = (existingBlock.blockStart, endTime, existingBlock.texts);
                    }
                    existingBlock.texts.Add(text);
                }
            }

            // Build the output
            var output = new StringBuilder();
            foreach (var block in blocks)
            {
                // Format: [HH:MM:SS->HH:MM:SS.FFFFFFF]
                output.AppendLine($"[{block.blockStart:hh\\:mm\\:ss}->{block.blockEnd:hh\\:mm\\:ss\\.FFFFFFF}]");
                foreach (var text in block.texts)
                {
                    output.AppendLine(text);
                }
                output.AppendLine(); // Empty line between blocks
            }

            return output.ToString();
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (transcriptor == null) return;

            if (transcriptor.IsProcessing || !transcriptor.IsComplete)
            {
                // Update progress bar
                TranscriptionProgressBar.Value = transcriptor.Progress;
                ProgressPercentageText.Text = $"{transcriptor.Progress:F1}%";

                // Calculate estimated time remaining
                if (transcriptor.Progress > 0 && processingStopwatch != null)
                {
                    var elapsedSeconds = processingStopwatch.Elapsed.TotalSeconds;
                    var estimatedTotalSeconds = elapsedSeconds / (transcriptor.Progress / 100.0);
                    var remainingSeconds = estimatedTotalSeconds - elapsedSeconds;

                    if (remainingSeconds > 0)
                    {
                        var remaining = TimeSpan.FromSeconds(remainingSeconds);
                        EstimatedTimeText.Text = $"Estimated time remaining: {remaining.Minutes}:{remaining.Seconds:D2}";
                    }
                }
            }
        }
    }
}