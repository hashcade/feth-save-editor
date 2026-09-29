using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using SaveEditor;
using SukiUI;

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
        ["Money:"] = (652, 1),
        ["Instruct Level:"] = (1153, 1),
        ["Reputation:"] = (651, 1),
        ["Difficulty:"] = (970, 1),
        ["Monthly Statistics"] = (3937, 1),
        ["Activity Points"] = (1163, 1),
        ["Goddess Statue"] = (461, 1),
        ["Stats"] = (893, 1),
        ["Skills"] = (569, 1),
        ["Abilities"] = (260, 1),
        ["Combat Arts"] = (264, 1),
        ["Experience"] = (2324, 1),
        ["Level"] = (2326, 1)
    };

    // Keep the editor's English navigation consistent, while retaining the
    // game's own menu terms for languages without a dedicated UI resource.
    private static readonly Dictionary<string, int> NavigationGameStrings = new(StringComparer.Ordinal)
    {
        ["Items"] = 1814,
        ["Roster"] = 1807,
        ["Characters"] = 1807,
        ["Battalions"] = 385,
        ["Quests"] = 1811,
        ["Support"] = 1060
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
        if (Database.BinaryDatabase is not null && language is not (enmLanguage.en_u or enmLanguage.en_e))
        {
            if (value is "NG+ Roster") return "NG+ " + Translate("Roster", language);
            if (value is "NG+ Support") return "NG+ " + Translate("Support", language);
            if (NavigationGameStrings.TryGetValue(value, out int id))
                return Database.GetString(id, 1);
        }
        if (Database.BinaryDatabase is not null && GameStrings.TryGetValue(value, out var text))
            return Database.GetString(text.Id, text.Table) + (value.EndsWith(':') ? ":" : "");
        return value;
    }

    public static string Format(string value, enmLanguage language, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Translate(value, language), arguments);

    public static string LanguageName(enmLanguage option, enmLanguage current)
    {
        string key = $"language.{option}";
        string translated = Translate(key, current);
        return translated == key ? option.GetDescription() : translated;
    }

    public static void Apply(Control root, enmLanguage language)
    {
        string sukiLocale = language switch
        {
            enmLanguage.zh_hans => "zh-CN",
            enmLanguage.jp => "ja-JP",
            enmLanguage.de => "de-DE",
            enmLanguage.fr_u or enmLanguage.fr_e => "fr-FR",
            enmLanguage.es_u or enmLanguage.es_e => "es-ES",
            enmLanguage.it => "it-IT",
            _ => "en-US"
        };
        var theme = SukiTheme.GetInstance();
        if (theme.Locale != sukiLocale) theme.Locale = sukiLocale;
        foreach (Control control in root.GetLogicalDescendants().OfType<Control>().Prepend(root))
        {
            if (control.TemplatedParent is not null) continue;
            switch (control)
            {
                case TextBlock text when control.Name is not ("Status" or "BattalionUsage"
                    or "StorageCount" or "CurrentCharacterTitle" or "CurrentClassInfo"
                    or "EquippedBattalionValue" or "StorageSlotLabel")
                    && (control.GetVisualParent() is not ContentPresenter presenter
                        || ReferenceEquals(presenter.Content, control)):
                    Replace(control, text.Text, value => text.Text = value, language);
                    break;
                case MenuItem menu when control.Name is not ("FileMenu" or "LanguageMenu")
                    && menu.Header is string header:
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
