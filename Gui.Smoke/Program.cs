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

int[] professorThresholds = [0, 100, 1500, 3600, 6400, 10900, 16300, 24000, 32800, 44500];
for (int rank = 0; rank < professorThresholds.Length; rank++)
{
    if (Database.TeacherLevelupRank[rank] != professorThresholds[rank]
        || Database.GetProfessorRankFromExperience(professorThresholds[rank]) != rank
        || rank > 0 && Database.GetProfessorRankFromExperience(professorThresholds[rank] - 1) != rank - 1)
        throw new InvalidOperationException($"Professor experience threshold for rank {rank} is incorrect.");
}

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
if (args.Length > 0 && window.FindControl<TextBlock>("PlayerCardTitle")?.Text != "Player")
    throw new InvalidOperationException("Player card title was mistranslated by the game database.");
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
var englishTabs = tabs.Items.OfType<TabItem>().Select(tab => tab.Header?.ToString()).ToArray();
if (englishTabs[2] != "Roster" || englishTabs[5] != "Support"
    || englishTabs[7] != "NG+ Roster" || englishTabs[8] != "NG+ Support")
    throw new InvalidOperationException("English navigation labels are inconsistent.");
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
tabs.SelectedIndex = 7;
Dispatcher.UIThread.RunJobs();
var inheritanceListCard = window.FindControl<Control>("InheritanceCharacterListCard")!;
var inheritanceEditorCard = window.FindControl<Control>("InheritanceCharacterEditorCard")!;
if (inheritanceListCard.Bounds.Width >= inheritanceEditorCard.Bounds.Width ||
    Math.Abs(inheritanceListCard.Bounds.Height - inheritanceEditorCard.Bounds.Height) > 1)
    throw new InvalidOperationException("Inheritance character cards are not aligned.");
var skillRows = window.FindControl<Avalonia.Controls.Primitives.UniformGrid>("SkillRows")!;
if (args.Length > 0)
{
    var firstSkillRow = (Grid)skillRows.Children[0];
    var secondSkillRow = (Grid)skillRows.Children[1];
    var firstSkillRank = firstSkillRow.Children.OfType<ComboBox>().Single();
    var secondSkillLabel = secondSkillRow.Children.OfType<TextBlock>().Single();
    var secondSkillRank = secondSkillRow.Children.OfType<ComboBox>().Single();
    double betweenSkills = secondSkillRow.Bounds.X + secondSkillLabel.Bounds.X
        - firstSkillRow.Bounds.X - firstSkillRank.Bounds.Right;
    double labelToRank = secondSkillRank.Bounds.X - secondSkillLabel.Bounds.Right;
    if (betweenSkills <= labelToRank || Math.Abs(firstSkillRank.Bounds.Width - secondSkillRank.Bounds.Width) > 1)
        throw new InvalidOperationException("Inheritance skill fields are not grouped and aligned by column.");
}
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
    window.Height = 1000;
    tabs.SelectedIndex = 0;
    Dispatcher.UIThread.RunJobs();
    string mainScreenshot = Path.Combine(Path.GetTempPath(), "feth-editor-main-full.png");
    (window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Main page did not render."))
        .Save(mainScreenshot, PngBitmapEncoderOptions.Default);
    Console.WriteLine(mainScreenshot);
    window.Height = 780;
    Dispatcher.UIThread.RunJobs();
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
    systemWindow.SetLanguage(enmLanguage.zh_hans);
    Database.Init(enmLanguage.zh_hans);
    systemWindow.LoadSystem(args[1]);
    Dispatcher.UIThread.RunJobs();
    if (systemWindow.Title != "系统存档编辑器"
        || systemWindow.FindControl<TextBlock>("SystemStatus")!.Text != "系统存档已加载。修改只保存在内存中，另存副本后才会写入。")
        throw new InvalidOperationException("System save editor was not localized.");
    string chineseSystemScreenshot = Path.Combine(Path.GetTempPath(), "feth-editor-zh-system.png");
    (systemWindow.CaptureRenderedFrame() ?? throw new InvalidOperationException("Chinese system editor did not render."))
        .Save(chineseSystemScreenshot, PngBitmapEncoderOptions.Default);
    Console.WriteLine(chineseSystemScreenshot);
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
        var expectedStorage = supportedLanguages[index] == enmLanguage.zh_hans ? "物品"
            : supportedLanguages[index] is enmLanguage.en_u or enmLanguage.en_e ? "Items"
            : Database.GetString(1814, 1);
        if (tabs.Items.OfType<TabItem>().ElementAt(1).Header?.ToString() != expectedStorage)
            throw new InvalidOperationException($"Navigation label did not follow {supportedLanguages[index]}.");
    }
    ((MenuItem)language.Items[11]!).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    Dispatcher.UIThread.RunJobs();
    if (tabs.Items.OfType<TabItem>().First().Header?.ToString() != "主页"
        || tabs.Items.OfType<TabItem>().ElementAt(1).Header?.ToString() != "物品"
        || tabs.Items.OfType<TabItem>().ElementAt(3).Header?.ToString() != "骑士团"
        || tabs.Items.OfType<TabItem>().ElementAt(4).Header?.ToString() != "任务"
        || tabs.Items.OfType<TabItem>().ElementAt(5).Header?.ToString() != "支援")
        throw new InvalidOperationException("Chinese interface was not applied.");
    if (tabs.Items.OfType<TabItem>().ElementAt(7).Header?.ToString() != "继承名册"
        || ((MenuItem)language.Items[11]!).Header?.ToString() != "✓ 简体中文")
        throw new InvalidOperationException("Inheritance labels or language names were not localized.");
    if (window.FindControl<TextBox>("StorageSearch")!.PlaceholderText != "搜索…"
        || window.FindControl<TextBox>("ClassSearch")!.PlaceholderText != "搜索…")
        throw new InvalidOperationException("Shared search placeholder was not localized.");
    if (!window.FindControl<TextBox>("DatabaseCharacterDetails")!.Text!.Contains("贝雷特"))
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
    tabs.SelectedIndex = 2;
    Dispatcher.UIThread.RunJobs();
    var characterStatRows = window.FindControl<StackPanel>("CharacterStatRows")!;
    if (!characterStatRows.Children.OfType<Grid>().SelectMany(row => row.Children.OfType<TextBlock>())
        .Any(label => label.Text == "力量"))
        throw new InvalidOperationException("Dynamic character stat labels were not localized.");
    if (tabs.Items.OfType<TabItem>().ElementAt(2).Header?.ToString() != "名册"
        || tabs.Items.OfType<TabItem>().ElementAt(7).Header?.ToString() != "继承名册"
        || tabs.Items.OfType<TabItem>().ElementAt(8).Header?.ToString() != "继承支援")
        throw new InvalidOperationException("NG+ navigation labels changed unexpectedly.");
    tabs.SelectedIndex = 0;
    characterTabs.SelectedIndex = 0;
    for (int index = 0; index < tabs.ItemCount; index++)
    {
        tabs.SelectedIndex = index;
        Dispatcher.UIThread.RunJobs();
        string pageScreenshot = Path.Combine(Path.GetTempPath(), $"feth-editor-zh-tab-{index}.png");
        (window.CaptureRenderedFrame() ?? throw new InvalidOperationException($"Chinese page {index} did not render."))
            .Save(pageScreenshot, PngBitmapEncoderOptions.Default);
        Console.WriteLine(pageScreenshot);
        if (index == 6)
        {
            var databasePages = window.FindControl<TabControl>("DatabaseTabs")!;
            foreach (var (databaseIndex, name) in new[] { (1, "classes"), (2, "items") })
            {
                databasePages.SelectedIndex = databaseIndex;
                Dispatcher.UIThread.RunJobs();
                string databaseScreenshot = Path.Combine(Path.GetTempPath(), $"feth-editor-zh-database-{name}.png");
                (window.CaptureRenderedFrame() ?? throw new InvalidOperationException($"Database {name} did not render."))
                    .Save(databaseScreenshot, PngBitmapEncoderOptions.Default);
                Console.WriteLine(databaseScreenshot);
            }
            databasePages.SelectedIndex = 0;
        }
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
    tabs.SelectedIndex = 0;
    Dispatcher.UIThread.RunJobs();
    var currentProfessorRank = window.FindControl<ComboBox>("CurrentProfessorRank")!;
    var professorExperience = window.FindControl<TextBox>("InstructExpInput")!;
    if (currentProfessorRank.ItemCount != professorThresholds.Length
        || currentProfessorRank.SelectedIndex != Database.GetProfessorRankFromExperience(
            int.Parse(professorExperience.Text!, System.Globalization.CultureInfo.InvariantCulture)))
        throw new InvalidOperationException("Current professor rank was not mapped from experience.");
    professorExperience.Text = "1517";
    Dispatcher.UIThread.RunJobs();
    if (currentProfessorRank.SelectedItem?.ToString() != "D" || professorExperience.Text != "1517")
        throw new InvalidOperationException("Typing professor experience did not update the rank exactly.");
    currentProfessorRank.SelectedIndex = 8;
    if (professorExperience.Text != "32800"
        || window.FindControl<ComboBox>("NgPlusProfessorRank")!.SelectedIndex != 9)
        throw new InvalidOperationException("Current professor rank did not use its own experience threshold.");
    currentProfessorRank.SelectedIndex = 9;
    if (professorExperience.Text != "44500")
        throw new InvalidOperationException("Choosing A+ professor rank did not set its experience threshold.");
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
    tabs.SelectedIndex = 8;
    Dispatcher.UIThread.RunJobs();
    var supportPreset = window.FindControl<ComboBox>("SupportRankPreset")!;
    if (supportPreset.ItemCount != 8 || supportPreset.SelectedItem?.ToString() != "S")
        throw new InvalidOperationException("NG+ support rank was not mapped from the stored value.");
    var inheritedSupportPoints = window.FindControl<TextBox>("InheritedSupportPoints")!;
    if (inheritedSupportPoints.Text != "1001")
        throw new InvalidOperationException("NG+ support points were not shown exactly.");
    inheritedSupportPoints.Text = "750";
    Dispatcher.UIThread.RunJobs();
    if (supportPreset.SelectedItem?.ToString() != "A" || inheritedSupportPoints.Text != "750")
        throw new InvalidOperationException($"NG+ support points did not update the rank without rounding: rank={supportPreset.SelectedItem}, points={inheritedSupportPoints.Text}.");
    if (window.FindControl<ListBox>("SupportList")!.Items[0]!.ToString()!.EndsWith(" · S", StringComparison.Ordinal) != true)
        throw new InvalidOperationException("NG+ support edit was applied before pressing Set.");
    window.FindControl<Button>("SetInheritedSupportButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (inheritedSupportPoints.Text != "750")
        throw new InvalidOperationException("NG+ support points lost their exact value after saving.");
    supportPreset.SelectedIndex = 2;
    if (inheritedSupportPoints.Text != "201")
        throw new InvalidOperationException("NG+ support rank did not fill its point threshold.");
    window.FindControl<Button>("SetInheritedSupportButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (window.FindControl<ListBox>("SupportList")!.Items[0]!.ToString()!.EndsWith(" · C+", StringComparison.Ordinal) != true)
        throw new InvalidOperationException("NG+ support rank edit did not update the list.");
    supportPreset.SelectedIndex = 7;
    window.FindControl<Button>("SetInheritedSupportButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    tabs.SelectedIndex = 1;
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

    tabs.SelectedIndex = 5;
    Dispatcher.UIThread.RunJobs();
    var supports = window.FindControl<ListBox>("CurrentSupportList")!;
    var currentSupportRank = window.FindControl<ComboBox>("CurrentSupportRank")!;
    var currentSupportPoints = window.FindControl<TextBox>("CurrentSupportPoints")!;
    if (currentSupportRank.ItemCount != 8)
        throw new InvalidOperationException("Current support ranks were not loaded.");
    string originalCurrentPoints = currentSupportPoints.Text ?? string.Empty;
    currentSupportPoints.Text = "750";
    Dispatcher.UIThread.RunJobs();
    if (currentSupportRank.SelectedItem?.ToString() != "A" || currentSupportPoints.Text != "750")
        throw new InvalidOperationException("Current support points did not update the rank without rounding.");
    window.FindControl<Button>("SetCurrentSupportButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (currentSupportPoints.Text != "750" ||
        !supports.Items[0]!.ToString()!.EndsWith(" · A", StringComparison.Ordinal))
        throw new InvalidOperationException("Current support edit did not retain exact points.");
    currentSupportRank.SelectedIndex = 2;
    if (currentSupportPoints.Text != "201")
        throw new InvalidOperationException("Current support rank did not fill its point threshold.");
    window.FindControl<Button>("SetCurrentSupportButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (!supports.Items[0]!.ToString()!.EndsWith(" · C+", StringComparison.Ordinal))
        throw new InvalidOperationException("Current support rank edit did not update the list.");
    currentSupportPoints.Text = originalCurrentPoints;
    window.FindControl<Button>("SetCurrentSupportButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    tabs.SelectedIndex = 3;
    var battalionExp = window.FindControl<TextBox>("BattalionExp")!;
    battalionExp.Text = "401";
    window.FindControl<Button>("SaveBattalionButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if (battalionExp.Text != "401")
        throw new InvalidOperationException("Battalion edit was not retained.");

    tabs.SelectedIndex = 2;
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
    if (!character.Items[0]!.ToString()!.Contains("Lv.4", StringComparison.Ordinal))
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

    tabs.SelectedIndex = 1;
    Dispatcher.UIThread.RunJobs();
    var miscSearch = window.FindControl<TextBox>("MiscSearch")!;
    miscSearch.Text = "010 ·";
    Dispatcher.UIThread.RunJobs();
    if (misc.ItemCount != 1 || !misc.Items[0]!.ToString()!.StartsWith("010 ·", StringComparison.Ordinal))
        throw new InvalidOperationException($"Misc search did not select the requested source row: {misc.ItemCount} rows; first={misc.Items.FirstOrDefault()}.");
    window.FindControl<TextBox>("MiscAmount")!.Text = "43";
    window.FindControl<Button>("SetMisc")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    miscSearch.Text = string.Empty;
    Dispatcher.UIThread.RunJobs();
    if (!misc.Items[10]!.ToString()!.EndsWith(" · 43", StringComparison.Ordinal)
        || !misc.Items[0]!.ToString()!.EndsWith(" · 42", StringComparison.Ordinal))
        throw new InvalidOperationException("Editing a filtered misc row changed the wrong source item.");

    tabs.SelectedIndex = 4;
    Dispatcher.UIThread.RunJobs();
    var questSearch = window.FindControl<TextBox>("QuestSearch")!;
    questSearch.Text = "001 ·";
    Dispatcher.UIThread.RunJobs();
    if (quests.ItemCount != 1 || !quests.Items[0]!.ToString()!.StartsWith("001 ·", StringComparison.Ordinal))
        throw new InvalidOperationException("Quest search did not select the requested source row.");
    window.FindControl<ComboBox>("QuestState")!.SelectedIndex = 4;
    window.FindControl<Button>("SetQuestStateButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    questSearch.Text = string.Empty;
    Dispatcher.UIThread.RunJobs();
    if (!quests.Items[1]!.ToString()!.EndsWith(" · 4", StringComparison.Ordinal)
        || !quests.Items[0]!.ToString()!.EndsWith(" · 5", StringComparison.Ordinal))
        throw new InvalidOperationException("Editing a filtered quest changed the wrong source item.");

    var databaseTabs = window.FindControl<TabControl>("DatabaseTabs")!;
    foreach (var (page, databasePage, searchName, listName) in new[]
    {
        (1, -1, "StorageSearch", "StorageList"),
        (1, -1, "GiftSearch", "GiftList"),
        (2, -1, "CurrentCharacterSearch", "CurrentCharacterList"),
        (3, -1, "BattalionSearch", "BattalionList"),
        (5, -1, "CurrentSupportSearch", "CurrentSupportList"),
        (6, 0, "DatabaseCharacterSearch", "DatabaseCharacters"),
        (6, 1, "DatabaseClassSearch", "DatabaseClasses"),
        (6, 2, "DatabaseItemSearch", "DatabaseItems")
    })
    {
        tabs.SelectedIndex = page;
        if (databasePage >= 0) databaseTabs.SelectedIndex = databasePage;
        Dispatcher.UIThread.RunJobs();
        var list = window.FindControl<ListBox>(listName)!;
        int originalCount = list.ItemCount;
        var search = window.FindControl<TextBox>(searchName)!;
        search.Text = "___not_a_real_entry___";
        Dispatcher.UIThread.RunJobs();
        if (list.ItemCount != 0)
            throw new InvalidOperationException($"{searchName} did not filter its list.");
        search.Text = string.Empty;
        Dispatcher.UIThread.RunJobs();
        if (list.ItemCount != originalCount)
            throw new InvalidOperationException($"{searchName} did not restore its list.");
    }

    tabs.SelectedIndex = 7;
    inheritanceTabs.SelectedIndex = 0;
    Dispatcher.UIThread.RunJobs();
    var inheritedSkills = window.FindControl<Panel>("SkillRows")!;
    ((Grid)inheritedSkills.Children[0]).Children.OfType<ComboBox>().Single().SelectedIndex = 0;
    var maxInheritedSkills = window.FindControl<Button>("MaxInheritedSkillsButton")!;
    if (maxInheritedSkills.HorizontalAlignment != Avalonia.Layout.HorizontalAlignment.Right ||
        !maxInheritedSkills.IsVisible ||
        maxInheritedSkills.Bounds.Width >= 200)
        throw new InvalidOperationException("Inherited skill action is not compact and right-aligned.");
    var tabHeader = window.FindControl<TabControl>("InheritanceTabs")!;
    var buttonOrigin = maxInheritedSkills.TranslatePoint(new Point(0, 0), window);
    var tabsOrigin = tabHeader.TranslatePoint(new Point(0, 0), window);
    if (buttonOrigin is null || tabsOrigin is null || Math.Abs(buttonOrigin.Value.Y - tabsOrigin.Value.Y) > 20)
        throw new InvalidOperationException("Inherited skill action is not aligned with the tabs.");
    maxInheritedSkills.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    inheritedSkills = window.FindControl<Panel>("SkillRows")!;
    if (inheritedSkills.Children.OfType<Grid>()
        .Any(row => row.Children.OfType<ComboBox>().Single().SelectedIndex != 11))
        throw new InvalidOperationException("Unlock All did not maximize every inherited skill rank.");
}
