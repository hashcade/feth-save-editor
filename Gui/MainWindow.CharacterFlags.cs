using System;
using System.Linq;
using Avalonia.Controls;
using SaveEditor;
using SaveEditor.Structs;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private void PopulateCharacterFlags(CharacterData_V23 character)
    {
        if (_save is null || _currentCharacter < 0) return;
        int slot = _currentCharacter;
        string prefix = $"Characters[{slot}].data.";
        PopulateFlagRows(CurrentCharacterFlags, prefix + "Flags", 32,
            index => ((character.Flags >> index) & 1) != 0,
            index => ((enmCharacterFlags)(1u << index)).GetDescription());
        PopulateFlagRows(CurrentClassFlags, prefix + "ClassFlags", 8,
            index => ((character.ClassFlags >> index) & 1) != 0,
            index => ((enmCharacterClassFlags)(1 << index)).GetDescription());
        PopulateFlagRows(CurrentClassUnlockFlags, prefix + "ClassUnlockFlags", Database.CLASS_FLAGS_COUNT,
            index => Bit(character.ClassUnlockFlags, index), index => Database.GetUnitClassName(index));
        PopulateFlagRows(CurrentAbilityFlags, prefix + "Abilities", Database.MAX_ABILITIES * 8,
            index => Bit(character.Abilities, index), index => Database.GetAbilityName(index, false));
        PopulateFlagRows(CurrentArtFlags, prefix + "CombatArts", Database.COMBAT_ARTS_COUNT,
            index => Bit(character.CombatArts, index), index => Database.GetCombatArtName(index));

        CurrentAdjutant.ItemsSource = _characterIds;
        CurrentAdjutant.SelectedItem = _characterIds?.FirstOrDefault(choice => choice.Id == character.AdjutantId);
    }

    private static bool Bit(byte[] bytes, int index) =>
        (bytes[index / 8] & (1 << (index % 8))) != 0;

    private void PopulateFlagRows(StackPanel container, string path, int count,
        Func<int, bool> isChecked, Func<int, string> name)
    {
        container.Children.Clear();
        int slot = _currentCharacter;
        for (int index = 0; index < count; index++)
        {
            int bit = index;
            var check = new CheckBox
            {
                Content = name(index),
                IsChecked = isChecked(index)
            };
            check.IsCheckedChanged += (_, _) =>
            {
                if (_loading || _save is null || slot != _currentCharacter) return;
                try
                {
                    _save.SetBit(path, bit, check.IsChecked == true);
                    MarkChanged();
                }
                catch (Exception error)
                {
                    Status.Text = error.Message;
                }
            };
            container.Children.Add(check);
        }
    }

    private void CurrentAdjutant_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || _save is null || _currentCharacter < 0
            || CurrentAdjutant.SelectedItem is not Choice choice) return;
        try
        {
            _save.Set($"Characters[{_currentCharacter}].data.AdjutantId", choice.Id);
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void UnlockAllAbilities_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        UnlockAllCharacterFlags("abilities");

    private void UnlockAllCombatArts_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        UnlockAllCharacterFlags("combat-arts");

    private void UnlockAllCharacterFlags(string kind)
    {
        if (_save is null || _currentCharacter < 0) return;
        try
        {
            _save.UnlockAll(_currentCharacter, kind);
            ShowCurrentCharacter();
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }
}
