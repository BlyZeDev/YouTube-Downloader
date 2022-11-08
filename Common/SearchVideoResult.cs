namespace YouTubeDownloaderV2.Common;

using System;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;

public readonly struct SearchVideoResult
{
    public string Url { get; init; }
    public string Title { get; init; }
    public string ThumbnailUrl { get; init; }
    public string ChannelName { get; init; }
    public string Duration { get; init; }

    public SearchVideoResult(VideoSearchResult searchResult)
    {
        Url = searchResult.Url;
        Title = searchResult.Title;
        ThumbnailUrl = searchResult.Thumbnails.TryGetWithHighestResolution()?.Url ?? AppManager.NoThumbnailUrl;
        ChannelName = searchResult.Author.ChannelTitle;
        Duration = searchResult.Duration.HasValue ? searchResult.Duration.Value.ToString("hh\\:mm\\:ss") : "Livestream";
    }
}