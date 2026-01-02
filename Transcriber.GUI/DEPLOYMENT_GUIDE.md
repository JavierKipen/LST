# Deployment Guide for Transcriber GUI

## Overview

The application is now self-contained in a single project with all necessary components integrated.

## Model Folder Locations

The application checks for models in the following order:

1. **`%APPDATA%\Transcriber\Models`** (Recommended for installed apps)
   - Example: `C:\Users\YourName\AppData\Roaming\Transcriber\Models\`
   
2. **`[Application Directory]\Models`** (Alternative)
   - Example: `C:\Program Files\Transcriber\Models\`
   
3. **Development path** (Fallback for debugging)
   - `C:\Users\javier.kipen\Documents\GitHub\LST\Models\KBLab\`

## Required Models

Place these model files in one of the above locations:

### Standard Models
- `kb-ggml-tiny.bin` (VeryLow accuracy)
- `kb-ggml-base.bin` (Low accuracy)
- `kb-ggml-small.bin` (Medium accuracy)
- `kb-ggml-medium.bin` (Good accuracy)
- `kb-ggml-large.bin` (VeryGood accuracy)

### Quantized Models (Optional)
- `kb-ggml-tiny-q5_0.bin`
- `kb-ggml-base-q5_0.bin`
- `kb-ggml-small-q5_0.bin`
- `kb-ggml-medium-q5_0.bin`
- `kb-ggml-large-q5_0.bin`

## Publishing the Application

### Option 1: Framework-Dependent (Smaller size)

```powershell
dotnet publish Transcriber.GUI\Transcriber.GUI.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o publish\transcriber
```

Requirements: Users must have .NET 10 Runtime installed.

### Option 2: Self-Contained (Recommended for installer)

```powershell
dotnet publish Transcriber.GUI\Transcriber.GUI.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -o publish\transcriber
```

This includes all .NET runtime files (larger, but no prerequisites).

## Creating an Installer

### Using Inno Setup (Recommended)

1. **Download Inno Setup**: https://jrsoftware.org/isdl.php

2. **Create `setup.iss` file**:

```inno
#define MyAppName "Audio Transcriber"
#define MyAppVersion "1.0"
#define MyAppPublisher "Your Name"
#define MyAppExeName "Transcriber.GUI.exe"

[Setup]
AppId={{YOUR-GUID-HERE}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=installer
OutputBaseFilename=TranscriberSetup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Application files
Source: "publish\transcriber\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Models (adjust source path to your models location)
Source: "Models\KBLab\*.bin"; DestDir: "{userappdata}\Transcriber\Models"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
```

3. **Compile the installer**:
   - Open `setup.iss` in Inno Setup
   - Click Build ? Compile
   - Output: `installer\TranscriberSetup.exe`

### Using WiX Toolset (Advanced)

For MSI installers, use WiX: https://wixtoolset.org/

## Portable Version (No Installer)

1. **Publish as above**

2. **Create folder structure**:
```
Transcriber-Portable\
??? Transcriber.GUI.exe
??? (all DLLs and dependencies)
??? Models\
    ??? kb-ggml-tiny.bin
    ??? kb-ggml-base.bin
    ??? kb-ggml-small.bin
    ??? kb-ggml-medium.bin
    ??? kb-ggml-large.bin
```

3. **Zip it**: `Transcriber-Portable-v1.0.zip`

Users extract and run directly!

## Automated Build Script

Create `build-installer.ps1`:

```powershell
# Build script for Transcriber GUI

$ErrorActionPreference = "Stop"

Write-Host "Building Transcriber GUI..." -ForegroundColor Cyan

# Clean previous builds
Remove-Item -Path "publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "installer" -Recurse -Force -ErrorAction SilentlyContinue

# Publish application
Write-Host "Publishing application..." -ForegroundColor Yellow
dotnet publish Transcriber.GUI\Transcriber.GUI.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -o publish\transcriber

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Copy models
Write-Host "Copying models..." -ForegroundColor Yellow
$modelsSource = "Models\KBLab"
$modelsDest = "publish\transcriber\Models"

if (Test-Path $modelsSource) {
    New-Item -ItemType Directory -Force -Path $modelsDest | Out-Null
    Copy-Item "$modelsSource\*.bin" -Destination $modelsDest -Force
    Write-Host "Models copied successfully" -ForegroundColor Green
} else {
    Write-Host "Warning: Models not found at $modelsSource" -ForegroundColor Yellow
}

# Create portable ZIP
Write-Host "Creating portable ZIP..." -ForegroundColor Yellow
Compress-Archive -Path "publish\transcriber\*" -DestinationPath "Transcriber-Portable.zip" -Force

Write-Host "`nBuild complete!" -ForegroundColor Green
Write-Host "Published files: publish\transcriber\" -ForegroundColor Cyan
Write-Host "Portable ZIP: Transcriber-Portable.zip" -ForegroundColor Cyan
Write-Host "`nTo create installer, run Inno Setup with setup.iss" -ForegroundColor Cyan
```

Run: `.\build-installer.ps1`

## Testing the Installation

1. **Copy to test machine** or **VM**
2. **Run installer** or extract portable version
3. **Verify models** are in the correct location
4. **Test transcription** with a small audio file
5. **Check output** `.txt` file is created

## Troubleshooting

### Models not found
- Check `%APPDATA%\Transcriber\Models` exists
- Verify `.bin` files are present
- Check file permissions

### Native DLL errors
- Ensure Visual C++ Redistributable 2015-2022 is installed
- Download: https://aka.ms/vs/17/release/vc_redist.x64.exe

### Missing .NET Runtime (framework-dependent only)
- Install .NET 10 Runtime: https://dotnet.microsoft.com/download

## Distribution Checklist

- [ ] Build published successfully
- [ ] Models copied to output
- [ ] Tested on clean Windows installation
- [ ] Installer creates all necessary folders
- [ ] Application launches without errors
- [ ] Transcription works with test file
- [ ] Output file saved correctly
- [ ] Uninstaller removes all files

## File Sizes (Approximate)

- **Application (self-contained)**: ~150 MB
- **Models (all 5 standard)**: ~2.5 GB total
  - tiny: 75 MB
  - base: 145 MB
  - small: 466 MB
  - medium: 1.5 GB
  - large: 2.9 GB
- **Total installer**: ~2.7 GB (with all models)

**Recommendation**: Include only tiny, base, and small models in installer by default (686 MB), let users download larger models separately if needed.
