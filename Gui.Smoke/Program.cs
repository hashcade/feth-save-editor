using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FethEditor.Gui;
using FethEditor.Core;
using SaveEditor;
using SaveEditor.Structs;

Environment.SetEnvironmentVariable("FETH_EDITOR_LANGUAGE", "en_u");
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
if (args.Length > 0 && !window.FindControl<MenuItem>("SaveMenuItem")!.IsEnabled)
    throw new InvalidOperationException("A loaded save cannot be written before editing.");
if (!Avalonia.Input.DragDrop.GetAllowDrop(window))
    throw new InvalidOperationException("Dropping a slot save on the editor is disabled.");

var tabs = window.FindControl<TabControl>("EditorTabs")
    ?? throw new InvalidOperationException("Editor tabs are missing.");
var navigation = window.FindControl<TabStrip>("MainNavigation")
    ?? throw new InvalidOperationException("Main navigation is missing.");
var obsoleteSeparator = tabs.GetVisualDescendants().OfType<Border>()
    .FirstOrDefault(border => border.Name == "PART_BorderSeparator");
if (obsoleteSeparator?.IsVisible == true)
    throw new InvalidOperationException("The old tab separator is still visible.");
if (tabs.ItemCount != 9)
    throw new InvalidOperationException("Editor should show nine functional sections without a rank-only tab.");
navigation.SelectedIndex = 3;
Dispatcher.UIThread.RunJobs();
if (tabs.SelectedIndex != 3)
    throw new InvalidOperationException("Main navigation did not switch the editor page.");
navigation.SelectedIndex = 0;
Dispatcher.UIThread.RunJobs();
tabs.SelectedIndex = 1;
Dispatcher.UIThread.RunJobs();
var storageListCard = window.FindControl<Control>("StorageListCard")!;
var storageEditorCard = window.FindControl<Control>("StorageEditorCard")!;
var storageToolsCard = window.FindControl<Control>("StorageToolsCard")!;
var storageMiscCard = window.FindControl<Control>("StorageMiscCard")!;
var storageGiftCard = window.FindControl<Control>("StorageGiftCard")!;
double storageHeight = storageListCard.Bounds.Height;
if (Math.Abs(storageMiscCard.Bounds.Height - storageHeight) > 1 ||
    Math.Abs(storageGiftCard.Bounds.Height - storageHeight) > 1 ||
    Math.Abs(storageEditorCard.Bounds.Height + storageToolsCard.Bounds.Height + 10 - storageHeight) > 1)
    throw new InvalidOperationException("Storage cards do not fill equally tall columns.");
tabs.SelectedIndex = 3;
Dispatcher.UIThread.RunJobs();
var battalionListCard = window.FindControl<Control>("BattalionListCard")!;
var battalionEditorCard = window.FindControl<Control>("BattalionEditorCard")!;
if (battalionListCard.Bounds.Width >= battalionEditorCard.Bounds.Width ||
    Math.Abs(battalionListCard.Bounds.Height - battalionEditorCard.Bounds.Height) > 1)
    throw new InvalidOperationException("Battalion cards are not aligned with a narrower list column.");
tabs.SelectedIndex = 4;
Dispatcher.UIThread.RunJobs();
var questListCard = window.FindControl<Control>("QuestListCard")!;
var questEditorCard = window.FindControl<Control>("QuestEditorCard")!;
if (Math.Abs(questListCard.Bounds.Width - battalionListCard.Bounds.Width) > 1 ||
    Math.Abs(questEditorCard.Bounds.Width - battalionEditorCard.Bounds.Width) > 1 ||
    Math.Abs(questListCard.Bounds.Height - questEditorCard.Bounds.Height) > 1)
    throw new InvalidOperationException("Quest cards do not match the battalion layout.");
tabs.SelectedIndex = 5;
Dispatcher.UIThread.RunJobs();
var supportListCard = window.FindControl<Control>("SupportListCard")!;
var supportEditorCard = window.FindControl<Control>("SupportEditorCard")!;
if (Math.Abs(supportListCard.Bounds.Width - battalionListCard.Bounds.Width) > 1 ||
    Math.Abs(supportEditorCard.Bounds.Width - battalionEditorCard.Bounds.Width) > 1 ||
    Math.Abs(supportListCard.Bounds.Height - supportEditorCard.Bounds.Height) > 1)
    throw new InvalidOperationException("Support cards do not match the battalion layout.");
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
    if (!Avalonia.Input.DragDrop.GetAllowDrop(systemWindow) ||
        !systemWindow.FindControl<MenuItem>("WriteSystemMenu")!.IsEnabled)
        throw new InvalidOperationException("System save drag/drop or writing is disabled.");
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

string legacySystem = Path.Combine(Path.GetTempPath(), $"feth-system-v5-{Guid.NewGuid():N}");
string upgradedSystem = legacySystem + "-upgraded";
string? legacyBackup = null;
try
{
    var legacy = new byte[SystemSave.SIZE_SAVE_V5];
    BitConverter.GetBytes(5u).CopyTo(legacy, 4);
    BitConverter.GetBytes(legacy.Length).CopyTo(legacy, 8);
    legacy[SystemSave.SIZE_SAVE_HEADER] = 0x07;
    legacy[SystemSave.SIZE_SAVE_HEADER + SystemSaveData_V5.COUNT_SAVES * SaveFileInfo.SIZE] = 0x81;
    BitConverter.GetBytes(Util.CalcChecksum32(legacy[SystemSave.SIZE_SAVE_HEADER..])).CopyTo(legacy, 0);
    File.WriteAllBytes(legacySystem, legacy);

    var converted = SystemBuffer.Open(legacySystem);
    if (converted.SourceVersion != 5 || converted.HasInvalidChecksum ||
        converted.Data.Infos.Length != SystemSaveData_V7.COUNT_SAVES ||
        converted.Data.Infos[0].Flags != 0x07 || converted.Data.Infos[7].Flags != 0x11 ||
        !converted.GetFlag(0) || !converted.GetFlag(7))
        throw new InvalidOperationException("Version-5 system save conversion lost slots or flags.");

    File.WriteAllBytes(upgradedSystem, legacy);
    legacyBackup = VerifiedFileWriter.Write(upgradedSystem, converted.FinishedBytes(), path =>
    {
        var verified = SystemBuffer.Open(path);
        return verified.SourceVersion == 7 && !verified.HasInvalidChecksum;
    });
    if (legacyBackup is null || !File.ReadAllBytes(legacyBackup).SequenceEqual(legacy) ||
        SystemBuffer.Open(upgradedSystem).SourceVersion != 7 ||
        !File.ReadAllBytes(legacySystem).SequenceEqual(legacy))
        throw new InvalidOperationException("Overwriting a system save did not preserve its exact backup.");
    legacy[SystemSave.SIZE_SAVE_HEADER] ^= 0x01;
    File.WriteAllBytes(legacySystem, legacy);
    var damaged = SystemBuffer.Open(legacySystem);
    if (!damaged.HasInvalidChecksum)
        throw new InvalidOperationException("Bad system checksum was not reported.");
    File.WriteAllBytes(upgradedSystem, damaged.FinishedBytes());
    if (SystemBuffer.Open(upgradedSystem).HasInvalidChecksum)
        throw new InvalidOperationException("Writing a system save did not repair its checksum.");
}
finally
{
    if (File.Exists(legacySystem)) File.Delete(legacySystem);
    if (File.Exists(upgradedSystem)) File.Delete(upgradedSystem);
    if (legacyBackup is not null && File.Exists(legacyBackup)) File.Delete(legacyBackup);
}

var characterTabs = window.FindControl<TabControl>("CharacterTabs")!;
tabs.SelectedIndex = 2;
var classFlagsTab = characterTabs.Items.OfType<TabItem>()
    .Single(tab => tab.Header?.ToString() == "Class Flags");
if (!classFlagsTab.IsEnabled)
    throw new InvalidOperationException("Current-run class unlock editing is disabled.");
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
    characterTabs.SelectedItem = classFlagsTab;
    Dispatcher.UIThread.RunJobs();
    var classUnlocks = window.FindControl<StackPanel>("CurrentClassUnlockFlags")!;
    if (classUnlocks.Children.Count != Database.CLASS_FLAGS_COUNT)
        throw new InvalidOperationException("Current-run class unlock list is incomplete.");
    var firstUnlock = (CheckBox)classUnlocks.Children[0];
    firstUnlock.IsChecked = firstUnlock.IsChecked != true;
    if (!window.FindControl<MenuItem>("SaveMenuItem")!.IsEnabled)
        throw new InvalidOperationException("Editing a class unlock did not enable saving.");
}

if (args.Length > 0)
{
    if (window.FindControl<ListBox>("DatabaseCharacters")!.ItemCount == 0 ||
        window.FindControl<ListBox>("DatabaseClasses")!.ItemCount == 0 ||
        window.FindControl<ListBox>("DatabaseItems")!.ItemCount == 0)
        throw new InvalidOperationException("Database viewer lists were not loaded.");
    var language = window.FindControl<MenuItem>("LanguageMenu")!;
    var menuItems = language.Items.OfType<MenuItem>().ToArray();
    var supportedLanguages = Enum.GetValues<enmLanguage>();
    if (menuItems.Length != supportedLanguages.Length)
        throw new InvalidOperationException("A game database language is missing from the menu.");
    for (int index = 0; index < supportedLanguages.Length; index++)
    {
        menuItems[index].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        var expectedStorage = supportedLanguages[index] == enmLanguage.zh_hans
            ? "物品" : Database.GetString(1814, 1);
        if (tabs.Items.OfType<TabItem>().ElementAt(1).Header?.ToString() != expectedStorage)
            throw new InvalidOperationException($"Game text did not follow {supportedLanguages[index]}.");
    }
    ((MenuItem)language.Items[11]!).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    Dispatcher.UIThread.RunJobs();
    if (tabs.Items.OfType<TabItem>().First().Header?.ToString() != "主页"
        || tabs.Items.OfType<TabItem>().ElementAt(1).Header?.ToString() != "物品"
        || tabs.Items.OfType<TabItem>().ElementAt(3).Header?.ToString() != "骑士团"
        || tabs.Items.OfType<TabItem>().ElementAt(4).Header?.ToString() != "任务"
        || tabs.Items.OfType<TabItem>().ElementAt(5).Header?.ToString() != "支援对话")
        throw new InvalidOperationException("Chinese interface was not applied.");
    if (tabs.Items.OfType<TabItem>().ElementAt(7).Header?.ToString() != "继承角色"
        || ((MenuItem)language.Items[11]!).Header?.ToString() != "✓ 简体中文")
        throw new InvalidOperationException("Inheritance labels or language names were not localized.");
    if (!window.FindControl<TextBlock>("DatabaseCharacterDetails")!.Text!.Contains("贝雷特"))
        throw new InvalidOperationException("Database details did not follow the selected language.");
    tabs.SelectedIndex = 0;
    Dispatcher.UIThread.RunJobs();
    string chineseScreenshot = Path.Combine(Path.GetTempPath(), "feth-editor-zh.png");
    (window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Chinese UI did not render."))
        .Save(chineseScreenshot, PngBitmapEncoderOptions.Default);
    Console.WriteLine(chineseScreenshot);
    var untranslatedTabs = window.GetVisualDescendants().OfType<TextBlock>()
        .Where(text => text.Text is "Roster" or "Barracks" or "Quests" or "Support Conversations")
        .ToArray();
    if (untranslatedTabs.Length > 0)
        throw new InvalidOperationException("Visual tab headers are not localized: "
            + string.Join(", ", untranslatedTabs.Select(text => $"{text.Text} ({text.GetVisualParent()?.GetType().Name}, {text.Name})")));
    var visibleText = window.GetVisualDescendants().OfType<TextBlock>()
        .Where(text => text.IsVisible).Select(text => text.Text).ToArray();
    if (!visibleText.Contains("玩家") || !visibleText.Contains("游戏设定"))
        throw new InvalidOperationException("Overview section headings are not localized.");
    characterTabs.SelectedIndex = 0;
    for (int index = 0; index < tabs.ItemCount; index++)
    {
        tabs.SelectedIndex = index;
        Dispatcher.UIThread.RunJobs();
        string pageScreenshot = Path.Combine(Path.GetTempPath(), $"feth-editor-zh-tab-{index}.png");
        (window.CaptureRenderedFrame() ?? throw new InvalidOperationException($"Chinese page {index} did not render."))
            .Save(pageScreenshot, PngBitmapEncoderOptions.Default);
        Console.WriteLine(pageScreenshot);
    }
    tabs.SelectedIndex = 2;
    characterTabs.SelectedIndex = 0;
    Dispatcher.UIThread.RunJobs();
    var characterMainScroll = window.FindControl<ScrollViewer>("CharacterMainScroll")!;
    characterMainScroll.Offset = new Vector(0, characterMainScroll.Extent.Height);
    Dispatcher.UIThread.RunJobs();
    string characterItemsScreenshot = Path.Combine(Path.GetTempPath(), "feth-editor-zh-character-items.png");
    (window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Character items did not render."))
        .Save(characterItemsScreenshot, PngBitmapEncoderOptions.Default);
    Console.WriteLine(characterItemsScreenshot);
    characterMainScroll.Offset = new Vector(0, 0);
    foreach (var (index, buttonName, fileName) in new[]
    {
        (5, "UnlockAllAbilitiesButton", "feth-editor-zh-abilities.png"),
        (6, "UnlockAllArtsButton", "feth-editor-zh-combat-arts.png"),
    })
    {
        characterTabs.SelectedIndex = index;
        Dispatcher.UIThread.RunJobs();
        var unlockButton = window.FindControl<Button>(buttonName)!;
        if (Grid.GetColumn(unlockButton) != 1 ||
            unlockButton.HorizontalAlignment != Avalonia.Layout.HorizontalAlignment.Right ||
            unlockButton.Bounds.Width >= 200)
            throw new InvalidOperationException($"{buttonName} is not a compact right-aligned title action.");
        string actionScreenshot = Path.Combine(Path.GetTempPath(), fileName);
        (window.CaptureRenderedFrame() ?? throw new InvalidOperationException($"Character tab {index} did not render."))
            .Save(actionScreenshot, PngBitmapEncoderOptions.Default);
        Console.WriteLine(actionScreenshot);
    }
    tabs.SelectedIndex = 7;
    Dispatcher.UIThread.RunJobs();
    string inheritanceScreenshot = Path.Combine(Path.GetTempPath(), "feth-editor-zh-inheritance.png");
    (window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Inheritance UI did not render."))
        .Save(inheritanceScreenshot, PngBitmapEncoderOptions.Default);
    Console.WriteLine(inheritanceScreenshot);
    var inheritanceTabs = window.FindControl<TabControl>("InheritanceTabs")!;
    inheritanceTabs.SelectedIndex = 1;
    window.InvalidateVisual();
    Dispatcher.UIThread.RunJobs();
    string masteryScreenshot = Path.Combine(Path.GetTempPath(), "feth-editor-class-mastery.png");
    (window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Class mastery UI did not render."))
        .Save(masteryScreenshot, PngBitmapEncoderOptions.Default);
    Console.WriteLine(masteryScreenshot);
    ((MenuItem)language.Items[1]!).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    if (tabs.Items.OfType<TabItem>().First().Header?.ToString() != "Main")
        throw new InvalidOperationException("English interface was not restored.");
    var gameRows = window.FindControl<StackPanel>("GameRows")!;
    var difficulty = ((Grid)gameRows.Children[1]).Children.OfType<ComboBox>().Single();
    if (difficulty.ItemCount != 4)
        throw new InvalidOperationException("Difficulty choices were not loaded.");
    difficulty.SelectedIndex = difficulty.SelectedIndex == 0 ? 1 : 0;
    var historicalRank = window.FindControl<ComboBox>("NgPlusProfessorRank")!;
    if (historicalRank.SelectedIndex != 9)
        throw new InvalidOperationException("NG+ professor rank was not loaded from the sample save.");
    historicalRank.SelectedIndex = 8;
    if (!window.FindControl<MenuItem>("SaveMenuItem")!.IsEnabled)
        throw new InvalidOperationException("NG+ professor rank edit did not mark the save as changed.");
    tabs.SelectedIndex = 7;
    var classRows = window.FindControl<Panel>("ClassRows")!;
    var firstClass = classRows.Children.OfType<CheckBox>().First();
    if (!firstClass.IsEnabled)
        throw new InvalidOperationException("NG+ class mastery editing is disabled.");
    bool wasMastered = firstClass.IsChecked == true;
    firstClass.IsChecked = !wasMastered;
    var classSearch = window.FindControl<TextBox>("ClassSearch")!;
    classSearch.Text = "99";
    classSearch.Text = string.Empty;
    if (classRows.Children.OfType<CheckBox>().First().IsChecked == wasMastered)
        throw new InvalidOperationException("NG+ class mastery edit was not retained after refreshing the rows.");
    tabs.SelectedIndex = 2;
    var historicalCharacters = window.FindControl<ListBox>("CharacterList")!;
    if (!historicalCharacters.Items.OfType<string>().Any(name => name.Contains("Yuri", StringComparison.Ordinal))
        || !historicalCharacters.Items.OfType<string>().Any(name => name.Contains("Jeritza", StringComparison.Ordinal)))
        throw new InvalidOperationException("DLC and Jeritza NG+ names are missing.");
    if (historicalCharacters.Items.OfType<string>().Any(name => name.Contains("Aelfric", StringComparison.Ordinal)))
        throw new InvalidOperationException("Non-recruitable NPCs should not appear in the NG+ editor.");
    var supportPreset = window.FindControl<ComboBox>("SupportRankPreset")!;
    if (supportPreset.SelectedItem?.ToString()?.Contains("1001", StringComparison.Ordinal) != true)
        throw new InvalidOperationException("NG+ support point preset did not represent the stored value.");

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
    var battalionLabel = window.FindControl<StackPanel>("CharacterStatRows")!.Children
        .OfType<TextBlock>().LastOrDefault();
    if (battalionLabel?.Text?.StartsWith("Equipped Battalion:", StringComparison.Ordinal) != true)
        throw new InvalidOperationException("Character equipped battalion is not shown.");
    characterTabs.SelectedIndex = 3;
    var classExp = window.FindControl<TextBox>("SelectedClassExp")!;
    classExp.Text = "12";
    window.FindControl<Button>("SetCharacterClassButton")!
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (classExp.Text != "12")
        throw new InvalidOperationException("Character class experience was not retained.");
    characterTabs.SelectedIndex = 0;
    var stats = window.FindControl<StackPanel>("CharacterStatRows")!;
    var level = ((Grid)stats.Children[2]).Children.OfType<TextBox>().Single();
    level.Focus();
    level.Text = "4";
    ((Grid)stats.Children[3]).Children.OfType<TextBox>().Single().Focus();
    Dispatcher.UIThread.RunJobs();
    if (!character.Items[0]!.ToString()!.Contains("Lv:4", StringComparison.Ordinal))
        throw new InvalidOperationException("Character level edit did not update the list.");
    characterTabs.SelectedIndex = 2;
    var flags = window.FindControl<StackPanel>("CurrentCharacterFlags")!;
    var firstFlag = (CheckBox)flags.Children[0];
    firstFlag.IsChecked = !firstFlag.IsChecked;
    bool editedFlag = firstFlag.IsChecked == true;
    characterTabs.SelectedIndex = 0;
    characterTabs.SelectedIndex = 2;
    if (((CheckBox)window.FindControl<StackPanel>("CurrentCharacterFlags")!.Children[0]).IsChecked != editedFlag)
        throw new InvalidOperationException("Character edit did not mark the save as changed.");
}
