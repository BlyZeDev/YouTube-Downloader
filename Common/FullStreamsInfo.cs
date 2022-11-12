namespace YouTubeDownloaderV2.Common;

using YoutubeExplode.Videos.Streams;

public sealed record FullStreamsInfo
{
    public VideoOnlyStreamInfo? VideoStream { get; init; }
    public AudioOnlyStreamInfo AudioStream { get; init; }
    public bool IsVideoStreamEmpty { get; init; }

    public FullStreamsInfo(VideoOnlyStreamInfo? videoStreamInfo, AudioOnlyStreamInfo audioStreamInfo)
    {
        VideoStream = videoStreamInfo;
        AudioStream = audioStreamInfo;
        IsVideoStreamEmpty = VideoStream is null;
    }
}