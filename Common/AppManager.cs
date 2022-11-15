namespace YouTubeDownloaderV2.Common;

using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

public static class AppManager
{
    public const string BackgroundIconUrl = "pack://application:,,,/background_icon.png";
    public const string BlyZeLogoUrl = "pack://application:,,,/blyze_logo.png";
    public const string InfoIconUrl = "pack://application:,,,/info_icon.png";
    public const string NoThumbnailUrl = "pack://application:,,,/no_thumbnail.png";


    public const string StandardThemeJson = "theme_standard.json";

    private static Theme StandardTheme { get; }


    public const string LightThemeJson = "theme_light.json";

    private static Theme LightTheme { get; }


    public const string DarkThemeJson = "theme_dark.json";

    private static Theme DarkTheme { get; }


    public const string NeonThemeJson = "theme_neon.json";

    private static Theme NeonTheme { get; }


    public const string CustomThemeJson = "theme_custom.json";

    public const string SelectedThemeFile = "theme_selected.txt";


    public const string DownloadPathFile = "download_path.txt";

    public const string ReadMeFile = "README.txt";

    public const string BackgroundImageFile = "background.png";

    public const string ReplaceBackgroundFile = "background_replacement.txt";


    public static Version CurrentVersion { get; }


    public static string ProgramFolderPath { get; }

    public static string FfmpegPath { get; }

    static AppManager()
    {
        CurrentVersion = new Version("2.0.0.0");

        ProgramFolderPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.Create), "YouTube Downloader");

        FfmpegPath = Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg.exe");

        StandardTheme = new Theme(new(46, 51, 73), new(192, 0, 0), new(176, 196, 222), new(255, 250, 205));
        LightTheme = new Theme(new(245, 245, 245), new(204, 204, 204), new(156, 156, 156), new(87, 87, 87));
        DarkTheme = new Theme(new(20, 20, 20), new(5, 5, 5), new(169, 169, 169), new(105, 105, 105));
        NeonTheme = new Theme(new(148, 0, 211), new(255, 255, 102), new(255, 20, 145), new(253, 158, 34));
    }

    public static T GetResource<T>(string resource) => (T)Application.Current.Resources[resource];

    public static void SetResource<T>(string resource, T value) => Application.Current.Resources[resource] = value;

    public static async ValueTask<string> GetReplaceBackgroundFile()
    {
        using (var sr = new StreamReader(ProgramFolder(ReplaceBackgroundFile)))
        {
            return await sr.ReadToEndAsync();
        }
    }

    public static async Task SetReplaceBackgroundFile(string backgroundPath)
    {
        using (var sw = new StreamWriter(ProgramFolder(ReplaceBackgroundFile)))
        {
            await sw.WriteAsync(backgroundPath);
        }
    }

    public static bool TryGetBackgroundImage(out BitmapImage background)
    {
        if (File.Exists(ProgramFolder(BackgroundImageFile)))
        {
            background = new BitmapImage(new Uri(ProgramFolder(BackgroundImageFile)));

            return true;
        }

        background = default!;
        return false;
    }

    public static bool TrySetBackgroundImage(string imagePath)
    {
        if (File.Exists(imagePath))
        {
            if (File.Exists(ProgramFolder(BackgroundImageFile))) File.Delete(ProgramFolder(BackgroundImageFile));

            Image.FromFile(imagePath).Save(ProgramFolder(BackgroundImageFile), ImageFormat.Png);

            return true;
        }

        return false;
    }

    public static void DeleteBackgroundImage() => File.Delete(ProgramFolder(BackgroundImageFile));

    public static string GetDownloadFolderPath()
    {
        using (var sr = new StreamReader(ProgramFolder(DownloadPathFile)))
        {
            return sr.ReadToEnd().Trim();
        }
    }

    public static async Task SetDownloadFolderPath(string path)
    {
        using (var sw = new StreamWriter(ProgramFolder(DownloadPathFile)))
        {
            await sw.WriteAsync(path);
        }
    }

    public static string GetSelectedThemeFile() => GetCurrentThemeJson();

    public static async ValueTask<Theme> GetSelectedTheme()
    {
        using (var sr = new StreamReader(ProgramFolder(GetCurrentThemeJson())))
        {
            return JsonConvert.DeserializeObject<Theme>(await sr.ReadToEndAsync())!;
        }
    }

    public static async Task OverrideTheme(Theme theme) => await OverrideTheme(CustomThemeJson, theme);

    public static async Task SetSelectedTheme(string themeJsonFile)
    {
        using (var sw = new StreamWriter(ProgramFolder(SelectedThemeFile)))
        {
            await sw.WriteAsync(themeJsonFile);
        }
    }

    public static async Task InitializeAppData()
    {
        if (!DoesDirectoryExist()) CreateDirectory();

        await CheckAndCreateFiles();
    }

    private static async Task OverrideTheme(string themeFile, Theme theme)
    {
        using (var sw = new StreamWriter(ProgramFolder(themeFile)))
        {
            await sw.WriteAsync(JsonConvert.SerializeObject(theme));
        }
    }

    private static string GetCurrentThemeJson()
    {
        using (var sr = new StreamReader(ProgramFolder(SelectedThemeFile)))
        {
            return sr.ReadToEnd().Trim();
        }
    }

    private static bool DoesDirectoryExist() => Directory.Exists(ProgramFolderPath);

    private static void CreateDirectory() => Directory.CreateDirectory(ProgramFolderPath);

    private static async Task CheckAndCreateFiles()
    {
        if (!File.Exists(ProgramFolder(StandardThemeJson)))
        {
            await OverrideTheme(StandardThemeJson, StandardTheme);
        }

        if (!File.Exists(ProgramFolder(LightThemeJson)))
        {
            await OverrideTheme(LightThemeJson, LightTheme);
        }

        if (!File.Exists(ProgramFolder(DarkThemeJson)))
        {
            await OverrideTheme(DarkThemeJson, DarkTheme);
        }

        if (!File.Exists(ProgramFolder(NeonThemeJson)))
        {
            await OverrideTheme(NeonThemeJson, NeonTheme);
        }

        if (!File.Exists(ProgramFolder(CustomThemeJson)))
        {
            await OverrideTheme(StandardTheme);
        }

        if (!File.Exists(ProgramFolder(SelectedThemeFile)))
        {
            using (var sw = new StreamWriter(ProgramFolder(SelectedThemeFile)))
            {
                await sw.WriteAsync(StandardThemeJson);
            }
        }

        if (!File.Exists(ProgramFolder(DownloadPathFile)))
        {
            using (var sw = new StreamWriter(ProgramFolder(DownloadPathFile)))
            {
                await sw.WriteAsync(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            }
        }

        if (!File.Exists(ProgramFolder(ReplaceBackgroundFile)))
        {
            using (var sw = new StreamWriter(ProgramFolder(ReplaceBackgroundFile)))
            {
                await sw.WriteAsync("");
            }
        }

        if (!File.Exists(ProgramFolder(ReadMeFile)))
        {
            using (var sw = new StreamWriter(ProgramFolder(ReadMeFile)))
            {
                await sw.WriteAsync("");
            }
        }
    }

    private static string ProgramFolder(string file) => Path.Combine(ProgramFolderPath, file);
}