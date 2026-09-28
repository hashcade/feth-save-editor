using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using SaveEditor;

namespace FethEditor.Gui;

internal static class UiStrings
{
    private sealed class Original(string value)
    {
        public string Value { get; } = value;
    }

    private static readonly ConditionalWeakTable<Control, Original> Originals = new();

    // The original editor's ChangeLanguage mapping uses game text IDs. These
    // labels follow the selected game database language.
    private static readonly Dictionary<string, (int Id, int Table)> GameStrings = new(StringComparer.Ordinal)
    {
        ["Player"] = (1455, 1),
        ["Money:"] = (652, 1),
        ["Instruct Level:"] = (1153, 1),
        ["Reputation:"] = (651, 1),
        ["Difficulty:"] = (970, 1),
        ["Monthly Statistics"] = (3937, 1),
        ["Activity Points"] = (1163, 1),
        ["Goddess Statue"] = (461, 1),
        ["Storage"] = (1814, 1),
        ["Characters"] = (1807, 1),
        ["Battalion"] = (385, 1),
        ["Quest"] = (1811, 1),
        ["Support Talks"] = (1060, 1),
        ["Stats"] = (893, 1),
        ["Skills"] = (569, 1),
        ["Class Exp"] = (540, 1),
        ["Abilities"] = (260, 1),
        ["Combat Arts"] = (264, 1),
        ["Experience"] = (2324, 1),
        ["Level"] = (2326, 1)
    };

    private static readonly Dictionary<enmLanguage, Dictionary<string, string>> Locales = new();

    private static Dictionary<string, string> LoadLocale(enmLanguage language)
    {
        using var stream = typeof(UiStrings).Assembly.GetManifestResourceStream($"FethEditor.Gui.Localization.{language}.json");
        if (stream is null) return new Dictionary<string, string>(StringComparer.Ordinal);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"Invalid UI language resource: {language}");
    }

    public static string Translate(string value, enmLanguage language)
    {
        if (!Locales.TryGetValue(language, out var locale))
            Locales[language] = locale = LoadLocale(language);
        if (locale.TryGetValue(value, out string? translated))
            return translated;
        if (Database.BinaryDatabase is not null && GameStrings.TryGetValue(value, out var text))
            return Database.GetString(text.Id, text.Table) + (value.EndsWith(':') ? ":" : "");
        return value;
    }

    public static string LanguageName(enmLanguage option, enmLanguage current)
    {
        string key = $"language.{option}";
        string translated = Translate(key, current);
        return translated == key ? option.GetDescription() : translated;
    }

    public static void Apply(Control root, enmLanguage language)
    {
        foreach (Control control in root.GetLogicalDescendants().OfType<Control>().Prepend(root))
        {
            if (control.TemplatedParent is not null) continue;
            switch (control)
            {
                case TextBlock text when control.Name is not ("Status" or "SelectedCharacter" or "SelectedSupport")
                    && control.GetVisualParent() is not ContentPresenter:
                    Replace(control, text.Text, value => text.Text = value, language);
                    break;
                case MenuItem menu when menu.Header is string header:
                    Replace(control, header, value => menu.Header = value, language);
                    break;
                case TabItem tab when tab.Header is string header:
                    Replace(control, header, value => tab.Header = value, language);
                    break;
                case Button button when button.Content is string content:
                    Replace(control, content, value => button.Content = value, language);
                    break;
                case ComboBoxItem item when item.Content is string content:
                    Replace(control, content, value => item.Content = value, language);
                    break;
                case TextBox input when input.PlaceholderText is string placeholder:
                    Replace(control, placeholder, value => input.PlaceholderText = value, language);
                    break;
            }
        }
    }

    private static void Replace(Control control, string? current, Action<string> set, enmLanguage language)
    {
        if (current is null) return;
        Original original = Originals.GetValue(control, _ => new Original(current));
        set(Translate(original.Value, language));
    }
}
