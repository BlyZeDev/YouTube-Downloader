namespace YouTubeDownloaderV2;

using System.Diagnostics;
using System.Windows;
using YouTubeDownloaderV2.Services;
using YoutubeExplode.Videos;

public sealed partial class VideoDetailsWindow : Window
{
    private readonly IYouTubeClient _client;
    private readonly Video _video;

    public VideoDetailsWindow(IYouTubeClient client, Video video)
    {
        InitializeComponent();
        _client = client;
        _video = video;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ChannelNameLabel.Content = _video.Author.ChannelTitle;
        ChannelNameLabel.ToolTip = _video.Author.ChannelUrl;
        UploadDateLabel.Content = _video.UploadDate.DateTime.ToString("D");
        ViewsLabel.Content = _video.Engagement.ViewCount.ToString("N0");
        LikesLabel.Content = _video.Engagement.LikeCount.ToString("N0");
        DescriptionTextBlock.Text = _video.Description;
        KeywordsTextBox.Text = string.Join(", ", _video.Keywords);

        var dislikes = await _client.GetVideoDislikesAsync(_video.Id);
        DislikesLabel.Content = dislikes == -1 ? "No dislikes available" : dislikes.ToString("N0");
    }

    private void ChannelNameLabel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => Process.Start(new ProcessStartInfo(_video.Author.ChannelUrl) { UseShellExecute = true });
}
