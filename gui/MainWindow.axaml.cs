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
    private enmLanguage _databaseLanguage = UiPreferences.Load();
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(6) };

    public MainWindow()
    {
        InitializeComponent();
        DragDrop.AddDragOverHandler(this, SaveDragOver);
        DragDrop.AddDropHandler(this, SaveDropped);
        Database.Init(_databaseLanguage);
        CurrentProfessorRank.ItemsSource = SkillRanks.Take(Database.TeacherLevelupRank.Length).ToArray();
        NgPlusProfessorRank.ItemsSource = SkillRanks.Take(10).ToArray();
        SetOverviewInputsEnabled(false);
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
            if (StatusNotice.IsVisible && !Status.Text!.StartsWith("Warning:", StringComparison.Ordinal)
                && !Status.Text.StartsWith("警告：", StringComparison.Ordinal))
                _statusTimer.Start();
        };
        EditorTabs.SelectionChanged += (_, _) => UiStrings.Apply(this, _databaseLanguage);
        UiStrings.Apply(this, _databaseLanguage);
        RefreshTopMenu();
        RefreshEmptyLabels();
        RefreshCurrentSummary();
        RefreshDatabaseViewer();
    }

    private void UpdateLanguageMenu()
    {
        LanguageMenu.ItemsSource = new[]
            {
                enmLanguage.jp, enmLanguage.en_u, enmLanguage.de,
                enmLanguage.fr_u, enmLanguage.es_u, enmLanguage.it,
                enmLanguage.kr, enmLanguage.zh_hant, enmLanguage.zh_hans
            }
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

    private void RefreshTopMenu()
    {
        FileMenu.Header = UiStrings.Translate("File", _databaseLanguage);
        LanguageMenu.Header = UiStrings.Translate("Language", _databaseLanguage);
        UpdateLanguageMenu();
    }

    private async void About_Click(object? sender, RoutedEventArgs e) =>
        await new AboutWindow(_databaseLanguage).ShowDialog(this);

    private void ChangeLanguage(enmLanguage next)
    {
        if (next == _databaseLanguage) return;
        try
        {
            Database.Init(next);
            _databaseLanguage = next;
            UiPreferences.Save(next);
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
            else
            {
                RefreshCurrentSummary();
                RefreshDatabaseViewer();
            }
            UiStrings.Apply(this, _databaseLanguage);
            RefreshTopMenu();
            RefreshSystemPanel();
            if (_save is null) RefreshEmptyLabels();
            Status.Text = string.Empty;
        }
        catch (Exception error)
        {
            Database.Init(_databaseLanguage);
            RefreshTopMenu();
            Status.Text = UiStrings.Format("Could not change database language: {0}", _databaseLanguage, error.Message);
        }
    }

    private void RefreshEmptyLabels()
    {
        StorageCount.Text = UiStrings.Translate("Item List", _databaseLanguage);
        CurrentCharacterTitle.Text = UiStrings.Translate("Select a character", _databaseLanguage);
        CurrentClassInfo.Text = UiStrings.Translate("Current class", _databaseLanguage);
    }

    private void SetOverviewInputsEnabled(bool enabled)
    {
        foreach (Control input in new Control[]
        {
            PlayerNameInput, PlaytimeInput, MoneyInput, RenownInput, InstructExpInput,
            CurrentProfessorRank, NgPlusProfessorRank
        })
            input.IsEnabled = enabled;
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
            Status.Text = UiStrings.Format("Could not open save: {0}", _databaseLanguage, error.Message);
        }
    }

    private async void OpenSave_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = UiStrings.Translate("Open a Fire Emblem: Three Houses save", _databaseLanguage),
                AllowMultiple = false
            });
            if (files.Count == 0) return;
            if (!files[0].Path.IsFile) throw new NotSupportedException("Only local files are supported.");
            LoadSave(files[0].Path.LocalPath);
        }
        catch (Exception error)
        {
            _loading = false;
            Status.Text = UiStrings.Format("Could not open save: {0}", _databaseLanguage, error.Message);
        }
    }

    public void LoadSave(string path)
    {
        SaveBuffer opened = SaveBuffer.Open(path);
        string sibling = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "system");
        if (_systemDirty && !string.Equals(_systemPath, sibling, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiStrings.Translate(
                "Save the edited system copy before loading another system save.", _databaseLanguage));
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
            SetOverviewInputsEnabled(true);
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
        Status.Text = string.Empty;
        LoadSiblingSystem(path);
        UiStrings.Apply(this, _databaseLanguage);
        if (opened.HasInvalidChecksum)
            Status.Text = UiStrings.Translate("Warning: save checksum does not match. Keep the original; writing will repair the checksum.", _databaseLanguage);
    }

    private void LoadSiblingSystem(string slotPath)
    {
        string sibling = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(slotPath))!, "system");
        if (string.Equals(_systemPath, sibling, StringComparison.OrdinalIgnoreCase)) return;
        if (!File.Exists(sibling))
        {
            ClearSystem();
            return;
        }
        try
        {
            LoadSystem(sibling);
        }
        catch (Exception error)
        {
            ClearSystem();
            Status.Text = UiStrings.Format("Could not load system save: {0}", _databaseLanguage, error.Message);
        }
    }

    private async void SaveCopy_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = UiStrings.Translate("Choose a folder for the edited save copies", _databaseLanguage),
                AllowMultiple = false
            });
            if (folders.Count == 0) return;
            if (!folders[0].Path.IsFile)
                throw new NotSupportedException("Only local folders are supported.");
            SaveCopyToDirectory(folders[0].Path.LocalPath);
        }
        catch (Exception error)
        {
            Status.Text = UiStrings.Format("Could not save copy: {0}", _databaseLanguage, error.Message);
        }
    }

    public void SaveCopyToDirectory(string directory)
    {
        if (_save is null || _sourcePath is null)
            throw new InvalidOperationException("Open a slot save before writing a copy.");
        string folder = Path.GetFullPath(directory);
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        if (string.Equals(folder, Path.GetDirectoryName(Path.GetFullPath(_sourcePath)),
            StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiStrings.Translate(
                "Choose a different folder to keep the original saves.", _databaseLanguage));

        string destination = Path.Combine(folder, Path.GetFileName(_sourcePath));
        byte[] output = _save.FinishedBytes();
        string? slotBackup = VerifiedFileWriter.Write(destination, output, path =>
        {
            var verified = SaveBuffer.Open(path);
            return !verified.HasInvalidChecksum && verified.Sha256 == SaveBuffer.Digest(output);
        });
        string? systemBackup = WriteSystemCopy(folder);
        string[] backups = new[] { slotBackup, systemBackup }.OfType<string>().ToArray();
        Status.Text = backups.Length == 0
            ? UiStrings.Format("Saved and verified: {0}", _databaseLanguage, folder)
            : UiStrings.Format("Saved and verified: {0} (backup: {1})", _databaseLanguage,
                folder, string.Join(", ", backups));
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
            .Select(index => DisplayName(_save.Inheritance.GetCharacterName(index))).ToArray();
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
                ColumnDefinitions = new ColumnDefinitions("110,200"),
                ColumnSpacing = 8,
                Margin = new Avalonia.Thickness(0, 0, 32, 12)
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

    private void UnlockInheritedClasses_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _selectedCharacter < 0) return;
        _save.Inheritance.UnlockAvailableClasses(_selectedCharacter);
        ShowClasses();
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
                Content = name,
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
            $"{DisplaySupportName(_save.Inheritance.GetSupportName(index))} · "
            + UiStrings.Translate(SupportPairRanks.RankForPoints(index,
                _save.Inheritance.GetSupportPoints(index)), _databaseLanguage)).ToArray();
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
            ShowSupportInput(SupportRankPreset, InheritedSupportPoints, -1, null);
            return;
        }
        int points = _save.Inheritance.GetSupportPoints(_selectedSupport);
        ShowSupportInput(SupportRankPreset, InheritedSupportPoints, _selectedSupport, points);
    }

    private void MaxInheritedSupport_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _selectedSupport < 0) return;
        try
        {
            int before = _save.Inheritance.GetSupportPoints(_selectedSupport);
            _save.Inheritance.ReachMaxSupportRank(_selectedSupport);
            if (_save.Inheritance.GetSupportPoints(_selectedSupport) == before) return;
            RefreshSupports();
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void ApplySupport_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _selectedSupport < 0) return;
        try
        {
            ushort points = ParseUShort(InheritedSupportPoints, "Support points");
            if (_save.Inheritance.GetSupportPoints(_selectedSupport) == points) return;
            _save.Inheritance.SetSupportPoints(_selectedSupport, points);
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
        if (_save is not null)
        {
            var data = _save.Data;
            PlaytimeInput.Text = data.Player.Playtime.ToString(CultureInfo.InvariantCulture);
            MoneyInput.Text = data.Player.Money.ToString(CultureInfo.InvariantCulture);
            ShowProfessorInputs(data.Activities.InstructExp);
            RenownInput.Text = data.Activities.Reputation.ToString(CultureInfo.InvariantCulture);
            PlayerNameInput.Text = SaveEditor.Util.DecodeString(data.PlayerName);
        }
        PopulateNumericRows(GameRows, GameFields);
        PopulateNumericRows(ActivityRows, ActivityFields);
        PopulateNumericRows(StatueRows, StatueFields);
        PopulateNumericRows(StatisticsRows, StatisticFields);
    }

    private void PopulateNumericRows(
        StackPanel container, IEnumerable<(string Label, string Path)> fields)
    {
        container.Children.Clear();
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
            if (_save is not null && path is ("Player.Difficulty" or "Player.Gamestyle"))
            {
                var choice = CreateGameChoice(path);
                Grid.SetColumn(choice, 1);
                row.Children.Add(choice);
                container.Children.Add(row);
                continue;
            }
            var input = new TextBox
            {
                Text = _save is null ? string.Empty : Convert.ToString(_save.Get(path), CultureInfo.InvariantCulture),
                IsEnabled = _save is not null
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
