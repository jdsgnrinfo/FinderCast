using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace FinderCast.Controls;

/// <summary>
/// Acrylic del escritorio que se ve siempre como en una ventana activa. La tarjeta de resultados
/// nunca se activa (el teclado sigue en la búsqueda) y con el Acrylic normal Windows la pintaría
/// con el relleno opaco de las ventanas inactivas, distinta de la de búsqueda.
/// </summary>
internal sealed partial class ActiveAcrylic : SystemBackdrop
{
    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _configuration;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(target, xamlRoot);
        _configuration = new SystemBackdropConfiguration { IsInputActive = true };
        UpdateTheme(xamlRoot);
        _controller = new DesktopAcrylicController();
        _controller.SetSystemBackdropConfiguration(_configuration);
        _controller.AddSystemBackdropTarget(target);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        base.OnTargetDisconnected(target);
        _controller?.RemoveSystemBackdropTarget(target);
        _controller?.Dispose();
        _controller = null;
    }

    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
        => UpdateTheme(xamlRoot);

    private void UpdateTheme(XamlRoot xamlRoot)
    {
        if (_configuration is null) return;
        _configuration.Theme = (xamlRoot.Content as FrameworkElement)?.ActualTheme switch
        {
            ElementTheme.Light => SystemBackdropTheme.Light,
            ElementTheme.Dark => SystemBackdropTheme.Dark,
            _ => SystemBackdropTheme.Default,
        };
    }
}
