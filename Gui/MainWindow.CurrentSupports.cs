using System;
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
            SetSearchRows(CurrentSupportList, CurrentSupportSearch, supports.Select((points, index) =>
                $"{index:D3} · {DisplaySupportName(Database.GetSupportTalkName(index))} · "
                + UiStrings.Translate(SupportRankFor(points).Label, _databaseLanguage))
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
        if (_save is null || index < 0) return;
        int points = _save.Data.Player.CharacterSupportValues[index];
        Choice[] ranks = SupportRanks
            .Select(rank => new Choice(rank.Id, UiStrings.Translate(rank.Label, _databaseLanguage)))
            .ToArray();
        CurrentSupportRank.ItemsSource = ranks;
        CurrentSupportRank.SelectedItem = ranks[System.Array.IndexOf(SupportRanks, SupportRankFor(points))];
    }

    private void SetCurrentSupport_Click(object? sender, RoutedEventArgs e)
    {
        int index = SelectedSourceIndex(CurrentSupportList);
        if (_save is null || index < 0) return;
        try
        {
            if (CurrentSupportRank.SelectedItem is not Choice rank)
                throw new InvalidOperationException("Select a support rank.");
            int points = _save.Data.Player.CharacterSupportValues[index];
            if (SupportRankFor(points).Id == rank.Id) return;
            _save.Set($"Player.CharacterSupportValues[{index}]", rank.Id);
            RefreshCurrentSupports(index);
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }
}
