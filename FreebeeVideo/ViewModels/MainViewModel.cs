using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FreebeeVideo.Models;
using FreebeeVideo.Services;
using Microsoft.Win32;

namespace FreebeeVideo.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly FFmpegService _ffmpegService;
    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        _ffmpegService = new FFmpegService();
    }

    // ── Clip list ──────────────────────────────────────────────────────────────

    public ObservableCollection<VideoClip> Clips { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedClip))]
    [NotifyCanExecuteChangedFor(nameof(RemoveClipCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetTrimStartCommand))]
    [NotifyCanExecuteChangedFor(nameof(SetTrimEndCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearTrimCommand))]
    private VideoClip? _selectedClip;

    public bool HasSelectedClip => SelectedClip != null;

    // ── Player state ───────────────────────────────────────────────────────────

    [ObservableProperty]
    private Uri? _mediaSource;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private TimeSpan _mediaDuration;

    [ObservableProperty]
    private TimeSpan _mediaPosition;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _volume = 0.75;

    // ── Export / progress ──────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelExportCommand))]
    private bool _isExporting;

    [ObservableProperty]
    private double _exportProgress;

    [ObservableProperty]
    private string _statusMessage = "Ready. Add videos to get started.";

    // ── Commands: clip list ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddClipAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Add Video File(s)",
            Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.m4v;*.ts;*.mts|All Files|*.*",
            Multiselect = true
        };

        if (dlg.ShowDialog() != true) return;

        foreach (string path in dlg.FileNames)
        {
            var clip = new VideoClip
            {
                FilePath = path,
                FileName = Path.GetFileName(path)
            };

            try
            {
                clip.Duration = await _ffmpegService.GetDurationAsync(path);
                clip.EndTime = clip.Duration;
            }
            catch
            {
                clip.Duration = TimeSpan.Zero;
                clip.EndTime = TimeSpan.Zero;
            }

            Clips.Add(clip);
        }

        StatusMessage = $"{Clips.Count} clip(s) in queue.";
        ExportCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedClip))]
    private void RemoveClip()
    {
        if (SelectedClip == null) return;
        int idx = Clips.IndexOf(SelectedClip);
        Clips.Remove(SelectedClip);
        SelectedClip = Clips.Count > 0 ? Clips[Math.Min(idx, Clips.Count - 1)] : null;
        ExportCommand.NotifyCanExecuteChanged();
        StatusMessage = $"{Clips.Count} clip(s) in queue.";
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedClip == null) return;
        int idx = Clips.IndexOf(SelectedClip);
        if (idx <= 0) return;
        Clips.Move(idx, idx - 1);
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveUp() => SelectedClip != null && Clips.IndexOf(SelectedClip) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedClip == null) return;
        int idx = Clips.IndexOf(SelectedClip);
        if (idx < 0 || idx >= Clips.Count - 1) return;
        Clips.Move(idx, idx + 1);
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveDown() => SelectedClip != null && Clips.IndexOf(SelectedClip) < Clips.Count - 1;

    // ── Commands: trim ─────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelectedClip))]
    private void SetTrimStart()
    {
        if (SelectedClip == null) return;
        SelectedClip.StartTime = MediaPosition;
        SelectedClip.IsTrimmed = SelectedClip.StartTime > TimeSpan.Zero
                                 || SelectedClip.EndTime < SelectedClip.Duration;
        StatusMessage = $"Trim start set to {SelectedClip.StartTime:hh\\:mm\\:ss\\.fff}";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedClip))]
    private void SetTrimEnd()
    {
        if (SelectedClip == null) return;
        SelectedClip.EndTime = MediaPosition;
        SelectedClip.IsTrimmed = SelectedClip.StartTime > TimeSpan.Zero
                                 || SelectedClip.EndTime < SelectedClip.Duration;
        StatusMessage = $"Trim end set to {SelectedClip.EndTime:hh\\:mm\\:ss\\.fff}";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedClip))]
    private void ClearTrim()
    {
        if (SelectedClip == null) return;
        SelectedClip.StartTime = TimeSpan.Zero;
        SelectedClip.EndTime = SelectedClip.Duration;
        SelectedClip.IsTrimmed = false;
        StatusMessage = "Trim points cleared.";
    }

    // ── Commands: playback ─────────────────────────────────────────────────────

    [RelayCommand]
    private void PlayPause()
    {
        IsPlaying = !IsPlaying;
    }

    // ── Commands: export ───────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save Output Video",
            Filter = "MP4 Video|*.mp4|MKV Video|*.mkv|AVI Video|*.avi",
            DefaultExt = "mp4",
            FileName = "FreebeeVideo_output"
        };

        if (dlg.ShowDialog() != true) return;

        string outputPath = dlg.FileName;

        var clipsSnapshot = Clips.Select(c => (
            c.FilePath,
            c.IsTrimmed,
            c.StartTime,
            c.EndTime
        )).ToList();

        _cts = new CancellationTokenSource();
        IsExporting = true;
        ExportProgress = 0;
        StatusMessage = "Exporting…";

        try
        {
            var progressReporter = new Progress<double>(p =>
            {
                ExportProgress = p;
                StatusMessage = $"Exporting… {p:F0}%";
            });

            await _ffmpegService.TrimAndJoinAsync(clipsSnapshot, outputPath, progressReporter, _cts.Token);

            ExportProgress = 100;
            StatusMessage = $"Export complete: {outputPath}";
            MessageBox.Show($"Export complete!\n{outputPath}", "FreebeeVideo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Export cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
            MessageBox.Show(
                $"Export failed.\n\n{ex.Message}\n\nMake sure FFmpeg is installed and available on PATH or next to the application.",
                "FreebeeVideo – Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsExporting = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanExport() => Clips.Count > 0 && !IsExporting;

    [RelayCommand(CanExecute = nameof(IsExporting))]
    private void CancelExport()
    {
        _cts?.Cancel();
        StatusMessage = "Cancelling…";
    }

    // ── Selection changes ──────────────────────────────────────────────────────

    partial void OnSelectedClipChanged(VideoClip? value)
    {
        if (value == null)
        {
            MediaSource = null;
            MediaDuration = TimeSpan.Zero;
            MediaPosition = TimeSpan.Zero;
        }
        else
        {
            MediaSource = new Uri(value.FilePath);
        }

        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    // ── Called from code-behind when MediaElement reports duration ─────────────

    public void OnMediaOpened(TimeSpan duration)
    {
        if (SelectedClip == null) return;

        // Only set duration if it hasn't been populated by FFprobe yet
        if (SelectedClip.Duration == TimeSpan.Zero)
        {
            SelectedClip.Duration = duration;
            SelectedClip.EndTime = duration;
        }

        MediaDuration = SelectedClip.Duration;
        StatusMessage = $"Loaded: {SelectedClip.FileName}  ({duration:hh\\:mm\\:ss})";
    }
}
