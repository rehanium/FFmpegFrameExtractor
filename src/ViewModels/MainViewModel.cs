using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FFmpegFrameExtractor.Models;
using FFmpegFrameExtractor.Services;
using Microsoft.Extensions.Logging;

namespace FFmpegFrameExtractor.ViewModels;

/// <summary>
/// Main application ViewModel. Orchestrates all sub-sections of the UI.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly FFmpegService _ffmpegService;
    private readonly FFprobeService _ffprobeService;
    private readonly FFmpegLocatorService _locatorService;
    private readonly SettingsService _settingsService;
    private readonly ILogger<MainViewModel> _logger;

    private CancellationTokenSource? _cts;

    // -----------------------------------------------------------------------
    // Observable properties
    // -----------------------------------------------------------------------

    [ObservableProperty] private string _title = "FFmpeg Frame Extractor";
    [ObservableProperty] private string _statusMessage = "Ready. Load a video file to begin.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isDarkMode = true;
    [ObservableProperty] private string _ffmpegStatus = "Checking FFmpeg...";
    [ObservableProperty] private bool _ffmpegAvailable;

    // --- File / Video info ---
    [ObservableProperty] private string _inputFilePath = string.Empty;
    [ObservableProperty] private VideoInfo? _videoInfo;
    [ObservableProperty] private bool _hasVideoLoaded;

    // --- Extraction options ---
    [ObservableProperty] private ExtractionOptionsViewModel _options;

    // --- Progress ---
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private string _progressFps = string.Empty;
    [ObservableProperty] private string _estimatedTime = string.Empty;
    [ObservableProperty] private bool _showProgress;

    // --- Results ---
    [ObservableProperty] private ObservableCollection<ExtractedFrame> _extractedFrames = new();
    [ObservableProperty] private int _totalFramesExtracted;
    [ObservableProperty] private string _extractionSummary = string.Empty;
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private ExtractedFrame? _selectedFrame;

    // --- FFmpeg info ---
    [ObservableProperty] private string _ffmpegVersion = string.Empty;
    [ObservableProperty] private string _ffprobeVersion = string.Empty;
    [ObservableProperty] private string _fFmpegPath = string.Empty;
    [ObservableProperty] private string _fFprobePath = string.Empty;

    // --- Tab index ---
    [ObservableProperty] private int _selectedTabIndex;

    // --- Command preview ---
    [ObservableProperty] private string _commandPreview = string.Empty;


    public MainViewModel(
        FFmpegService ffmpegService,
        FFprobeService ffprobeService,
        FFmpegLocatorService locatorService,
        SettingsService settingsService,
        ILogger<MainViewModel> logger)
    {
        _ffmpegService = ffmpegService;
        _ffprobeService = ffprobeService;
        _locatorService = locatorService;
        _settingsService = settingsService;
        _logger = logger;

        _options = new ExtractionOptionsViewModel();
        _options.PropertyChanged += (_, _) => UpdateCommandPreview();

        _ = InitializeAsync();
    }

    // -----------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------

    [RelayCommand]
    private async Task BrowseInputFileAsync()
    {
        var dlg = new WpfOpenFileDialog
        {
            Title = "Select Video File",
            Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.m4v;*.ts;*.mts;*.m2ts;*.vob;*.3gp;*.rmvb;*.asf|All Files|*.*",
            Multiselect = false,
        };

        if (!string.IsNullOrEmpty(_settingsService.Current.LastInputDirectory))
            dlg.InitialDirectory = _settingsService.Current.LastInputDirectory;

        if (dlg.ShowDialog() == true)
        {
            InputFilePath = dlg.FileName;
            _settingsService.Current.LastInputDirectory = Path.GetDirectoryName(dlg.FileName)!;

            if (string.IsNullOrEmpty(Options.OutputDirectory))
            {
                Options.OutputDirectory = Path.Combine(
                    Path.GetDirectoryName(dlg.FileName)!,
                    Path.GetFileNameWithoutExtension(dlg.FileName) + "_frames");
            }

            await LoadVideoInfoAsync();
        }
    }

    [RelayCommand]
    private async Task LoadDroppedFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;
        InputFilePath = filePath;

        if (string.IsNullOrEmpty(Options.OutputDirectory))
        {
            Options.OutputDirectory = Path.Combine(
                Path.GetDirectoryName(filePath)!,
                Path.GetFileNameWithoutExtension(filePath) + "_frames");
        }

        await LoadVideoInfoAsync();
    }

    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Output Directory for Extracted Frames",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };

        if (!string.IsNullOrEmpty(Options.OutputDirectory))
            dialog.InitialDirectory = Options.OutputDirectory;
        else if (!string.IsNullOrEmpty(_settingsService.Current.LastOutputDirectory))
            dialog.InitialDirectory = _settingsService.Current.LastOutputDirectory;

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            Options.OutputDirectory = dialog.SelectedPath;
            _settingsService.Current.LastOutputDirectory = dialog.SelectedPath;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartExtraction))]
    private async Task StartExtractionAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ShowProgress = true;
        ProgressValue = 0;
        ExtractedFrames.Clear();
        HasResults = false;
        ExtractionSummary = string.Empty;
        SelectedTabIndex = 0;

        _cts = new CancellationTokenSource();
        StatusMessage = "Starting extraction...";

        var opts = Options.BuildOptions(InputFilePath);
        var progress = new Progress<ExtractionProgress>(OnProgressReport);

        try
        {
            var result = await _ffmpegService.ExtractFramesAsync(opts, VideoInfo, progress, _cts.Token);

            if (result.Success)
            {
                foreach (var frame in result.Frames)
                    ExtractedFrames.Add(frame);

                TotalFramesExtracted = result.TotalFramesExtracted;
                HasResults = result.TotalFramesExtracted > 0;
                ExtractionSummary = BuildSummary(result);
                StatusMessage = $"Extraction complete — {result.TotalFramesExtracted} frames in {result.ElapsedDisplay}";
                SelectedTabIndex = 4; // Results tab

                _settingsService.Current.LastOptions = opts;
                await _settingsService.SaveAsync();

                if (_settingsService.Current.AutoOpenOutputFolder && Directory.Exists(opts.OutputDirectory))
                    OpenFolder(opts.OutputDirectory);
            }
            else
            {
                StatusMessage = $"Extraction failed: {result.ErrorMessage}";
                ShowErrorDialog(result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Extraction cancelled.";
        }
        finally
        {
            IsBusy = false;
            ShowProgress = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanStartExtraction() =>
        HasVideoLoaded && FfmpegAvailable && !string.IsNullOrWhiteSpace(Options.OutputDirectory);

    [RelayCommand]
    private void CancelExtraction()
    {
        _cts?.Cancel();
        StatusMessage = "Cancelling...";
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        var dir = Options.OutputDirectory;
        if (Directory.Exists(dir))
            OpenFolder(dir);
    }

    [RelayCommand]
    private void OpenSelectedFrame()
    {
        if (SelectedFrame?.FilePath is { } path && File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task LocateFFmpegAsync()
    {
        try
        {
            var dlg = new WpfOpenFileDialog
            {
                Title = "Locate ffmpeg.exe",
                Filter = "ffmpeg.exe|ffmpeg.exe|All Executables (*.exe)|*.exe",
            };
            if (dlg.ShowDialog() == true)
            {
                _settingsService.Current.FFmpegPath = dlg.FileName;
                
                // Auto-detect sibling ffprobe.exe
                var dir = Path.GetDirectoryName(dlg.FileName);
                if (!string.IsNullOrEmpty(dir))
                {
                    var siblingProbe = Path.Combine(dir, "ffprobe.exe");
                    if (File.Exists(siblingProbe))
                        _settingsService.Current.FFprobePath = siblingProbe;
                }

                await _settingsService.SaveAsync();
                await InitFFmpegAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while setting FFmpeg path");
            StatusMessage = $"Error setting FFmpeg path: {ex.Message}";
            ShowErrorDialog($"Could not configure FFmpeg path:\n\n{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LocateFFprobeAsync()
    {
        try
        {
            var dlg = new WpfOpenFileDialog
            {
                Title = "Locate ffprobe.exe",
                Filter = "ffprobe.exe|ffprobe.exe|All Executables (*.exe)|*.exe",
            };
            if (dlg.ShowDialog() == true)
            {
                _settingsService.Current.FFprobePath = dlg.FileName;
                
                // Auto-detect sibling ffmpeg.exe
                var dir = Path.GetDirectoryName(dlg.FileName);
                if (!string.IsNullOrEmpty(dir))
                {
                    var siblingFfmpeg = Path.Combine(dir, "ffmpeg.exe");
                    if (File.Exists(siblingFfmpeg))
                        _settingsService.Current.FFmpegPath = siblingFfmpeg;
                }

                await _settingsService.SaveAsync();
                await InitFFmpegAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while setting FFprobe path");
            StatusMessage = $"Error setting FFprobe path: {ex.Message}";
            ShowErrorDialog($"Could not configure FFprobe path:\n\n{ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleDarkMode()
    {
        IsDarkMode = !IsDarkMode;
        _settingsService.Current.DarkMode = IsDarkMode;
        ApplyTheme();
    }

    [RelayCommand]
    private void CopyCommandToClipboard()
    {
        if (!string.IsNullOrEmpty(CommandPreview))
            System.Windows.Clipboard.SetText(CommandPreview);
    }

    [RelayCommand]
    private void ClearResults()
    {
        ExtractedFrames.Clear();
        HasResults = false;
        TotalFramesExtracted = 0;
        ExtractionSummary = string.Empty;
        StatusMessage = "Results cleared.";
    }

    // -----------------------------------------------------------------------
    // Initialization
    // -----------------------------------------------------------------------

    private async Task InitializeAsync()
    {
        try
        {
            _settingsService.Load();
            IsDarkMode = _settingsService.Current.DarkMode;
            ApplyTheme();

            if (_settingsService.Current.RememberLastOptions && _settingsService.Current.LastOptions != null)
                Options.LoadFrom(_settingsService.Current.LastOptions);

            await InitFFmpegAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during app initialization");
            StatusMessage = $"Initialization error: {ex.Message}";
        }
    }

    private async Task InitFFmpegAsync()
    {
        try
        {
            FfmpegStatus = "Locating FFmpeg...";
            FfmpegAvailable = false;

            await _locatorService.LocateAsync(
                _settingsService.Current.FFmpegPath,
                _settingsService.Current.FFprobePath);

            FFmpegPath = _locatorService.FFmpegPath ?? _settingsService.Current.FFmpegPath ?? string.Empty;
            FFprobePath = _locatorService.FFprobePath ?? _settingsService.Current.FFprobePath ?? string.Empty;

            if (_locatorService.IsAvailable)
            {
                var ffmpegPath = _locatorService.FFmpegPath!;
                var ffprobePath = _locatorService.FFprobePath!;

                _ffmpegService.SetExecutablePath(ffmpegPath);
                _ffprobeService.SetExecutablePath(ffprobePath);

                FfmpegVersion = await _ffmpegService.GetVersionAsync() ?? "unknown";
                FfprobeVersion = await _ffprobeService.GetVersionAsync() ?? "unknown";

                FfmpegAvailable = true;
                FfmpegStatus = $"FFmpeg {FfmpegVersion}";
                StatusMessage = "FFmpeg ready. Load a video file to begin.";
            }
            else if (!string.IsNullOrEmpty(FFmpegPath) && string.IsNullOrEmpty(FFprobePath))
            {
                FfmpegStatus = "FFprobe not found — please configure ffprobe.exe";
                StatusMessage = "FFprobe.exe not found. Please locate it in the Advanced tab.";
            }
            else
            {
                FfmpegStatus = "FFmpeg not found — configure paths in Advanced tab";
                StatusMessage = "FFmpeg not found. Please configure the path in Advanced tab.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error probing FFmpeg version");
            FfmpegStatus = "FFmpeg check failed";
            StatusMessage = $"FFmpeg error: {ex.Message}";
        }

        StartExtractionCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadVideoInfoAsync()
    {
        if (!FfmpegAvailable)
        {
            StatusMessage = "FFmpeg not available. Cannot probe video.";
            return;
        }

        IsBusy = true;
        HasVideoLoaded = false;
        VideoInfo = null;
        StatusMessage = "Loading video info...";

        try
        {
            VideoInfo = await _ffprobeService.ProbeAsync(InputFilePath);
            HasVideoLoaded = true;
            Title = $"FFmpeg Frame Extractor — {VideoInfo.FileName}";
            StatusMessage = $"Loaded: {VideoInfo.FileName} ({VideoInfo.ResolutionDisplay}, {VideoInfo.DurationDisplay}, {VideoInfo.FrameRateDisplay})";
            UpdateCommandPreview();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load video info");
            StatusMessage = $"Failed to load video: {ex.Message}";
            ShowErrorDialog($"Failed to read video metadata:\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            StartExtractionCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnProgressReport(ExtractionProgress progress)
    {
        ProgressValue = progress.Percentage;
        ProgressText = progress.TotalFrames > 0
            ? $"Frame {progress.CurrentFrame} / {progress.TotalFrames} ({progress.Percentage:F1}%)"
            : $"Frame {progress.CurrentFrame}...";

        if (progress.ProcessingFps > 0)
            ProgressFps = $"{progress.ProcessingFps:F1} fps";

        if (progress.EstimatedRemaining.HasValue)
            EstimatedTime = $"~{progress.EstimatedRemaining.Value:mm\\:ss} remaining";

        StatusMessage = progress.StatusMessage;
    }

    private void UpdateCommandPreview()
    {
        if (!HasVideoLoaded) return;
        try
        {
            var opts = Options.BuildOptions(InputFilePath);
            CommandPreview = $"ffmpeg {_ffmpegService.BuildArguments(opts, VideoInfo)}";
        }
        catch
        {
            // Ignore preview errors
        }
    }

    private static string BuildSummary(ExtractionResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Frames Extracted: {result.TotalFramesExtracted}");
        if (result.FailedFrames > 0)
            sb.AppendLine($"Failed: {result.FailedFrames}");
        sb.AppendLine($"Time Elapsed: {result.ElapsedDisplay}");
        if (result.TotalOutputSizeBytes > 0)
        {
            double mb = result.TotalOutputSizeBytes / 1_048_576.0;
            sb.AppendLine($"Total Size: {mb:F2} MB");
        }
        return sb.ToString().TrimEnd();
    }

    private void ApplyTheme()
    {
        var app = WpfApplication.Current;
        if (app == null) return;

        var dict = app.Resources.MergedDictionaries;
        var themeUri = new Uri(IsDarkMode
            ? "pack://application:,,,/Themes/DarkTheme.xaml"
            : "pack://application:,,,/Themes/LightTheme.xaml",
            UriKind.Absolute);

        if (dict.Count > 0) dict.RemoveAt(0);
        dict.Insert(0, new System.Windows.ResourceDictionary { Source = themeUri });
    }

    private static void OpenFolder(string path)
    {
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private static void ShowErrorDialog(string message)
    {
        System.Windows.MessageBox.Show(message, "Error",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);
    }
}
