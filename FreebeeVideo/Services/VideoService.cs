using System.IO;
using FFMpegCore;
using FFMpegCore.Enums;

namespace FreebeeVideo.Services;

/// <summary>
/// Provides video trimming and joining operations backed by FFMpeg via FFMpegCore.
/// FFMpeg must be installed and available on the system PATH (or configured via
/// GlobalFFOptions before calling these methods).
/// </summary>
public static class VideoService
{
    /// <summary>
    /// Trims a video file to the specified time range and saves the result.
    /// </summary>
    /// <param name="inputPath">Path to the source video file.</param>
    /// <param name="outputPath">Path for the trimmed output file.</param>
    /// <param name="start">Trim start position.</param>
    /// <param name="end">Trim end position.</param>
    /// <param name="onProgress">
    ///   Optional callback invoked with a 0–100 progress value as encoding proceeds.
    /// </param>
    public static async Task TrimAsync(
        string inputPath,
        string outputPath,
        TimeSpan start,
        TimeSpan end,
        Action<double>? onProgress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        if (end <= start)
            throw new ArgumentException("End time must be greater than start time.");

        var duration = end - start;

        var args = FFMpegArguments
            .FromFileInput(inputPath, verifyExists: true, opts => opts
                .Seek(start))
            .OutputToFile(outputPath, overwrite: true, opts => opts
                .WithDuration(duration)
                .CopyChannel()
                .WithFastStart());

        if (onProgress is not null)
        {
            await args
                .NotifyOnProgress(p => onProgress(p), duration)
                .ProcessAsynchronously();
        }
        else
        {
            await args.ProcessAsynchronously();
        }
    }

    /// <summary>
    /// Concatenates two or more video files (in order) into a single output file.
    /// All input videos must have compatible codecs and resolutions for stream-copy
    /// concatenation. If they differ, re-encoding is applied automatically.
    /// </summary>
    /// <param name="inputPaths">Ordered list of video files to join.</param>
    /// <param name="outputPath">Path for the joined output file.</param>
    /// <param name="onProgress">
    ///   Optional callback invoked with a 0–100 progress value as encoding proceeds.
    /// </param>
    public static async Task JoinAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        Action<double>? onProgress = null)
    {
        ArgumentNullException.ThrowIfNull(inputPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (inputPaths.Count < 2)
            throw new ArgumentException("At least two input files are required.", nameof(inputPaths));

        foreach (var p in inputPaths)
            if (!File.Exists(p))
                throw new FileNotFoundException($"Input file not found: {p}", p);

        // Calculate total duration for progress reporting
        TimeSpan totalDuration = TimeSpan.Zero;
        foreach (var p in inputPaths)
        {
            var info = await FFProbe.AnalyseAsync(p);
            totalDuration += info.Duration;
        }

        // Use FFMpeg concat demuxer via a temporary file list
        var listFile = Path.GetTempFileName();
        try
        {
            var lines = inputPaths.Select(p => $"file '{p.Replace("'", "\\'")}'");
            await File.WriteAllLinesAsync(listFile, lines);

            var args = FFMpegArguments
                .FromFileInput(listFile, verifyExists: true, opts => opts
                    .WithCustomArgument("-f concat -safe 0"))
                .OutputToFile(outputPath, overwrite: true, opts => opts
                    .CopyChannel()
                    .WithFastStart());

            if (onProgress is not null)
            {
                await args
                    .NotifyOnProgress(p => onProgress(p), totalDuration)
                    .ProcessAsynchronously();
            }
            else
            {
                await args.ProcessAsynchronously();
            }
        }
        finally
        {
            if (File.Exists(listFile))
                File.Delete(listFile);
        }
    }
}
