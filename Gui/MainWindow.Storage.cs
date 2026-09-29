using System;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FethEditor.Core;
using SaveEditor;
using SaveEditor.Structs;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private sealed record Choice(int Id, string Label)
    {
        public override string ToString() => Label;
    }

    private Choice[]? _itemChoices;

    private void RefreshStorage(int itemIndex = 0, int miscIndex = 0, int giftIndex = 0)
    {
        if (_save is null) return;
        _itemChoices ??= Database.ItemList.OrderBy(pair => pair.Key)
            .Select(pair => new Choice(pair.Key,
                pair.Key < 0 ? pair.Value : Database.GetItemName(pair.Key))).ToArray();
        StorageItemCombo.ItemsSource = _itemChoices;

        var data = _save.Data;
        bool previousLoading = _loading;
        _loading = true;
        try
        {
            StorageCount.Text = $"{UiStrings.Translate("Item List", _databaseLanguage)} "
                + $"{data.ItemCount} / {data.Items.Length}";
            SetSearchRows(StorageList, StorageSearch,
                data.Items.Select(item => item.ToString()).ToArray(), itemIndex);
            SetSearchRows(MiscList, MiscSearch, Enumerable.Range(0, Player_V23.COUNT_MISC_ITEMS)
                .Select(index => $"{Database.GetMiscItemName(index)} · {data.Player.MiscItems[index]}")
                .ToArray(), miscIndex);
            SetSearchRows(GiftList, GiftSearch, Enumerable.Range(0, Player_V23.COUNT_GIFT_ITEMS)
                .Select(index => $"{Database.GetGiftItemName(index)} · {data.Player.GetGiftItem(index)}")
                .ToArray(), giftIndex);
        }
        finally
        {
            _loading = previousLoading;
        }
        ShowStorageItem();
        ShowMiscAmount();
        ShowGiftAmount();
    }

    private void StorageList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading) ShowStorageItem();
    }

    private void MiscList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading) ShowMiscAmount();
    }

    private void GiftList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading) ShowGiftAmount();
    }

    private void ShowStorageItem()
    {
        int index = SelectedSourceIndex(StorageList);
        if (_save is null || index < 0) return;
        StorageSlotLabel.Text = UiStrings.Format("Slot {0}", _databaseLanguage, index);
        var item = _save.Data.Items[index];
        StorageItemCombo.SelectedItem = _itemChoices?.FirstOrDefault(choice => choice.Id == item.Id);
        StorageDurability.Text = item.Durability.ToString(CultureInfo.InvariantCulture);
        StorageAmount.Text = item.Amount.ToString(CultureInfo.InvariantCulture);
    }

    private void ShowMiscAmount()
    {
        int index = SelectedSourceIndex(MiscList);
        if (_save is null || index < 0) return;
        MiscAmount.Text = _save.Data.Player.MiscItems[index]
            .ToString(CultureInfo.InvariantCulture);
    }

    private void ShowGiftAmount()
    {
        int index = SelectedSourceIndex(GiftList);
        if (_save is null || index < 0) return;
        GiftAmount.Text = _save.Data.Player.GetGiftItem(index)
            .ToString(CultureInfo.InvariantCulture);
    }

    private static byte ParseAmount(TextBox input, string name)
    {
        if (!byte.TryParse(input.Text, NumberStyles.None, CultureInfo.InvariantCulture, out byte value))
            throw new FormatException($"{name} must be a whole number from 0 to 255.");
        return value;
    }

    private void ApplyStorageEdit(Action<SaveBuffer> action, int? item = null, int? misc = null, int? gift = null)
    {
        if (_save is null) return;
        try
        {
            action(_save);
            RefreshStorage(item ?? SelectedSourceIndex(StorageList), misc ?? SelectedSourceIndex(MiscList),
                gift ?? SelectedSourceIndex(GiftList));
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void SaveStorageItem_Click(object? sender, RoutedEventArgs e)
    {
        int slot = SelectedSourceIndex(StorageList);
        if (slot < 0 || StorageItemCombo.SelectedItem is not Choice choice) return;
        try
        {
            byte durability = ParseAmount(StorageDurability, "Durability");
            byte amount = ParseAmount(StorageAmount, "Amount");
            ApplyStorageEdit(save =>
            {
                save.Set($"Items[{slot}].Id", choice.Id);
                save.Set($"Items[{slot}].Durability", choice.Id == -1 ? 0 : durability);
                save.Set($"Items[{slot}].Amount", choice.Id == -1 ? 0 : amount);
                save.SortItems();
            }, item: 0);
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void SortStorage_Click(object? sender, RoutedEventArgs e) =>
        ApplyStorageEdit(save => save.SortItems(), item: 0);

    private void AddEssentialItems_Click(object? sender, RoutedEventArgs e) =>
        ApplyStorageEdit(save => save.AddEssentialItems(), item: 0);

    private void SetStorageDurability_Click(object? sender, RoutedEventArgs e)
    {
        string mode = StorageDurabilityMode.SelectedIndex switch
        {
            0 => "normal",
            1 => "unlimited",
            2 => "weapons-unlimited",
            _ => throw new InvalidOperationException("Choose a durability mode.")
        };
        ApplyStorageEdit(save => save.SetInventoryDurability(mode));
    }

    private void FillMisc_Click(object? sender, RoutedEventArgs e) =>
        ApplyStorageEdit(save => save.FillItems("misc", 99));

    private void FillGifts_Click(object? sender, RoutedEventArgs e) =>
        ApplyStorageEdit(save => save.FillItems("gifts", 99));

    private void SetMisc_Click(object? sender, RoutedEventArgs e)
    {
        int index = SelectedSourceIndex(MiscList);
        if (index < 0) return;
        try
        {
            byte amount = ParseAmount(MiscAmount, "Misc amount");
            ApplyStorageEdit(save => save.Set($"Player.MiscItems[{index}]", amount));
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void SetGift_Click(object? sender, RoutedEventArgs e)
    {
        int index = SelectedSourceIndex(GiftList);
        if (index < 0) return;
        try
        {
            byte amount = ParseAmount(GiftAmount, "Gift amount");
            ApplyStorageEdit(save => save.Set($"gifts[{index}]", amount));
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }
}
