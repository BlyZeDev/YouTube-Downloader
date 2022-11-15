namespace YouTubeDownloaderV2;

using System.Windows;
using System.Windows.Media;
using YouTubeDownloaderV2.Common;

public sealed partial class ThemeEditorWindow : Window
{
    public ThemeEditorWindow()
    {
        InitializeComponent();
    }

    private void Window_SourceInitialized(object sender, System.EventArgs e)
    {
        BackgroundColorPicker.Color = (UniColor)AppManager.GetResource<SolidColorBrush>("Background");
        ControlColorPicker.Color = (UniColor)AppManager.GetResource<SolidColorBrush>("Control");
        ButtonColorPicker.Color = (UniColor)AppManager.GetResource<SolidColorBrush>("Button");
        TextColorPicker.Color = (UniColor)AppManager.GetResource<SolidColorBrush>("Text");
    }

    private void BackgroundColorPicker_ColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (BackgroundColorPicker.Color.A < 1)
            BackgroundColorPicker.Color = Color.FromRgb(
                BackgroundColorPicker.Color.R, BackgroundColorPicker.Color.G, BackgroundColorPicker.Color.B);

        SetResource("BackgroundTE", (SolidColorBrush)(UniColor)BackgroundColorPicker.Color);
    }

    private void ControlColorPicker_ColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (ControlColorPicker.Color.A < 1)
            ControlColorPicker.Color = Color.FromRgb(
                ControlColorPicker.Color.R, ControlColorPicker.Color.G, ControlColorPicker.Color.B);

        SetResource("ControlTE", (SolidColorBrush)(UniColor)ControlColorPicker.Color);
    }

    private void ButtonColorPicker_ColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (ButtonColorPicker.Color.A < 1)
            ButtonColorPicker.Color = Color.FromRgb(
                ButtonColorPicker.Color.R, ButtonColorPicker.Color.G, ButtonColorPicker.Color.B);

        SetResource("ButtonTE", (SolidColorBrush)(UniColor)ButtonColorPicker.Color);
    }

    private void TextColorPicker_ColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (TextColorPicker.Color.A < 1)
            TextColorPicker.Color = Color.FromRgb(
                TextColorPicker.Color.R, TextColorPicker.Color.G, TextColorPicker.Color.B);

        SetResource("TextTE", (SolidColorBrush)(UniColor)TextColorPicker.Color);
    }

    private async void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Do you want to override the Custom Theme?", "",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) is not MessageBoxResult.Yes) return;

        await AppManager.OverrideTheme(
            new Theme(
                BackgroundColorPicker.Color,
                ButtonColorPicker.Color,
                ControlColorPicker.Color,
                TextColorPicker.Color));
    }

    private void SetResource<T>(string resource, T value) => Resources[resource] = value;
}