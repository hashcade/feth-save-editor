using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using FethEditor.Core;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private bool _updatingSupportInputs;

    private void ShowSupportInput(ComboBox rank, TextBox pointsInput, int index, int? points)
    {
        _updatingSupportInputs = true;
        try
        {
            string maximum = index < 0 ? "None" : SupportPairRanks.MaxRank(index);
            TextBlock maximumLabel = ReferenceEquals(rank, CurrentSupportRank)
                ? CurrentSupportMaxRank : InheritedSupportMaxRank;
            Button maximumButton = ReferenceEquals(rank, CurrentSupportRank)
                ? MaxCurrentSupportButton : MaxInheritedSupportButton;
            maximumLabel.Text = UiStrings.Translate("Maximum rank:", _databaseLanguage)
                + " " + (maximum == "None" ? "-" : maximum);
            maximumButton.IsEnabled = points is not null && maximum != "None";
            Choice[] choices = points is null || index < 0 ? [] : SupportPairRanks.AvailableRanks(index)
                .Select(name => new Choice(SupportRankPresets.PointsFor(name),
                    UiStrings.Translate(name, _databaseLanguage)))
                .ToArray();
            rank.ItemsSource = choices;
            rank.SelectedIndex = points is null
                ? -1 : Array.FindIndex(choices, choice => choice.Id == SupportRankPresets.PointsFor(
                    SupportPairRanks.RankForPoints(index, points.Value)));
            pointsInput.Text = points?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }
        finally
        {
            _updatingSupportInputs = false;
        }
    }

    private void SupportRank_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingSupportInputs || sender is not ComboBox rank || rank.SelectedItem is not Choice choice)
            return;
        TextBox pointsInput = ReferenceEquals(rank, CurrentSupportRank)
            ? CurrentSupportPoints : InheritedSupportPoints;
        _updatingSupportInputs = true;
        try
        {
            pointsInput.Text = choice.Id.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _updatingSupportInputs = false;
        }
    }

    private void SupportPoints_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingSupportInputs || sender is not TextBox pointsInput
            || !ushort.TryParse(pointsInput.Text, NumberStyles.None,
                CultureInfo.InvariantCulture, out ushort points))
            return;
        ComboBox rank = ReferenceEquals(pointsInput, CurrentSupportPoints)
            ? CurrentSupportRank : SupportRankPreset;
        int index = ReferenceEquals(rank, CurrentSupportRank)
            ? SelectedSourceIndex(CurrentSupportList) : _selectedSupport;
        if (index < 0) return;
        _updatingSupportInputs = true;
        try
        {
            int target = SupportRankPresets.PointsFor(SupportPairRanks.RankForPoints(index, points));
            rank.SelectedIndex = Array.FindIndex(rank.Items.OfType<Choice>().ToArray(),
                choice => choice.Id == target);
        }
        finally
        {
            _updatingSupportInputs = false;
        }
    }
}
