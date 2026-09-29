using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FethEditor.Core;
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
            SetSearchRows(CurrentSupportList, CurrentSupportSearch, supports.Select((points, index) =>
                $"{DisplaySupportName(Database.GetSupportTalkName(index))} · "
                + UiStrings.Translate(SupportPairRanks.RankForPoints(index, points), _databaseLanguage))
                .ToArray(), selectedIndex);
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
        int index = SelectedSourceIndex(CurrentSupportList);
        int? points = _save is null || index < 0
            ? null : _save.Data.Player.CharacterSupportValues[index];
        ShowSupportInput(CurrentSupportRank, CurrentSupportPoints, index, points);
    }

    private void MaxCurrentSupport_Click(object? sender, RoutedEventArgs e)
    {
        int index = SelectedSourceIndex(CurrentSupportList);
        if (_save is null || index < 0) return;
        try
        {
            int before = _save.Data.Player.CharacterSupportValues[index];
            _save.ReachMaxSupportRank(index);
            if (_save.Data.Player.CharacterSupportValues[index] == before) return;
            RefreshCurrentSupports(index);
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void MaxAllCurrentSupports_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null) return;
        try
        {
            int updated = _save.ReachMaxSupportRanks();
            if (updated == 0) return;
            RefreshCurrentSupports(SelectedSourceIndex(CurrentSupportList));
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void SetCurrentSupport_Click(object? sender, RoutedEventArgs e)
    {
        int index = SelectedSourceIndex(CurrentSupportList);
        if (_save is null || index < 0) return;
        try
        {
            ushort points = ParseUShort(CurrentSupportPoints, "Support points");
            if (_save.Data.Player.CharacterSupportValues[index] == points) return;
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
