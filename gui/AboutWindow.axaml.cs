using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaveEditor;

namespace FethEditor.Gui;

public partial class AboutWindow : Window
{
    private const string ProjectUrl = "https://github.com/jinghaihan/feth-save-editor";

    public AboutWindow() : this(enmLanguage.en_u) { }

    public AboutWindow(enmLanguage language)
    {
        InitializeComponent();
        Title = UiStrings.Translate("About", language);
        VersionLabel.Text = UiStrings.Translate("Version", language) + ":";
        VersionValue.Text = GetVersion();
    }

    public static string GetVersion() =>
        typeof(AboutWindow).Assembly.GetName().Version?.ToString(3) ?? "unknown";

    private async void OpenProject_Click(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri(ProjectUrl));
}
