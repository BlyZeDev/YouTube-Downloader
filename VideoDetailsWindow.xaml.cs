namespace YouTubeDownloaderV2;

using System.Diagnostics;
using System.Windows;
using YoutubeExplode.Videos;

public sealed partial class VideoDetailsWindow : Window
{
    private readonly Video _video;

    public VideoDetailsWindow(Video video)
    {
        InitializeComponent();
        _video = video;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ChannelNameLabel.Content = _video.Author.ChannelTitle;
        ChannelNameLabel.ToolTip = _video.Author.ChannelUrl;
        UploadDateLabel.Content = _video.UploadDate.DateTime.ToString("D");
        ViewsLabel.Content = _video.Engagement.ViewCount.ToString("N0");
        LikesLabel.Content = _video.Engagement.LikeCount.ToString("N0");
        DescriptionTextBlock.Text = _video.Description;
        KeywordsTextBox.Text = string.Join(", ", _video.Keywords);
    }

    private void ChannelNameLabel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => Process.Start(new ProcessStartInfo(_video.Author.ChannelUrl) { UseShellExecute = true });
}
