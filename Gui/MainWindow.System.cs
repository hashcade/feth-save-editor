using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using FethEditor.Core;
using SaveEditor;
using SaveEditor.Structs;

namespace FethEditor.Gui;

public partial class MainWindow
{
    private SystemBuffer? _system;
    private string? _systemPath;
    private SystemFlagRow[] _systemFlagRows = [];
    private byte[]? _systemBaseline;
    private bool _systemDirty;

    public void LoadSystem(string path)
    {
        if (_systemDirty && !string.Equals(_systemPath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiStrings.Translate(
                "Save the edited system copy before loading another system save.", _databaseLanguage));
        _system = SystemBuffer.Open(path);
        _systemPath = Path.GetFullPath(path);
        _systemBaseline = _system.FinishedBytes();
        _systemDirty = false;
        RefreshSystemPanel();
        Status.Text = LoadedSystemWarning(_system);
    }

    private void ClearSystem()
    {
        _system = null;
        _systemPath = null;
        _systemFlagRows = [];
        _systemBaseline = null;
        _systemDirty = false;
        RefreshSystemPanel();
    }

    private void RefreshSystemPanel()
    {
        if (_system is null)
        {
            SystemSlots.ItemsSource = null;
            SystemFlags.ItemsSource = null;
            return;
        }

        SystemSlots.ItemsSource = _system.Data.Infos
            .Select((slot, index) => $"[{index:D2}] {slot.GetPlayerName()} · {slot.GetPlaytime()}")
            .ToArray();
        SystemSlots.SelectedIndex = 0;
        _systemFlagRows = Enumerable.Range(0, SystemSaveData_V7.COUNT_FLAGS)
            .Select(index => new SystemFlagRow(index,
                index is >= 8 and < 108
                    ? $"[{index:D4}] {{MOVIE}} {Database.GetString(12823 + index - 8)}"
                    : $"[{index:D4}]",
                _system.GetFlag(index), enabled => OnSystemFlagChanged(index, enabled)))
            .ToArray();
        FilterSystemFlags();
    }

    private string LoadedSystemWarning(SystemBuffer opened)
    {
        if (opened.HasInvalidChecksum)
            return UiStrings.Translate("Warning: system save checksum does not match. Keep the original; saving an edited copy will repair it.", _databaseLanguage);
        if (opened.SourceVersion == 5)
            return UiStrings.Translate("Version-5 system save loaded. Editing and saving it will upgrade it to version 7; keep a backup for older games.", _databaseLanguage);
        return string.Empty;
    }

    private void SystemFlagSearch_TextChanged(object? sender, TextChangedEventArgs e) => FilterSystemFlags();

    private void FilterSystemFlags()
    {
        string search = SystemFlagSearch.Text?.Trim() ?? string.Empty;
        SystemFlags.ItemsSource = _systemFlagRows
            .Where(row => row.Label.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private void OnSystemFlagChanged(int index, bool enabled)
    {
        if (_system is null) return;
        _system.SetFlag(index, enabled);
        _systemDirty = !_system.FinishedBytes().SequenceEqual(_systemBaseline!);
        Status.Text = string.Empty;
    }

    private string? WriteSystemCopy(string directory)
    {
        if (_system is null || !_systemDirty) return null;
        string destination = Path.Combine(directory, "system");
        string? backup = VerifiedFileWriter.Write(destination, _system.FinishedBytes(), path =>
        {
            var verified = SystemBuffer.Open(path);
            return verified.SourceVersion == 7 && !verified.HasInvalidChecksum;
        });
        _systemBaseline = _system.FinishedBytes();
        _systemDirty = false;
        return backup;
    }
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
