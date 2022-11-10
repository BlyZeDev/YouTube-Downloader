namespace YouTubeDownloaderV2.Common;

using System;

public class Metadata
{
    public string Url { get; init; }
    public string FullPath { get; private set; }
    public int ResolutionHeight { get; init; }
    public int KbpsBitrate { get; init; }
    public bool OnlyAudio { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }

    public Metadata(string url, string fullPath, int resolutionHeight, int kbpsBitrate, bool onlyAudio, TimeSpan startTime, TimeSpan endTime)
    {
        Url = url;
        FullPath = fullPath;
        ResolutionHeight = resolutionHeight;
        KbpsBitrate = kbpsBitrate;
        OnlyAudio = onlyAudio;
        StartTime = startTime;
        EndTime = endTime;
    }

    public TimeSpan GetDuration() => EndTime - StartTime;

    public void AddExtender(string extender) => FullPath += extender;
}