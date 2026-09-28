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
}
