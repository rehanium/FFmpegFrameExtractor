using System.IO;
using System.Windows.Input;

namespace FFmpegFrameExtractor.Views;

public partial class MainWindow : System.Windows.Window
{
    private readonly ViewModels.MainViewModel _viewModel;

    public MainWindow(ViewModels.MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    // ── Drag & Drop ──────────────────────────────────────────────────────────

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            bool isVideo = files?.Length == 1 && IsVideoFile(files[0]);
            e.Effects = isVideo ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files?.Length == 1 && IsVideoFile(files[0]))
                _ = _viewModel.LoadDroppedFileCommand.ExecuteAsync(files[0]);
        }
    }

    private void DropZone_Click(object sender, MouseButtonEventArgs e)
    {
        _ = _viewModel.BrowseInputFileCommand.ExecuteAsync(null);
    }

    // ── Results grid double-click ─────────────────────────────────────────

    private void ResultsGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedFrame?.FilePath is { } path && File.Exists(path))
            _viewModel.OpenSelectedFrameCommand.Execute(null);
    }

    // ── Window closing ────────────────────────────────────────────────────

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.CancelExtractionCommand.Execute(null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
        ".m4v", ".ts", ".mts", ".m2ts", ".vob", ".3gp", ".rmvb",
        ".asf", ".ogv", ".mpg", ".mpeg", ".divx", ".xvid"
    };

    private static bool IsVideoFile(string path) =>
        VideoExtensions.Contains(Path.GetExtension(path));
}
