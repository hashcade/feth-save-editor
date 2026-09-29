using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private readonly Dictionary<ListBox, (string[] Labels, int[] VisibleIndices, int[]? SearchIds)> _searchRows = new();

    private int SelectedSourceIndex(ListBox list)
    {
        if (!_searchRows.TryGetValue(list, out var rows) || list.SelectedIndex < 0
            || list.SelectedIndex >= rows.VisibleIndices.Length) return -1;
        return rows.VisibleIndices[list.SelectedIndex];
    }

    private void SetSearchRows(ListBox list, TextBox search, string[] labels, int selectedSourceIndex,
        int[]? searchIds = null)
    {
        if (searchIds is not null && searchIds.Length != labels.Length)
            throw new ArgumentException("Search IDs must match the list length.", nameof(searchIds));
        _searchRows[list] = (labels, [], searchIds);
        FilterSearchRows(list, search, selectedSourceIndex);
    }

    private void FilterSearchRows(ListBox list, TextBox search, int? selectedSourceIndex = null)
    {
        if (!_searchRows.TryGetValue(list, out var rows)) return;
        int selected = selectedSourceIndex ?? SelectedSourceIndex(list);
        string query = search.Text?.Trim() ?? string.Empty;
        int[] visible = Enumerable.Range(0, rows.Labels.Length)
            .Where(index => rows.Labels[index].Contains(query, StringComparison.OrdinalIgnoreCase)
                || (rows.SearchIds?[index] ?? index).ToString("D3", CultureInfo.InvariantCulture)
                    .Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        _searchRows[list] = (rows.Labels, visible, rows.SearchIds);
        bool previousLoading = _loading;
        _loading = true;
        try
        {
            list.ItemsSource = visible.Select(index => rows.Labels[index]).ToArray();
            int position = Array.IndexOf(visible, selected);
            list.SelectedIndex = position >= 0 ? position : visible.Length > 0 ? 0 : -1;
        }
        finally
        {
            _loading = previousLoading;
        }
    }

    private void SearchableList_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_loading || sender is not TextBox search) return;
        ListBox? list = search.Name switch
        {
            "StorageSearch" => StorageList,
            "MiscSearch" => MiscList,
            "GiftSearch" => GiftList,
            "CurrentCharacterSearch" => CurrentCharacterList,
            "BattalionSearch" => BattalionList,
            "QuestSearch" => QuestList,
            "CurrentSupportSearch" => CurrentSupportList,
            "DatabaseCharacterSearch" => DatabaseCharacters,
            "DatabaseClassSearch" => DatabaseClasses,
            "DatabaseItemSearch" => DatabaseItems,
            _ => null
        };
        if (list is null || !_searchRows.ContainsKey(list)) return;
        FilterSearchRows(list, search);
        if (list == StorageList) ShowStorageItem();
        else if (list == MiscList) ShowMiscAmount();
        else if (list == GiftList) ShowGiftAmount();
        else if (list == CurrentCharacterList)
        {
            _currentCharacter = SelectedSourceIndex(list);
            ShowCurrentCharacter();
        }
        else if (list == BattalionList) ShowBattalion();
        else if (list == QuestList) ShowQuest();
        else if (list == CurrentSupportList) ShowCurrentSupport();
        else if (list == DatabaseCharacters) ShowDatabaseCharacter();
        else if (list == DatabaseClasses) ShowDatabaseClass();
        else if (list == DatabaseItems) ShowDatabaseItem();
    }
}
