using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FFmpegFrameExtractor.Models;

namespace FFmpegFrameExtractor.ViewModels;

/// <summary>
/// ViewModel wrapper around ExtractionOptions for data binding.
/// </summary>
public partial class ExtractionOptionsViewModel : ObservableObject
{
    // --- Mode ---
    [ObservableProperty] private ExtractionMode _mode = ExtractionMode.EveryNSeconds;
    [ObservableProperty] private bool _modeEveryNSeconds = true;
    [ObservableProperty] private bool _modeFrameRate;
    [ObservableProperty] private bool _modeSpecificTimestamps;
    [ObservableProperty] private bool _modeTimeRange;
    [ObservableProperty] private bool _modeEveryNthFrame;
    [ObservableProperty] private bool _modeEvenlyDistributed;
    [ObservableProperty] private bool _modeSingleFrame;
    [ObservableProperty] private bool _modeSceneChange;

    // --- Mode parameters ---
    [ObservableProperty] private double _intervalSeconds = 1.0;
    [ObservableProperty] private double _extractFps = 1.0;
    [ObservableProperty] private string _timestampList = string.Empty;
    [ObservableProperty] private string _rangeStart = "00:00:00.000";
    [ObservableProperty] private string _rangeEnd = "00:00:10.000";
    [ObservableProperty] private int _everyNthFrame = 10;
    [ObservableProperty] private int _distributedCount = 10;
    [ObservableProperty] private string _singleTimestamp = "00:00:01.000";
    [ObservableProperty] private double _sceneChangeThreshold = 0.4;

    // --- Output ---
    [ObservableProperty] private string _outputDirectory = string.Empty;
    [ObservableProperty] private string _fileNamePattern = "frame_%04d";
    [ObservableProperty] private OutputFormat _outputFormat = OutputFormat.Jpeg;
    [ObservableProperty] private bool _useQualityPreset = true;
    [ObservableProperty] private QualityPreset _qualityPreset = QualityPreset.High;
    [ObservableProperty] private int _customQuality = 90;
    [ObservableProperty] private int _pngCompression = 2;

    // --- Resize ---
    [ObservableProperty] private bool _enableResize;
    [ObservableProperty] private int _resizeWidth;
    [ObservableProperty] private int _resizeHeight;
    [ObservableProperty] private bool _maintainAspectRatio = true;

    // --- Color / Filters ---
    [ObservableProperty] private bool _enableGrayscale;
    [ObservableProperty] private bool _enableDeinterlace;
    [ObservableProperty] private bool _enableDenoise;
    [ObservableProperty] private double _brightness;         // -1..1
    [ObservableProperty] private double _contrast = 1.0;    // 0..2
    [ObservableProperty] private double _saturation = 1.0;  // 0..3
    [ObservableProperty] private double _gamma = 1.0;       // 0.1..10
    [ObservableProperty] private bool _enableSharpness;
    [ObservableProperty] private double _sharpnessAmount = 1.5;

    // --- Trim ---
    [ObservableProperty] private bool _enableTrim;
    [ObservableProperty] private string _trimStart = "00:00:00.000";
    [ObservableProperty] private string _trimEnd = "00:00:00.000";

    // --- Hardware / Performance ---
    [ObservableProperty] private bool _useHardwareAcceleration;
    [ObservableProperty] private string _hwAccelType = "auto";
    [ObservableProperty] private int _threadCount;

    // --- Overlay ---
    [ObservableProperty] private bool _drawTimestamp;
    [ObservableProperty] private bool _drawFrameNumber;

    // --- Computed display ---
    public bool IsJpegFormat => OutputFormat == OutputFormat.Jpeg;
    public bool IsWebpFormat => OutputFormat == OutputFormat.Webp;
    public bool IsPngFormat => OutputFormat == OutputFormat.Png;
    public bool ShowQualitySlider => IsJpegFormat || IsWebpFormat;

    public IEnumerable<ExtractionMode> ExtractionModes =>
        Enum.GetValues<ExtractionMode>();

    public IEnumerable<OutputFormat> OutputFormats =>
        Enum.GetValues<OutputFormat>();

    public IEnumerable<QualityPreset> QualityPresets =>
        Enum.GetValues<QualityPreset>();

    public IEnumerable<string> HwAccelTypes =>
        new[] { "auto", "dxva2", "d3d11va", "cuda", "qsv", "vulkan" };

    partial void OnModeChanged(ExtractionMode value)
    {
        // Keep bool flags in sync (used by radio-button-style UI)
        ModeEveryNSeconds = value == ExtractionMode.EveryNSeconds;
        ModeFrameRate = value == ExtractionMode.FrameRate;
        ModeSpecificTimestamps = value == ExtractionMode.SpecificTimestamps;
        ModeTimeRange = value == ExtractionMode.TimeRange;
        ModeEveryNthFrame = value == ExtractionMode.EveryNthFrame;
        ModeEvenlyDistributed = value == ExtractionMode.EvenlyDistributed;
        ModeSingleFrame = value == ExtractionMode.SingleFrame;
        ModeSceneChange = value == ExtractionMode.SceneChange;
    }

    partial void OnOutputFormatChanged(OutputFormat value)
    {
        OnPropertyChanged(nameof(IsJpegFormat));
        OnPropertyChanged(nameof(IsWebpFormat));
        OnPropertyChanged(nameof(IsPngFormat));
        OnPropertyChanged(nameof(ShowQualitySlider));
    }

    /// <summary>
    /// Builds an ExtractionOptions model from the current ViewModel state.
    /// </summary>
    public ExtractionOptions BuildOptions(string inputFilePath)
    {
        var opts = new ExtractionOptions
        {
            InputFilePath = inputFilePath,
            Mode = Mode,
            IntervalSeconds = IntervalSeconds,
            ExtractFps = ExtractFps,
            TimestampList = TimestampList,
            EveryNthFrameValue = EveryNthFrame,
            DistributedCount = DistributedCount,
            SceneChangeThreshold = SceneChangeThreshold,
            OutputDirectory = OutputDirectory,
            FileNamePattern = FileNamePattern,
            Format = OutputFormat,
            UseQualityPreset = UseQualityPreset,
            QualityPreset = QualityPreset,
            CustomQuality = CustomQuality,
            PngCompression = PngCompression,
            EnableResize = EnableResize,
            ResizeWidth = ResizeWidth,
            ResizeHeight = ResizeHeight,
            MaintainAspectRatio = MaintainAspectRatio,
            EnableGrayscale = EnableGrayscale,
            EnableDeinterlace = EnableDeinterlace,
            EnableDenoise = EnableDenoise,
            Brightness = Brightness,
            Contrast = Contrast,
            Saturation = Saturation,
            Gamma = Gamma,
            EnableSharpness = EnableSharpness,
            SharpnessAmount = SharpnessAmount,
            EnableTrim = EnableTrim,
            UseHardwareAcceleration = UseHardwareAcceleration,
            HwAccelType = HwAccelType,
            ThreadCount = ThreadCount,
            DrawTimestamp = DrawTimestamp,
            DrawFrameNumber = DrawFrameNumber,
        };

        // Parse timestamps
        if (TryParseTimestamp(RangeStart, out var rs)) opts.RangeStart = rs;
        if (TryParseTimestamp(RangeEnd, out var re)) opts.RangeEnd = re;
        if (TryParseTimestamp(SingleTimestamp, out var st)) opts.SingleFrameTimestamp = st;
        if (TryParseTimestamp(TrimStart, out var ts)) opts.TrimStart = ts;
        if (TryParseTimestamp(TrimEnd, out var te)) opts.TrimEnd = te;

        return opts;
    }

    /// <summary>
    /// Loads values from an existing ExtractionOptions (for "remember last" feature).
    /// </summary>
    public void LoadFrom(ExtractionOptions opts)
    {
        Mode = opts.Mode;
        IntervalSeconds = opts.IntervalSeconds;
        ExtractFps = opts.ExtractFps;
        TimestampList = opts.TimestampList;
        EveryNthFrame = opts.EveryNthFrameValue;
        DistributedCount = opts.DistributedCount;
        SceneChangeThreshold = opts.SceneChangeThreshold;
        OutputDirectory = opts.OutputDirectory;
        FileNamePattern = opts.FileNamePattern;
        OutputFormat = opts.Format;
        UseQualityPreset = opts.UseQualityPreset;
        QualityPreset = opts.QualityPreset;
        CustomQuality = opts.CustomQuality;
        PngCompression = opts.PngCompression;
        EnableResize = opts.EnableResize;
        ResizeWidth = opts.ResizeWidth;
        ResizeHeight = opts.ResizeHeight;
        MaintainAspectRatio = opts.MaintainAspectRatio;
        EnableGrayscale = opts.EnableGrayscale;
        EnableDeinterlace = opts.EnableDeinterlace;
        EnableDenoise = opts.EnableDenoise;
        Brightness = opts.Brightness;
        Contrast = opts.Contrast;
        Saturation = opts.Saturation;
        Gamma = opts.Gamma;
        EnableSharpness = opts.EnableSharpness;
        SharpnessAmount = opts.SharpnessAmount;
        EnableTrim = opts.EnableTrim;
        UseHardwareAcceleration = opts.UseHardwareAcceleration;
        HwAccelType = opts.HwAccelType;
        ThreadCount = opts.ThreadCount;
        DrawTimestamp = opts.DrawTimestamp;
        DrawFrameNumber = opts.DrawFrameNumber;

        TrimStart = opts.TrimStart.ToString(@"hh\:mm\:ss\.fff");
        TrimEnd = opts.TrimEnd.ToString(@"hh\:mm\:ss\.fff");
        RangeStart = opts.RangeStart.ToString(@"hh\:mm\:ss\.fff");
        RangeEnd = opts.RangeEnd.ToString(@"hh\:mm\:ss\.fff");
        SingleTimestamp = opts.SingleFrameTimestamp.ToString(@"hh\:mm\:ss\.fff");
    }

    private static bool TryParseTimestamp(string s, out TimeSpan result)
    {
        if (TimeSpan.TryParseExact(s,
            new[] { @"h\:mm\:ss\.fff", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff",
                    @"h\:mm\:ss", @"hh\:mm\:ss", @"mm\:ss" },
            null, out result))
            return true;
        if (double.TryParse(s, out double secs))
        {
            result = TimeSpan.FromSeconds(secs);
            return true;
        }
        result = TimeSpan.Zero;
        return false;
    }
}
