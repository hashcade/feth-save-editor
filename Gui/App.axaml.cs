using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace FethEditor.Gui;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Length > 1 && System.IO.File.Exists(arguments[1]))
                Dispatcher.UIThread.Post(() => window.LoadSave(arguments[1]));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
