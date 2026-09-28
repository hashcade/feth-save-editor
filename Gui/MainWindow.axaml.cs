using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using FethEditor.Core;
using SaveEditor;

namespace FethEditor.Gui;

public partial class MainWindow : Window
{
    private static readonly string[] SkillNames =
    [
        "Sword", "Lance", "Axe", "Bow", "Brawling", "Reason", "Faith",
        "Authority", "Heavy Armor", "Riding", "Flying"
    ];

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
    private bool _databaseReady;
    private enmLanguage _databaseLanguage = enmLanguage.en_u;

    public MainWindow()
    {
        InitializeComponent();
        DatabaseLanguage.ItemsSource = Enum.GetValues<enmLanguage>()
            .Select(language => new Choice((int)language, language.GetDescription())).ToArray();
        DatabaseLanguage.SelectedIndex = (int)_databaseLanguage;
        NgPlusProfessorRank.ItemsSource = SkillRanks.Take(10).ToArray();
    }

    private void DatabaseLanguage_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_databaseReady || DatabaseLanguage.SelectedItem is not Choice choice
            || choice.Id == (int)_databaseLanguage) return;
        enmLanguage next = (enmLanguage)choice.Id;
        try
        {
            Database.Init(next);
            _databaseLanguage = next;
            _itemChoices = null;
            _characterIds = null;
            _abilityChoices = null;
            _artChoices = null;
            _battalionCharacters = null;
            _battalionTypes = null;
            _battalionSkills = null;
            if (_save is null) return;
            RefreshStorage(StorageList.SelectedIndex, MiscList.SelectedIndex, GiftList.SelectedIndex);
            RefreshCurrentCharacters(Math.Max(0, _currentCharacter));
            RefreshBattalions(BattalionList.SelectedIndex);
            RefreshQuests();
            RefreshCurrentSupports();
            RefreshCharacters();
            RefreshSupports();
            RefreshDatabaseViewer();
            Status.Text = "Database language changed. Save bytes were not modified.";
        }
        catch (Exception error)
        {
            Database.Init(_databaseLanguage);
            DatabaseLanguage.SelectedIndex = (int)_databaseLanguage;
            Status.Text = "Could not change database language: " + error.Message;
        }
    }

    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();

    private void OpenSystemEditor_Click(object? sender, RoutedEventArgs e)
    {
        if (!_databaseReady)
        {
            Database.Init(_databaseLanguage);
            _databaseReady = true;
        }
        new SystemWindow().Show(this);
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
            SaveMenuItem.IsEnabled = false;
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
        Status.Text = "Save loaded. Changes stay in memory until you save a new copy.";
    }

    private async void SaveCopy_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _sourcePath is null) return;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save edited copy",
                SuggestedFileName = Path.GetFileName(_sourcePath) + "-edited"
            });
            if (file is null) return;
            if (!file.Path.IsFile) throw new NotSupportedException("Only local files are supported.");
            string destination = Path.GetFullPath(file.Path.LocalPath);
            if (string.Equals(destination, Path.GetFullPath(_sourcePath), StringComparison.OrdinalIgnoreCase))
                throw new IOException("The original save cannot be overwritten. Choose a new path.");
            if (File.Exists(destination))
                throw new IOException("The destination already exists. Choose a new filename.");

            byte[] output = _save.FinishedBytes();
            string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporary, output);
                var verified = SaveBuffer.Open(temporary);
                if (verified.Sha256 != SaveBuffer.Digest(output))
                    throw new InvalidDataException("Written save failed verification.");
                File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            Status.Text = "Saved and verified edited copy: " + destination;
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
        foreach (int index in Enumerable.Range(0, NgPlusJournal.CharacterCount))
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
            SelectedCharacter.Text = "Select a character";
            return;
        }

        int character = _selectedCharacter;
        SelectedCharacter.Text = $"{DisplayName(_save.Inheritance.GetCharacterName(character))} · record {character}";
        for (int skill = 0; skill < NgPlusJournal.SkillCount; skill++)
        {
            int skillIndex = skill;
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(new TextBlock
            {
                Text = SkillNames[skill], Width = 130,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            });
            var rank = new ComboBox { ItemsSource = SkillRanks, Width = 100,
                SelectedIndex = _save.Inheritance.GetSkillRank(character, skill) };
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
                IsChecked = _save.Inheritance.IsClassMastered(character, classId)
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
            $"{index:D3} · {DisplaySupportName(_save.Inheritance.GetSupportName(index))} · {_save.Inheritance.GetSupportPoints(index)}").ToArray();
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
            SelectedSupport.Text = "Select a support pair";
            SupportPointsInput.Text = string.Empty;
            return;
        }
        SelectedSupport.Text = $"{DisplaySupportName(_save.Inheritance.GetSupportName(_selectedSupport))} · index {_selectedSupport}";
        SupportPointsInput.Text = _save.Inheritance.GetSupportPoints(_selectedSupport)
            .ToString(CultureInfo.InvariantCulture);
    }

    private void ApplySupport_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _selectedSupport < 0) return;
        try
        {
            if (!int.TryParse(SupportPointsInput.Text, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int points))
                throw new FormatException("Enter a non-negative whole number.");
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
        if (_save is null) return;
        var data = _save.Data;
        PlaytimeInput.Text = data.Player.Playtime.ToString(CultureInfo.InvariantCulture);
        MoneyInput.Text = data.Player.Money.ToString(CultureInfo.InvariantCulture);
        InstructExpInput.Text = data.Activities.InstructExp.ToString(CultureInfo.InvariantCulture);
        ProfessorLevelValue.Text = data.Activities.GetInstructRank();
        RenownInput.Text = data.Activities.Reputation.ToString(CultureInfo.InvariantCulture);
        PlayerNameInput.Text = SaveEditor.Util.DecodeString(data.PlayerName);
        PopulateNumericRows(GameRows, GameFields, 130);
        PopulateNumericRows(ActivityRows, ActivityFields, 116);
        PopulateNumericRows(StatueRows, StatueFields, 116);
        PopulateNumericRows(StatisticsRows, StatisticFields, 118);
    }

    private void PopulateNumericRows(
        StackPanel container, IEnumerable<(string Label, string Path)> fields, double labelWidth)
    {
        container.Children.Clear();
        if (_save is null) return;
        foreach (var (label, path) in fields)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = labelWidth,
                VerticalAlignment = VerticalAlignment.Center
            });
            var input = new TextBox
            {
                Width = container == GameRows ? 88 : 70,
                Text = Convert.ToString(_save.Get(path), CultureInfo.InvariantCulture)
            };
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

    private void MarkChanged()
    {
        if (_save is null) return;
        SaveMenuItem.IsEnabled = _save.ChangedBytes > 0;
        Status.Text = _save.ChangedBytes > 0
            ? $"{_save.ChangedBytes} save bytes changed in memory. Save an edited copy to keep them."
            : "No changes to save bytes.";
    }

    private static string DisplayName(string name) =>
        name.Replace("♂", " ♂", StringComparison.Ordinal)
            .Replace("♀", " ♀", StringComparison.Ordinal);

    private static string DisplaySupportName(string name) =>
        Regex.Replace(DisplayName(name), @"\s*🔗\s*", " — ");
}
