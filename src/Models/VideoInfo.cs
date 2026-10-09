using System;
using System.Collections.Generic;

namespace FFmpegFrameExtractor.Models;

/// <summary>
/// Represents metadata extracted from a video file via FFprobe.
/// </summary>
public class VideoInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public string Format { get; set; } = string.Empty;
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double FrameRate { get; set; }
    public TimeSpan Duration { get; set; }
    public long FileSizeBytes { get; set; }
    public long BitRate { get; set; }
    public string PixelFormat { get; set; } = string.Empty;
    public double AspectRatio => Height > 0 ? (double)Width / Height : 0;
    public long TotalFrames { get; set; }
    public string ColorSpace { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public int BitDepth { get; set; }
    public bool HasAudio { get; set; }
    public int AudioChannels { get; set; }
    public int AudioSampleRate { get; set; }
    public string? Rotation { get; set; }

    public string ResolutionDisplay => $"{Width}×{Height}";
    public string FrameRateDisplay => FrameRate > 0 ? $"{FrameRate:F3} fps" : "Unknown";
    public string DurationDisplay => Duration.TotalHours >= 1
        ? Duration.ToString(@"h\:mm\:ss\.fff")
        : Duration.ToString(@"mm\:ss\.fff");
    public string FileSizeDisplay => FormatFileSize(FileSizeBytes);
    public string BitRateDisplay => BitRate > 0 ? $"{BitRate / 1000} kb/s" : "Unknown";

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F2} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F2} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F2} KB";
        return $"{bytes} B";
    }
}
