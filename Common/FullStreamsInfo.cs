namespace YouTubeDownloaderV2.Common;

using YoutubeExplode.Videos.Streams;

public record FullStreamsInfo
{
    public VideoOnlyStreamInfo? VideoStream { get; init; }
    public AudioOnlyStreamInfo AudioStream { get; init; }
    public bool IsVideoStreamEmpty { get; private init; }

    public FullStreamsInfo(VideoOnlyStreamInfo? videoStreamInfo, AudioOnlyStreamInfo audioStreamInfo)
    {
        VideoStream = videoStreamInfo;
        AudioStream = audioStreamInfo;
        IsVideoStreamEmpty = VideoStream is null;
    }
}