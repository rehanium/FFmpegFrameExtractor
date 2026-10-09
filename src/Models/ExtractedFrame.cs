using System;

namespace FFmpegFrameExtractor.Models;

/// <summary>
/// Represents a single extracted frame result.
/// </summary>
public class ExtractedFrame
{
    public int Index { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public TimeSpan Timestamp { get; set; }
    public long FileSizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public string TimestampDisplay => Timestamp.TotalHours >= 1
        ? Timestamp.ToString(@"h\:mm\:ss\.fff")
        : Timestamp.ToString(@"mm\:ss\.fff");

    public string FileSizeDisplay => FileSizeBytes >= 1_048_576
        ? $"{FileSizeBytes / 1_048_576.0:F1} MB"
        : $"{FileSizeBytes / 1024.0:F1} KB";

    public string ResolutionDisplay => Width > 0 ? $"{Width}×{Height}" : string.Empty;
}
