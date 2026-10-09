using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FFmpegFrameExtractor.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FFmpegFrameExtractor.Services;

/// <summary>
/// Wraps FFprobe to extract detailed video metadata.
/// </summary>
public class FFprobeService
{
    private readonly ILogger<FFprobeService> _logger;
    private string _ffprobePath = "ffprobe";

    public FFprobeService(ILogger<FFprobeService> logger)
    {
        _logger = logger;
    }

    public void SetExecutablePath(string path)
    {
        _ffprobePath = path;
        _logger.LogDebug("FFprobe path set to: {Path}", path);
    }

    /// <summary>
    /// Probes a video file and returns structured VideoInfo.
    /// </summary>
    public async Task<VideoInfo> ProbeAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Video file not found.", filePath);

        var args = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"";

        _logger.LogDebug("Running FFprobe: {Args}", args);

        var output = await RunProcessAsync(_ffprobePath, args, ct);
        return ParseProbeOutput(output, filePath);
    }

    /// <summary>
    /// Verifies the FFprobe executable is available and returns its version string.
    /// </summary>
    public async Task<string?> GetVersionAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await RunProcessAsync(_ffprobePath, "-version", ct);
            var match = Regex.Match(output, @"ffprobe version (\S+)");
            return match.Success ? match.Groups[1].Value : output.Split('\n')[0];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not get FFprobe version");
            return null;
        }
    }

    private VideoInfo ParseProbeOutput(string json, string filePath)
    {
        var info = new VideoInfo { FilePath = filePath };

        try
        {
            var fileInfo = new FileInfo(filePath);
            info.FileSizeBytes = fileInfo.Length;

            var root = JObject.Parse(json);

            // --- Format ---
            var format = root["format"];
            if (format != null)
            {
                info.Format = format["format_long_name"]?.ToString() ?? format["format_name"]?.ToString() ?? string.Empty;
                if (double.TryParse(format["duration"]?.ToString(), out double dur))
                    info.Duration = TimeSpan.FromSeconds(dur);
                if (long.TryParse(format["bit_rate"]?.ToString(), out long br))
                    info.BitRate = br;
            }

            // --- Streams ---
            var streams = root["streams"] as JArray;
            if (streams == null) return info;

            foreach (var stream in streams)
            {
                var codecType = stream["codec_type"]?.ToString();

                if (codecType == "video" && string.IsNullOrEmpty(info.VideoCodec))
                {
                    info.VideoCodec = stream["codec_name"]?.ToString() ?? string.Empty;
                    info.Profile = stream["profile"]?.ToString() ?? string.Empty;
                    info.Width = stream["width"]?.Value<int>() ?? 0;
                    info.Height = stream["height"]?.Value<int>() ?? 0;
                    info.PixelFormat = stream["pix_fmt"]?.ToString() ?? string.Empty;
                    info.ColorSpace = stream["color_space"]?.ToString() ?? string.Empty;
                    info.BitDepth = ParseBitDepth(stream["bits_per_raw_sample"]?.ToString());

                    // Parse frame rate (stored as fraction like "30000/1001")
                    info.FrameRate = ParseFraction(stream["r_frame_rate"]?.ToString()
                        ?? stream["avg_frame_rate"]?.ToString());

                    if (long.TryParse(stream["nb_frames"]?.ToString(), out long frames))
                        info.TotalFrames = frames;
                    else if (info.Duration.TotalSeconds > 0 && info.FrameRate > 0)
                        info.TotalFrames = (long)(info.Duration.TotalSeconds * info.FrameRate);

                    // Rotation tag
                    var tags = stream["tags"];
                    info.Rotation = tags?["rotate"]?.ToString();
                }
                else if (codecType == "audio" && !info.HasAudio)
                {
                    info.HasAudio = true;
                    info.AudioCodec = stream["codec_name"]?.ToString() ?? string.Empty;
                    info.AudioChannels = stream["channels"]?.Value<int>() ?? 0;
                    if (int.TryParse(stream["sample_rate"]?.ToString(), out int sr))
                        info.AudioSampleRate = sr;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse FFprobe JSON output");
        }

        return info;
    }

    private static double ParseFraction(string? fraction)
    {
        if (string.IsNullOrWhiteSpace(fraction)) return 0;
        var parts = fraction.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out double num) &&
            double.TryParse(parts[1], out double den) &&
            den != 0)
            return num / den;
        if (double.TryParse(fraction, out double val)) return val;
        return 0;
    }

    private static int ParseBitDepth(string? value)
    {
        if (int.TryParse(value, out int depth)) return depth;
        return 8;
    }

    private async Task<string> RunProcessAsync(string executable, string arguments, CancellationToken ct)
    {
        if (!File.Exists(executable) && !string.Equals(executable, "ffprobe", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException($"FFprobe executable not found at: '{executable}'", executable);

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi };
        var sb = new StringBuilder();
        var errSb = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errSb.AppendLine(e.Data); };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start or run FFprobe executable '{Exe}'", executable);
            throw;
        }

        return sb.ToString();
    }
}
