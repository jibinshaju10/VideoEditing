using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using FreebeeVideo.ViewModels;

namespace FreebeeVideo;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _positionTimer;
    private bool _isDraggingSeek;
    private MainViewModel Vm => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();

        // Timer that updates the seek slider while media is playing
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _positionTimer.Tick += PositionTimer_Tick;

        // Watch for SelectedClip changes so we can load the media
        Vm.PropertyChanged += Vm_PropertyChanged;
    }

    // ── MediaElement event handlers ────────────────────────────────────────────

    private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            TimeSpan duration = MediaPlayer.NaturalDuration.TimeSpan;
            Vm.OnMediaOpened(duration);
            SeekSlider.Maximum = duration.TotalSeconds;
        }
    }

    private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        _positionTimer.Stop();
        Vm.IsPlaying = false;
        PlayPauseButton.Content = "▶  Play";
        MediaPlayer.Stop();
    }

    private void MediaPlayer_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        Vm.StatusMessage = $"Media error: {e.ErrorException?.Message ?? "Unknown error"}";
    }

    // ── Playback ───────────────────────────────────────────────────────────────

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.IsPlaying)
        {
            MediaPlayer.Pause();
            _positionTimer.Stop();
            PlayPauseButton.Content = "▶  Play";
        }
        else
        {
            MediaPlayer.Play();
            _positionTimer.Start();
            PlayPauseButton.Content = "⏸  Pause";
        }
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isDraggingSeek && MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            Vm.MediaPosition = MediaPlayer.Position;
            SeekSlider.Value = MediaPlayer.Position.TotalSeconds;
        }
    }

    // ── Seek slider ────────────────────────────────────────────────────────────

    private void SeekSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isDraggingSeek = true;
    }

    private void SeekSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDraggingSeek = false;
        TimeSpan seekTo = TimeSpan.FromSeconds(SeekSlider.Value);
        MediaPlayer.Position = seekTo;
        Vm.MediaPosition = seekTo;
    }

    // ── Trim text boxes ────────────────────────────────────────────────────────

    private void TrimStartBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedClip == null) return;
        if (TimeSpan.TryParse(TrimStartBox.Text, out TimeSpan ts))
        {
            Vm.SelectedClip.StartTime = ts;
            Vm.SelectedClip.IsTrimmed = ts > TimeSpan.Zero
                                        || Vm.SelectedClip.EndTime < Vm.SelectedClip.Duration;
        }
    }

    private void TrimEndBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedClip == null) return;
        if (TimeSpan.TryParse(TrimEndBox.Text, out TimeSpan ts))
        {
            Vm.SelectedClip.EndTime = ts;
            Vm.SelectedClip.IsTrimmed = Vm.SelectedClip.StartTime > TimeSpan.Zero
                                        || ts < Vm.SelectedClip.Duration;
        }
    }

    // ── ViewModel property changes ─────────────────────────────────────────────

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.MediaSource))
        {
            // Stop any ongoing playback before switching source
            MediaPlayer.Stop();
            _positionTimer.Stop();
            Vm.IsPlaying = false;
            PlayPauseButton.Content = "▶  Play";

            MediaPlayer.Source = Vm.MediaSource;

            if (Vm.MediaSource != null)
                MediaPlayer.Play(); // open / buffer — then immediately pause so thumbnail appears
        }
        else if (e.PropertyName == nameof(MainViewModel.IsPlaying))
        {
            // Keep button label in sync if IsPlaying is toggled externally
            if (!Vm.IsPlaying)
            {
                PlayPauseButton.Content = "▶  Play";
            }
        }
    }
}
