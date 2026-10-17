using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FethEditor.Core;
using FethEditor.Gui;
using SaveEditor;

internal static class GenderTests
{
    public static void Run(string source, string screenshot)
    {
        byte[] original = File.ReadAllBytes(source);
        var save = SaveBuffer.Open(source);
        int initial = save.ReadProtagonistGender() ?? throw new Exception("No protagonist.");
        byte[] baseline = save.FinishedBytes();
        foreach (int gender in new[] { 1 - initial, initial })
        {
            save.SetProtagonistGender(gender);
            Check(save.ReadProtagonistGender() == gender, "Wrong gender.");
            byte[] edited = save.FinishedBytes();
            int changes = baseline.Skip(4).Zip(edited.Skip(4)).Count(pair => pair.First != pair.Second);
            Check(changes == (gender == initial ? 0 : 1), "Changed data outside protagonist identity.");
        }
        Check(save.FinishedBytes().SequenceEqual(baseline), "Round trip changed unrelated data.");
        foreach (int invalid in new[] { -1, 2, 255 })
        {
            try { save.SetProtagonistGender(invalid); throw new Exception("Invalid gender accepted."); }
            catch (ArgumentOutOfRangeException) { }
        }
        Environment.SetEnvironmentVariable("FETH_EDITOR_LANGUAGE", "zh_hans");
        Database.Init(enmLanguage.zh_hans);
        var window = new MainWindow { Width = 1232, Height = 880 };
        window.Show();
        var input = window.FindControl<ComboBox>("PlayerGenderInput")!;
        Check(!input.IsEnabled, "Gender is editable without a save.");
        window.LoadSave(source);
        Check(input.IsEnabled && input.SelectedIndex == initial, "Main gender did not load.");
        foreach (int gender in new[] { 1 - initial, initial })
        {
            input.SelectedIndex = gender;
            Dispatcher.UIThread.RunJobs();
            var current = (SaveBuffer)typeof(MainWindow).GetField("_save", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            Check(current.ReadProtagonistGender() == gender, "Main selection did not edit gender.");
            Check(baseline.Skip(4).Zip(current.FinishedBytes().Skip(4)).Count(pair => pair.First != pair.Second)
                  == (gender == initial ? 0 : 1), "GUI changed unrelated data.");
        }
        for (int i = 0; i < 12; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            System.Threading.Thread.Sleep(20);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(screenshot))!);
        using var frame = window.CaptureRenderedFrame() ?? throw new Exception("No Main render.");
        frame.Save(screenshot, PngBitmapEncoderOptions.Default);
        window.Close();
        Check(File.ReadAllBytes(source).SequenceEqual(original), "Private source save changed.");
        Console.WriteLine($"Gender core/GUI, exact byte preservation and input limits passed. Screenshot: {screenshot}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
