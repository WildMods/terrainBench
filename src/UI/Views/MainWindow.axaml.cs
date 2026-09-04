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

    FilePickerFileType CreatePickerFileType(string name, string[] extensions) {
        var t = new FilePickerFileType(name);
        t.Patterns = extensions;
        return t;
    }

    private async void LoadHeightmapImage(object sender, RoutedEventArgs e) {
        var top = TopLevel.GetTopLevel(this);
        FilePickerFileType[] filters = {
            CreatePickerFileType("PNG Image", new[]{"*.png"}),
            CreatePickerFileType("JPEG Image", new[]{"*.jpg", "*.jpeg"}),
            CreatePickerFileType("WEBP Image", new[]{"*.webp"}),
            CreatePickerFileType("HEIF Image", new[]{"*.heif", "*.heic"}),
            CreatePickerFileType("AVIF Image", new[]{"*.avif"}),
            CreatePickerFileType("JPEG XL Image", new[]{"*.jpegxl", "*.jxl"}),
            CreatePickerFileType("KTX Image", new[]{"*.ktx", "*.ktx2"}),
            CreatePickerFileType("ASTC Image", new[]{"*.astc"}),
            CreatePickerFileType("BMP Image", new[]{"*.bmp"}),
            CreatePickerFileType("PKM Image", new[]{"*.pkm"}),
        };
        var file = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            { AllowMultiple = false, FileTypeFilter = filters });
        
        Debug.Assert(DataContext is EditorState);
        var vm = (EditorState)DataContext;
        string path = file[0].TryGetLocalPath();
        if (path == null) {
            return; // Picker cancelled?
        }

        vm.ReloadFromHeightmap(path);
    }


    public MainWindow() {
        InitializeComponent();
    }
}