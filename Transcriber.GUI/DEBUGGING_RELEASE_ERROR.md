# Debugging ExecutionEngineException in Release Mode

## Current Status
- ? Native DLLs ARE being copied to output folder
- ? Still getting `ExecutionEngineException` in Release mode
- ? Debug mode works fine
- ? Console app works fine

## Next Steps to Diagnose

### 1. Run with Enhanced Logging
I've added diagnostic logging to MainWindow.xaml.cs. 

**Run the Release build and check the Output window (Debug pane) for:**
- Base directory path
- Whether whisper.dll is found
- At what point it crashes (before or after SetupRun)

### 2. Possible Causes of ExecutionEngineException

#### A. AVX Instruction Set Mismatch
Whisper.net includes different native DLL variants:
- `runtimes/win-x64/` - Standard (requires AVX)
- `runtimes/noavx/win-x64/` - No AVX required

If your CPU doesn't support AVX, or Release mode is loading a different variant, it could crash.

**Check CPU Support:**
```powershell
# Run in PowerShell
wmic cpu get caption, deviceid, name, numberofcores, maxclockspeed
# Or
Get-CimInstance -ClassName Win32_Processor | Select-Object Name, Description
```

Then check if AVX is supported:
```powershell
# This PowerShell command checks for AVX support
(Get-WmiObject -Class Win32_Processor).Name
# Google your CPU model + "AVX support"
```

#### B. .NET Runtime Configuration Difference
Release builds use different runtime configuration.

**Check runtime config:**
```
Transcriber.GUI\bin\Release\net10.0-windows\Transcriber.GUI.runtimeconfig.json
```

Compare with Debug:
```
Transcriber.GUI\bin\Debug\net10.0-windows\Transcriber.GUI.runtimeconfig.json
```

#### C. Missing C++ Runtime Dependencies
The native whisper.dll requires specific C++ runtime DLLs.

**Install (if not already installed):**
- Visual C++ 2015-2022 Redistributable (x64)
- Download: https://aka.ms/vs/17/release/vc_redist.x64.exe

**Check if already installed:**
```powershell
Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* | Where-Object {$_.DisplayName -like "*Visual C++*"} | Select-Object DisplayName, DisplayVersion
```

#### D. Read-Only or Locked Files
Sometimes Release builds have files locked.

**Try:**
1. Close Visual Studio
2. Delete `bin` and `obj` folders manually
3. Reopen and rebuild

### 3. Force Specific Runtime

Try forcing the NoAVX runtime to see if it's an AVX issue.

**Add to Transcriber.GUI.csproj:**
```xml
<PropertyGroup>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>

<ItemGroup>
  <!-- Try using only the NoAVX runtime -->
  <PackageReference Include="Whisper.net" Version="1.9.0" />
  <PackageReference Include="Whisper.net.Runtime.NoAvx" Version="1.9.0" />
</ItemGroup>
```

### 4. Check for Exceptions During Static Initialization

The `ExecutionEngineException` might be happening during WhisperFactory's static initialization.

**Add this test to MainWindow constructor:**
```csharp
public MainWindow()
{
    InitializeComponent();
    
    // Test if Whisper library loads at all
    try
    {
        var info = Whisper.net.WhisperFactory.GetRuntimeInfo();
        System.Diagnostics.Debug.WriteLine($"Whisper Runtime Info: {info}");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Whisper library failed to initialize:\n{ex.Message}\n\nType: {ex.GetType().Name}", 
            "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

### 5. Use Dependency Walker or Process Monitor
To see EXACTLY which DLL is failing to load:

**Download Dependencies (modern Dependency Walker):**
https://github.com/lucasg/Dependencies/releases

**Then:**
1. Open `Transcriber.GUI.exe` (Release build) in Dependencies
2. Look for any missing DLLs in red

**Or use Process Monitor:**
1. Download: https://learn.microsoft.com/en-us/sysinternals/downloads/procmon
2. Run procmon.exe
3. Filter: Process Name = Transcriber.GUI.exe
4. Run your app
5. Look for "NAME NOT FOUND" or "ACCESS DENIED" on DLL files

### 6. Compare Debug vs Release DLL Loading

**Check if different whisper.dll variants are being loaded:**

Add to MainWindow constructor:
```csharp
AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
{
    System.Diagnostics.Debug.WriteLine($"Assembly Loaded: {args.LoadedAssembly.FullName}");
};
```

## What to Try First

1. **Add the test code to MainWindow constructor** (from step 4)
2. **Run Release build** and see if error happens immediately on startup
3. **Check the enhanced error message** I added to TranscribeButton_Click
4. **Check Output window** for diagnostic messages

## Report Back

When you run it, tell me:
1. Does the error happen on app startup or when clicking Transcribe?
2. What does the detailed error message show?
3. What's in the Output window (Debug pane)?
4. Do you see "Whisper Runtime Info" logged on startup?
