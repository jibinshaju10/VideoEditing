using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FreebeeVideo.Services;

namespace FreebeeVideo;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> _joinFiles = new();
    private static readonly string[] VideoFilter =
        ["Video files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.m4v;*.ts;*.mpeg;*.mpg",
         "All files|*.*"];

    public MainWindow()
    {
        InitializeComponent();
        JoinFilesList.ItemsSource = _joinFiles;
    }

    // ─────────────────────────────────────────────────────────────
    //  TRIM tab
    // ─────────────────────────────────────────────────────────────

    private void TrimBrowseInput_Click(object sender, RoutedEventArgs e)
    {
        var path = PickOpenFile("Select input video");
        if (path is null) return;

        TrimInputPath.Text = path;
        TrimInputPath.Foreground = (System.Windows.Media.Brush)FindResource("TextPrimary");
        UpdateTrimButton();
    }

    private void TrimBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = PickSaveFile("Save trimmed video as", GetExtension(TrimInputPath.Text));
        if (path is null) return;

        TrimOutputPath.Text = path;
        TrimOutputPath.Foreground = (System.Windows.Media.Brush)FindResource("TextPrimary");
        UpdateTrimButton();
    }

    private void TrimTime_TextChanged(object sender, TextChangedEventArgs e) =>
        UpdateTrimButton();

    private void UpdateTrimButton()
    {
        bool hasInput  = File.Exists(TrimInputPath.Text);
        bool hasOutput = !string.IsNullOrWhiteSpace(TrimOutputPath.Text) &&
                         TrimOutputPath.Text != "No file selected…";
        bool startOk   = TimeSpan.TryParse(TrimStart.Text, out _);
        bool endOk     = TimeSpan.TryParse(TrimEnd.Text, out _);
        TrimButton.IsEnabled = hasInput && hasOutput && startOk && endOk;
    }

    private async void TrimButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeSpan.TryParse(TrimStart.Text, out var start) ||
            !TimeSpan.TryParse(TrimEnd.Text,   out var end))
        {
            ShowError("Invalid time format. Use hh:mm:ss.");
            return;
        }

        if (end <= start)
        {
            ShowError("End time must be greater than start time.");
            return;
        }

        SetTrimBusy(true);
        SetTrimStatus("Trimming…", isError: false);

        try
        {
            await VideoService.TrimAsync(
                TrimInputPath.Text,
                TrimOutputPath.Text,
                start, end,
                progress => Dispatcher.Invoke(() =>
                {
                    TrimProgress.IsIndeterminate = false;
                    TrimProgress.Value = progress;
                }));

            SetTrimStatus($"Done!  Saved to: {TrimOutputPath.Text}", isError: false);
            FooterStatus.Text = "Trim complete.";
        }
        catch (Exception ex)
        {
            SetTrimStatus($"Error: {ex.Message}", isError: true);
            FooterStatus.Text = "Trim failed.";
        }
        finally
        {
            SetTrimBusy(false);
        }
    }

    private void SetTrimBusy(bool busy)
    {
        TrimButton.IsEnabled    = !busy;
        TrimProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        TrimProgress.IsIndeterminate = busy;
        if (busy) TrimProgress.Value = 0;
    }

    private void SetTrimStatus(string message, bool isError)
    {
        TrimStatus.Visibility = Visibility.Visible;
        TrimStatus.Text = message;
        TrimStatus.Foreground = isError
            ? (System.Windows.Media.Brush)FindResource("DangerBrush")
            : (System.Windows.Media.Brush)FindResource("SuccessBrush");
    }

    // ─────────────────────────────────────────────────────────────
    //  JOIN tab
    // ─────────────────────────────────────────────────────────────

    private void JoinAdd_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Add video files",
            Filter = string.Join("|", VideoFilter),
            Multiselect = true
        };
        if (ofd.ShowDialog() != true) return;

        foreach (var f in ofd.FileNames)
            if (!_joinFiles.Contains(f))
                _joinFiles.Add(f);

        UpdateJoinButton();
    }

    private void JoinRemove_Click(object sender, RoutedEventArgs e)
    {
        var selected = JoinFilesList.SelectedItem as string;
        if (selected != null) _joinFiles.Remove(selected);
        UpdateJoinButton();
    }

    private void JoinMoveUp_Click(object sender, RoutedEventArgs e)
    {
        int idx = JoinFilesList.SelectedIndex;
        if (idx <= 0) return;
        _joinFiles.Move(idx, idx - 1);
        JoinFilesList.SelectedIndex = idx - 1;
    }

    private void JoinMoveDown_Click(object sender, RoutedEventArgs e)
    {
        int idx = JoinFilesList.SelectedIndex;
        if (idx < 0 || idx >= _joinFiles.Count - 1) return;
        _joinFiles.Move(idx, idx + 1);
        JoinFilesList.SelectedIndex = idx + 1;
    }

    private void JoinFilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool sel = JoinFilesList.SelectedIndex >= 0;
        JoinRemoveButton.IsEnabled  = sel;
        JoinUpButton.IsEnabled      = sel && JoinFilesList.SelectedIndex > 0;
        JoinDownButton.IsEnabled    = sel && JoinFilesList.SelectedIndex < _joinFiles.Count - 1;
    }

    private void JoinBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var ext = _joinFiles.Count > 0 ? GetExtension(_joinFiles[0]) : ".mp4";
        var path = PickSaveFile("Save joined video as", ext);
        if (path is null) return;

        JoinOutputPath.Text = path;
        JoinOutputPath.Foreground = (System.Windows.Media.Brush)FindResource("TextPrimary");
        UpdateJoinButton();
    }

    private void UpdateJoinButton()
    {
        bool hasFiles  = _joinFiles.Count >= 2;
        bool hasOutput = !string.IsNullOrWhiteSpace(JoinOutputPath.Text) &&
                         JoinOutputPath.Text != "No file selected…";
        JoinButton.IsEnabled = hasFiles && hasOutput;
    }

    private async void JoinButton_Click(object sender, RoutedEventArgs e)
    {
        SetJoinBusy(true);
        SetJoinStatus("Joining…", isError: false);

        try
        {
            await VideoService.JoinAsync(
                [.. _joinFiles],
                JoinOutputPath.Text,
                progress => Dispatcher.Invoke(() =>
                {
                    JoinProgress.IsIndeterminate = false;
                    JoinProgress.Value = progress;
                }));

            SetJoinStatus($"Done!  Saved to: {JoinOutputPath.Text}", isError: false);
            FooterStatus.Text = "Join complete.";
        }
        catch (Exception ex)
        {
            SetJoinStatus($"Error: {ex.Message}", isError: true);
            FooterStatus.Text = "Join failed.";
        }
        finally
        {
            SetJoinBusy(false);
        }
    }

    private void SetJoinBusy(bool busy)
    {
        JoinButton.IsEnabled    = !busy;
        JoinProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        JoinProgress.IsIndeterminate = busy;
        if (busy) JoinProgress.Value = 0;
    }

    private void SetJoinStatus(string message, bool isError)
    {
        JoinStatus.Visibility = Visibility.Visible;
        JoinStatus.Text = message;
        JoinStatus.Foreground = isError
            ? (System.Windows.Media.Brush)FindResource("DangerBrush")
            : (System.Windows.Media.Brush)FindResource("SuccessBrush");
    }

    // ─────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────

    private string? PickOpenFile(string title)
    {
        var ofd = new OpenFileDialog
        {
            Title  = title,
            Filter = string.Join("|", VideoFilter)
        };
        return ofd.ShowDialog() == true ? ofd.FileName : null;
    }

    private static string? PickSaveFile(string title, string extension)
    {
        extension = extension.TrimStart('.');
        var sfd = new SaveFileDialog
        {
            Title            = title,
            DefaultExt       = extension,
            Filter           = $"{extension.ToUpperInvariant()} file|*.{extension}|All files|*.*",
            AddExtension     = true
        };
        return sfd.ShowDialog() == true ? sfd.FileName : null;
    }

    private static string GetExtension(string path)
    {
        try { return Path.GetExtension(path).ToLowerInvariant(); }
        catch { return ".mp4"; }
    }

    private static void ShowError(string message) =>
        MessageBox.Show(message, "FreebeeVideo", MessageBoxButton.OK, MessageBoxImage.Warning);
}