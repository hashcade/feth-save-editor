using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FethEditor.Gui;
using FethEditor.Core;

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

if (args.Length > 1)
{
    var systemWindow = new SystemWindow();
    systemWindow.Show();
    systemWindow.LoadSystem(args[1]);
    Dispatcher.UIThread.RunJobs();
    if (systemWindow.FindControl<ListBox>("SystemSlots")!.ItemCount != 37 ||
        systemWindow.FindControl<ListBox>("SystemFlags")!.ItemCount != 2464)
        throw new InvalidOperationException("System save lists were not loaded.");
    var systemFrame = systemWindow.CaptureRenderedFrame()
        ?? throw new InvalidOperationException("System editor did not render.");
    string systemScreenshot = Path.Combine(Path.GetTempPath(), "feth-editor-system.png");
    systemFrame.Save(systemScreenshot, PngBitmapEncoderOptions.Default);
    Console.WriteLine(systemScreenshot);
    var flagList = systemWindow.FindControl<ListBox>("SystemFlags")!;
    var row = (SystemFlagRow)flagList.Items[0]!;
    var firstCheck = flagList.GetVisualDescendants().OfType<CheckBox>().First();
    firstCheck.IsChecked = !firstCheck.IsChecked;
    Dispatcher.UIThread.RunJobs();
    if (row.Enabled != firstCheck.IsChecked)
        throw new InvalidOperationException("System flag checkbox did not update its model.");
    if (!systemWindow.FindControl<MenuItem>("WriteSystemMenu")!.IsEnabled)
        throw new InvalidOperationException("System flag edit did not enable saving.");

    var system = SystemBuffer.Open(args[1]);
    bool oldValue = system.GetFlag(0);
    system.SetFlag(0, !oldValue);
    string copy = Path.Combine(Path.GetTempPath(), $"feth-system-smoke-{Guid.NewGuid():N}");
    try
    {
        File.WriteAllBytes(copy, system.FinishedBytes());
        var reopened = SystemBuffer.Open(copy);
        if (reopened.GetFlag(0) == oldValue)
            throw new InvalidOperationException("System flag edit did not persist.");
    }
    finally
    {
        if (File.Exists(copy)) File.Delete(copy);
    }
}

var characterTabs = window.FindControl<TabControl>("CharacterTabs")!;
tabs.SelectedIndex = 2;
for (int index = 0; index < characterTabs.ItemCount; index++)
{
    characterTabs.SelectedIndex = index;
    Dispatcher.UIThread.RunJobs();
    var frame = window.CaptureRenderedFrame()
        ?? throw new InvalidOperationException($"Character tab {index} did not render.");
    string screenshot = Path.Combine(Path.GetTempPath(), $"feth-editor-character-{index}.png");
    frame.Save(screenshot, PngBitmapEncoderOptions.Default);
    Console.WriteLine(screenshot);
}

if (args.Length > 0)
{
    if (window.FindControl<ListBox>("DatabaseCharacters")!.ItemCount == 0 ||
        window.FindControl<ListBox>("DatabaseClasses")!.ItemCount == 0 ||
        window.FindControl<ListBox>("DatabaseItems")!.ItemCount == 0)
        throw new InvalidOperationException("Database viewer lists were not loaded.");
    var language = window.FindControl<ComboBox>("DatabaseLanguage")!;
    language.SelectedIndex = 11;
    if (!window.FindControl<TextBlock>("Status")!.Text!.Contains("not modified", StringComparison.Ordinal))
        throw new InvalidOperationException("Language switch modified the save or failed.");
    language.SelectedIndex = 1;
    var gameRows = window.FindControl<StackPanel>("GameRows")!;
    var difficulty = ((StackPanel)gameRows.Children[1]).Children.OfType<ComboBox>().Single();
    if (difficulty.ItemCount != 4)
        throw new InvalidOperationException("Difficulty choices were not loaded.");
    difficulty.SelectedIndex = difficulty.SelectedIndex == 0 ? 1 : 0;
    var historicalRank = window.FindControl<ComboBox>("NgPlusProfessorRank")!;
    if (historicalRank.SelectedIndex != 9)
        throw new InvalidOperationException("NG+ professor rank was not loaded from the sample save.");
    historicalRank.SelectedIndex = 8;
    if (!window.FindControl<TextBlock>("Status")!.Text!.Contains("changed", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("NG+ professor rank edit did not mark the save as changed.");

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

    var character = window.FindControl<ListBox>("CurrentCharacterList")!;
    if (character.ItemCount == 0)
        throw new InvalidOperationException("Character list was not loaded.");
    characterTabs.SelectedIndex = 3;
    var classExp = window.FindControl<TextBox>("SelectedClassExp")!;
    classExp.Text = "12";
    window.FindControl<Button>("SetCharacterClassButton")!
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (classExp.Text != "12")
        throw new InvalidOperationException("Character class experience was not retained.");
    if (!window.FindControl<TextBlock>("Status")!.Text!.Contains("changed", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Character edit did not mark the save as changed.");
}
