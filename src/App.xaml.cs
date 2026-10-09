using System.IO;
using FFmpegFrameExtractor.Services;

using FFmpegFrameExtractor.ViewModels;
using FFmpegFrameExtractor.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FFmpegFrameExtractor;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FFmpegFrameExtractor");
        Directory.CreateDirectory(appData);
        var crashLogPath = Path.Combine(appData, "crash.log");

        void LogCrash(string source, System.Exception? ex, object? exObj = null)
        {
            var msg = $"[{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]\r\n{ex?.ToString() ?? exObj?.ToString()}\r\n\r\n";
            try { File.AppendAllText(crashLogPath, msg); } catch { }
        }

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            LogCrash("AppDomain.UnhandledException", args.ExceptionObject as System.Exception, args.ExceptionObject);
            System.Windows.MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.ExceptionObject}\n\nCrash log saved to:\n{crashLogPath}",
                "FFmpeg Frame Extractor - Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            LogCrash("DispatcherUnhandledException", args.Exception);
            System.Windows.MessageBox.Show(
                $"An error occurred:\n\n{args.Exception.Message}\n\nDetails:\n{args.Exception}\n\nLog saved to:\n{crashLogPath}",
                "FFmpeg Frame Extractor - Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            args.Handled = true;
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogCrash("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            this.MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (System.Exception ex)
        {
            LogCrash("StartupException", ex);
            System.Windows.MessageBox.Show($"Startup error: {ex.Message}\n\n{ex.StackTrace}", "FFmpeg Frame Extractor - Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            throw;
        }
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Logging
        services.AddLogging(b =>
        {
            b.SetMinimumLevel(LogLevel.Debug);
            b.AddDebug();
        });

        // Services
        services.AddSingleton<FFmpegLocatorService>();
        services.AddSingleton<FFmpegService>();
        services.AddSingleton<FFprobeService>();
        services.AddSingleton<SettingsService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _serviceProvider?.GetService<SettingsService>()?.Save();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
