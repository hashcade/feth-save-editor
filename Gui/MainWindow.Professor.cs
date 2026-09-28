using System;
using System.Globalization;
using Avalonia.Controls;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private bool _updatingProfessorInputs;

    private void ShowProfessorInputs(int experience)
    {
        _updatingProfessorInputs = true;
        try
        {
            CurrentProfessorRank.SelectedIndex = SaveEditor.Database.GetProfessorRankFromExperience(experience);
            InstructExpInput.Text = experience.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _updatingProfessorInputs = false;
        }
    }

    private void CurrentProfessorRank_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingProfessorInputs || _loading || _save is null || CurrentProfessorRank.SelectedIndex < 0)
            return;
        try
        {
            int experience = SaveEditor.Database.TeacherLevelupRank[CurrentProfessorRank.SelectedIndex];
            if (_save.Data.Activities.InstructExp == experience)
                return;
            _save.Set("Activities.InstructExp", experience);
            ShowProfessorInputs(experience);
            MarkChanged();
        }
        catch (Exception error)
        {
            ShowProfessorInputs(_save.Data.Activities.InstructExp);
            Status.Text = error.Message;
        }
    }

    private void InstructExpInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingProfessorInputs || _save is null)
            return;
        _updatingProfessorInputs = true;
        try
        {
            CurrentProfessorRank.SelectedIndex = int.TryParse(InstructExpInput.Text, NumberStyles.None,
                CultureInfo.InvariantCulture, out int experience)
                && experience <= SaveEditor.Database.MAX_INSTRUCT_EXP
                    ? SaveEditor.Database.GetProfessorRankFromExperience(experience)
                    : -1;
        }
        finally
        {
            _updatingProfessorInputs = false;
        }
    }
}
