# Transcriber GUI

A modern WPF application for transcribing audio files using Whisper AI models.

## Features

### ?? Audio File Support
- Browse and select audio files (WAV, MP3, M4A)
- Displays selected filename in the UI

### ?? Model Accuracy Selection
Choose from 5 accuracy levels:
- **Very Low** (Fastest) - Uses tiny model
- **Low** - Uses base model (default)
- **Medium** - Uses small model
- **Good** - Uses medium model
- **Very Good** (Slowest) - Uses large model

### ? Speed Boost Option
- Enable "Speed Boost" to use quantized models
- Provides faster processing with minimal quality loss
- Quantized models have `-q5_0` suffix

### ?? Real-Time Progress
- Visual progress bar showing transcription progress
- Percentage display updates every 100ms
- Status messages during loading and processing

### ?? Automatic Saving
- Transcription results are automatically saved
- Saved as `.txt` file in the same folder as the audio file
- Same filename as audio, but with `.txt` extension

## How to Use

1. **Select Audio File**
   - Click "Browse..." button
   - Choose an audio file (WAV, MP3, or M4A)
   - Filename appears in the text box

2. **Configure Settings**
   - Select desired accuracy level from dropdown
   - Optionally enable "Speed Boost" for faster processing

3. **Start Transcription**
   - Click "Start Transcription" button (enabled after file selection)
   - Wait while the model loads and processes
   - Progress bar shows current completion percentage

4. **Get Results**
   - When complete, a success message shows the output file path
   - Transcription is saved as `.txt` file next to your audio file

## Technical Details

### Architecture
- Built with WPF (.NET 10)
- Uses async/await for non-blocking UI
- References `Transcriber.Core` library for transcription logic

### UI Controls
- **BrowseButton**: Opens file dialog
- **FilePathTextBox**: Displays selected filename (read-only)
- **AccuracyComboBox**: Select model accuracy
- **SpeedBoostCheckBox**: Toggle quantized models
- **TranscribeButton**: Start transcription (disabled until file selected)
- **ProgressPanel**: Shows during processing with progress bar

### Progress Monitoring
The GUI updates progress every 100ms by:
1. Calling `SetupRun()` to load model and audio
2. Starting `ProcessAsync()` in background
3. Polling `transcriptor.Progress` property
4. Updating progress bar and percentage text
5. Retrieving result with `GetResult()` when complete

### Error Handling
- Validates file selection before processing
- Shows error messages for exceptions
- Re-enables controls after completion or error
- Properly disposes transcriptor resources

## Example Workflow

```csharp
// User clicks Browse
// User selects: C:\Audio\interview.mp3

// User sets accuracy to "Medium"
// User enables "Speed Boost"

// User clicks "Start Transcription"
// GUI shows: "Loading model and audio..."
// GUI shows: "Transcribing... 25.3%"
// GUI shows: "Transcribing... 50.7%"
// GUI shows: "Transcribing... 75.2%"
// GUI shows: "Complete! 100%"

// Output saved to: C:\Audio\interview.txt
```

## Dependencies

- **Transcriber.Core** - Core transcription library
- **Microsoft.Win32** - File dialogs
- **System.IO** - File operations
- **System.Windows** - WPF framework

## Models Required

The application expects models in:
```
C:\Users\[username]\Documents\GitHub\LST\Models\KBLab\
```

### Normal Models
- `kb-ggml-tiny.bin`
- `kb-ggml-base.bin`
- `kb-ggml-small.bin`
- `kb-ggml-medium.bin`
- `kb-ggml-large.bin`

### Quantized Models (Speed Boost)
- `kb-ggml-tiny-q5_0.bin`
- `kb-ggml-base-q5_0.bin`
- `kb-ggml-small-q5_0.bin`
- `kb-ggml-medium-q5_0.bin`
- `kb-ggml-large-q5_0.bin`

## Language

Currently configured for Swedish ("sv") audio transcription.
