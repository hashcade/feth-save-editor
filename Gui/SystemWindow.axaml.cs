using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
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

    public SystemWindow() => InitializeComponent();

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
        WriteSystemMenu.IsEnabled = false;
        SystemStatus.Text = "System save loaded. Edits remain in memory until you write a new copy.";
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
                Title = "Save edited system copy",
                SuggestedFileName = "system-edited"
            });
            if (file is null) return;
            if (!file.Path.IsFile) throw new NotSupportedException("Only local files are supported.");
            string destination = Path.GetFullPath(file.Path.LocalPath);
            if (string.Equals(destination, Path.GetFullPath(_sourcePath), StringComparison.OrdinalIgnoreCase))
                throw new IOException("The original system save cannot be overwritten.");
            if (File.Exists(destination)) throw new IOException("The destination already exists.");
            string temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporary, _system.FinishedBytes());
                _ = SystemBuffer.Open(temporary);
                File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            SystemStatus.Text = "Saved and verified edited copy: " + destination;
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
