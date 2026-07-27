using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WindowsAdminShortcuts;

internal enum AppLanguage
{
    Russian,
    English,
    Uzbek
}

internal enum AppTheme
{
    Light,
    Dark
}

internal sealed record AppSettings(AppLanguage Language, AppTheme Theme);

internal static class AppSettingsService
{
    internal const string SettingsFileName = "settings.json";

    private static readonly object Sync = new();
    private static AppSettings _current = CreateDefaults();
    private static string _settingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsAdminShortcuts");

    internal static event EventHandler? Changed;

    internal static AppSettings Current
    {
        get
        {
            lock (Sync)
            {
                return _current;
            }
        }
    }

    internal static void Initialize()
    {
        lock (Sync)
        {
            _current = Load(_settingsDirectory);
        }
    }

    internal static void SetLanguage(AppLanguage language)
    {
        Update(Current with { Language = language });
    }

    internal static void SetTheme(AppTheme theme)
    {
        Update(Current with { Theme = theme });
    }

    internal static AppSettings Load(string directory)
    {
        string path = Path.Combine(directory, SettingsFileName);
        if (!File.Exists(path))
        {
            return CreateDefaults();
        }

        SettingsDocument document;
        try
        {
            document = JsonSerializer.Deserialize<SettingsDocument>(
                File.ReadAllText(path, Encoding.UTF8))
                ?? throw new InvalidDataException("Settings document is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid settings file: {path}", ex);
        }

        if (!Enum.TryParse(document.Language, ignoreCase: true, out AppLanguage language) ||
            !Enum.IsDefined(language))
        {
            throw new InvalidDataException($"Unsupported interface language in {path}.");
        }

        if (!Enum.TryParse(document.Theme, ignoreCase: true, out AppTheme theme) ||
            !Enum.IsDefined(theme))
        {
            throw new InvalidDataException($"Unsupported interface theme in {path}.");
        }

        return new AppSettings(language, theme);
    }

    internal static void Save(string directory, AppSettings settings)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, SettingsFileName);
        string temporaryPath = Path.Combine(
            directory,
            $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");
        string json = JsonSerializer.Serialize(
            new SettingsDocument(settings.Language.ToString(), settings.Theme.ToString()),
            new JsonSerializerOptions { WriteIndented = true });

        try
        {
            File.WriteAllText(
                temporaryPath,
                $"{json}\r\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
            if (Load(directory) != settings)
            {
                throw new InvalidDataException("Saved interface settings failed verification.");
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    internal static void UseTransient(AppSettings settings)
    {
        lock (Sync)
        {
            _current = settings;
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static void Update(AppSettings settings)
    {
        lock (Sync)
        {
            if (_current == settings)
            {
                return;
            }

            Save(_settingsDirectory, settings);
            _current = settings;
        }

        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static AppSettings CreateDefaults()
    {
        string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        AppLanguage appLanguage = language.Equals("uz", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Uzbek
            : language.Equals("ru", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.Russian
                : AppLanguage.English;
        return new AppSettings(appLanguage, AppTheme.Light);
    }

    private sealed record SettingsDocument(string Language, string Theme);
}
