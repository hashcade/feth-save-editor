using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FethEditor.Core;
using SaveEditor;

namespace FethEditor.Gui;

public partial class MainWindow : Window
{
    private static readonly string[] SkillRanks =
        ["E", "E+", "D", "D+", "C", "C+", "B", "B+", "A", "A+", "S", "S+"];

    private static readonly Choice[] SupportRanks =
    [
        new(0, "None"),
        new(101, "C"),
        new(201, "C+"),
        new(301, "B"),
        new(451, "B+"),
        new(601, "A"),
        new(801, "A+"),
        new(1001, "S")
    ];

    private static readonly (string Label, string Path)[] GameFields =
    [
        ("Chapter:", "Player.Chapter"),
        ("Difficulty:", "Player.Difficulty"),
        ("Gamestyle:", "Player.Gamestyle"),
        ("Route:", "Player.Route"),
        ("Battle MapId:", "Player.MapID")
    ];

    private static readonly (string Label, string Path)[] ActivityFields =
    [
        ("Explore:", "Activities.ActivityExplore"),
        ("Lesson:", "Activities.ActivityLesson"),
        ("Battle:", "Activities.ActivityBattle")
    ];

    private static readonly (string Label, string Path)[] StatueFields =
    [
        ("Statue1:", "Activities.Statue1"),
        ("Statue2:", "Activities.Statue2"),
        ("Statue3:", "Activities.Statue3"),
        ("Statue4:", "Activities.Statue4")
    ];

    private static readonly (string Label, string Path)[] StatisticFields =
    [
        ("PlayLog_Wark:", "Activities.PlayLog_Wark"),
        ("PlayLog_Lecture:", "Activities.PlayLog_Lecture"),
        ("PlayLog_ToBtl:", "Activities.PlayLog_ToBtl"),
        ("PlayLog_Rest:", "Activities.PlayLog_Rest"),
        ("PlayLog_Trnmnt:", "Activities.PlayLog_Trnmnt"),
        ("PlayLog_Sing:", "Activities.PlayLog_Sing"),
        ("PlayLog_Lunch:", "Activities.PlayLog_Lunch"),
        ("PlayLog_Cooking:", "Activities.PlayLog_Cooking"),
        ("PlayLog_Drill:", "Activities.PlayLog_Drill"),
        ("PlayLog_Teaparty:", "Activities.PlayLog_Teaparty"),
        ("PlayLog_SCOUT:", "Activities.PlayLog_SCOUT")
    ];

    private SaveBuffer? _save;
    private string? _sourcePath;
    private readonly List<int> _visibleCharacters = [];
    private readonly List<int> _visibleSupports = [];
    private int _selectedCharacter = -1;
    private int _selectedSupport = -1;
    private bool _loading;
    private bool _databaseReady;
    private enmLanguage _databaseLanguage = UiPreferences.Load();
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(6) };

    public MainWindow()
    {
        InitializeComponent();
        DragDrop.AddDragOverHandler(this, SaveDragOver);
        DragDrop.AddDropHandler(this, SaveDropped);
        UpdateLanguageMenu();
        NgPlusProfessorRank.ItemsSource = SkillRanks.Take(10).ToArray();
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            StatusNotice.IsVisible = false;
        };
        Status.PropertyChanged += (_, change) =>
        {
            if (change.Property != TextBlock.TextProperty) return;
            _statusTimer.Stop();
            StatusNotice.IsVisible = !string.IsNullOrWhiteSpace(Status.Text);
            if (StatusNotice.IsVisible && !Status.Text!.StartsWith("Warning:", StringComparison.Ordinal))
                _statusTimer.Start();
        };
        EditorTabs.SelectionChanged += (_, _) => UiStrings.Apply(this, _databaseLanguage);
        UiStrings.Apply(this, _databaseLanguage);
    }

    private void UpdateLanguageMenu()
    {
        LanguageMenu.ItemsSource = Enum.GetValues<enmLanguage>()
            .Select(language =>
            {
                var item = new MenuItem
                {
                    Header = (language == _databaseLanguage ? "✓ " : "  ")
                        + UiStrings.LanguageName(language, _databaseLanguage)
                };
                item.Click += (_, _) => ChangeLanguage(language);
                return item;
            }).ToArray();
    }

    private void ChangeLanguage(enmLanguage next)
    {
        if (next == _databaseLanguage) return;
        try
        {
            if (_databaseReady) Database.Init(next);
            _databaseLanguage = next;
            UiPreferences.Save(next);
            UpdateLanguageMenu();
            _itemChoices = null;
            _characterIds = null;
            _abilityChoices = null;
            _artChoices = null;
            _battalionCharacters = null;
            _battalionTypes = null;
            _battalionSkills = null;
            if (_save is not null)
            {
                RefreshCurrentSummary();
                RefreshStorage(SelectedSourceIndex(StorageList), SelectedSourceIndex(MiscList),
                    SelectedSourceIndex(GiftList));
                RefreshCurrentCharacters(Math.Max(0, _currentCharacter));
                RefreshBattalions(SelectedSourceIndex(BattalionList));
                RefreshQuests();
                RefreshCurrentSupports();
                RefreshCharacters();
                RefreshSupports();
                RefreshDatabaseViewer();
            }
            UiStrings.Apply(this, _databaseLanguage);
            Status.Text = string.Empty;
        }
        catch (Exception error)
        {
            if (_databaseReady) Database.Init(_databaseLanguage);
            UpdateLanguageMenu();
            Status.Text = "Could not change database language: " + error.Message;
        }
    }

    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();

    private void SaveDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy : DragDropEffects.None;

    private void SaveDropped(object? sender, DragEventArgs e)
    {
        var file = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault();
        if (file is null) return;
        try
        {
            if (!file.Path.IsFile) throw new NotSupportedException("Only local files are supported.");
            LoadSave(file.Path.LocalPath);
        }
        catch (Exception error)
        {
            Status.Text = "Could not open save: " + error.Message;
        }
    }

    private void OpenSystemEditor_Click(object? sender, RoutedEventArgs e)
    {
        if (!_databaseReady)
        {
            Database.Init(_databaseLanguage);
            _databaseReady = true;
        }
        var editor = new SystemWindow();
        UiStrings.Apply(editor, _databaseLanguage);
        editor.Show(this);
    }

    private async void OpenSave_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open a Fire Emblem: Three Houses save",
                AllowMultiple = false
            });
            if (files.Count == 0) return;
            if (!files[0].Path.IsFile) throw new NotSupportedException("Only local files are supported.");
            LoadSave(files[0].Path.LocalPath);
        }
        catch (Exception error)
        {
            _loading = false;
            Status.Text = "Could not open save: " + error.Message;
        }
    }

    public void LoadSave(string path)
    {
        if (!_databaseReady)
        {
            Database.Init(_databaseLanguage);
            _databaseReady = true;
        }

        SaveBuffer opened = SaveBuffer.Open(path);
        _loading = true;
        try
        {
            _save = opened;
            _sourcePath = path;
            CharacterSearch.Text = string.Empty;
            ClassSearch.Text = string.Empty;
            SupportSearch.Text = string.Empty;
            StorageSearch.Text = MiscSearch.Text = GiftSearch.Text = string.Empty;
            CurrentCharacterSearch.Text = BattalionSearch.Text = QuestSearch.Text = string.Empty;
            CurrentSupportSearch.Text = string.Empty;
            DatabaseCharacterSearch.Text = DatabaseClassSearch.Text = DatabaseItemSearch.Text = string.Empty;
            SaveMenuItem.IsEnabled = true;
            _selectedCharacter = -1;
            _selectedSupport = -1;
        }
        finally
        {
            _loading = false;
        }

        RefreshCurrentSummary();
        RefreshStorage();
        RefreshCurrentCharacters();
        RefreshBattalions();
        RefreshQuests();
        RefreshCurrentSupports();
        RefreshCharacters();
        RefreshSupports();
        RefreshNgPlusProfessorRank();
        RefreshDatabaseViewer();
        UiStrings.Apply(this, _databaseLanguage);
        Status.Text = opened.HasInvalidChecksum
            ? "Warning: save checksum does not match. Keep the original; writing will repair the checksum."
            : string.Empty;
    }

    private async void SaveCopy_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _sourcePath is null) return;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Write save",
                SuggestedFileName = Path.GetFileName(_sourcePath)
            });
            if (file is null) return;
            if (!file.Path.IsFile) throw new NotSupportedException("Only local files are supported.");
            string destination = Path.GetFullPath(file.Path.LocalPath);
            byte[] output = _save.FinishedBytes();
            string? backup = VerifiedFileWriter.Write(destination, output, path =>
            {
                var verified = SaveBuffer.Open(path);
                return !verified.HasInvalidChecksum && verified.Sha256 == SaveBuffer.Digest(output);
            });
            Status.Text = backup is null
                ? "Saved and verified: " + destination
                : "Saved and verified: " + destination + " (backup: " + backup + ")";
        }
        catch (Exception error)
        {
            Status.Text = "Could not save copy: " + error.Message;
        }
    }

    private void PlayerField_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _save is null || sender is not TextBox { Tag: string path } input) return;
        try
        {
            if (!long.TryParse(input.Text, NumberStyles.None, CultureInfo.InvariantCulture, out long value))
                throw new FormatException("Enter a non-negative whole number.");
            if (value == Convert.ToInt64(_save.Get(path), CultureInfo.InvariantCulture)) return;
            _save.Set(path, value);
            RefreshCurrentSummary();
            MarkChanged();
        }
        catch (Exception error)
        {
            input.Text = Convert.ToString(_save.Get(path), CultureInfo.InvariantCulture);
            Status.Text = error.Message;
        }
    }

    private void PlayerName_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_loading || _save is null) return;
        try
        {
            string name = PlayerNameInput.Text ?? string.Empty;
            if (name == _save.Get("playerName").ToString()) return;
            _save.SetName(name);
            RefreshCurrentSummary();
            MarkChanged();
        }
        catch (Exception error)
        {
            PlayerNameInput.Text = _save.Get("playerName").ToString();
            Status.Text = error.Message;
        }
    }

    private void CharacterSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_loading) RefreshCharacters();
    }

    private void CharacterList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || CharacterList.SelectedIndex < 0
            || CharacterList.SelectedIndex >= _visibleCharacters.Count) return;
        _selectedCharacter = _visibleCharacters[CharacterList.SelectedIndex];
        ShowCharacter();
    }

    private void ClassSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_loading) ShowClasses();
    }

    private void RefreshCharacters()
    {
        if (_save is null) return;
        string search = CharacterSearch.Text?.Trim() ?? string.Empty;
        _visibleCharacters.Clear();
        foreach (int index in Enumerable.Range(0, NgPlusJournal.CharacterCount)
            .Where(index => index <= 34 || index is >= 38 and <= 41 || index is 43 or 44))
        {
            string name = DisplayName(_save.Inheritance.GetCharacterName(index));
            if (name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || index.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase))
                _visibleCharacters.Add(index);
        }
        _loading = true;
        CharacterList.ItemsSource = _visibleCharacters
            .Select(index => $"{index:D2} · {DisplayName(_save.Inheritance.GetCharacterName(index))}").ToArray();
        int selected = _visibleCharacters.IndexOf(_selectedCharacter);
        CharacterList.SelectedIndex = selected >= 0 ? selected : _visibleCharacters.Count > 0 ? 0 : -1;
        _loading = false;
        _selectedCharacter = CharacterList.SelectedIndex >= 0
            ? _visibleCharacters[CharacterList.SelectedIndex] : -1;
        ShowCharacter();
    }

    private void ShowCharacter()
    {
        SkillRows.Children.Clear();
        ClassRows.Children.Clear();
        if (_save is null || _selectedCharacter < 0)
        {
            return;
        }

        int character = _selectedCharacter;
        for (int skill = 0; skill < NgPlusJournal.SkillCount; skill++)
        {
            int skillIndex = skill;
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("56,200"),
                ColumnSpacing = 8,
                Margin = new Avalonia.Thickness(0, 0, 0, 12)
            };
            row.Children.Add(new TextBlock
            {
                Text = Database.GetString(7214 + skill),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            var rank = new ComboBox { ItemsSource = SkillRanks,
                SelectedIndex = _save.Inheritance.GetSkillRank(character, skill) };
            Grid.SetColumn(rank, 1);
            rank.Classes.Add("Small");
            rank.SelectionChanged += (_, _) =>
            {
                if (_save is null || rank.SelectedIndex < 0) return;
                _save.Inheritance.SetSkillRank(character, skillIndex, rank.SelectedIndex);
                MarkChanged();
            };
            row.Children.Add(rank);
            SkillRows.Children.Add(row);
        }
        ShowClasses();
    }

    private void MaxInheritedSkills_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _selectedCharacter < 0) return;
        for (int skill = 0; skill < NgPlusJournal.SkillCount; skill++)
            _save.Inheritance.SetSkillRank(_selectedCharacter, skill, SkillRanks.Length - 1);
        ShowCharacter();
        MarkChanged();
    }

    private void ShowClasses()
    {
        ClassRows.Children.Clear();
        if (_save is null || _selectedCharacter < 0) return;
        int character = _selectedCharacter;
        string search = ClassSearch.Text?.Trim() ?? string.Empty;
        foreach (int classId in Enumerable.Range(0, NgPlusJournal.ClassCount))
        {
            string name = Database.GetClassName(classId);
            if (!name.Contains(search, StringComparison.OrdinalIgnoreCase)
                && !classId.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;
            int selectedClass = classId;
            var check = new CheckBox
            {
                Content = $"{classId:D2} · {name}",
                IsChecked = _save.Inheritance.IsClassMastered(character, classId),
                Margin = new Avalonia.Thickness(0, 0, 8, 8)
            };
            check.IsCheckedChanged += (_, _) =>
            {
                if (_save is null) return;
                _save.Inheritance.SetClassMastered(character, selectedClass, check.IsChecked == true);
                MarkChanged();
            };
            ClassRows.Children.Add(check);
        }
    }

    private void SupportSearch_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_loading) RefreshSupports();
    }

    private void SupportList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || SupportList.SelectedIndex < 0
            || SupportList.SelectedIndex >= _visibleSupports.Count) return;
        _selectedSupport = _visibleSupports[SupportList.SelectedIndex];
        ShowSupport();
    }

    private void RefreshSupports()
    {
        if (_save is null) return;
        string search = SupportSearch.Text?.Trim() ?? string.Empty;
        _visibleSupports.Clear();
        for (int index = 0; index < SaveEditor.Structs.Player_V23.COUNT_SUPPORT; index++)
        {
            string name = DisplaySupportName(_save.Inheritance.GetSupportName(index));
            if (name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || index.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase))
                _visibleSupports.Add(index);
        }
        _loading = true;
        SupportList.ItemsSource = _visibleSupports.Select(index =>
            $"{index:D3} · {DisplaySupportName(_save.Inheritance.GetSupportName(index))} · "
            + UiStrings.Translate(SupportRankFor(_save.Inheritance.GetSupportPoints(index)).Label, _databaseLanguage)).ToArray();
        int selected = _visibleSupports.IndexOf(_selectedSupport);
        SupportList.SelectedIndex = selected >= 0 ? selected : _visibleSupports.Count > 0 ? 0 : -1;
        _loading = false;
        _selectedSupport = SupportList.SelectedIndex >= 0
            ? _visibleSupports[SupportList.SelectedIndex] : -1;
        ShowSupport();
    }

    private void ShowSupport()
    {
        if (_save is null || _selectedSupport < 0)
        {
            SupportRankPreset.ItemsSource = null;
            return;
        }
        int points = _save.Inheritance.GetSupportPoints(_selectedSupport);
        Choice[] ranks = SupportRanks
            .Select(rank => new Choice(rank.Id, UiStrings.Translate(rank.Label, _databaseLanguage)))
            .ToArray();
        SupportRankPreset.ItemsSource = ranks;
        SupportRankPreset.SelectedItem = ranks[Array.IndexOf(SupportRanks, SupportRankFor(points))];
    }

    private static Choice SupportRankFor(int points) =>
        SupportRanks.Last(rank => points >= rank.Id);

    private void ApplySupport_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _selectedSupport < 0) return;
        try
        {
            if (SupportRankPreset.SelectedItem is not Choice choice)
                throw new InvalidOperationException("Select a support rank.");
            int current = _save.Inheritance.GetSupportPoints(_selectedSupport);
            if (SupportRankFor(current).Id == choice.Id) return;
            _save.Inheritance.SetSupportPoints(_selectedSupport, choice.Id);
            RefreshSupports();
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void RefreshCurrentSummary()
    {
        if (_save is null) return;
        var data = _save.Data;
        PlaytimeInput.Text = data.Player.Playtime.ToString(CultureInfo.InvariantCulture);
        MoneyInput.Text = data.Player.Money.ToString(CultureInfo.InvariantCulture);
        InstructExpInput.Text = data.Activities.InstructExp.ToString(CultureInfo.InvariantCulture);
        ProfessorLevelValue.Text = data.Activities.GetInstructRank();
        RenownInput.Text = data.Activities.Reputation.ToString(CultureInfo.InvariantCulture);
        PlayerNameInput.Text = SaveEditor.Util.DecodeString(data.PlayerName);
        PopulateNumericRows(GameRows, GameFields);
        PopulateNumericRows(ActivityRows, ActivityFields);
        PopulateNumericRows(StatueRows, StatueFields);
        PopulateNumericRows(StatisticsRows, StatisticFields);
    }

    private void PopulateNumericRows(
        StackPanel container, IEnumerable<(string Label, string Path)> fields)
    {
        container.Children.Clear();
        if (_save is null) return;
        foreach (var (label, path) in fields)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("160,*"),
                ColumnSpacing = 8
            };
            row.Children.Add(new TextBlock
            {
                Text = LocalizedFieldLabel(path, label),
                VerticalAlignment = VerticalAlignment.Center
            });
            if (path is "Player.Difficulty" or "Player.Gamestyle")
            {
                var choice = CreateGameChoice(path);
                Grid.SetColumn(choice, 1);
                row.Children.Add(choice);
                container.Children.Add(row);
                continue;
            }
            var input = new TextBox
            {
                Text = Convert.ToString(_save.Get(path), CultureInfo.InvariantCulture)
            };
            Grid.SetColumn(input, 1);
            input.Classes.Add("Small");
            input.LostFocus += (_, _) =>
            {
                if (_save is null) return;
                try
                {
                    if (!long.TryParse(input.Text, NumberStyles.None, CultureInfo.InvariantCulture, out long value))
                        throw new FormatException("Enter a non-negative whole number.");
                    _save.Set(path, value);
                    MarkChanged();
                }
                catch (Exception error)
                {
                    input.Text = Convert.ToString(_save.Get(path), CultureInfo.InvariantCulture);
                    Status.Text = error.Message;
                }
            };
            row.Children.Add(input);
            container.Children.Add(row);
        }
    }

    private string LocalizedFieldLabel(string path, string fallback)
    {
        int? stringId = path switch
        {
            "Activities.PlayLog_Wark" or "Activities.ActivityExplore" => 1025,
            "Activities.PlayLog_Lecture" or "Activities.ActivityLesson" => 1026,
            "Activities.PlayLog_ToBtl" or "Activities.ActivityBattle" => 1027,
            "Activities.PlayLog_Rest" => 1212,
            "Activities.PlayLog_Trnmnt" => 405,
            "Activities.PlayLog_Sing" => 402,
            "Activities.PlayLog_Lunch" => 401,
            "Activities.PlayLog_Cooking" => 555,
            "Activities.PlayLog_Drill" => 1860,
            "Activities.PlayLog_Teaparty" => 3696,
            "Activities.PlayLog_SCOUT" => 1820,
            _ => null
        };
        if (stringId is not null) return Database.GetString(stringId.Value, 1) + ":";
        if (path.StartsWith("Activities.Statue", StringComparison.Ordinal)
            && int.TryParse(path.AsSpan("Activities.Statue".Length), out int statue)
            && statue is >= 1 and <= 4)
            return Database.GetString(9763 + statue) + ":";
        return UiStrings.Translate(fallback, _databaseLanguage);
    }

    private ComboBox CreateGameChoice(string path)
    {
        int count = path == "Player.Difficulty" ? Database.DIFFICULTY_COUNT : Database.GAMESTYLE_COUNT;
        int firstString = path == "Player.Difficulty" ? 373 : 381;
        var choices = Enumerable.Range(0, count)
            .Select(index => new Choice(index, Database.GetString(firstString + index, 1)))
            .ToArray();
        var combo = new ComboBox { ItemsSource = choices };
        combo.Classes.Add("Small");
        combo.SelectedIndex = checked((int)(long)_save!.Get(path));
        combo.SelectionChanged += (_, _) =>
        {
            if (_save is null || combo.SelectedItem is not Choice choice) return;
            try
            {
                if ((long)_save.Get(path) == choice.Id) return;
                _save.Set(path, choice.Id);
                MarkChanged();
            }
            catch (Exception error)
            {
                combo.SelectedIndex = checked((int)(long)_save.Get(path));
                Status.Text = error.Message;
            }
        };
        return combo;
    }

    private void MarkChanged()
    {
        if (_save is null) return;
        SaveMenuItem.IsEnabled = true;
        Status.Text = string.Empty;
    }

    private static string DisplayName(string name) =>
        name.Replace("♂", " ♂", StringComparison.Ordinal)
            .Replace("♀", " ♀", StringComparison.Ordinal);

    private static string DisplaySupportName(string name) =>
        Regex.Replace(DisplayName(name), @"\s*🔗\s*", " — ");
}
