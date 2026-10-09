using System;
using System.IO;
using System.Threading.Tasks;
using FFmpegFrameExtractor.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FFmpegFrameExtractor.Services;

/// <summary>
/// Persists application settings to a JSON file in AppData.
/// </summary>
public class SettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;
    private AppSettings _current = new();

    public AppSettings Current => _current;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "FFmpegFrameExtractor");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _current = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                _logger.LogInformation("Settings loaded from {Path}", _settingsPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
            _current = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_current, Formatting.Indented);
            File.WriteAllText(_settingsPath, json);
            _logger.LogDebug("Settings saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_current, Formatting.Indented);
            await File.WriteAllTextAsync(_settingsPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings async");
        }
    }
}
