using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using FethEditor.Core;
using SaveEditor;
using SaveEditor.Structs;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private static readonly (string Label, string Field)[] CharacterStats =
    [
        ("RNG Value", "RNG_VALUE"), ("Level", "Level"), ("Experience", "Exp"),
        ("HP", "HP"), ("Strength", "Strength"), ("Magic", "Magic"),
        ("Dexterity", "Dexterity"), ("Speed", "Speed"), ("Luck", "Luck"),
        ("Defense", "Defense"), ("Resistance", "Resistance"),
        ("Movement", "Movement"), ("Charm", "Charm"), ("Motivation", "Motivation")
    ];

    private Choice[]? _characterIds;
    private Choice[]? _abilityChoices;
    private Choice[]? _artChoices;
    private int _currentCharacter = -1;

    private void RefreshCurrentCharacters(int selectedIndex = 0)
    {
        if (_save is null) return;
        _characterIds ??= Database.UnitList.OrderBy(pair => pair.Key)
            .Select(pair => new Choice(pair.Key, pair.Value)).ToArray();
        _abilityChoices ??= Database.AbilityList.Where(pair => pair.Key >= 0 && pair.Key < 255)
            .OrderBy(pair => pair.Key).Select(pair => new Choice(pair.Key, pair.Value))
            .Append(new Choice(255, Database.STR_NONE)).ToArray();
        _artChoices ??= Database.CombatArtList.OrderBy(pair => pair.Key)
            .Select(pair => new Choice(pair.Key, pair.Value)).ToArray();
        CurrentCharacterId.ItemsSource = _characterIds;

        bool previousLoading = _loading;
        _loading = true;
        try
        {
            var characters = _save.Data.Characters;
            SetSearchRows(CurrentCharacterList, CurrentCharacterSearch, characters
                .Select((character, index) => $"[{index:D3}] {Database.GetUnitName(character.data.Id)}"
                    + $" · Lv.{character.data.Level}")
                .ToArray(), selectedIndex);
            _currentCharacter = SelectedSourceIndex(CurrentCharacterList);
        }
        finally
        {
            _loading = previousLoading;
        }
        ShowCurrentCharacter();
    }

    private void CurrentCharacterList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _currentCharacter = SelectedSourceIndex(CurrentCharacterList);
        ShowCurrentCharacter();
    }

    private void ShowCurrentCharacter()
    {
        if (_save is null || _currentCharacter < 0) return;
        CharacterData_V23 character = _save.Data.Characters[_currentCharacter].data;
        bool previousLoading = _loading;
        _loading = true;
        try
        {
            CurrentCharacterTitle.Text = $"[{_currentCharacter:D3}] {Database.GetUnitName(character.Id)}";
            CurrentCharacterId.SelectedItem = _characterIds?.FirstOrDefault(choice => choice.Id == character.Id);
            PopulateCharacterStats(character);
            PopulateCharacterItems(character);
            PopulateEquippedChoices(character);
            PopulateCharacterSkills(character);
            PopulateCharacterFlags(character);
            RefreshCharacterClasses(character);
        }
        finally
        {
            _loading = previousLoading;
        }
    }

    private void PopulateCharacterStats(CharacterData_V23 character)
    {
        CharacterStatRows.Children.Clear();
        CharacterStatRows.Children.Add(new TextBlock
        {
            Text = $"{UiStrings.Translate("Class:", _databaseLanguage)} {Database.GetClassName(character.Class)}"
        });
        foreach (var (label, field) in CharacterStats)
        {
            int slot = _currentCharacter;
            string path = $"Characters[{slot}].data.{field}";
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("125,*"),
                ColumnSpacing = 8
            };
            row.Children.Add(new TextBlock
            {
                Text = UiStrings.Translate(label, _databaseLanguage),
                VerticalAlignment = VerticalAlignment.Center
            });
            var input = new TextBox
            {
                Text = Convert.ToString(_save!.Get(path), CultureInfo.InvariantCulture)
            };
            Grid.SetColumn(input, 1);
            input.Classes.Add("Small");
            input.LostFocus += (_, _) => SetCharacterNumber(slot, path, input);
            row.Children.Add(input);
            CharacterStatRows.Children.Add(row);
        }
        string battalion = character.EquippedBattalion.Type >= Database.BATTALION_COUNT
            ? Database.STR_NONE : Database.GetBattalionName(character.EquippedBattalion.Type);
        CharacterStatRows.Children.Add(new TextBlock
        {
            Name = "EquippedBattalionValue",
            Text = UiStrings.Translate("Equipped Battalion:", _databaseLanguage) + " " + battalion,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
    }

    private void SetCharacterNumber(int slot, string path, TextBox input)
    {
        if (_loading || _save is null || slot != _currentCharacter) return;
        try
        {
            if (!long.TryParse(input.Text, NumberStyles.None, CultureInfo.InvariantCulture, out long value))
                throw new FormatException("Enter a non-negative whole number.");
            if (value == Convert.ToInt64(_save.Get(path), CultureInfo.InvariantCulture)) return;
            ValidateCharacterStat(slot, path, value);
            _save.Set(path, value);
            RefreshCurrentCharacters(slot);
            MarkChanged();
        }
        catch (Exception error)
        {
            input.Text = Convert.ToString(_save.Get(path), CultureInfo.InvariantCulture);
            Status.Text = error.Message;
        }
    }

    private void ValidateCharacterStat(int slot, string path, long value)
    {
        string field = path[(path.LastIndexOf('.') + 1)..];
        int stat = Array.IndexOf(new[]
        {
            "Strength", "Magic", "Dexterity", "Speed", "Luck", "Defense",
            "Resistance", "Movement", "Charm"
        }, field);
        if (field != "HP" && stat < 0) return;
        var character = _save!.Data.Characters[slot].data;
        int maximum = field == "HP"
            ? Database.GetMaxHP(character.Id, character.Class)
            : Database.GetMaxStat(character.Id, character.Class, stat);
        if (value > maximum)
            throw new ArgumentOutOfRangeException(nameof(value), $"{field} exceeds this character's maximum of {maximum}.");
    }

    private void PopulateCharacterItems(CharacterData_V23 character)
    {
        CharacterItemRows.Children.Clear();
        CharacterItemRows.Children.Add(new TextBlock { Text = $"{character.ItemCount} / {Database.MAX_CHARA_ITEMS}" });
        for (int index = 0; index < character.Items.Length; index++)
            CharacterItemRows.Children.Add(new TextBlock
            {
                Text = $"[{index}] {character.Items[index].EquippedName}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });
    }

    private void PopulateEquippedChoices(CharacterData_V23 character)
    {
        EquippedAbilityRows.Children.Clear();
        EquippedArtRows.Children.Clear();
        for (int index = 0; index < Database.MAX_CHARA_ABILITIES; index++)
            AddEquippedChoice(EquippedAbilityRows, _abilityChoices!, character.EquippedAbilities[index],
                index, "EquippedAbilities");
        for (int index = 0; index < Database.MAX_CHARA_COMBAT_ARTS; index++)
            AddEquippedChoice(EquippedArtRows, _artChoices!, character.EquippedCombatArts[index],
                index, "EquippedCombatArts");
    }

    private void AddEquippedChoice(StackPanel container, Choice[] choices, int value, int index, string field)
    {
        int slot = _currentCharacter;
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("24,*"),
            ColumnSpacing = 8
        };
        row.Children.Add(new TextBlock
        {
            Text = (index + 1).ToString(CultureInfo.InvariantCulture),
            VerticalAlignment = VerticalAlignment.Center
        });
        var combo = new ComboBox { ItemsSource = choices };
        Grid.SetColumn(combo, 1);
        combo.Classes.Add("Small");
        combo.SelectedItem = choices.FirstOrDefault(choice => choice.Id == value);
        combo.SelectionChanged += (_, _) =>
        {
            if (_loading || _save is null || slot != _currentCharacter || combo.SelectedItem is not Choice choice)
                return;
            try
            {
                _save.Set($"Characters[{slot}].data.{field}[{index}]", choice.Id);
                MarkChanged();
            }
            catch (Exception error)
            {
                Status.Text = error.Message;
            }
        };
        row.Children.Add(combo);
        container.Children.Add(row);
    }

    private void PopulateCharacterSkills(CharacterData_V23 character)
    {
        CurrentSkillRows.Children.Clear();
        CharacterMagicRows.Children.Clear();
        for (int index = 0; index < Database.MAX_SKILLS; index++)
        {
            int slot = _currentCharacter;
            int skill = index;
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("125,*"),
                ColumnSpacing = 8
            };
            int rank = character.SkillLevel[index];
            row.Children.Add(new TextBlock
            {
                Text = $"{Database.GetString(7214 + index)} ({(rank < SkillRanks.Length ? SkillRanks[rank] : rank.ToString(CultureInfo.InvariantCulture))})",
                VerticalAlignment = VerticalAlignment.Center
            });
            var input = new TextBox
            {
                Text = character.SkillExp[index].ToString(CultureInfo.InvariantCulture)
            };
            Grid.SetColumn(input, 1);
            input.Classes.Add("Small");
            input.LostFocus += (_, _) => SetCharacterNumber(slot,
                $"Characters[{slot}].data.SkillExp[{skill}]", input);
            row.Children.Add(input);
            CurrentSkillRows.Children.Add(row);
        }
        for (int index = 0; index < Database.MAX_MAGIC; index++)
            CharacterMagicRows.Children.Add(new TextBlock
            {
                Text = $"{index:D2} · {Database.GetMagicSkillName(character.LearnedMagic[index])} · {character.MagicDurability[index]}"
            });
    }

    private void CurrentCharacterId_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || _save is null || _currentCharacter < 0
            || CurrentCharacterId.SelectedItem is not Choice choice) return;
        try
        {
            _save.Set($"Characters[{_currentCharacter}].data.Id", choice.Id);
            RefreshCurrentCharacters(_currentCharacter);
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void SetCharacterItemsDurability_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _currentCharacter < 0) return;
        try
        {
            _save.SetCharacterItemDurability(_currentCharacter);
            ShowCurrentCharacter();
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void MaxCharacterSkills_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _currentCharacter < 0) return;
        try
        {
            _save.MaxSkillExperience(_currentCharacter);
            ShowCurrentCharacter();
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private async void ExportCharacter_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _currentCharacter < 0) return;
        try
        {
            var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = UiStrings.Translate("Export character", _databaseLanguage),
                SuggestedFileName = $"character-{_currentCharacter:D3}.character"
            });
            if (target is null) return;
            if (!target.Path.IsFile) throw new NotSupportedException("Only local files are supported.");
            string path = target.Path.LocalPath;
            if (File.Exists(path)) throw new IOException("The destination already exists.");
            File.WriteAllBytes(path, _save.ExportCharacter(_currentCharacter));
            Status.Text = UiStrings.Format("Character exported: {0}", _databaseLanguage, path);
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private async void ImportCharacter_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _currentCharacter < 0) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = UiStrings.Translate("Import character", _databaseLanguage),
                AllowMultiple = false
            });
            if (files.Count == 0) return;
            if (!files[0].Path.IsFile) throw new NotSupportedException("Only local files are supported.");
            _save.ImportCharacter(_currentCharacter, File.ReadAllBytes(files[0].Path.LocalPath));
            RefreshCurrentCharacters(_currentCharacter);
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }
}
