namespace GbaLoader;

using GbaLoader.Helpers;
using System.Text.Json;

internal static class AppSettings
{
    private static JsonSettings _settings;

    static AppSettings()
    {
        if (LoadAppSettings("appsettings.json"))
            return;

        //Read from the readonly backup if the appsettings are corrupted
        if (LoadAppSettings("appsettings.backup.json"))
        {
            Save();
            return;
        }

        //Final resort, create using default values
        _settings = new JsonSettings
        {
            ApplicationName = "Mecha Boy Advance",
            ApplicationVersion = "Unknown",
            Message = "Settings file was corrupted, created a new one",
            OwnerName = "Unknown",
            RomDirectory = "/mnt/roms",
            AudioVolume = 5,
            SshAccess = true
        };
        Save();
    }

    private static bool LoadAppSettings(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;

            var json = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json))
                return false;

            _settings = JsonSerializer.Deserialize<JsonSettings>(
                json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })!;

            return true;
        }
        catch
        { 
            return false;
        }
    }

    static void Save()
    {
        FileHelper.SafeWrite("appsettings.json", JsonSerializer.Serialize(
            _settings,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));
    }

    public static string ApplicationName => _settings.ApplicationName;
    public static string ApplicationVersion => _settings.ApplicationVersion;
    public static string OwnerName => _settings.OwnerName;
    public static string Message => _settings.Message;
    public static string RomDirectory => _settings.RomDirectory;
    public static int AudioVolume => _settings.AudioVolume;
    public static bool SshAccess => _settings.SshAccess;

    public static void SetAudioVolume(int volume)
    {
        _settings.AudioVolume = volume;

        Save();
    }

    private class JsonSettings
    {
        public required string ApplicationName { get; set; }
        public required string ApplicationVersion { get; set; }
        public required string OwnerName { get; set; }
        public required string Message { get; set; }
        public required string RomDirectory { get; set; }
        public int AudioVolume { get; set; }
        public bool SshAccess { get; set; }
    }
}