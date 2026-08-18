using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using terrainBench.UI.ViewModels;
using terrainBench.UI.Views;

namespace terrainBench.UI;
public class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow = new MainWindow {
                Width = 900,
                Height = 600,
                Title = "Terrain Workbench",
                DataContext = new EditorState(desktop.Args),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}