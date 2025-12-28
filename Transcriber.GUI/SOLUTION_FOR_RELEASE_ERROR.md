# Solution: ExecutionEngineException in Release Mode

## Root Cause Identified

From your error screenshot, the issue is:
```
Whisper.net.Internals.ModelLoader.WhisperProcessorModelFileLoader.LoadNativeContext
```

This happens when **loading the .bin model file**, not the native DLL.

## Why It Works in Debug But Not Release

Release mode uses different optimizations:
- **Stricter memory mapping** for large files
- **Different file handle management**
- **Memory-mapped file validation** is more aggressive

Your KBLab model files might have:
1. Non-standard GGML format variations
2. Memory alignment issues Release mode doesn't tolerate
3. File corruption that Debug mode ignores

## Immediate Solutions

### Solution 1: Try a Different Model File (QUICKEST)

The KBLab models might have compatibility issues. Try downloading a standard Whisper model:

**Download a test model:**
```powershell
# In PowerShell, download official tiny model for testing
$url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin"
$output = "C:\Users\javier.kipen\Documents\GitHub\LST\Models\ggml-tiny.bin"
Invoke-WebRequest -Uri $url -OutFile $output
```

**Then test in your GUI** by temporarily changing the model path.

### Solution 2: Use DelayInitialization Option

Modify `SetupRun` to use delayed initialization:

```csharp
public async Task SetupRun(string audioFilePath, ModelAccuracy? accuracy = null, bool? speedBoost = null)
{
    // ... existing validation code ...

    try
    {
        // Use delayed initialization - this defers model loading
        var options = new WhisperFactoryOptions
        {
            DelayInitialization = true  // This might help with Release mode
        };
        
        whisperFactory = WhisperFactory.FromPath(selectedModelPath, options);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"Failed to load Whisper model from: {selectedModelPath}\n" +
            $"File size: {new FileInfo(selectedModelPath).Length} bytes\n" +
            $"Error: {ex.Message}",
            ex);
    }

    // Rest of code...
}
```

### Solution 3: Check Model File Integrity

Your KB model files might be corrupted. **Verify the file:**

```powershell
# Check file size and properties
Get-Item "C:\Users\javier.kipen\Documents\GitHub\LST\Models\KBLab\kb-ggml-base.bin" | Format-List *

# Try to read the file header
$bytes = [System.IO.File]::ReadAllBytes("C:\Users\javier.kipen\Documents\GitHub\LST\Models\KBLab\kb-ggml-base.bin")[0..100]
$bytes
```

GGML files should start with specific magic bytes. If corrupted, re-download them.

### Solution 4: Use Memory-Mapped Loading

Instead of file path, load model into memory first:

```csharp
public async Task SetupRun(string audioFilePath, ModelAccuracy? accuracy = null, bool? speedBoost = null)
{
    // ... existing code ...

    try
    {
        // Load model into memory first (might avoid Release mode file handling issues)
        byte[] modelBytes = await File.ReadAllBytesAsync(selectedModelPath);
        whisperFactory = WhisperFactory.FromBuffer(modelBytes);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"Failed to load Whisper model from: {selectedModelPath}\n" +
            $"File size: {new FileInfo(selectedModelPath).Length} bytes\n" +
            $"Error: {ex.Message}",
            ex);
    }

    // Rest of code...
}
```

**Note:** This loads the entire model file (75-1500MB) into RAM, so only use for smaller models.

### Solution 5: Disable Optimization for Specific Method

Add this attribute to `SetupRun`:

```csharp
[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
public async Task SetupRun(string audioFilePath, ModelAccuracy? accuracy = null, bool? speedBoost = null)
{
    // ... existing code ...
}
```

This prevents Release mode from optimizing this method, making it behave more like Debug.

## What I've Already Added

? File existence validation
? File accessibility check
? Better error messages with file size
? Exception wrapping for better diagnostics
? Alternative constructor for custom model paths

## Next Steps

1. **Stop the running GUI** (close it completely)
2. **Rebuild** both projects
3. **Try Solution 1** first (test with official whisper model)
4. **If that works**, your KBLab models have issues
5. **If still fails**, try Solution 2 (delayed initialization)

## Why This Is Specifically a Release Issue

Release mode:
- Uses **aggressive inlining** and optimization
- Has **stricter memory management**
- Uses **different P/Invoke marshalling**
- Has **tighter security checks** on file operations
- **Memory-mapped files** behave differently

The KBLab models likely have subtle format issues that Debug mode tolerates but Release mode doesn't.

## Recommended: Test with Official Model

Download and test with the official Whisper tiny model (75MB):

```
https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin
```

If this works in Release mode, we know the issue is with your KBLab model files specifically.

## File Lock Issue

You're getting "file is locked by Transcriber.GUI" - you need to:
1. Close the GUI application completely
2. Clean the solution
3. Rebuild

Or just close Visual Studio and reopen it.
