using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FFmpegFrameExtractor.Models;
using Microsoft.Extensions.Logging;

namespace FFmpegFrameExtractor.Services;

/// <summary>
/// Core service that builds FFmpeg argument strings and runs frame extraction jobs.
/// </summary>
public class FFmpegService
{
    private readonly ILogger<FFmpegService> _logger;
    private string _ffmpegPath = "ffmpeg";

    // Matches FFmpeg progress lines: frame=  42 fps= 30 ...
    private static readonly Regex FrameProgressRegex =
        new(@"frame=\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex FpsProgressRegex =
        new(@"fps=\s*([\d.]+)", RegexOptions.Compiled);
    private static readonly Regex TimeProgressRegex =
        new(@"time=(\d{2}:\d{2}:\d{2}\.\d+)", RegexOptions.Compiled);

    public FFmpegService(ILogger<FFmpegService> logger)
    {
        _logger = logger;
    }

    public void SetExecutablePath(string path)
    {
        _ffmpegPath = path;
        _logger.LogDebug("FFmpeg path set to: {Path}", path);
    }

    /// <summary>
    /// Returns the FFmpeg version string, or null if the executable is not found.
    /// </summary>
    public async Task<string?> GetVersionAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await RunCaptureAsync(_ffmpegPath, "-version", ct);
            var match = Regex.Match(output, @"ffmpeg version (\S+)");
            return match.Success ? match.Groups[1].Value : output.Split('\n')[0];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not get FFmpeg version");
            return null;
        }
    }

    /// <summary>
    /// Runs a frame extraction job, reporting progress via IProgress.
    /// </summary>
    public async Task<ExtractionResult> ExtractFramesAsync(
        ExtractionOptions options,
        VideoInfo? videoInfo,
        IProgress<ExtractionProgress>? progress,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ExtractionResult();

        Directory.CreateDirectory(options.OutputDirectory);

        // Build estimated total frame count for progress
        int estimatedTotal = EstimateTotalFrames(options, videoInfo);

        try
        {
            if (options.Mode == ExtractionMode.SpecificTimestamps)
            {
                // Each timestamp is a separate FFmpeg call
                result = await ExtractSpecificTimestampsAsync(options, progress, ct);
            }
            else if (options.Mode == ExtractionMode.EvenlyDistributed)
            {
                result = await ExtractEvenlyDistributedAsync(options, videoInfo, progress, ct);
            }
            else
            {
                // Single FFmpeg invocation
                var args = BuildArguments(options, videoInfo);
                _logger.LogInformation("FFmpeg args: {Args}", args);

                int frameCount = 0;
                var startTime = DateTime.Now;

                await RunWithProgressAsync(_ffmpegPath, args, (frame, fps, elapsed) =>
                {
                    frameCount = Math.Max(frameCount, frame);
                    var prog = new ExtractionProgress
                    {
                        CurrentFrame = frame,
                        TotalFrames = estimatedTotal,
                        Elapsed = elapsed,
                        ProcessingFps = fps,
                        StatusMessage = $"Extracting frame {frame}...",
                    };
                    if (estimatedTotal > 0 && fps > 0)
                    {
                        double remaining = (estimatedTotal - frame) / fps;
                        prog.EstimatedRemaining = TimeSpan.FromSeconds(remaining);
                    }
                    progress?.Report(prog);
                }, ct);

                result = await BuildResultFromOutputDirectory(options, stopwatch.Elapsed);
            }

            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Operation was cancelled.";
            _logger.LogInformation("Extraction cancelled by user");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Extraction failed");
        }

        stopwatch.Stop();
        result.ElapsedTime = stopwatch.Elapsed;
        return result;
    }

    // -----------------------------------------------------------------------
    // Argument builders
    // -----------------------------------------------------------------------

    public string BuildArguments(ExtractionOptions options, VideoInfo? videoInfo)
    {
        var sb = new StringBuilder();

        // --- Hardware acceleration ---
        if (options.UseHardwareAcceleration)
            sb.Append($"-hwaccel {options.HwAccelType} ");

        // --- Thread count ---
        if (options.ThreadCount > 0)
            sb.Append($"-threads {options.ThreadCount} ");

        // --- Input trim (seek before input for speed) ---
        if (options.EnableTrim && options.TrimStart > TimeSpan.Zero)
            sb.Append($"-ss {options.TrimStart.TotalSeconds:F3} ");

        // --- Input ---
        sb.Append($"-i \"{options.InputFilePath}\" ");

        // --- Post-input trim end ---
        if (options.EnableTrim && options.TrimEnd > options.TrimStart)
        {
            var duration = options.TrimEnd - options.TrimStart;
            sb.Append($"-t {duration.TotalSeconds:F3} ");
        }

        // --- Video filter chain ---
        var filters = BuildFilterChain(options);
        if (!string.IsNullOrEmpty(filters))
            sb.Append($"-vf \"{filters}\" ");

        // --- Extraction-mode-specific args ---
        AppendModeArgs(sb, options, videoInfo);

        // --- Output format ---
        AppendFormatArgs(sb, options);

        // --- Output pattern ---
        string ext = options.Format.ToString().ToLower();
        string pattern = Path.Combine(options.OutputDirectory,
            $"{options.FileNamePattern}.{ext}");
        sb.Append($"\"{pattern}\"");

        // --- Overwrite ---
        sb.Append(" -y");

        return sb.ToString();
    }

    private void AppendModeArgs(StringBuilder sb, ExtractionOptions options, VideoInfo? videoInfo)
    {
        switch (options.Mode)
        {
            case ExtractionMode.EveryNSeconds:
                sb.Append($"-vf \"fps=1/{options.IntervalSeconds}\" "
                    .Replace("-vf \"fps=", "-vf \"").Replace("\"", ""));
                // Note: fps filter is inserted in BuildFilterChain for EveryNSeconds
                break;

            case ExtractionMode.FrameRate:
                // handled by filter chain
                break;

            case ExtractionMode.TimeRange:
                // Trim already applied via -ss/-t above; no extra needed
                break;

            case ExtractionMode.EveryNthFrame:
                // select filter is included in filter chain
                break;

            case ExtractionMode.SingleFrame:
                sb.Append($"-ss {options.SingleFrameTimestamp.TotalSeconds:F3} -frames:v 1 ");
                break;

            case ExtractionMode.SceneChange:
                // scene filter in filter chain
                break;
        }
    }

    private void AppendFormatArgs(StringBuilder sb, ExtractionOptions options)
    {
        switch (options.Format)
        {
            case OutputFormat.Jpeg:
                sb.Append($"-q:v {JpegQualityToQscale(options.EffectiveQuality)} ");
                break;

            case OutputFormat.Png:
                sb.Append($"-compression_level {options.PngCompression} ");
                break;

            case OutputFormat.Webp:
                sb.Append($"-quality {options.EffectiveQuality} ");
                break;

            case OutputFormat.Tiff:
            case OutputFormat.Bmp:
                // No special quality arg needed
                break;
        }
    }

    /// <summary>
    /// Converts a 1-100 quality value to FFmpeg's 2-31 q:v scale (lower = better).
    /// </summary>
    private static int JpegQualityToQscale(int quality)
    {
        // quality 100 -> qscale 2, quality 1 -> qscale 31
        int q = (int)Math.Round(2 + (100 - quality) * 29.0 / 99.0);
        return Math.Clamp(q, 2, 31);
    }

    private string BuildFilterChain(ExtractionOptions options)
    {
        var filters = new List<string>();

        // --- Deinterlace ---
        if (options.EnableDeinterlace)
            filters.Add("yadif=0:-1:0");

        // --- Denoise ---
        if (options.EnableDenoise)
            filters.Add("hqdn3d=3:3:6:6");

        // --- Frame selection filters ---
        switch (options.Mode)
        {
            case ExtractionMode.EveryNSeconds:
                filters.Add($"fps=1/{options.IntervalSeconds}");
                break;

            case ExtractionMode.FrameRate:
                filters.Add($"fps={options.ExtractFps}");
                break;

            case ExtractionMode.EveryNthFrame:
                filters.Add($"select='not(mod(n\\,{options.EveryNthFrameValue}))'");
                filters.Add("setpts=N/FRAME_RATE/TB");
                break;

            case ExtractionMode.SceneChange:
                double t = Math.Clamp(options.SceneChangeThreshold, 0.0, 1.0);
                filters.Add($"select='gt(scene\\,{t:F2})'");
                filters.Add("setpts=N/FRAME_RATE/TB");
                break;

            case ExtractionMode.TimeRange:
                // fps=1 within range
                filters.Add("fps=1");
                break;
        }

        // --- Color adjustments ---
        bool hasColorAdjust = options.Brightness != 0.0 ||
                              options.Contrast != 1.0 ||
                              options.Saturation != 1.0 ||
                              options.Gamma != 1.0;
        if (hasColorAdjust)
        {
            // eq filter: brightness, contrast, saturation, gamma
            filters.Add($"eq=brightness={options.Brightness:F2}:" +
                        $"contrast={options.Contrast:F2}:" +
                        $"saturation={options.Saturation:F2}:" +
                        $"gamma={options.Gamma:F2}");
        }

        // --- Grayscale ---
        if (options.EnableGrayscale)
            filters.Add("format=gray");

        // --- Sharpness ---
        if (options.EnableSharpness)
            filters.Add($"unsharp=5:5:{options.SharpnessAmount:F1}:5:5:0.0");

        // --- Scale ---
        if (options.EnableResize && !string.IsNullOrEmpty(options.ScaleFilter))
            filters.Add(options.ScaleFilter);

        // --- Timestamp overlay ---
        if (options.DrawTimestamp)
            filters.Add("drawtext=fontfile=/Windows/Fonts/arial.ttf:" +
                       "text='%{pts\\:hms}':x=10:y=10:fontsize=24:" +
                       "fontcolor=white:box=1:boxcolor=black@0.5");

        // --- Frame number overlay ---
        if (options.DrawFrameNumber)
            filters.Add("drawtext=fontfile=/Windows/Fonts/arial.ttf:" +
                       "text='Frame %{n}':x=10:y=40:fontsize=24:" +
                       "fontcolor=yellow:box=1:boxcolor=black@0.5");

        return string.Join(",", filters);
    }

    // -----------------------------------------------------------------------
    // Special extraction modes
    // -----------------------------------------------------------------------

    private async Task<ExtractionResult> ExtractSpecificTimestampsAsync(
        ExtractionOptions options,
        IProgress<ExtractionProgress>? progress,
        CancellationToken ct)
    {
        var result = new ExtractionResult();
        var timestamps = options.TimestampList.Split(new[] { ',', ';', '\n' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < timestamps.Length; i++)
        {
            ct.ThrowIfCancellationRequested();

            var ts = timestamps[i].Trim();
            if (!TryParseTimestamp(ts, out TimeSpan time)) continue;

            string ext = options.Format.ToString().ToLower();
            string outFile = Path.Combine(options.OutputDirectory,
                $"{options.FileNamePattern.Replace("%04d", $"{i + 1:D4}")}.{ext}");

            // Build single-frame args
            var singleOpts = CloneOptions(options);
            singleOpts.Mode = ExtractionMode.SingleFrame;
            singleOpts.SingleFrameTimestamp = time;

            var filters = BuildFilterChain(singleOpts);
            var filterArg = string.IsNullOrEmpty(filters) ? "" : $"-vf \"{filters}\" ";
            var qArg = BuildQualityArg(options);
            var args = $"-ss {time.TotalSeconds:F3} -i \"{options.InputFilePath}\" {filterArg}-frames:v 1 {qArg}\"{outFile}\" -y";

            _logger.LogDebug("Extracting frame at {Timestamp}: {Args}", ts, args);

            try
            {
                await RunCaptureAsync(_ffmpegPath, args, ct);
                var frame = BuildFrameInfo(outFile, i + 1, time);
                result.Frames.Add(frame);
            }
            catch (Exception ex)
            {
                result.Frames.Add(new ExtractedFrame
                {
                    Index = i + 1,
                    FilePath = outFile,
                    Timestamp = time,
                    Success = false,
                    ErrorMessage = ex.Message,
                });
                result.FailedFrames++;
            }

            progress?.Report(new ExtractionProgress
            {
                CurrentFrame = i + 1,
                TotalFrames = timestamps.Length,
                Elapsed = stopwatch.Elapsed,
                StatusMessage = $"Extracted frame {i + 1} of {timestamps.Length}",
            });
        }

        return result;
    }

    private async Task<ExtractionResult> ExtractEvenlyDistributedAsync(
        ExtractionOptions options,
        VideoInfo? videoInfo,
        IProgress<ExtractionProgress>? progress,
        CancellationToken ct)
    {
        var result = new ExtractionResult();
        double durationSec = videoInfo?.Duration.TotalSeconds ?? 0;

        if (durationSec <= 0)
        {
            result.ErrorMessage = "Cannot determine video duration for evenly distributed extraction.";
            return result;
        }

        int count = options.DistributedCount;
        var timestamps = new List<TimeSpan>();
        for (int i = 0; i < count; i++)
        {
            double t = (i + 0.5) * durationSec / count;
            timestamps.Add(TimeSpan.FromSeconds(t));
        }

        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < timestamps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var time = timestamps[i];
            string ext = options.Format.ToString().ToLower();
            string outFile = Path.Combine(options.OutputDirectory,
                $"{options.FileNamePattern.Replace("%04d", $"{i + 1:D4}")}.{ext}");

            var filters = BuildFilterChain(options);
            var filterArg = string.IsNullOrEmpty(filters) ? "" : $"-vf \"{filters}\" ";
            var qArg = BuildQualityArg(options);
            var args = $"-ss {time.TotalSeconds:F3} -i \"{options.InputFilePath}\" {filterArg}-frames:v 1 {qArg}\"{outFile}\" -y";

            try
            {
                await RunCaptureAsync(_ffmpegPath, args, ct);
                var frame = BuildFrameInfo(outFile, i + 1, time);
                result.Frames.Add(frame);
            }
            catch (Exception ex)
            {
                result.Frames.Add(new ExtractedFrame
                {
                    Index = i + 1,
                    FilePath = outFile,
                    Timestamp = time,
                    Success = false,
                    ErrorMessage = ex.Message,
                });
                result.FailedFrames++;
            }

            progress?.Report(new ExtractionProgress
            {
                CurrentFrame = i + 1,
                TotalFrames = count,
                Elapsed = stopwatch.Elapsed,
                StatusMessage = $"Extracted frame {i + 1} of {count} at {time:mm\\:ss\\.ff}",
            });
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Process management
    // -----------------------------------------------------------------------

    private async Task RunWithProgressAsync(
        string exe, string args,
        Action<int, double, TimeSpan> onProgress,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = false,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var errorLog = new StringBuilder();
        var startTime = DateTime.Now;

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            errorLog.AppendLine(e.Data);
            _logger.LogTrace("FFmpeg: {Line}", e.Data);

            var frameMatch = FrameProgressRegex.Match(e.Data);
            var fpsMatch = FpsProgressRegex.Match(e.Data);

            int frame = frameMatch.Success ? int.Parse(frameMatch.Groups[1].Value) : 0;
            double fps = fpsMatch.Success && double.TryParse(fpsMatch.Groups[1].Value, out double f) ? f : 0;

            if (frame > 0)
                onProgress(frame, fps, DateTime.Now - startTime);
        };

        process.Start();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            throw;
        }

        if (process.ExitCode != 0 && !ct.IsCancellationRequested)
        {
            var stderr = errorLog.ToString();
            _logger.LogError("FFmpeg exited with code {Code}. Stderr: {Err}", process.ExitCode, stderr);
            throw new InvalidOperationException(
                $"FFmpeg exited with code {process.ExitCode}. " +
                $"Error: {ExtractLastError(stderr)}");
        }
    }

    private async Task<string> RunCaptureAsync(string exe, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            throw;
        }

        if (process.ExitCode != 0 && !ct.IsCancellationRequested)
            throw new InvalidOperationException(
                $"Process exited with code {process.ExitCode}: {ExtractLastError(stderr.ToString())}");

        return stdout.ToString();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<ExtractionResult> BuildResultFromOutputDirectory(
        ExtractionOptions options, TimeSpan elapsed)
    {
        var result = new ExtractionResult { ElapsedTime = elapsed };
        string ext = options.Format.ToString().ToLower();

        if (!Directory.Exists(options.OutputDirectory))
            return result;

        var files = Directory.GetFiles(options.OutputDirectory, $"*.{ext}")
                             .OrderBy(f => f)
                             .ToArray();

        for (int i = 0; i < files.Length; i++)
        {
            var fi = new FileInfo(files[i]);
            result.Frames.Add(new ExtractedFrame
            {
                Index = i + 1,
                FilePath = files[i],
                FileSizeBytes = fi.Length,
                Success = true,
            });
            result.TotalOutputSizeBytes += fi.Length;
        }

        return result;
    }

    private static ExtractedFrame BuildFrameInfo(string filePath, int index, TimeSpan timestamp)
    {
        var fi = new FileInfo(filePath);
        return new ExtractedFrame
        {
            Index = index,
            FilePath = filePath,
            Timestamp = timestamp,
            FileSizeBytes = fi.Exists ? fi.Length : 0,
            Success = fi.Exists,
            ErrorMessage = fi.Exists ? null : "Output file not created",
        };
    }

    private static string BuildQualityArg(ExtractionOptions options) =>
        options.Format switch
        {
            OutputFormat.Jpeg => $"-q:v {JpegQualityToQscale2(options.EffectiveQuality)} ",
            OutputFormat.Webp => $"-quality {options.EffectiveQuality} ",
            OutputFormat.Png => $"-compression_level {options.PngCompression} ",
            _ => string.Empty,
        };

    private static int JpegQualityToQscale2(int quality)
    {
        int q = (int)Math.Round(2 + (100 - quality) * 29.0 / 99.0);
        return Math.Clamp(q, 2, 31);
    }

    private static int EstimateTotalFrames(ExtractionOptions options, VideoInfo? info)
    {
        if (info == null) return 0;
        return options.Mode switch
        {
            ExtractionMode.EveryNSeconds =>
                info.Duration.TotalSeconds > 0
                    ? (int)(info.Duration.TotalSeconds / options.IntervalSeconds)
                    : 0,
            ExtractionMode.FrameRate =>
                info.Duration.TotalSeconds > 0
                    ? (int)(info.Duration.TotalSeconds * options.ExtractFps)
                    : 0,
            ExtractionMode.EvenlyDistributed => options.DistributedCount,
            ExtractionMode.SingleFrame => 1,
            ExtractionMode.EveryNthFrame =>
                info.TotalFrames > 0
                    ? (int)(info.TotalFrames / options.EveryNthFrameValue)
                    : 0,
            _ => 0,
        };
    }

    private static ExtractionOptions CloneOptions(ExtractionOptions src)
    {
        return src.Clone();
    }

    private static bool TryParseTimestamp(string s, out TimeSpan result)
    {
        if (TimeSpan.TryParseExact(s, new[] { @"h\:mm\:ss\.fff", @"mm\:ss\.fff",
                                               @"h\:mm\:ss", @"mm\:ss" },
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

    private static string ExtractLastError(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr)) return "Unknown error";
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Find last line that contains "Error" or return last non-empty line
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                lines[i].Contains("Invalid", StringComparison.OrdinalIgnoreCase) ||
                lines[i].Contains("No such", StringComparison.OrdinalIgnoreCase))
                return lines[i].Trim();
        }
        return lines[^1].Trim();
    }
}
