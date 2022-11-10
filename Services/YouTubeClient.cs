namespace YouTubeDownloaderV2.Services;

using AngleSharp.Common;
using FFMpegCore;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using YouTubeDownloaderV2.Common;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

public interface IYouTubeClient
{
    public bool IsDownloading { get; }

    public IAsyncEnumerator<VideoSearchResult> GetVideoSearchAsync(string searchQuery);

    public ValueTask<Video?> TryGetVideoInfoAsync(string url);

    public ValueTask<StreamManifest> GetVideoManifestAsync(string url);

    public IEnumerable<Resolution> GetVideoResolutions(StreamManifest manifest);

    public IEnumerable<Bitrate> GetAudioBitrates(StreamManifest manifest);

    public ValueTask<bool> TryDownloadThumbnailAsync(VideoId videoId, string fullPath);

    public ValueTask<bool> DownloadVideoAsync(Metadata metadata, DownloadStartedCallback started, DownloadProgressCallback progress, DownloadCancelledCallback cancelled);

    public Task CancelDownloadIfRunning();
}

public sealed class YouTubeClient : IYouTubeClient
{
    private VideoClient Videos { get; }

    private SearchClient Search { get; }

    private CancellationTokenSource _cts;

    public bool IsDownloading { get; private set; }

    private YouTubeClient(HttpClient http)
    {
        Videos = new VideoClient(http);
        Search = new SearchClient(http);

        _cts = new();

        IsDownloading = false;
    }

    public YouTubeClient() : this(Http.Client) { }

    public IAsyncEnumerator<VideoSearchResult> GetVideoSearchAsync(string searchQuery)
        => Search.GetVideosAsync(searchQuery).GetAsyncEnumerator();

    public async ValueTask<Video?> TryGetVideoInfoAsync(string url)
    {
        try
        {
            return await Videos.GetAsync(url);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async ValueTask<StreamManifest> GetVideoManifestAsync(string url)
        => await Videos.Streams.GetManifestAsync(url);

    public IEnumerable<Resolution> GetVideoResolutions(StreamManifest manifest)
        => manifest.GetVideoOnlyStreams().Select(x => x.VideoResolution).Distinct();

    public IEnumerable<Bitrate> GetAudioBitrates(StreamManifest manifest)
        => manifest.GetAudioOnlyStreams().Select(x => x.Bitrate).DistinctBy(x => (int)x.KiloBitsPerSecond);

    public async ValueTask<bool> TryDownloadThumbnailAsync(VideoId videoId, string fullPath)
    {
        string url;

        for (int retry = 0; retry < 6; retry++)
        {
            url = retry switch
            {
                0 => GetMaxResThumbnailUrl(videoId),
                1 => GetSdThumbnailUrl(videoId),
                2 => GetThumbnailUrl(videoId),
                3 => GetHqThumbnailUrl(videoId),
                4 => GetMqThumbnailUrl(videoId),
                5 => GetDfThumbnailUrl(videoId),
                _ => ""
            };

            try
            {
                using (var client = new HttpClient())
                {
                    using (var stream = await client.GetStreamAsync(url))
                    {
                        Image.FromStream(stream).Save(fullPath, ImageFormat.Jpeg);

                        return true;
                    }
                }
            }
            catch (Exception) { }
        }

        return false;
    }

    public async ValueTask<bool> DownloadVideoAsync(Metadata metadata, DownloadStartedCallback started, DownloadProgressCallback progress, DownloadCancelledCallback cancelled)
    {
        IsDownloading = true;

        string temppath = Path.GetTempPath(), videotemp = "", audiotemp = "";

        var deletionFiles = new List<string>();

        try
        {
            CreateCts();

            var fullStreams = await GetStreamsAsync(metadata);

            started();

            if (fullStreams.IsVideoStreamEmpty)
            {
                metadata.AddExtender(AddExtender(metadata.FullPath, fullStreams.AudioStream.Container.Name));

                deletionFiles.Add(metadata.FullPath + '.' + fullStreams.AudioStream.Container.Name);

                await DownloadAudioFile(fullStreams.AudioStream, metadata, progress);
            }
            else
            {
                videotemp = Path.Combine(temppath, Guid.NewGuid().ToString() + ".tmp");
                audiotemp = Path.Combine(temppath, Guid.NewGuid().ToString() + ".tmp");

                deletionFiles.Add(videotemp + '.' + fullStreams.VideoStream!.Container.Name);
                deletionFiles.Add(audiotemp + '.' + fullStreams.AudioStream.Container.Name);

                await DownloadVideoTempFile(fullStreams.VideoStream!, metadata, videotemp, x => progress(x / 2));

                await DownloadAudioTempFile(fullStreams.AudioStream, metadata, audiotemp, x => progress(x / 2 + 50));

                metadata.AddExtender(AddExtender(metadata.FullPath, fullStreams.VideoStream!.Container.Name));

                deletionFiles.Add(metadata.FullPath + '.' + fullStreams.VideoStream!.Container.Name);

                SaveCombinedVideoAndAudio(metadata, videotemp, audiotemp, fullStreams.VideoStream!.Container.Name, fullStreams.AudioStream.Container.Name);
            }

            return true;
        }
        catch (Exception)
        {
            await cancelled(deletionFiles);

            return false;
        }
        finally
        {
            IsDownloading = false;

            _cts.Dispose();
        }
    }

    private async ValueTask<FullStreamsInfo> GetStreamsAsync(Metadata metadata)
    {
        var manifest = await Videos.Streams.GetManifestAsync(metadata.Url, _cts.Token);

        return new FullStreamsInfo(
            metadata.OnlyAudio ? null
            : manifest.GetVideoOnlyStreams().First(x => x.VideoResolution.Height == metadata.ResolutionHeight),
            manifest.GetAudioOnlyStreams().First(x => (int)x.Bitrate.KiloBitsPerSecond == metadata.KbpsBitrate));
    }

    private async Task DownloadVideoTempFile(VideoOnlyStreamInfo videoStream, Metadata metadata, string temppath, DownloadProgressCallback progress)
    {
        await FFMpegArguments
            .FromUrlInput(new Uri(videoStream.Url))
            .OutputToFile(temppath + '.' + videoStream.Container.Name, true, x =>
            {
                x.WithCustomArgument($"-ss {metadata.StartTime} -t {metadata.GetDuration()}");
                x.WithVideoBitrate((int)videoStream.Bitrate.KiloBitsPerSecond);
                x.WithFastStart();
                x.UsingShortest(true);
                x.UsingMultithreading(true);
                x.WithSpeedPreset(FFMpegCore.Enums.Speed.UltraFast);
                x.WithoutMetadata();
                x.DisableChannel(FFMpegCore.Enums.Channel.Audio);
            })
            .NotifyOnProgress(x => progress(x), metadata.GetDuration())
            .CancellableThrough(_cts.Token)
            .ProcessAsynchronously();
    }

    private async Task DownloadAudioTempFile(AudioOnlyStreamInfo videoStream, Metadata metadata, string temppath, DownloadProgressCallback progress)
    {
        await FFMpegArguments
            .FromUrlInput(new Uri(videoStream.Url))
            .OutputToFile(temppath + '.' + videoStream.Container.Name, true, x =>
            {
                x.WithCustomArgument($"-ss {metadata.StartTime} -t {metadata.GetDuration()}");
                x.WithAudioBitrate((int)videoStream.Bitrate.KiloBitsPerSecond);
                x.WithFastStart();
                x.UsingShortest(true);
                x.UsingMultithreading(true);
                x.WithSpeedPreset(FFMpegCore.Enums.Speed.UltraFast);
                x.WithoutMetadata();
                x.DisableChannel(FFMpegCore.Enums.Channel.Video);
            })
            .NotifyOnProgress(x => progress(x), metadata.GetDuration())
            .CancellableThrough(_cts.Token)
            .ProcessAsynchronously();
    }

    private async Task DownloadAudioFile(AudioOnlyStreamInfo audioStream, Metadata metadata, DownloadProgressCallback progress)
    {
        await FFMpegArguments
            .FromUrlInput(new Uri(audioStream.Url))
            .OutputToFile(metadata.FullPath + '.' + audioStream.Container.Name, true, x =>
            {
                x.WithCustomArgument($"-ss {metadata.StartTime} -t {metadata.GetDuration()} -strict -2");
                x.WithAudioBitrate((int)audioStream.Bitrate.KiloBitsPerSecond);
                x.WithFastStart();
                x.UsingShortest(true);
                x.UsingMultithreading(true);
                x.WithSpeedPreset(FFMpegCore.Enums.Speed.UltraFast);
                x.WithoutMetadata();
                x.DisableChannel(FFMpegCore.Enums.Channel.Video);
            })
            .NotifyOnProgress(x => progress(x), metadata.GetDuration())
            .CancellableThrough(_cts.Token)
            .ProcessAsynchronously();
    }

    private static void SaveCombinedVideoAndAudio(Metadata metadata, string videoFilePath, string audioFilePath, string videoExtension, string audioExtension)
        => FFMpeg.ReplaceAudio(videoFilePath + '.' + videoExtension, audioFilePath + '.' + audioExtension, metadata.FullPath + '.' + videoExtension, true);

    public Task CancelDownloadIfRunning()
    {
        try
        {
            if (IsDownloading && _cts.Token.CanBeCanceled) _cts.Cancel();

            _cts.Dispose();
        }
        catch (Exception) { }

        return Task.CompletedTask;
    }

    private static string GetDfThumbnailUrl(VideoId videoId) => BuildLink(videoId, "default");

    private static string GetMqThumbnailUrl(VideoId videoId) => BuildLink(videoId, "mqdefault");

    private static string GetHqThumbnailUrl(VideoId videoId) => BuildLink(videoId, "hqdefault");

    private static string GetThumbnailUrl(VideoId videoId) => BuildLink(videoId, "0");

    private static string GetSdThumbnailUrl(VideoId videoId) => BuildLink(videoId, "sddefault");

    private static string GetMaxResThumbnailUrl(VideoId videoId) => BuildLink(videoId, "maxresdefault");

    private static string BuildLink(VideoId videoId, string quality) => $"https://img.youtube.com/vi/{videoId}/{quality}.jpg";

    private static string AddExtender(string fullPath, string fileExtension)
    {
        if (File.Exists(fullPath + '.' + fileExtension))
        {
            int counter = 0;

            while (File.Exists($"{fullPath}({counter}).{fileExtension}"))
            {
                counter++;
            }

            return $"({counter})";
        }

        return "";
    }

    private void CreateCts()
    {
        _cts = new();
        _cts.Token.ThrowIfCancellationRequested();
    }

    private static class Http
    {
        private static readonly Lazy<HttpClient> HttpClientLazy = new(() =>
        {
            var handler = new HttpClientHandler
            {
                UseCookies = false
            };

            if (handler.SupportsAutomaticDecompression)
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            return new HttpClient(handler, true);
        });

        public static HttpClient Client => HttpClientLazy.Value;
    }
}

public delegate void DownloadStartedCallback();

public delegate void DownloadProgressCallback(double progress);

public delegate Task DownloadCancelledCallback(IEnumerable<string> files);