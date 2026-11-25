using System.Windows;
using FirewallControl.App.ViewModels;

namespace FirewallControl.App.Services;

public interface ISalesPosWindow
{
    AbmVentasAutoserviceViewModel ViewModel { get; }
    bool AllowPermanentClose { get; set; }
    bool IsVisible { get; }
    bool IsLoaded { get; }
    Window Window { get; }
}

public interface IItemsSelectorWindow
{
    ItemsFacturaVentasViewModel ViewModel { get; }
    bool AllowPermanentClose { get; set; }
    bool IsVisible { get; }
    bool IsLoaded { get; }
    Window Window { get; }
}
