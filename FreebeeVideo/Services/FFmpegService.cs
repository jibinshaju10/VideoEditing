using System.IO;
using Xabe.FFmpeg;

namespace FreebeeVideo.Services;

public class FFmpegService
{
    public FFmpegService()
    {
        // Xabe.FFmpeg will locate ffmpeg/ffprobe from PATH or the directory set below.
        // Users can place ffmpeg.exe / ffprobe.exe next to the application binary or
        // install FFmpeg system-wide so it is on PATH.
        FFmpeg.SetExecutablesPath(AppDomain.CurrentDomain.BaseDirectory);
    }

    /// <summary>
    /// Returns the total duration of the media file at <paramref name="filePath"/>.
    /// </summary>
    public async Task<TimeSpan> GetDurationAsync(string filePath)
    {
        IMediaInfo info = await FFmpeg.GetMediaInfo(filePath);
        return info.Duration;
    }

    /// <summary>
    /// Trims a single video to [startTime, endTime] and writes the result to
    /// <paramref name="outputPath"/>.
    /// </summary>
    public async Task TrimAsync(
        string inputPath,
        TimeSpan startTime,
        TimeSpan endTime,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        IMediaInfo info = await FFmpeg.GetMediaInfo(inputPath, cancellationToken);

        IConversion conversion = FFmpeg.Conversions.New()
            .AddStream(info.VideoStreams.FirstOrDefault())
            .AddStream(info.AudioStreams.FirstOrDefault())
            .SetOutput(outputPath)
            .AddParameter($"-ss {startTime:hh\\:mm\\:ss\\.fff}")
            .AddParameter($"-to {endTime:hh\\:mm\\:ss\\.fff}")
            .SetOverwriteOutput(true);

        if (progress != null)
        {
            TimeSpan clipDuration = endTime - startTime;
            conversion.OnProgress += (_, args) =>
            {
                double percent = clipDuration.TotalSeconds > 0
                    ? args.Duration.TotalSeconds / clipDuration.TotalSeconds * 100.0
                    : 0;
                progress.Report(Math.Clamp(percent, 0, 100));
            };
        }

        await conversion.Start(cancellationToken);
    }

    /// <summary>
    /// Concatenates all <paramref name="inputPaths"/> (in order) into one output file at
    /// <paramref name="outputPath"/> using FFmpeg's concat demuxer.
    /// </summary>
    public async Task JoinAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (inputPaths.Count == 0)
            throw new ArgumentException("At least one input file is required.", nameof(inputPaths));

        if (inputPaths.Count == 1)
        {
            File.Copy(inputPaths[0], outputPath, overwrite: true);
            return;
        }

        // Write a concat list file.
        string listFile = Path.Combine(Path.GetTempPath(), $"freebee_concat_{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllLinesAsync(
                listFile,
                inputPaths.Select(p => $"file '{p.Replace("'", "'\\''")}'"),
                cancellationToken);

            IConversion conversion = FFmpeg.Conversions.New()
                .AddParameter($"-f concat -safe 0 -i \"{listFile}\"")
                .AddParameter("-c copy")
                .SetOutput(outputPath)
                .SetOverwriteOutput(true);

            if (progress != null)
            {
                double totalSeconds = 0;
                foreach (string p in inputPaths)
                {
                    IMediaInfo info = await FFmpeg.GetMediaInfo(p, cancellationToken);
                    totalSeconds += info.Duration.TotalSeconds;
                }

                conversion.OnProgress += (_, args) =>
                {
                    double percent = totalSeconds > 0
                        ? args.Duration.TotalSeconds / totalSeconds * 100.0
                        : 0;
                    progress.Report(Math.Clamp(percent, 0, 100));
                };
            }

            await conversion.Start(cancellationToken);
        }
        finally
        {
            if (File.Exists(listFile))
                File.Delete(listFile);
        }
    }

    /// <summary>
    /// Trims each clip and then joins all of them into a single output file.
    /// Clips that are not trimmed are used as-is.
    /// </summary>
    public async Task TrimAndJoinAsync(
        IReadOnlyList<(string FilePath, bool IsTrimmed, TimeSpan StartTime, TimeSpan EndTime)> clips,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"freebee_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var processedPaths = new List<string>();
            int total = clips.Count;

            for (int i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var clip = clips[i];

                if (clip.IsTrimmed)
                {
                    string ext = Path.GetExtension(clip.FilePath);
                    string trimmed = Path.Combine(tempDir, $"clip_{i:D3}{ext}");

                    // Sub-progress for this clip: occupies [i/total, (i+0.9)/total] of overall range
                    IProgress<double>? subProgress = progress == null ? null :
                        new Progress<double>(p =>
                            progress.Report((i + p / 100.0 * 0.9) / total * 100.0));

                    await TrimAsync(clip.FilePath, clip.StartTime, clip.EndTime, trimmed, subProgress, cancellationToken);
                    processedPaths.Add(trimmed);
                }
                else
                {
                    processedPaths.Add(clip.FilePath);
                }

                progress?.Report((double)(i + 1) / total * 90.0);
            }

            // Join phase occupies [90, 100]
            IProgress<double>? joinProgress = progress == null ? null :
                new Progress<double>(p => progress.Report(90.0 + p / 10.0));

            await JoinAsync(processedPaths, outputPath, joinProgress, cancellationToken);

            progress?.Report(100.0);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }
}
