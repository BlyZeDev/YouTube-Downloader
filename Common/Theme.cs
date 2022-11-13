namespace YouTubeDownloaderV2.Common;

using Newtonsoft.Json;
using System;

[Serializable]
public sealed record Theme
{
    [JsonRequired]
    [JsonProperty("BackgroundColor")]
    public UniColor Background { get; init; }

    [JsonRequired]
    [JsonProperty("ButtonColor")]
    public UniColor Button { get; init; }

    [JsonRequired]
    [JsonProperty("ControlColor")]
    public UniColor Control { get; init; }

    [JsonRequired]
    [JsonProperty("TextColor")]
    public UniColor Text { get; init; }

    [JsonConstructor]
    public Theme(UniColor background, UniColor button, UniColor control, UniColor text)
    {
        Background = background;
        Button = button;
        Control = control;
        Text = text;
    }
}