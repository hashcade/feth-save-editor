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
        QuestState.ItemsSource = Enum.GetValues<enmQuestState>()
            .Select(state => new Choice((int)state,
                UiStrings.Translate(state.GetDescription(), _databaseLanguage)))
            .ToArray();
        var states = QuestState.ItemsSource.Cast<Choice>().ToDictionary(choice => choice.Id, choice => choice.Label);
        var quests = _save.Data.Activities.QuestStateList;
        bool previousLoading = _loading;
        _loading = true;
        try
        {
            SetSearchRows(QuestList, QuestSearch, quests.Select((state, index) =>
                $"{Database.GetQuestName(index)} · {(states.TryGetValue(state, out string? label) ? label : state.ToString())}")
                .ToArray(), selectedIndex);
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
        int index = SelectedSourceIndex(QuestList);
        if (_save is null || index < 0) return;
        int state = _save.Data.Activities.QuestStateList[index];
        QuestState.SelectedItem = QuestState.ItemsSource!.Cast<Choice>()
            .FirstOrDefault(choice => choice.Id == state);
    }

    private void SetQuestState_Click(object? sender, RoutedEventArgs e)
    {
        int index = SelectedSourceIndex(QuestList);
        if (_save is null || index < 0 || QuestState.SelectedItem is not Choice state) return;
        try
        {
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
