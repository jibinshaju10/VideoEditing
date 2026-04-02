using CommunityToolkit.Mvvm.ComponentModel;

namespace FreebeeVideo.Models;

public partial class VideoClip : ObservableObject
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private TimeSpan _duration;

    [ObservableProperty]
    private TimeSpan _startTime;

    [ObservableProperty]
    private TimeSpan _endTime;

    [ObservableProperty]
    private bool _isTrimmed;

    public TimeSpan TrimDuration => EndTime - StartTime;

    public string DisplayName => IsTrimmed
        ? $"{FileName} [{StartTime:hh\\:mm\\:ss} – {EndTime:hh\\:mm\\:ss}]"
        : FileName;

    partial void OnStartTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(TrimDuration));
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnEndTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(TrimDuration));
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnIsTrimmedChanged(bool value) => OnPropertyChanged(nameof(DisplayName));

    partial void OnFileNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));
}
