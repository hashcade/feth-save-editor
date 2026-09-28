using System;
using System.IO;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
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
if (tabs.ItemCount != 3)
    throw new InvalidOperationException($"Expected three functional tabs, found {tabs.ItemCount}.");
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
