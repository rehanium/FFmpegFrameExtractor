using System;
using System.Collections.Generic;

namespace FFmpegFrameExtractor.Models;

/// <summary>
/// Represents the result of an extraction job.
/// </summary>
public class ExtractionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ExtractedFrame> Frames { get; set; } = new();
    public int TotalFramesExtracted => Frames.Count;
    public int FailedFrames { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public long TotalOutputSizeBytes { get; set; }

    public string ElapsedDisplay => ElapsedTime.TotalHours >= 1
        ? ElapsedTime.ToString(@"h\:mm\:ss\.f")
        : ElapsedTime.ToString(@"mm\:ss\.f");
}

/// <summary>
/// Progress report emitted during extraction.
/// </summary>
public class ExtractionProgress
{
    public int CurrentFrame { get; set; }
    public int TotalFrames { get; set; }
    public double Percentage => TotalFrames > 0 ? (double)CurrentFrame / TotalFrames * 100 : 0;
    public TimeSpan Elapsed { get; set; }
    public TimeSpan? EstimatedRemaining { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string? CurrentOutputFile { get; set; }
    public double ProcessingFps { get; set; }
}

/// <summary>
/// Represents a named FFmpeg preset that can be saved and reloaded.
/// </summary>
public class ExtractionPreset
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public ExtractionOptions Options { get; set; } = new();
}

/// <summary>
/// Application settings persisted between sessions.
/// </summary>
public class AppSettings
{
    public string LastInputDirectory { get; set; } = string.Empty;
    public string LastOutputDirectory { get; set; } = string.Empty;
    public string FFmpegPath { get; set; } = string.Empty;
    public string FFprobePath { get; set; } = string.Empty;
    public bool DarkMode { get; set; } = true;
    public bool AutoOpenOutputFolder { get; set; } = false;
    public bool RememberLastOptions { get; set; } = true;
    public int MaxConcurrentJobs { get; set; } = 1;
    public bool ShowNotifications { get; set; } = true;
    public List<ExtractionPreset> SavedPresets { get; set; } = new();
    public ExtractionOptions? LastOptions { get; set; }
    public WindowState MainWindowState { get; set; } = new();
}

public class WindowState
{
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 800;
    public bool IsMaximized { get; set; } = false;
}
