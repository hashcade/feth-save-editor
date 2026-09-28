using System;
using System.Globalization;
using System.IO;
using SaveEditor;

namespace FethEditor.Gui;

internal static class UiPreferences
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FethEditor", "language.txt");

    public static enmLanguage Load()
    {
        string? forced = Environment.GetEnvironmentVariable("FETH_EDITOR_LANGUAGE");
        if (TryLanguage(forced, out enmLanguage language)) return language;

        try
        {
            if (File.Exists(SettingsPath) && TryLanguage(File.ReadAllText(SettingsPath).Trim(), out language))
                return language;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        string system = CultureInfo.CurrentUICulture.Name;
        return system.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase)
            || system.Equals("zh-CN", StringComparison.OrdinalIgnoreCase)
                ? enmLanguage.zh_hans : enmLanguage.en_u;
    }

    public static void Save(enmLanguage language)
    {
        if (Environment.GetEnvironmentVariable("FETH_EDITOR_LANGUAGE") is not null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, language.ToString());
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool TryLanguage(string? value, out enmLanguage language) =>
        Enum.TryParse(value, true, out language) && Enum.IsDefined(language);
}
