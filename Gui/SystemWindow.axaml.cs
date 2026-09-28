using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FethEditor.Core;
using SaveEditor;
using SaveEditor.Structs;

namespace FethEditor.Gui;

public partial class SystemWindow : Window
{
    private SystemBuffer? _system;
    private string? _sourcePath;

    public SystemWindow()
    {
        InitializeComponent();
        DragDrop.AddDragOverHandler(this, SystemDragOver);
        DragDrop.AddDropHandler(this, SystemDropped);
    }

    private void SystemDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy : DragDropEffects.None;

    private void SystemDropped(object? sender, DragEventArgs e)
    {
        var file = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault();
        if (file is null) return;
        try
        {
            if (!file.Path.IsFile) throw new NotSupportedException("Only local files are supported.");
            LoadSystem(file.Path.LocalPath);
        }
        catch (Exception error)
        {
            SystemStatus.Text = "Could not load system save: " + error.Message;
        }
    }

    public void LoadSystem(string path)
    {
        var opened = SystemBuffer.Open(path);
        _system = opened;
        _sourcePath = path;
        SystemSlots.ItemsSource = opened.Data.Infos
            .Select((slot, index) => $"[{index:D2}] {slot.GetPlayerName()} · {slot.GetPlaytime()}")
            .ToArray();
        SystemFlags.ItemsSource = Enumerable.Range(0, SystemSaveData_V7.COUNT_FLAGS)
            .Select(index => new SystemFlagRow(index,
                index is >= 8 and < 108
                    ? $"[{index:D4}] {{MOVIE}} {Database.GetString(12823 + index - 8)}"
                    : $"[{index:D4}]",
                opened.GetFlag(index), enabled => OnFlagChanged(index, enabled)))
            .ToArray();
        SystemSlots.SelectedIndex = 0;
        WriteSystemMenu.IsEnabled = true;
        SystemStatus.Text = opened.HasInvalidChecksum
            ? "Warning: system save checksum does not match. Keep the original; writing will repair the checksum."
            : opened.SourceVersion == 5
                ? "Version-5 system save loaded. Writing will upgrade it to version 7; keep a backup for older games."
                : "System save loaded. Edits remain in memory until written.";
    }

    private void OnFlagChanged(int index, bool enabled)
    {
        if (_system is null) return;
        _system.SetFlag(index, enabled);
        WriteSystemMenu.IsEnabled = true;
        SystemStatus.Text = $"Flag {index} changed. Write an edited copy to keep it.";
    }

    private async void OpenSystem_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open a Fire Emblem system save",
                AllowMultiple = false
            });
            if (files.Count == 0) return;
            if (!files[0].Path.IsFile) throw new NotSupportedException("Only local files are supported.");
            LoadSystem(files[0].Path.LocalPath);
        }
        catch (Exception error)
        {
            SystemStatus.Text = "Could not load system save: " + error.Message;
        }
    }

    private async void WriteSystem_Click(object? sender, RoutedEventArgs e)
    {
        if (_system is null || _sourcePath is null) return;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Write system save",
                SuggestedFileName = Path.GetFileName(_sourcePath)
            });
            if (file is null) return;
            if (!file.Path.IsFile) throw new NotSupportedException("Only local files are supported.");
            string destination = Path.GetFullPath(file.Path.LocalPath);
            string? backup = VerifiedFileWriter.Write(destination, _system.FinishedBytes(), path =>
            {
                var verified = SystemBuffer.Open(path);
                return verified.SourceVersion == 7 && !verified.HasInvalidChecksum;
            });
            SystemStatus.Text = backup is null
                ? "Saved and verified: " + destination
                : "Saved and verified: " + destination + " (backup: " + backup + ")";
        }
        catch (Exception error)
        {
            SystemStatus.Text = "Could not save copy: " + error.Message;
        }
    }

    private void Exit_Click(object? sender, RoutedEventArgs e) => Close();

}

public sealed class SystemFlagRow(int index, string label, bool enabled, Action<bool> changed)
{
    private bool _enabled = enabled;
    public int Index { get; } = index;
    public string Label { get; } = label;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            changed(value);
        }
    }
}
