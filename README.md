# VideoEditing – FreebeeVideo

A modern **WPF** application for trimming and joining video files, built with .NET and FFmpeg.

## Features

- 🎬 **Video preview** – built-in media player with seek slider and volume control
- ✂️ **Frame-accurate trimming** – set trim start/end at the current playback position, or type timestamps manually
- 🔗 **Join multiple clips** – build a queue of videos (each with independent trim points) and export them as one file
- 📋 **Clip queue management** – add, remove, and reorder clips with Move Up / Move Down
- 📊 **Export progress** – real-time progress bar with cancel support
- 🎨 **Dark UI** – clean, modern dark theme

## Prerequisites

### FFmpeg

FreebeeVideo uses [FFmpeg](https://ffmpeg.org/) for all video processing. You must have FFmpeg installed before using the export feature.

**Option A – System-wide install (recommended)**

| Platform | Install command |
|----------|----------------|
| Windows  | `winget install Gyan.FFmpeg` or download from <https://www.gyan.dev/ffmpeg/builds/> and add the `bin/` folder to your `PATH` |

**Option B – Place next to the application**

Copy `ffmpeg.exe` and `ffprobe.exe` into the same folder as `FreebeeVideo.exe`.

### .NET Runtime

Requires **.NET 10** (Windows). Download from <https://dot.net>.

## Building from Source

```powershell
git clone https://github.com/jibinshaju10/VideoEditing.git
cd VideoEditing
dotnet build FreebeeVideo/FreebeeVideo.csproj --configuration Release
```

Run directly:

```powershell
dotnet run --project FreebeeVideo/FreebeeVideo.csproj
```

Or publish a self-contained executable:

```powershell
dotnet publish FreebeeVideo/FreebeeVideo.csproj -c Release -r win-x64 --self-contained
```

## Usage

1. Click **➕ Add Video(s)** to load one or more video files into the queue.
2. Select a clip in the queue – the video player will load it automatically.
3. Use **▶ Play / ⏸ Pause** and the seek slider to navigate.
4. Click **Set** next to *Trim Start* or *Trim End* to capture the current position as a trim point, or type a timestamp directly (`hh:mm:ss.fff`).
5. Repeat for each clip; trimmed clips show a purple badge with the selected range.
6. Click **🎬 Trim & Export** to choose an output file and begin processing.
7. The status bar shows live progress; click **Cancel** at any time to abort.

## Project Structure

```
FreebeeVideo/
├── Models/
│   └── VideoClip.cs          # Observable model for a clip with trim points
├── Services/
│   └── FFmpegService.cs      # FFmpeg wrapper (trim, join, trim-and-join)
├── ViewModels/
│   └── MainViewModel.cs      # MVVM view model (CommunityToolkit.Mvvm)
├── Converters/
│   └── ValueConverters.cs    # TimeSpan, Bool→Visibility, Double→Percent
├── App.xaml                  # Application resources & dark theme styles
├── MainWindow.xaml           # Main UI layout
└── MainWindow.xaml.cs        # Code-behind (MediaElement wiring, seek timer)
```

## Dependencies

| Package | Purpose |
|---------|---------|
| [Xabe.FFmpeg](https://xabe.net/product/xabe_ffmpeg/) | .NET wrapper for FFmpeg CLI |
| [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) | Source-generated MVVM base classes |
| [Newtonsoft.Json 13+](https://www.newtonsoft.com/json) | Overrides vulnerable transitive dep from Xabe.FFmpeg |
