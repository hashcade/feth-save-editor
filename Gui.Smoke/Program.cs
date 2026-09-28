using System;
using System.IO;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FethEditor.Gui;

AppBuilder.Configure<App>()
    .UseSkia()
    .WithInterFont()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

var window = new MainWindow();
window.Show();
if (args.Length > 0)
    window.LoadSave(args[0]);
Dispatcher.UIThread.RunJobs();

var tabs = window.FindControl<TabControl>("EditorTabs")
    ?? throw new InvalidOperationException("Editor tabs are missing.");
if (tabs.ItemCount == 0)
    throw new InvalidOperationException("Editor has no tabs.");
for (int index = 0; index < tabs.ItemCount; index++)
{
    tabs.SelectedIndex = index;
    Dispatcher.UIThread.RunJobs();
    var frame = window.CaptureRenderedFrame()
        ?? throw new InvalidOperationException($"Tab {index} did not render.");
    string screenshot = Path.Combine(Path.GetTempPath(), $"feth-editor-tab-{index}.png");
    frame.Save(screenshot, PngBitmapEncoderOptions.Default);
    Console.WriteLine(screenshot);
}

if (args.Length > 0)
{
    var items = window.FindControl<ListBox>("StorageList")!;
    var misc = window.FindControl<ListBox>("MiscList")!;
    if (items.ItemCount != 400 || misc.ItemCount != 223)
        throw new InvalidOperationException("Storage lists were not loaded.");
    window.FindControl<TextBox>("MiscAmount")!.Text = "42";
    window.FindControl<Button>("SetMisc")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (!misc.Items[0]!.ToString()!.EndsWith(" · 42", StringComparison.Ordinal))
        throw new InvalidOperationException("Misc item edit did not update the list.");

    var quests = window.FindControl<ListBox>("QuestList")!;
    window.FindControl<ComboBox>("QuestState")!.SelectedIndex = 5;
    window.FindControl<Button>("SetQuestStateButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (!quests.Items[0]!.ToString()!.EndsWith(" · 5", StringComparison.Ordinal))
        throw new InvalidOperationException("Quest edit did not update the list.");

    var supports = window.FindControl<ListBox>("CurrentSupportList")!;
    window.FindControl<TextBox>("CurrentSupportPoints")!.Text = "1234";
    window.FindControl<Button>("SetCurrentSupportButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (!supports.Items[0]!.ToString()!.EndsWith(" · 1234", StringComparison.Ordinal))
        throw new InvalidOperationException("Current support edit did not update the list.");

    var battalionExp = window.FindControl<TextBox>("BattalionExp")!;
    battalionExp.Text = "401";
    window.FindControl<Button>("SaveBattalionButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (battalionExp.Text != "401")
        throw new InvalidOperationException("Battalion edit was not retained.");
}
