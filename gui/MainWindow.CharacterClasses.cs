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
    private void RefreshCharacterClasses(CharacterData_V23 character)
    {
        bool previousLoading = _loading;
        _loading = true;
        try
        {
            CurrentClassInfo.Text = $"{UiStrings.Translate("Current class:", _databaseLanguage)} "
                + $"{Database.GetClassName(character.Class)} · "
                + $"{UiStrings.Translate("Experience", _databaseLanguage)} "
                + $"{character.CurrentClassExp} · "
                + $"{UiStrings.Translate("Mastery:", _databaseLanguage)} {character.CurrentClassLevel}";
            CurrentClassExpList.ItemsSource = Enumerable.Range(0, Database.MAX_CLASS)
                .Select(index => $"{Database.GetClassName(index)} · "
                    + $"{character.ClassExp[index]} / {character.ClassLevel[index]}")
                .ToArray();
            CurrentClassExpList.SelectedIndex = character.Class < Database.MAX_CLASS
                ? character.Class : 0;
        }
        finally
        {
            _loading = previousLoading;
        }
        ShowSelectedClass();
    }

    private void CurrentClassExpList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading) ShowSelectedClass();
    }

    private void ShowSelectedClass()
    {
        if (_save is null || _currentCharacter < 0 || CurrentClassExpList.SelectedIndex < 0) return;
        int classId = CurrentClassExpList.SelectedIndex;
        var character = _save.Data.Characters[_currentCharacter].data;
        SelectedClassExp.Text = character.ClassExp[classId].ToString(CultureInfo.InvariantCulture);
        SelectedClassLevel.Text = character.ClassLevel[classId].ToString(CultureInfo.InvariantCulture);
    }

    private void SetCharacterClass_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _currentCharacter < 0 || CurrentClassExpList.SelectedIndex < 0) return;
        try
        {
            int slot = _currentCharacter;
            int classId = CurrentClassExpList.SelectedIndex;
            ushort exp = ParseUShort(SelectedClassExp, "Class experience");
            byte level = ParseAmount(SelectedClassLevel, "Class mastery level");
            if (level > Database.MAX_CLASS_LEVEL)
                throw new ArgumentOutOfRangeException(nameof(level), "Class mastery must be 0 or 1.");
            if (exp > Database.GetMaxClassExp(classId))
                throw new ArgumentOutOfRangeException(nameof(exp), "Experience exceeds this class's maximum.");
            _save.Set($"Characters[{slot}].data.ClassExp[{classId}]", exp);
            _save.Set($"Characters[{slot}].data.ClassLevel[{classId}]", level);
            ShowCurrentCharacter();
            CurrentClassExpList.SelectedIndex = classId;
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void MaxCharacterClasses_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || _currentCharacter < 0) return;
        try
        {
            _save.MaxClassExperience(_currentCharacter);
            ShowCurrentCharacter();
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }
}
