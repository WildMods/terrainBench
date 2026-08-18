using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace terrainBench.UI;

public partial class GlOverlay : UserControl {
    public GlOverlay() {
        AvaloniaXamlLoader.Load(this);
    }
}
