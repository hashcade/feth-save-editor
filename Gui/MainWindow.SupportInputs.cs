using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private bool _updatingSupportInputs;

    private void ShowSupportInput(ComboBox rank, TextBox pointsInput, int? points)
    {
        _updatingSupportInputs = true;
        try
        {
            rank.ItemsSource = points is null ? null : SupportRanks
                .Select(choice => new Choice(choice.Id, UiStrings.Translate(choice.Label, _databaseLanguage)))
                .ToArray();
            rank.SelectedIndex = points is null
                ? -1 : Array.IndexOf(SupportRanks, SupportRankFor(points.Value));
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
        _updatingSupportInputs = true;
        try
        {
            rank.SelectedIndex = Array.IndexOf(SupportRanks, SupportRankFor(points));
        }
        finally
        {
            _updatingSupportInputs = false;
        }
    }
}
