namespace YouTubeDownloaderV2.Services;

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
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
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

    public ValueTask<bool> DownloadVideoAsync(string url, string filePathAndTitle, int resolutionHeight, int kbpsBitrate, bool onlyAudio, DownloadStartedCallback started, DownloadProgressCallback progress, DownloadCancelledCallback cancelled);

    public ValueTask<bool> DownloadVideoAsync(string url, string filePathAndTitle, int resolutionHeight, int kbpsBitrate, bool onlyAudio, TimeSpan startTime, TimeSpan endTime, DownloadStartedCallback started, DownloadProgressCallback progress, DownloadCancelledCallback cancelled);

    public Task CancelDownloadIfRunning();
}

public sealed class YouTubeClient : IYouTubeClient
{
    private string FFmpegPath { get; } = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg.exe");

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

    public async ValueTask<bool> DownloadVideoAsync(string url, string filePathAndTitle, int resolutionHeight, int kbpsBitrate, bool onlyAudio, DownloadStartedCallback started, DownloadProgressCallback progress, DownloadCancelledCallback cancelled)
    {
        IsDownloading = true;

        StreamManifest manifest;
        try
        {
            CreateCts();

            manifest = await Videos.Streams.GetManifestAsync(url, _cts.Token);
        }
        catch (Exception)
        {
            _cts.Dispose();

            return false;
        }

        var videoStream = onlyAudio ? null : manifest.GetVideoOnlyStreams().First(x => x.VideoResolution.Height == resolutionHeight);
        var audioStream = manifest.GetAudioOnlyStreams().First(x => (int)x.Bitrate.KiloBitsPerSecond == kbpsBitrate);

        IStreamInfo[] fullStream;

        if (videoStream is null) fullStream = new IStreamInfo[] { audioStream };
        else fullStream = new IStreamInfo[] { videoStream, audioStream };

        try
        {
            CreateCts();

            started();

            await Videos.DownloadAsync(fullStream, new ConversionRequestBuilder($"{filePathAndTitle}.{(onlyAudio ? "mp3" : "mp4")}")
                .SetFFmpegPath(FFmpegPath).SetPreset(ConversionPreset.UltraFast).SetContainer(onlyAudio ? "mp3" : "mp4").Build(),
                new Progress<double>(x => progress(x)), _cts.Token);

            return true;
        }
        catch (Exception)
        {
            await cancelled(new string[]
            {
                $"{filePathAndTitle}{(onlyAudio ? ".mp3" : ".mp4")}",
                $"{filePathAndTitle}{(onlyAudio ? ".mp3" : ".mp4")}.stream-0.tmp",
                $"{filePathAndTitle}{(onlyAudio ? ".mp3" : ".mp4")}.stream-1.tmp"
            });

            return false;
        }
        finally
        {
            IsDownloading = false;
            _cts.Dispose();
        }
    }

    public async ValueTask<bool> DownloadVideoAsync(string url, string filePathAndTitle, int resolutionHeight, int kbpsBitrate, bool onlyAudio, TimeSpan startTime, TimeSpan endTime, DownloadStartedCallback started, DownloadProgressCallback progress, DownloadCancelledCallback cancelled)
    {
        IsDownloading = true;

        StreamManifest manifest;
        try
        {
            CreateCts();

            manifest = await Videos.Streams.GetManifestAsync(url, _cts.Token);
        }
        catch (Exception)
        {
            _cts.Dispose();

            return false;
        }

        IStreamInfo? videoStream = onlyAudio ? null : manifest.GetVideoOnlyStreams().First(x => x.VideoResolution.Height == resolutionHeight);
        IStreamInfo audioStream = manifest.GetAudioOnlyStreams().First(x => (int)x.Bitrate.KiloBitsPerSecond == kbpsBitrate);

        string videoTemp = "";
        string audioTemp = "";

        try
        {
            endTime -= startTime;

            CreateCts();

            started();

            if (videoStream is null)
            {
                await FFMpegArguments
                    .FromUrlInput(new Uri(audioStream.Url))
                    .OutputToFile(filePathAndTitle + ".mp3", true, x =>
                    {
                        x.WithArgument(new FFMpegCore.Arguments.SpeedPresetArgument(FFMpegCore.Enums.Speed.UltraFast));
                        x.WithCustomArgument($"-ss {startTime} -t {endTime}");
                        x.WithFastStart();
                        x.UsingShortest(true);
                        x.UsingMultithreading(true);
                    })
                    .NotifyOnProgress(x => progress(x), endTime)
                    .CancellableThrough(_cts.Token)
                    .ProcessAsynchronously();
            }
            else
            {
                videoTemp = filePathAndTitle + '_' + Guid.NewGuid() + ".mp4";
                audioTemp = filePathAndTitle + '_' + Guid.NewGuid() + ".mp3";

                await FFMpegArguments
                    .FromUrlInput(new Uri(videoStream.Url))
                    .OutputToFile(videoTemp, true, x =>
                    {
                        x.WithArgument(new FFMpegCore.Arguments.SpeedPresetArgument(FFMpegCore.Enums.Speed.UltraFast));
                        x.WithCustomArgument($"-ss {startTime} -t {endTime}");
                        x.WithFastStart();
                        x.UsingShortest(true);
                        x.UsingMultithreading(true);
                    })
                    .NotifyOnProgress(x => progress(x), endTime)
                    .CancellableThrough(_cts.Token)
                    .ProcessAsynchronously();

                await FFMpegArguments
                    .FromUrlInput(new Uri(audioStream.Url))
                    .OutputToFile(audioTemp, true, x =>
                    {
                        x.WithArgument(new FFMpegCore.Arguments.SpeedPresetArgument(FFMpegCore.Enums.Speed.UltraFast));
                        x.WithCustomArgument($"-ss {startTime} -t {endTime}");
                        x.WithFastStart();
                        x.UsingShortest(true);
                        x.UsingMultithreading(true);
                    })
                    .NotifyOnProgress(x => progress(x), endTime)
                    .CancellableThrough(_cts.Token)
                    .ProcessAsynchronously();

                FFMpeg.ReplaceAudio(videoTemp, audioTemp, filePathAndTitle + ".mp4", true);

                if (File.Exists(videoTemp)) File.Delete(videoTemp);
                if (File.Exists(audioTemp)) File.Delete(audioTemp);
            }

            return true;
        }
        catch (Exception)
        {
            await cancelled(new string[]
            {
                $"{filePathAndTitle}.mp4",
                $"{filePathAndTitle}.mp3",
                videoTemp,
                audioTemp
            });

            return false;
        }
        finally
        {
            IsDownloading = false;
            _cts.Dispose();
        }
    }

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