# FFmpeg Frame Extractor (.NET / WPF)

A Windows desktop utility built with C# and WPF targeting .NET with MVVM architecture, designed specifically for extracting frames from video files using FFmpeg and FFprobe.

---

## 🌟 Key Features

### 🎞 Multiple Extraction Modes
- **Every N Seconds:** Extract frames at user-defined regular intervals (e.g. 1 frame every 0.5s, 1s, 5s, etc.).
- **Fixed FPS Rate:** Extract frames at exact output frame rates.
- **Specific Timestamps:** Comma/newline-delimited timestamp lists (`HH:MM:SS.mmm` or raw seconds).
- **Time Range:** Extract frames between specific start and end timestamps.
- **Every N-th Frame:** Extract every Nth frame by frame index.
- **Evenly Distributed:** Extract N frames evenly spaced across the entire video duration.
- **Single Frame (Thumbnail):** Grab a single frame at a precise timestamp.
- **Scene Change Detection:** Detect and extract frames based on scene transitions with custom threshold sliders.

### 🖼 Output Options & Image Processing
- **Multiple Image Formats:** JPEG, PNG, WebP, BMP, TIFF.
- **Quality Presets & Custom Quality:** Preset modes (Maximum, High, Good, Medium, Low) and granular quality sliders (1–100) for JPEG and WebP.
- **PNG Compression Control:** Fast-to-maximum compression control (0–9).
- **Proportional / Free Resize:** Aspect-ratio-preserving or exact width/height resizing.
- **Color Adjustments & Filters:**
  - Brightness, Contrast, Saturation, and Gamma adjustments (`eq` filter).
  - Grayscale conversion.
  - Deinterlacing (`yadif`).
  - Denoising (`hqdn3d`).
  - Unsharp Mask / Sharpening.
- **Burn-in Overlays:** Optional timestamp and frame number overlays.
- **Input Trimming:** Fast seeking with `-ss` and `-t` parameters.
- **Hardware Acceleration:** Hardware-accelerated decoding support (`auto`, `dxva2`, `d3d11va`, `cuda`, `qsv`, `vulkan`).

### 💻 UI & Desktop Experience
- **MVVM Architecture:** Clean separation using `CommunityToolkit.Mvvm` and Dependency Injection via `Microsoft.Extensions.DependencyInjection`.
- **Theme Support:** Modern Dark Mode & Light Mode with instant dynamic theme switching.
- **Asynchronous Execution:** Background async execution without blocking the UI thread.
- **Real-time Progress:** Displays frame counts, processing FPS, progress bar, and estimated completion time.
- **Results DataGrid:** Browse extracted frames, view file sizes, timestamps, and double-click to open.
- **Command Preview & Clipboard Copy:** Inspect the exact FFmpeg command line generated and copy it to clipboard.
- **Drag & Drop:** Drop any video file onto the drop-zone to inspect and load metadata immediately.
- **Automatic Tool Discovery:** Automatically locates `ffmpeg.exe` and `ffprobe.exe` from `PATH` or standard installation locations, with fallback manual path configuration.

---

## 📂 Project Structure

```
FFmpegFrameExtractor/
├── src/
│   ├── App.xaml / App.xaml.cs          # Application entry point & DI configuration
│   ├── app.manifest                    # DPI awareness & Windows 10/11 compatibility
│   ├── GlobalUsings.cs                 # Global using aliases
│   ├── Models/
│   │   ├── AppModels.cs                # Settings, Results, Progress models
│   │   ├── ExtractionOptions.cs        # Filter chains, format options, mode enums
│   │   ├── ExtractedFrame.cs           # Individual frame metadata
│   │   └── VideoInfo.cs                # Video stream & container metadata
│   ├── Services/
│   │   ├── FFmpegLocatorService.cs     # Finds FFmpeg/FFprobe binaries on the machine
│   │   ├── FFmpegService.cs            # Builds CLI args and runs FFmpeg with progress
│   │   ├── FFprobeService.cs           # Queries file metadata in JSON mode
│   │   └── SettingsService.cs          # JSON persistence in %APPDATA%
│   ├── ViewModels/
│   │   ├── MainViewModel.cs            # Primary ViewModel with RelayCommands
│   │   └── ExtractionOptionsViewModel.cs # Databound extraction configuration
│   ├── Views/
│   │   ├── MainWindow.xaml             # Themed multi-tab Windows desktop layout
│   │   └── MainWindow.xaml.cs          # Drag/Drop & event code-behind
│   ├── Converters/
│   │   └── ValueConverters.cs          # WPF Value Converters
│   ├── Themes/
│   │   ├── DarkTheme.xaml              # Modern dark theme palette & brushes
│   │   └── LightTheme.xaml             # Clean light theme palette & brushes
│   └── Resources/
│       └── Styles.xaml                 # Custom button, card, progress bar styles
```

---

## 🚀 Building and Running

### Prerequisites
- .NET SDK (8.0 or newer)
- FFmpeg and FFprobe installed on the system or located in `PATH`

### Build Command
```powershell
dotnet build src/FFmpegFrameExtractor.csproj -c Release
```

### Run Command
```powershell
dotnet run --project src/FFmpegFrameExtractor.csproj
```
