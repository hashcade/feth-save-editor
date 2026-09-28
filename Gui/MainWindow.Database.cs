using System.Linq;
using Avalonia.Controls;
using SaveEditor;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private int[] _databaseItemIds = [];

    private void RefreshDatabaseViewer()
    {
        var database = Database.BinaryDatabase;
        if (database is null) return;
        _databaseItemIds = database.ItemEntries.Keys.ToArray();
        SetSearchRows(DatabaseCharacters, DatabaseCharacterSearch,
            Enumerable.Range(0, database.CharacterEntries.Count)
            .Select(index => $"[{index:D4}] {Database.GetUnitName(index, ShowGender: true)}")
            .ToArray(), SelectedSourceIndex(DatabaseCharacters));
        SetSearchRows(DatabaseClasses, DatabaseClassSearch,
            Enumerable.Range(0, database.ClassEntries.Count)
            .Select(index => $"[{index:D2}] {Database.GetClassName(index)}")
            .ToArray(), SelectedSourceIndex(DatabaseClasses));
        SetSearchRows(DatabaseItems, DatabaseItemSearch, _databaseItemIds
            .Select(index => $"[{index:D4}] {Database.GetItemName(index)}")
            .ToArray(), SelectedSourceIndex(DatabaseItems));
        ShowDatabaseCharacter();
        ShowDatabaseClass();
        ShowDatabaseItem();
    }

    private void DatabaseCharacters_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        ShowDatabaseCharacter();

    private void DatabaseClasses_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        ShowDatabaseClass();

    private void DatabaseItems_SelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        ShowDatabaseItem();

    private void ShowDatabaseCharacter()
    {
        int index = SelectedSourceIndex(DatabaseCharacters);
        DatabaseCharacterDetails.Text = index >= 0
            ? Database.BinaryDatabase.CharacterEntries[index].GenerateDebugOut() : string.Empty;
    }

    private void ShowDatabaseClass()
    {
        int index = SelectedSourceIndex(DatabaseClasses);
        DatabaseClassDetails.Text = index >= 0
            ? Database.BinaryDatabase.ClassEntries[index].GenerateDebugOut() : string.Empty;
    }

    private void ShowDatabaseItem()
    {
        int index = SelectedSourceIndex(DatabaseItems);
        DatabaseItemDetails.Text = index >= 0 && index < _databaseItemIds.Length
            ? Database.BinaryDatabase.ItemEntries[_databaseItemIds[index]].GenerateDebugOut()
            : string.Empty;
    }
}
