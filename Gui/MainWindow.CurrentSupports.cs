using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaveEditor;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private void RefreshCurrentSupports(int selectedIndex = 0)
    {
        if (_save is null) return;
        var supports = _save.Data.Player.CharacterSupportValues;
        bool previousLoading = _loading;
        _loading = true;
        try
        {
            CurrentSupportList.ItemsSource = supports.Select((points, index) =>
                $"{index:D3} · {DisplaySupportName(Database.GetSupportTalkName(index))} · {points}")
                .ToArray();
            CurrentSupportList.SelectedIndex = Math.Clamp(selectedIndex, 0, supports.Length - 1);
        }
        finally
        {
            _loading = previousLoading;
        }
        ShowCurrentSupport();
    }

    private void CurrentSupportList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading) ShowCurrentSupport();
    }

    private void ShowCurrentSupport()
    {
        if (_save is null || CurrentSupportList.SelectedIndex < 0) return;
        CurrentSupportPoints.Text = _save.Data.Player.CharacterSupportValues[CurrentSupportList.SelectedIndex]
            .ToString(CultureInfo.InvariantCulture);
    }

    private void SetCurrentSupport_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || CurrentSupportList.SelectedIndex < 0) return;
        try
        {
            int index = CurrentSupportList.SelectedIndex;
            ushort points = ParseUShort(CurrentSupportPoints, "Support points");
            _save.Set($"Player.CharacterSupportValues[{index}]", points);
            RefreshCurrentSupports(index);
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }
}
