namespace YouTubeDownloaderV2;

using Ookii.Dialogs.Wpf;
using Syncfusion.Windows.Tools.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YouTubeDownloaderV2.Common;
using YouTubeDownloaderV2.Services;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;

public sealed partial class MainWindow : Window
{
    private bool IsBackgroundImage { get; set; }

    private Video? CurrentVideo { get; set; }

    private readonly IYouTubeClient _client;

    public MainWindow(IYouTubeClient client)
    {
        InitializeComponent();
        _client = client;

        CurrentVideo = null;
    }

    private async void Window_SourceInitialized(object sender, EventArgs e)
    {
        SetGroupBoxesState(false);

        TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;

        SetGroupBoxesState(true);

        VideoDetailsGroupBox.IsEnabled = false;

        await AppManager.InitializeAppData();

        var replacementBackground = await AppManager.GetReplaceBackgroundFile();

        if (!string.IsNullOrWhiteSpace(replacementBackground))
        {
            if (replacementBackground == "null") AppManager.DeleteBackgroundImage();
            else AppManager.TrySetBackgroundImage(replacementBackground);
        }

        if (AppManager.TryGetBackgroundImage(out var background))
        {
            Background = new ImageBrush(background);
            IsBackgroundImage = true;
        }
        else IsBackgroundImage = false;

        await SetThemeResources();

        await ApplyNonDynamicTheme();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeThemeComboBox();

        DownloadProgressLabel.Visibility = Visibility.Collapsed;

        DownloadPathTextBox.Text = AppManager.GetDownloadFolderPath();

        LinkTextBox.Focus();
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_client.IsDownloading)
        {
            if (MessageBox.Show("Are you sure you want to close? The current download progress will be lost!", "",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) is not MessageBoxResult.Yes)
            {
                e.Cancel = true;

                return;
            }
        }

        var ffmpegProcess = Process.GetProcessesByName("ffmpeg");

        if (ffmpegProcess.Length == 1) ffmpegProcess.First().Kill();

        await _client.CancelDownloadIfRunning();
    }

    private void MainGroupBox_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (MainGroupBox.IsEnabled)
        {
            ThemeComboBoxOverlay.Visibility = Visibility.Collapsed;
            ThemeComboBox.Visibility = Visibility.Visible;

            if (LinkTextBox.Text == "Enter link here") LinkTextBox.Clear();
        }
        else
        {
            ThemeComboBoxOverlay.Visibility = Visibility.Visible;
            ThemeComboBox.Visibility = Visibility.Collapsed;

            if (LinkTextBox.IsEmpty()) LinkTextBox.Text = "Enter link here";
        }
    }

    private void LinkTextBox_IsKeyboardFocusedChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (LinkTextBox.IsKeyboardFocused) StartBtn.IsDefault = true;
        else StartBtn.IsDefault = false;
    }

    private void SearchBtn_Click(object sender, RoutedEventArgs e)
        => new SearchWindow(_client, LinkTextBox).ShowDialog();

    private async void BackgroundImageBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new VistaOpenFileDialog()
        {
            CheckFileExists = true,
            CheckPathExists = true,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Multiselect = false,
            Filter = "PNG (*png)|*png|JPG (*jpg)|*jpg|JPEG (*jpeg)|*jpeg"
        };

        if (!(dialog.ShowDialog() ?? false)) return;

        if (!File.Exists(dialog.FileName)) return;

        if (IsBackgroundImage)
        {
            if (MessageBox.Show(
                "Do you want to replace the background image?",
                "", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No) is not MessageBoxResult.Yes) return;
        }

        Visibility = Visibility.Collapsed;

        IsBackgroundImage = true;

        Background = new ImageBrush(new BitmapImage(new Uri(dialog.FileName)) { CacheOption = BitmapCacheOption.OnLoad });

        await AppManager.SetReplaceBackgroundFile(dialog.FileName);

        Visibility = Visibility.Visible;
    }

    private async void BackgroundImageBtn_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            if (!IsBackgroundImage) return;

            if (MessageBox.Show("Do you want to delete the background image?", "",
                MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No) is not MessageBoxResult.Yes) return;

            IsBackgroundImage = false;

            await ApplyNonDynamicTheme();

            await AppManager.SetReplaceBackgroundFile("null");
        }
    }

    private async void ThemeComboBox_DropDownClosed(object sender, EventArgs e)
    {
        switch (ThemeComboBox.SelectedIndex)
        {
            case 0: await AppManager.SetSelectedTheme(AppManager.StandardThemeJson); break;

            case 1: await AppManager.SetSelectedTheme(AppManager.DarkThemeJson); break;

            case 2: await AppManager.SetSelectedTheme(AppManager.LightThemeJson); break;

            case 3: await AppManager.SetSelectedTheme(AppManager.NeonThemeJson); break;

            case 4: await AppManager.SetSelectedTheme(AppManager.CustomThemeJson); break;

            default: return;
        }

        await SetThemeResources();

        await ApplyNonDynamicTheme();
    }

    private void ThemeComboBox_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
        {
            //Open Theme Creator
        }
    }

    private void BlyZeLogoBtn_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo("https://github.com/BlyZeYT") { UseShellExecute = true });

    private async void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
        DownloadProgressBar.IsIndeterminate = true;

        SetGroupBoxesState(false);

        CurrentVideo = null;

        var (video, isHttpError) = await _client.TryGetVideoInfoAsync(LinkTextBox.Text);

        if (video is null)
        {
            TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
            DownloadProgressBar.IsIndeterminate = false;

            if (isHttpError) MessageBox.Show("You have no internet connection!", "", MessageBoxButton.OK, MessageBoxImage.Error);
            else MessageBox.Show("Invalid Link", "", MessageBoxButton.OK, MessageBoxImage.Error);

            MainGroupBox.IsEnabled = true;

            return;
        }

        if (video.Duration is null)
        {
            TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
            DownloadProgressBar.IsIndeterminate = false;

            MessageBox.Show("Can not download an ongoing livestream", "", MessageBoxButton.OK, MessageBoxImage.Error);

            MainGroupBox.IsEnabled = true;

            return;
        }

        Focus();

        CurrentVideo = video;

        TaskbarInfo.Description = CurrentVideo.Title;

        ClearVideoDetails();

        await FillVideoDetails(CurrentVideo);

        SetGroupBoxesState(true);

        TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
        DownloadProgressBar.IsIndeterminate = false;
    }

    private void VideoDetailsGroupBox_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (VideoDetailsGroupBox.IsEnabled)
        {
            StartTimeMaskBox.Visibility = Visibility.Visible;
            EndTimeMaskBox.Visibility = Visibility.Visible;

            VideoQualityComboBoxOverlay.Visibility = Visibility.Collapsed;
            AudioQualityComboBoxOverlay.Visibility = Visibility.Collapsed;

            VideoQualityComboBox.Visibility = Visibility.Visible;
            AudioQualityComboBox.Visibility = Visibility.Visible;
        }
        else
        {
            StartTimeMaskBox.Visibility = Visibility.Collapsed;
            EndTimeMaskBox.Visibility = Visibility.Collapsed;

            VideoQualityComboBox.Visibility = Visibility.Collapsed;
            AudioQualityComboBox.Visibility = Visibility.Collapsed;

            VideoQualityComboBoxOverlay.Visibility = Visibility.Visible;
            AudioQualityComboBoxOverlay.Visibility = Visibility.Visible;
        }
    }

    private void VideoInfoButton_Click(object sender, RoutedEventArgs e)
        => new VideoDetailsWindow(CurrentVideo!).ShowDialog();

    private async void DownloadPathTextBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dialog = new VistaFolderBrowserDialog()
        {
            Multiselect = false,
            RootFolder = Environment.SpecialFolder.Desktop,
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() ?? false)
        {
            DownloadPathTextBox.Text = dialog.SelectedPath;
            await AppManager.SetDownloadFolderPath(dialog.SelectedPath);
        }
    }

    private async void ThumbnailImageBox_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton is System.Windows.Input.MouseButtonState.Pressed)
        {
            if (CurrentVideo is null) return;

            var dialog = new VistaSaveFileDialog()
            {
                AddExtension = true,
                CheckFileExists = true,
                CheckPathExists = true,
                DefaultExt = ".jpg", 
                FileName = "Thumbnail",
                Filter = "JPG (*jpg)|*jpg",
                InitialDirectory = DownloadPathTextBox.Text,
                OverwritePrompt = true,
                ValidateNames = true
            };

            if (!(dialog.ShowDialog() ?? false)) return;

            if (await _client.TryDownloadThumbnailAsync(CurrentVideo.Id, dialog.FileName))
                MessageBox.Show("Thumbnail was downloaded successfully!", "", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("Thumbnail failed to download!", "", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DownloadBtn_Click(object sender, RoutedEventArgs e)
    {
        TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;

        SetGroupBoxesState(false);

        if (!IsValidFileName(TitleTextBox.Text))
        {
            TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;

            MessageBox.Show("Invalid file name!", "", MessageBoxButton.OK, MessageBoxImage.Error);
            SetGroupBoxesState(true);
            return;
        }

        if (!Directory.Exists(DownloadPathTextBox.Text))
        {
            TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
            MessageBox.Show("This folder doesn't exist!", "", MessageBoxButton.OK, MessageBoxImage.Error);
            SetGroupBoxesState(true);
            return;
        }

        if (!HaveWritingPermission(DownloadPathTextBox.Text))
        {
            TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
            MessageBox.Show("No writing access on this folder!", "", MessageBoxButton.OK, MessageBoxImage.Error);
            SetGroupBoxesState(true);
            return;
        }

        TimeSpan starttime = TimeSpan.Zero, endtime = TimeSpan.Zero;

        if (!(StartTimeMaskBox.IsEmpty() && EndTimeMaskBox.IsEmpty()))
        {
            if (StartTimeMaskBox.Text.Contains('_') || EndTimeMaskBox.Text.Contains('_'))
            {
                TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
                MessageBox.Show("The start or end time doesn't have correct values!", "", MessageBoxButton.OK, MessageBoxImage.Error);
                SetGroupBoxesState(true);
                return;
            }

            (starttime, endtime) = ConvertTimestamps(StartTimeMaskBox.Text, EndTimeMaskBox.Text, CurrentVideo!.Duration!.Value);

            if (starttime >= endtime)
            {
                TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
                MessageBox.Show("The start time can't be higher or equal to the end time!", "", MessageBoxButton.OK, MessageBoxImage.Error);
                SetGroupBoxesState(true);
                return;
            }

            if (endtime > CurrentVideo!.Duration)
            {
                TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
                MessageBox.Show("The end time can't be higher than the video duration!", "", MessageBoxButton.OK, MessageBoxImage.Error);
                SetGroupBoxesState(true);
                return;
            }
        }

        TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Indeterminate;
        DownloadProgressBar.IsIndeterminate = true;

        var wasDownloadedSuccessfully = await _client.DownloadVideoAsync(
            new Metadata(
                CurrentVideo!.Url,
                Path.Combine(DownloadPathTextBox.Text, TitleTextBox.Text),
                Convert.ToInt32(VideoQualityComboBox.Text[0..^1]),
                Convert.ToInt32(AudioQualityComboBox.Text[0..^4]),
                SoundOnlyCheckBox.IsChecked ?? false,
                starttime, endtime),
            DownloadStartedCallback,
            DownloadCallback,
            DownloadCancelledCallback);

        if (wasDownloadedSuccessfully)
        {
            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Progress = 0;
            DownloadProgressLabel.Visibility = Visibility.Collapsed;
            TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
            TaskbarInfo.Description = CurrentVideo.Title;
            TaskbarInfo.ProgressValue = 0;

            MessageBox.Show("Download completed successfully!", "", MessageBoxButton.OK, MessageBoxImage.Information);

            SetGroupBoxesState(true);
        }
        else
        {
            MessageBox.Show("Download was cancelled!", "", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DownloadStartedCallback()
    {
        Dispatcher.Invoke(() =>
        {
            DownloadProgressLabel.Visibility = Visibility.Visible;
            DownloadProgressBar.IsIndeterminate = false;
            TaskbarInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
        });
    }

    private void DownloadCallback(double progress)
    {
        Dispatcher.Invoke(() =>
        {
            TaskbarInfo.Description = "Downloading: " + (int)progress + '%';
            DownloadProgressBar.Progress = progress;
            TaskbarInfo.ProgressValue = progress / 100;
        });
    }

    private Task DownloadCancelledCallback(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            if (File.Exists(file)) File.Delete(file);
        }

        return Task.CompletedTask;
    }

    private void InitializeThemeComboBox()
    {
        switch (AppManager.GetSelectedThemeFile())
        {
            case AppManager.StandardThemeJson: ThemeComboBox.SelectedIndex = 0; break;

            case AppManager.DarkThemeJson: ThemeComboBox.SelectedIndex = 1; break;

            case AppManager.LightThemeJson: ThemeComboBox.SelectedIndex = 2; break;

            case AppManager.NeonThemeJson: ThemeComboBox.SelectedIndex = 3; break;

            case AppManager.CustomThemeJson: ThemeComboBox.SelectedIndex = 4; break;
        }
    }

    private Task ApplyNonDynamicTheme()
    {
        UniColor background = (UniColor)AppManager.GetResource<SolidColorBrush>("Background");
        UniColor text = (UniColor)AppManager.GetResource<SolidColorBrush>("Text");

        if (!IsBackgroundImage) Background = (SolidColorBrush)background;

        BackgroundImageBtnImage.Source = GetColoredBackgroundIcon(text);
        BlyZeLogoImage.Source = GetColoredBlyZeLogo(text);
        VideoInfoImage.Source = GetColoredInfoIcon(text);

        return Task.CompletedTask;
    }

    private void SetGroupBoxesState(bool isEnabled)
    {
        MainGroupBox.IsEnabled = isEnabled;
        VideoDetailsGroupBox.IsEnabled = isEnabled;
    }

    private void ClearVideoDetails()
    {
        TitleTextBox.Clear();
        StartTimeMaskBox.Clear();
        EndTimeMaskBox.Clear();
        ThumbnailImageBox.Source = null;
        VideoQualityComboBox.Items.Clear();
        AudioQualityComboBox.Items.Clear();
        SoundOnlyCheckBox.IsChecked = false;
    }

    private async Task FillVideoDetails(Video video)
    {
        var manifest = await _client.GetVideoManifestAsync(video.Url);

        TitleTextBox.Text = video.Title;

        StartTimeMaskBox.Value = new TimeSpan(0, 0, 0);
        EndTimeMaskBox.Value = video.Duration ?? new TimeSpan(0,0,0);

        ThumbnailImageBox.Source = new BitmapImage(new Uri(video.Thumbnails.TryGetWithHighestResolution()?.Url ?? AppManager.NoThumbnailUrl));

        ComboBoxItemAdv comboBoxItem;
        foreach (var resolution in _client.GetVideoResolutions(manifest).OrderByDescending(x => x.Height))
        {
            comboBoxItem = new ComboBoxItemAdv
            {
                Content = resolution.Height + "p",
                BorderThickness = new Thickness(0)
            };
            comboBoxItem.SetResourceReference(BackgroundProperty, "Control");
            comboBoxItem.SetResourceReference(ForegroundProperty, "Text");

            VideoQualityComboBox.Items.Add(comboBoxItem);
        }

        VideoQualityComboBox.SelectedIndex = 0;

        foreach (var bitrate in _client.GetAudioBitrates(manifest).OrderByDescending(x => x.KiloBitsPerSecond))
        {
            comboBoxItem = new ComboBoxItemAdv
            {
                Content = (int)bitrate.KiloBitsPerSecond + "kbps",
                BorderThickness = new Thickness(0)
            };
            comboBoxItem.SetResourceReference(BackgroundProperty, "Control");
            comboBoxItem.SetResourceReference(ForegroundProperty, "Text");

            AudioQualityComboBox.Items.Add(comboBoxItem);
        }

        AudioQualityComboBox.SelectedIndex = 0;
    }

    private static async Task SetThemeResources()
    {
        var theme = await AppManager.GetSelectedTheme();

        AppManager.SetResource("Background", (SolidColorBrush)theme.Background);
        AppManager.SetResource("Control", (SolidColorBrush)theme.Control);
        AppManager.SetResource("Button", (SolidColorBrush)theme.Button);
        AppManager.SetResource("Text", (SolidColorBrush)theme.Text);
    }

    private static BitmapSource GetColoredBackgroundIcon(UniColor color)
        => new BitmapImage(new Uri(AppManager.BackgroundIconUrl)).ReplaceColor(UniColor.Black, color);

    private static BitmapSource GetColoredBlyZeLogo(UniColor toColor)
        => new BitmapImage(new Uri(AppManager.BlyZeLogoUrl)).ReplaceColor(toColor);

    private static BitmapSource GetColoredInfoIcon(UniColor color)
        => new BitmapImage(new Uri(AppManager.InfoIconUrl)).ReplaceColor(UniColor.Black, color);

    private static bool IsValidFileName(string name) => !Path.GetInvalidFileNameChars().Any(invalidChar => name.Contains(invalidChar));

    private static bool HaveWritingPermission(string path)
    {
        try
        {
            var fullPath = Path.Combine(path, Guid.NewGuid().ToString());

            using (File.Create(fullPath)) { }

            File.Delete(fullPath);

            return true;
        }
        catch (Exception) { return false; }
    }

    private static (TimeSpan starttime, TimeSpan endtime) ConvertTimestamps(string starttime, string endtime, TimeSpan duration)
    {
        return TimeSpan.TryParseExact(starttime, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, TimeSpanStyles.None, out var startTime)
            && TimeSpan.TryParseExact(endtime, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, TimeSpanStyles.None, out var endTime)
            ? ((TimeSpan starttime, TimeSpan endtime))(startTime, endTime)
            : ((TimeSpan starttime, TimeSpan endtime))(TimeSpan.Zero, duration);
    }
}
