using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SaveEditor;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private void RefreshQuests(int selectedIndex = 0)
    {
        if (_save is null) return;
        QuestState.ItemsSource ??= Enum.GetValues<enmQuestState>()
            .Select(state => new Choice((int)state, $"{(int)state} · {state.GetDescription()}"))
            .ToArray();
        var quests = _save.Data.Activities.QuestStateList;
        bool previousLoading = _loading;
        _loading = true;
        try
        {
            QuestList.ItemsSource = quests.Select((state, index) =>
                $"{index:D3} · {Database.GetQuestName(index)} · {state}").ToArray();
            QuestList.SelectedIndex = Math.Clamp(selectedIndex, 0, quests.Length - 1);
        }
        finally
        {
            _loading = previousLoading;
        }
        ShowQuest();
    }

    private void QuestList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading) ShowQuest();
    }

    private void ShowQuest()
    {
        if (_save is null || QuestList.SelectedIndex < 0) return;
        int state = _save.Data.Activities.QuestStateList[QuestList.SelectedIndex];
        QuestState.SelectedItem = QuestState.ItemsSource!.Cast<Choice>()
            .FirstOrDefault(choice => choice.Id == state);
    }

    private void SetQuestState_Click(object? sender, RoutedEventArgs e)
    {
        if (_save is null || QuestList.SelectedIndex < 0 || QuestState.SelectedItem is not Choice state) return;
        try
        {
            int index = QuestList.SelectedIndex;
            _save.Set($"Activities.QuestStateList[{index}]", state.Id);
            RefreshQuests(index);
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }
}
