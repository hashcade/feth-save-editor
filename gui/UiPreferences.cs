using System;
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
        if (TryLanguage(forced, out enmLanguage language)) return CanonicalLanguage(language);

        try
        {
            if (File.Exists(SettingsPath) && TryLanguage(File.ReadAllText(SettingsPath).Trim(), out language))
                return CanonicalLanguage(language);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return enmLanguage.en_u;
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

    private static enmLanguage CanonicalLanguage(enmLanguage language) => language switch
    {
        enmLanguage.en_e => enmLanguage.en_u,
        enmLanguage.fr_e => enmLanguage.fr_u,
        enmLanguage.es_e => enmLanguage.es_u,
        _ => language
    };
}
