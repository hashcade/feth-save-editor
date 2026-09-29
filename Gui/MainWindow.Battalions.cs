using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaveEditor;
using SaveEditor.Structs;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private Choice[]? _battalionCharacters;
    private Choice[]? _battalionTypes;
    private Choice[]? _battalionSkills;

    private void RefreshBattalions(int selectedIndex = 0)
    {
        if (_save is null) return;
        _battalionCharacters ??= Database.UnitList.OrderBy(pair => pair.Key)
            .Select(pair => new Choice(pair.Key,
                pair.Key < 0 ? pair.Value : Database.GetUnitName(pair.Key))).ToArray();
        _battalionTypes ??= Database.BattalionList.OrderBy(pair => pair.Key)
            .Select(pair => new Choice(pair.Key == -1 ? Database.BATTALION_COUNT : pair.Key, pair.Value))
            .ToArray();
        _battalionSkills ??= Database.BattalionSkillList.OrderBy(pair => pair.Key)
            .Select(pair => new Choice(pair.Key, pair.Value)).ToArray();
        BattalionCharacter.ItemsSource = _battalionCharacters;
        BattalionType.ItemsSource = _battalionTypes;
        BattalionSkill.ItemsSource = _battalionSkills;

        bool previousLoading = _loading;
        _loading = true;
        try
        {
            var battalions = _save.Data.Player.Battalions;
            SetSearchRows(BattalionList, BattalionSearch, battalions
                .Select(battalion => battalion.GetBarracksName())
                .ToArray(), selectedIndex);
        }
        finally
        {
            _loading = previousLoading;
        }
        ShowBattalion();
    }

    private void BattalionList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading) ShowBattalion();
    }

    private void ShowBattalion()
    {
        int index = SelectedSourceIndex(BattalionList);
        if (_save is null || index < 0) return;
        Battalion value = _save.Data.Player.Battalions[index];
        BattalionCharacter.SelectedItem = _battalionCharacters?.FirstOrDefault(choice => choice.Id == value.CharacterId);
        BattalionType.SelectedItem = _battalionTypes?.FirstOrDefault(choice => choice.Id == value.Type);
        BattalionSkill.SelectedItem = _battalionSkills?.FirstOrDefault(choice => choice.Id == value.Skill);
        BattalionExp.Text = value.Exp.ToString(CultureInfo.InvariantCulture);
        BattalionStamina.Text = value.Stamina.ToString(CultureInfo.InvariantCulture);
    }

    private static ushort ParseUShort(TextBox input, string name)
    {
        if (!ushort.TryParse(input.Text, NumberStyles.None, CultureInfo.InvariantCulture, out ushort value))
            throw new FormatException($"{name} must be a whole number from 0 to 65535.");
        return value;
    }

    private void SaveBattalion_Click(object? sender, RoutedEventArgs e)
    {
        int index = SelectedSourceIndex(BattalionList);
        if (_save is null || index < 0) return;
        try
        {
            int character = (BattalionCharacter.SelectedItem as Choice)?.Id ?? -1;
            int type = (BattalionType.SelectedItem as Choice)?.Id ?? Database.BATTALION_COUNT;
            int skill = (BattalionSkill.SelectedItem as Choice)?.Id ?? Database.BATTALION_SKILL_COUNT;
            ushort exp = ParseUShort(BattalionExp, "Battalion experience");
            ushort stamina = ParseUShort(BattalionStamina, "Battalion stamina");
            string prefix = $"Player.Battalions[{index}].";
            _save.Set(prefix + "CharacterId", character);
            _save.Set(prefix + "Type", type);
            _save.Set(prefix + "Skill", skill);
            _save.Set(prefix + "Exp", exp);
            _save.Set(prefix + "Stamina", stamina);
            RefreshBattalions(index);
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void SortBattalion_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null) return;
        try
        {
            _save.SortBattalions();
            RefreshBattalions();
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }
}
