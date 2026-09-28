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
            .Select(pair => new Choice(pair.Key, pair.Value)).ToArray();
        StorageItemCombo.ItemsSource = _itemChoices;

        var data = _save.Data;
        bool previousLoading = _loading;
        _loading = true;
        try
        {
            StorageCount.Text = $"Item List {data.ItemCount} / {data.Items.Length}";
            StorageList.ItemsSource = data.Items.Select((item, index) => $"[{index:D3}] {item}").ToArray();
            MiscList.ItemsSource = Enumerable.Range(0, Player_V23.COUNT_MISC_ITEMS)
                .Select(index => $"{index:D3} · {Database.GetMiscItemName(index)} · {data.Player.MiscItems[index]}")
                .ToArray();
            GiftList.ItemsSource = Enumerable.Range(0, Player_V23.COUNT_GIFT_ITEMS)
                .Select(index => $"{index:D3} · {Database.GetGiftItemName(index)} · {data.Player.GetGiftItem(index)}")
                .ToArray();
            StorageList.SelectedIndex = Math.Clamp(itemIndex, 0, data.Items.Length - 1);
            MiscList.SelectedIndex = Math.Clamp(miscIndex, 0, Player_V23.COUNT_MISC_ITEMS - 1);
            GiftList.SelectedIndex = Math.Clamp(giftIndex, 0, Player_V23.COUNT_GIFT_ITEMS - 1);
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
        if (_save is null || StorageList.SelectedIndex < 0) return;
        var item = _save.Data.Items[StorageList.SelectedIndex];
        StorageItemCombo.SelectedItem = _itemChoices?.FirstOrDefault(choice => choice.Id == item.Id);
        StorageDurability.Text = item.Durability.ToString(CultureInfo.InvariantCulture);
        StorageAmount.Text = item.Amount.ToString(CultureInfo.InvariantCulture);
    }

    private void ShowMiscAmount()
    {
        if (_save is null || MiscList.SelectedIndex < 0) return;
        MiscAmount.Text = _save.Data.Player.MiscItems[MiscList.SelectedIndex]
            .ToString(CultureInfo.InvariantCulture);
    }

    private void ShowGiftAmount()
    {
        if (_save is null || GiftList.SelectedIndex < 0) return;
        GiftAmount.Text = _save.Data.Player.GetGiftItem(GiftList.SelectedIndex)
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
            RefreshStorage(item ?? StorageList.SelectedIndex, misc ?? MiscList.SelectedIndex,
                gift ?? GiftList.SelectedIndex);
            MarkChanged();
        }
        catch (Exception error)
        {
            Status.Text = error.Message;
        }
    }

    private void SaveStorageItem_Click(object? sender, RoutedEventArgs e)
    {
        if (StorageList.SelectedIndex < 0 || StorageItemCombo.SelectedItem is not Choice choice) return;
        int slot = StorageList.SelectedIndex;
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
        int index = MiscList.SelectedIndex;
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
        int index = GiftList.SelectedIndex;
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
