namespace YouTubeDownloaderV2;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using YouTubeDownloaderV2.Common;
using YouTubeDownloaderV2.Services;
using YoutubeExplode.Search;

public sealed partial class SearchWindow : Window
{
    private readonly IYouTubeClient _client;
    private readonly TextBox _linkTextBox;

    private ObservableCollection<SearchVideoResult> SearchResults { get; set; }
    private IAsyncEnumerator<VideoSearchResult>? CurrentSearch { get; set; }

    private bool CanSearchOnScroll { get; set; }

    public SearchWindow(IYouTubeClient client, TextBox linkTextBox)
    {
        InitializeComponent();

        _client = client;
        _linkTextBox = linkTextBox;

        SearchResults = new();

        SearchResultsListBox.ItemsSource = SearchResults;

        CanSearchOnScroll = true;

        SearchTextBox.Focus();
    }

    private void Window_Closed(object sender, System.EventArgs e)
    {
        if (SearchResultsListBox.SelectedIndex != -1)
        {
            _linkTextBox.Text = ((SearchVideoResult)SearchResultsListBox.SelectedItem).Url;

            if (_linkTextBox.Focusable) _linkTextBox.Focus();
        }
    }

    private void SearchTextBox_IsKeyboardFocusedChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (SearchTextBox.IsKeyboardFocused) SearchBtn.IsDefault = true;
        else SearchBtn.IsDefault = false;
    }

    private async void SearchBtn_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.IsEnabled = false;
        SearchBtn.IsEnabled = false;
        SearchBtn.Opacity = 0.5;

        SearchResults.Clear();

        CurrentSearch = _client.GetVideoSearchAsync(SearchTextBox.Text);

        await AddResultsAsync(8);

        SearchTextBox.IsEnabled = true;
        SearchBtn.IsEnabled = true;
        SearchBtn.Opacity = 1;
    }

    private async void SearchResultsListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange > 0 && CanSearchOnScroll)
        {
            await AddResultsAsync(5);
        }
    }

    private void SearchResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => Close();

    private async Task AddResultsAsync(int resultNumber)
    {
        if (CurrentSearch is null) return;

        CanSearchOnScroll = false;

        for (int i = 1; i <= resultNumber; i++)
        {
            if (!await CurrentSearch.MoveNextAsync())
            {
                await CurrentSearch.DisposeAsync();
                break;
            }

            SearchResults.Add(new(CurrentSearch.Current));
        }

        CanSearchOnScroll = true;
    }
}