using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using terrainBench.UI.ViewModels;
using terrainBench.Settings;

namespace terrainBench.UI.Views;

public partial class MainWindow : Avalonia.Controls.Window {
    
    private async void PickModFolder(object sender, RoutedEventArgs e) {
        var top = TopLevel.GetTopLevel(this);
        var folder = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        
        Debug.Assert(DataContext is EditorState);
        var vm = (EditorState)DataContext;
        string path = folder[0].TryGetLocalPath();
        if (path == null) {
            return; // Picker cancelled?
        }

        var settings = Settings.Settings.Load();
        settings.modDir = path;
        settings.Save();
        vm.game.modPath = path;
        Console.WriteLine("Set mod folder to {0}", path);
    }

    
    public MainWindow() {
        InitializeComponent();
    }
}