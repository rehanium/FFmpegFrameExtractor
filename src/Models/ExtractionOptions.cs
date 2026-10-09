using System;

namespace FFmpegFrameExtractor.Models;

/// <summary>
/// Defines the extraction mode — how frames are selected from the video.
/// </summary>
public enum ExtractionMode
{
    /// <summary>Extract one frame every N seconds.</summary>
    EveryNSeconds,

    /// <summary>Extract at a fixed frames-per-second rate.</summary>
    FrameRate,

    /// <summary>Extract specific frames by timestamp list.</summary>
    SpecificTimestamps,

    /// <summary>Extract a range of frames between two timestamps.</summary>
    TimeRange,

    /// <summary>Extract every Nth frame by frame number.</summary>
    EveryNthFrame,

    /// <summary>Extract N frames evenly distributed across the video.</summary>
    EvenlyDistributed,

    /// <summary>Extract only the first frame (thumbnail).</summary>
    SingleFrame,
    
    /// <summary>Extract frames matching a scene change threshold.</summary>
    SceneChange,
}

/// <summary>
/// Supported output image formats.
/// </summary>
public enum OutputFormat
{
    Jpeg,
    Png,
    Bmp,
    Tiff,
    Webp,
}

/// <summary>
/// Quality level preset mapped to JPEG/WebP quality values.
/// </summary>
public enum QualityPreset
{
    Maximum = 100,
    High = 90,
    Good = 80,
    Medium = 70,
    Low = 60,
    Minimum = 50,
}

/// <summary>
/// All parameters controlling a frame extraction job.
/// </summary>
public class ExtractionOptions
{
    // -- Source --
    public string InputFilePath { get; set; } = string.Empty;

    // -- Extraction mode --
    public ExtractionMode Mode { get; set; } = ExtractionMode.EveryNSeconds;
    public double IntervalSeconds { get; set; } = 1.0;
    public double ExtractFps { get; set; } = 1.0;
    public string TimestampList { get; set; } = string.Empty;  // comma-separated HH:MM:SS.mmm
    public TimeSpan RangeStart { get; set; } = TimeSpan.Zero;
    public TimeSpan RangeEnd { get; set; } = TimeSpan.Zero;
    public int EveryNthFrameValue { get; set; } = 10;
    public int DistributedCount { get; set; } = 10;
    public TimeSpan SingleFrameTimestamp { get; set; } = TimeSpan.Zero;
    public double SceneChangeThreshold { get; set; } = 0.4;

    // -- Output --
    public string OutputDirectory { get; set; } = string.Empty;
    public string FileNamePattern { get; set; } = "frame_%04d";
    public OutputFormat Format { get; set; } = OutputFormat.Jpeg;
    public bool UseQualityPreset { get; set; } = true;
    public QualityPreset QualityPreset { get; set; } = QualityPreset.High;
    public int CustomQuality { get; set; } = 90;       // 1-100 for JPEG/WebP, ignored for PNG/BMP/TIFF
    public int PngCompression { get; set; } = 2;        // 0-9

    // -- Resize --
    public bool EnableResize { get; set; } = false;
    public int ResizeWidth { get; set; } = 0;
    public int ResizeHeight { get; set; } = 0;
    public bool MaintainAspectRatio { get; set; } = true;
    public string ScaleFilter => BuildScaleFilter();

    // -- Filters --
    public bool EnableGrayscale { get; set; } = false;
    public bool EnableDeinterlace { get; set; } = false;
    public bool EnableDenoise { get; set; } = false;
    public double Brightness { get; set; } = 0.0;   // -1.0 to 1.0
    public double Contrast { get; set; } = 1.0;     // 0.0 to 2.0
    public double Saturation { get; set; } = 1.0;   // 0.0 to 3.0
    public double Gamma { get; set; } = 1.0;        // 0.1 to 10.0
    public bool EnableSharpness { get; set; } = false;
    public double SharpnessAmount { get; set; } = 1.5;

    // -- Trim --
    public bool EnableTrim { get; set; } = false;
    public TimeSpan TrimStart { get; set; } = TimeSpan.Zero;
    public TimeSpan TrimEnd { get; set; } = TimeSpan.Zero;

    // -- Hardware acceleration --
    public bool UseHardwareAcceleration { get; set; } = false;
    public string HwAccelType { get; set; } = "auto";

    // -- Metadata overlay --
    public bool DrawTimestamp { get; set; } = false;
    public bool DrawFrameNumber { get; set; } = false;

    // -- Performance --
    public int ThreadCount { get; set; } = 0;  // 0 = auto

    public int EffectiveQuality => UseQualityPreset ? (int)QualityPreset : CustomQuality;

    private string BuildScaleFilter()
    {
        if (!EnableResize) return string.Empty;
        if (MaintainAspectRatio)
        {
            if (ResizeWidth > 0 && ResizeHeight == 0)
                return $"scale={ResizeWidth}:-2";
            if (ResizeHeight > 0 && ResizeWidth == 0)
                return $"scale=-2:{ResizeHeight}";
            if (ResizeWidth > 0 && ResizeHeight > 0)
                return $"scale={ResizeWidth}:{ResizeHeight}:force_original_aspect_ratio=decrease";
        }
        return $"scale={ResizeWidth}:{ResizeHeight}";
    }

    public ExtractionOptions Clone() => (ExtractionOptions)MemberwiseClone();
}

