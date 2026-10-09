using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FFmpegFrameExtractor.Services;

/// <summary>
/// Locates and validates FFmpeg/FFprobe executables.
/// Searches common install paths, PATH, and user-configured paths.
/// </summary>
public class FFmpegLocatorService
{
    private readonly ILogger<FFmpegLocatorService> _logger;

    public string? FFmpegPath { get; private set; }
    public string? FFprobePath { get; private set; }
    public bool IsAvailable => FFmpegPath != null && FFprobePath != null;

    // Common locations for FFmpeg on Windows
    private static readonly string[] SearchPaths =
    {
        @"C:\ffmpeg\bin",
        @"C:\Program Files\ffmpeg\bin",
        @"C:\Program Files (x86)\ffmpeg\bin",
        @"C:\tools\ffmpeg\bin",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"ffmpeg\bin"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"ffmpeg\bin"),
    };

    public FFmpegLocatorService(ILogger<FFmpegLocatorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Searches PATH and common directories for FFmpeg executables.
    /// Returns true if both ffmpeg and ffprobe are found.
    /// </summary>
    public async Task<bool> LocateAsync(string? customFFmpegPath = null, string? customFFprobePath = null,
        CancellationToken ct = default)
    {
        FFmpegPath = null;
        FFprobePath = null;

        // 1. User-specified paths take priority
        if (!string.IsNullOrWhiteSpace(customFFmpegPath) && File.Exists(customFFmpegPath))
        {
            FFmpegPath = customFFmpegPath;
            // Also check brother directory for ffprobe
            if (string.IsNullOrWhiteSpace(customFFprobePath))
            {
                var dir = Path.GetDirectoryName(customFFmpegPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    var siblingProbe = Path.Combine(dir, "ffprobe.exe");
                    if (File.Exists(siblingProbe))
                        FFprobePath = siblingProbe;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(customFFprobePath) && File.Exists(customFFprobePath))
        {
            FFprobePath = customFFprobePath;
            // Also check brother directory for ffmpeg
            if (FFmpegPath == null)
            {
                var dir = Path.GetDirectoryName(customFFprobePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    var siblingFfmpeg = Path.Combine(dir, "ffmpeg.exe");
                    if (File.Exists(siblingFfmpeg))
                        FFmpegPath = siblingFfmpeg;
                }
            }
        }

        // 2. Search common directories
        if (FFmpegPath == null || FFprobePath == null)
        {
            foreach (var dir in SearchPaths)
            {
                if (!Directory.Exists(dir)) continue;

                var ffmpegExe = Path.Combine(dir, "ffmpeg.exe");
                var ffprobeExe = Path.Combine(dir, "ffprobe.exe");

                if (FFmpegPath == null && File.Exists(ffmpegExe))
                    FFmpegPath = ffmpegExe;
                if (FFprobePath == null && File.Exists(ffprobeExe))
                    FFprobePath = ffprobeExe;

                if (FFmpegPath != null && FFprobePath != null) break;
            }
        }

        // 3. Try PATH (where.exe)
        if (FFmpegPath == null)
            FFmpegPath = await WhereAsync("ffmpeg.exe", ct);
        if (FFprobePath == null)
            FFprobePath = await WhereAsync("ffprobe.exe", ct);

        if (IsAvailable)
            _logger.LogInformation("FFmpeg found: {F} | FFprobe found: {P}", FFmpegPath, FFprobePath);
        else
            _logger.LogWarning("FFmpeg or FFprobe not found. FFmpeg='{F}', FFprobe='{P}'", FFmpegPath, FFprobePath);

        return IsAvailable;
    }

    public async Task<bool> ValidatePathsAsync(string ffmpegPath, string ffprobePath, CancellationToken ct = default)
    {
        bool ffmpegOk = await CanExecuteAsync(ffmpegPath, ct);
        bool ffprobeOk = await CanExecuteAsync(ffprobePath, ct);
        return ffmpegOk && ffprobeOk;
    }

    private async Task<string?> WhereAsync(string exe, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = exe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            var line = await p.StandardOutput.ReadLineAsync(ct);
            await p.WaitForExitAsync(ct);
            return p.ExitCode == 0 && !string.IsNullOrWhiteSpace(line) ? line.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> CanExecuteAsync(string exe, CancellationToken ct)
    {
        if (!File.Exists(exe)) return false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi)!;
            await p.WaitForExitAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
