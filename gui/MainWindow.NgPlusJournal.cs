using System;
using Avalonia.Controls;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private void RefreshNgPlusProfessorRank()
    {
        if (_save is null) return;
        bool previousLoading = _loading;
        _loading = true;
        try
        {
            NgPlusProfessorRank.SelectedIndex = _save.Inheritance.ProfessorRank;
        }
        finally
        {
            _loading = previousLoading;
        }
    }

    private void NgPlusProfessorRank_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || _save is null || NgPlusProfessorRank.SelectedIndex < 0) return;
        try
        {
            _save.Inheritance.SetProfessorRank(NgPlusProfessorRank.SelectedIndex);
            MarkChanged();
        }
        catch (Exception error)
        {
            RefreshNgPlusProfessorRank();
            Status.Text = error.Message;
        }
    }
}
