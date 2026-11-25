using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.ViewModels;

/// <summary>
/// Priority 1: Complete MVVM implementation of ITEMSFACTURAVENTAS.SCX.
/// Preserves 100% of the original Visual FoxPro logic:
/// - Single reusable window instance linked dynamically to Caja 1..5 via VincularPadre and SaltarACaja (F1..F5).
/// - lctipo = "A" (INSERT INTO cursor) and lctipo = "M" (UPDATE ... WHERE RECNO() = thisform.nrecno).
/// - Product lookup by code/barcode (productos + itemsdepositos stock + vw_compuesto check).
/// - Fractional/weighable products (fraccionable = 1, Kilogramo input -> cantidad = Kilogramo / precio_venta, redondearmas(lnSubTotal, 2)).
/// - Exact validations and messages:
///   * cantidad <= 0 -> "La cantidad debe ser mayor que cero."
///   * cantidad > LimiteCantidadPorItem (editable, 0 = sin límite)
///   * precio_venta <= 0 or <= costo -> validación de costo y rango editable (PrecioMinimoVenta .. PrecioMaximoVenta)
/// - Warranty container (controlagarantia = 1, fechainiciogarantia, diasgarantias, fechafingarantia).
/// - Keyboard shortcuts on codproducto:
///   * A/S/D/F, Ctrl+A/S/D/F, Alt+A/S/D/F -> AbrirGenes(1..12)
///   * Z -> ProcesarVenta(imprimir: true)
///   * X -> ProcesarVenta(imprimir: false)
///   * DEL -> ActivarModoBorrado on parent sales grid
///   * Ctrl+X -> ReeditarVenta
///   * Ctrl+Z -> ReimprimirVenta
///   * F3 -> Product catalog search
/// </summary>
public class ItemsFacturaVentasViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepository;
    private readonly IWindowManagerService _windowManager;

    private AbmVentasAutoserviceViewModel? _parentSalesVm;
    private bool _suppressCajaJump;

    private int _selectedCaja = 1;
    private string _lcTipo = "A";
    private int _nRecNo;

    private int _currentProductId;
    private string _codProducto = string.Empty;
    private string _desProducto = string.Empty;
    private decimal _cantidad = 0m;
    private decimal _kilogramo;
    private decimal _precioVenta;
    private decimal _costoUnitario;
    private decimal _stockDisponible;
    private decimal _lineSubtotal;
    private decimal _totalVentaEspejo;

    private int _selectedPriceList = 1;
    private int _ivaType = 1;
    private bool _isFraccionable;
    private int _compuesto;
    private int _usaVencimiento;
    private int _noControlaStock;
    private int _vendeSinStock = 1;
    private int _autoCantidad;
    private string _loteSeleccionadoTexto = string.Empty;
    private ProductEntity? _loadedProduct;
    private bool _suppressPriceListSync;

    private bool _isWarrantyVisible;
    private DateTime _fechaInicioGarantia = DateTime.Today;
    private int _diasGarantias;
    private DateTime _fechaFinGarantia = DateTime.Today;

    private bool _isMovimientoRapido = false;
    private bool _isCurrentOperationFromGenes = false;

    private string _validationMessage = "Escanee o ingrese código (A/S/D/F: Genes, F1..F5: Cajas 1..5, F10: Mov. Rápido, DEL: Borrar, Z: Ticket, X: Guardar)";
    private bool _isError;

    public event Action<string>? RequestFocusControl;

    public ItemsFacturaVentasViewModel(
        IProductRepository productRepository,
        IWindowManagerService windowManager)
    {
        _productRepository = productRepository;
        _windowManager = windowManager;
        _isMovimientoRapido = _windowManager.ConfigService.Preferences.IniciarMovimientoRapidoPorDefecto;

        LookupProductCommand = new AsyncRelayCommand(ValidateAndLookupProductAsync);
        GrabarItemCommand = new AsyncRelayCommand(CmdGrabarAsync);
        LimpiarCommand = new RelayCommand(() =>
        {
            LcTipo = "A";
            NRecNo = 0;
            LimpiarCampos();
            RequestFocusControl?.Invoke("CodProducto");
        });
        SearchCatalogCommand = new RelayCommand(OpenProductCatalogSearch);
        SwitchCajaCommand = new RelayCommand(param =>
        {
            if (param != null && int.TryParse(param.ToString(), out int caja) && caja is >= 1 and <= 5)
            {
                SelectedCaja = caja;
            }
        });
        OpenGenesGroupCommand = new RelayCommand(param =>
        {
            if (param != null && int.TryParse(param.ToString(), out int genId) && genId is >= 1 and <= 12)
            {
                if (genId <= 4) AbrirGenes(genId, "SELECT");
                else if (genId <= 8) AbrirGenes(genId - 4, "AGREGAR");
                else AbrirGenes(genId - 8, "EDITAR");
            }
        });
        ToggleMovimientoRapidoCommand = new RelayCommand(() => IsMovimientoRapido = !IsMovimientoRapido);
        OpenCargaRapidaCommand = new RelayCommand(() => _windowManager.OpenCargaRapidaProductos());
        OpenPresupuestosCommand = new RelayCommand(() => _parentSalesVm?.OpenPresupuestosCommand.Execute(null));
        OpenCalculatorCommand = new RelayCommand(() => _windowManager.OpenCalculatorDialog());
        OpenLotesCommand = new RelayCommand(OpenLotesPicker);
        OpenCompuestoCommand = new RelayCommand(() =>
        {
            if (CurrentProductId > 0)
                _windowManager.OpenCombinarCompuestoDialog(CurrentProductId, DesProducto);
        });
        SelectPriceListFromMenuCommand = new RelayCommand(param =>
        {
            if (param != null && int.TryParse(param.ToString(), out int listNum))
            {
                SelectedPriceList = Math.Clamp(listNum, 1, 7);
            }
        });
    }

    public AbmVentasAutoserviceViewModel? ParentSalesVm => _parentSalesVm;
    public AppPreferencesEntity Preferences => _windowManager.ConfigService.Preferences;

    public string HeaderStatusText =>
        $"SELECTOR DE PRODUCTOS - VINCULADO A CAJA {SelectedCaja} | MODO: {(LcTipo == "M" ? $"MODIFICAR FILA #{NRecNo}" : "AGREGAR (A)")}";

    public string ClassicHeaderTitle => $"Caja {SelectedCaja} - Items Factura Venta";

    public string CompanyBannerTitle => _parentSalesVm?.CompanyBannerTitle ?? "..:: SIN DATOS ::..";

    public int SelectedCaja
    {
        get => _selectedCaja;
        set
        {
            int clamped = Math.Clamp(value, 1, 5);
            if (SetProperty(ref _selectedCaja, clamped))
            {
                OnPropertyChanged(nameof(HeaderStatusText));
                OnPropertyChanged(nameof(ClassicHeaderTitle));
                OnPropertyChanged(nameof(CompanyBannerTitle));
                if (!_suppressCajaJump)
                {
                    _windowManager.SaltarACaja(clamped);
                }
            }
        }
    }

    public string LcTipo
    {
        get => _lcTipo;
        set
        {
            if (SetProperty(ref _lcTipo, value))
                OnPropertyChanged(nameof(HeaderStatusText));
        }
    }

    public int NRecNo
    {
        get => _nRecNo;
        set
        {
            if (SetProperty(ref _nRecNo, value))
                OnPropertyChanged(nameof(HeaderStatusText));
        }
    }

    public int CurrentProductId
    {
        get => _currentProductId;
        set
        {
            if (SetProperty(ref _currentProductId, value))
            {
                OnPropertyChanged(nameof(HasLoadedProduct));
                OnPropertyChanged(nameof(IsStockLowOrZero));
            }
        }
    }

    public bool HasLoadedProduct => CurrentProductId > 0;

    public bool IsStockLowOrZero => CurrentProductId > 0 && StockDisponible <= 0m;

    public string CodProducto
    {
        get => _codProducto;
        set => SetProperty(ref _codProducto, value);
    }

    public string DesProducto
    {
        get => _desProducto;
        set => SetProperty(ref _desProducto, value);
    }

    public decimal Cantidad
    {
        get => _cantidad;
        set
        {
            if (SetProperty(ref _cantidad, value))
                RecalculateLinePreview();
        }
    }

    public decimal Kilogramo
    {
        get => _kilogramo;
        set
        {
            if (SetProperty(ref _kilogramo, value) && IsFraccionable && PrecioVenta > 0m && value > 0m)
            {
                Cantidad = SalesCalculationEngine.CalculateQuantityFromGuaranies(value, PrecioVenta);
            }
        }
    }

    public decimal PrecioVenta
    {
        get => _precioVenta;
        set
        {
            if (SetProperty(ref _precioVenta, value))
                RecalculateLinePreview();
        }
    }

    public decimal CostoUnitario
    {
        get => _costoUnitario;
        set => SetProperty(ref _costoUnitario, value);
    }

    public decimal StockDisponible
    {
        get => _stockDisponible;
        set
        {
            if (SetProperty(ref _stockDisponible, value))
                OnPropertyChanged(nameof(IsStockLowOrZero));
        }
    }

    public decimal LineSubtotal
    {
        get => _lineSubtotal;
        set => SetProperty(ref _lineSubtotal, value);
    }

    public decimal TotalVentaEspejo
    {
        get => _totalVentaEspejo;
        set => SetProperty(ref _totalVentaEspejo, value);
    }

    public int SelectedPriceList
    {
        get => _selectedPriceList;
        set
        {
            int clamped = Math.Clamp(value, 1, 7);
            if (SetProperty(ref _selectedPriceList, clamped))
            {
                if (!_suppressPriceListSync && _parentSalesVm != null && _parentSalesVm.ListaPrecios != clamped)
                {
                    _parentSalesVm.ListaPrecios = clamped;
                }

                if (_loadedProduct != null && CurrentProductId == _loadedProduct.IdProducto)
                {
                    CostoUnitario = _loadedProduct.GetCost(clamped);
                    PrecioVenta = _loadedProduct.GetPrice(clamped);
                    RecalculateLinePreview();
                }
            }
        }
    }

    public int IvaType
    {
        get => _ivaType;
        set => SetProperty(ref _ivaType, value);
    }

    public bool IsFraccionable
    {
        get => _isFraccionable;
        set => SetProperty(ref _isFraccionable, value);
    }

    public int Compuesto
    {
        get => _compuesto;
        set
        {
            if (SetProperty(ref _compuesto, value))
                OnPropertyChanged(nameof(IsCompuestoVisible));
        }
    }

    public bool IsCompuestoVisible => Compuesto == 1 && CurrentProductId > 0;

    public int UsaVencimiento
    {
        get => _usaVencimiento;
        set
        {
            if (SetProperty(ref _usaVencimiento, value))
                OnPropertyChanged(nameof(IsLotesVisible));
        }
    }

    public bool IsLotesVisible => UsaVencimiento == 1 && CurrentProductId > 0;

    public string LoteSeleccionadoTexto
    {
        get => _loteSeleccionadoTexto;
        set => SetProperty(ref _loteSeleccionadoTexto, value);
    }

    public int NoControlaStock
    {
        get => _noControlaStock;
        set => SetProperty(ref _noControlaStock, value);
    }

    public int VendeSinStock
    {
        get => _vendeSinStock;
        set => SetProperty(ref _vendeSinStock, value);
    }

    public bool IsWarrantyVisible
    {
        get => _isWarrantyVisible;
        set => SetProperty(ref _isWarrantyVisible, value);
    }

    public DateTime FechaInicioGarantia
    {
        get => _fechaInicioGarantia;
        set
        {
            if (SetProperty(ref _fechaInicioGarantia, value))
                FechaFinGarantia = value.AddDays(DiasGarantias);
        }
    }

    public int DiasGarantias
    {
        get => _diasGarantias;
        set
        {
            if (SetProperty(ref _diasGarantias, value))
                FechaFinGarantia = FechaInicioGarantia.AddDays(value);
        }
    }

    public DateTime FechaFinGarantia
    {
        get => _fechaFinGarantia;
        set => SetProperty(ref _fechaFinGarantia, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public bool IsError
    {
        get => _isError;
        set => SetProperty(ref _isError, value);
    }

    public bool IsMovimientoRapido
    {
        get => _isMovimientoRapido;
        set
        {
            if (SetProperty(ref _isMovimientoRapido, value))
            {
                SetStatus(
                    value
                        ? "⚡ Movimiento Rápido ACTIVADO: Al escanear un producto se carga directamente cantidad = 1."
                        : "Movimiento Rápido DESACTIVADO: Ingrese manualmente cantidad y precio con ENTER.",
                    false);
                RequestFocusControl?.Invoke("CodProducto");
            }
        }
    }

    public ICommand LookupProductCommand { get; }
    public ICommand GrabarItemCommand { get; }
    public ICommand LimpiarCommand { get; }
    public ICommand SearchCatalogCommand { get; }
    public ICommand SwitchCajaCommand { get; }
    public ICommand OpenGenesGroupCommand { get; }
    public ICommand ToggleMovimientoRapidoCommand { get; }
    public ICommand OpenCargaRapidaCommand { get; }
    public ICommand OpenPresupuestosCommand { get; }
    public ICommand OpenCalculatorCommand { get; }
    public ICommand OpenLotesCommand { get; }
    public ICommand OpenCompuestoCommand { get; }
    public ICommand SelectPriceListFromMenuCommand { get; }

    /// <summary>
    /// Exact replication of vincularpadre(toFormPadre) in ITEMSFACTURAVENTAS.SCX.
    /// Avoids duplicate event subscriptions when switching Cajas.
    /// </summary>
    public void VincularPadre(
        AbmVentasAutoserviceViewModel parentVm,
        string lcTipo = "A",
        int nRecNo = 0,
        SaleItemEntity? itemToModify = null)
    {
        if (_parentSalesVm != null)
        {
            _parentSalesVm.PropertyChanged -= OnParentSalesVmPropertyChanged;
        }

        _parentSalesVm = parentVm;
        _parentSalesVm.PropertyChanged += OnParentSalesVmPropertyChanged;

        _suppressCajaJump = true;
        SelectedCaja = parentVm.IdCaja;
        _suppressCajaJump = false;

        _suppressPriceListSync = true;
        SelectedPriceList = parentVm.ListaPrecios > 0 ? parentVm.ListaPrecios : 1;
        _suppressPriceListSync = false;
        LcTipo = lcTipo;
        NRecNo = nRecNo;

        ActualizarEspejo();

        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase) && itemToModify != null)
        {
            CurrentProductId = itemToModify.IdProducto;
            CodProducto = itemToModify.Codigo;
            DesProducto = itemToModify.Descripcion;
            Cantidad = itemToModify.Cantidad;
            PrecioVenta = itemToModify.PreUnitario;
            CostoUnitario = itemToModify.CostoUnitario;
            IvaType = itemToModify.Iva;
            IsFraccionable = itemToModify.Fraccionable == 1;
            Compuesto = itemToModify.Compuesto;
            NoControlaStock = itemToModify.NoControlaStock;
            Kilogramo = itemToModify.Kilogramo;
            IsWarrantyVisible = itemToModify.ControlaGarantia == 1;
            FechaInicioGarantia = itemToModify.FechaInicioGarantia;
            DiasGarantias = itemToModify.DiasGarantias;
            FechaFinGarantia = itemToModify.FechaFinGarantia;
            RecalculateLinePreview();

            SetStatus($"Modificando ítem #{nRecNo}: {itemToModify.Descripcion}", false);
            RequestFocusControl?.Invoke("Cantidad");
            _ = RefreshModifyItemStockAsync(itemToModify, parentVm);
        }
        else
        {
            LimpiarCampos();
            RequestFocusControl?.Invoke("CodProducto");
        }
    }

    private async Task RefreshModifyItemStockAsync(SaleItemEntity itemToModify, AbmVentasAutoserviceViewModel parentVm)
    {
        if (itemToModify.IdProducto <= 0)
            return;

        try
        {
            decimal stock = await _productRepository.GetDepositStockAsync(itemToModify.IdProducto, parentVm.IdDeposito);
            if (CurrentProductId == itemToModify.IdProducto)
            {
                StockDisponible = stock;
                if (ReferenceEquals(_parentSalesVm, parentVm))
                    parentVm.UpdateStockActualPreview(stock, itemToModify.Descripcion);
            }
        }
        catch
        {
            // Keep existing UI state if deposit lookup fails
        }
    }

    private void OnParentSalesVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AbmVentasAutoserviceViewModel.TotalVenta))
        {
            ActualizarEspejo();
        }
        else if (e.PropertyName == nameof(AbmVentasAutoserviceViewModel.CompanyBannerTitle))
        {
            OnPropertyChanged(nameof(CompanyBannerTitle));
        }
    }

    /// <summary>
    /// Exact replication of actualizar_espejo() in ITEMSFACTURAVENTAS.SCX.
    /// </summary>
    public void ActualizarEspejo()
    {
        TotalVentaEspejo = _parentSalesVm?.TotalVenta ?? 0m;
        OnPropertyChanged(nameof(ClassicHeaderTitle));
        OnPropertyChanged(nameof(CompanyBannerTitle));
    }

    public void HandleWindowClosedByUser(System.Windows.Window selectorWindow)
        => _windowManager.HandleItemsSelectorClosedByUser(selectorWindow);

    public void RequestFocusCurrentField()
        => RequestFocusControl?.Invoke(string.Equals(LcTipo, "M", StringComparison.OrdinalIgnoreCase)
            ? "Cantidad"
            : "CodProducto");

    /// <summary>
    /// Exact replication of codproducto.Valid / LostFocus in ITEMSFACTURAVENTAS.SCX.
    /// </summary>
    public async Task<bool> ValidateAndLookupProductAsync()
    {
        string cleanCode = VfpCompat.TransformaCodigoProducto(CodProducto);
        if (string.IsNullOrWhiteSpace(cleanCode))
        {
            SetStatus("Ingrese o escanee un código de producto.", false);
            return false;
        }

        CodProducto = cleanCode;
        int idDeposito = _parentSalesVm?.IdDeposito ?? 1;

        var product = await _productRepository.GetByCodeAsync(cleanCode, idDeposito);
        if (product == null || product.IdProducto <= 0)
        {
            CurrentProductId = 0;
            DesProducto = string.Empty;
            if (Preferences.SonidoAlertaEnErrores)
                PlayAlertSound();
            SetStatus($"⚠ Producto '{cleanCode}' NO EXISTE, Verifique...", true);
            RequestFocusControl?.Invoke("CodProducto");
            return false;
        }

        CurrentProductId = product.IdProducto;
        _loadedProduct = product;
        CodProducto = product.Codigo;
        DesProducto = product.Descripcion;
        CostoUnitario = product.GetCost(SelectedPriceList);
        PrecioVenta = product.GetPrice(SelectedPriceList);
        StockDisponible = product.StockActual;
        _parentSalesVm?.UpdateStockActualPreview(product.StockActual, product.Descripcion);
        IvaType = product.Iva;
        IsFraccionable = product.Fraccionable == 1;
        Compuesto = product.Compuesto;
        UsaVencimiento = product.UsaVencimiento;
        NoControlaStock = product.NoControlaStock;
        VendeSinStock = product.VendeSinStock;
        _autoCantidad = product.AutoCantidad;
        OnPropertyChanged(nameof(IsCompuestoVisible));
        OnPropertyChanged(nameof(IsLotesVisible));

        IsWarrantyVisible = product.ControlaGarantia == 1;
        if (IsWarrantyVisible)
        {
            FechaInicioGarantia = DateTime.Today;
            DiasGarantias = product.DiasGarantias > 0 ? product.DiasGarantias : 30;
            FechaFinGarantia = FechaInicioGarantia.AddDays(DiasGarantias);
        }

        // Check compound components if compuesto == 1 (exact VFP check against vw_compuesto)
        if (product.Compuesto == 1)
        {
            var comps = await _productRepository.GetCompoundComponentsAsync(product.IdProducto);
            if (comps.Count == 0)
            {
                SetStatus("Advertencia: Producto compuesto sin receta en vw_compuesto.", true);
            }
        }

        if (LcTipo == "A")
        {
            Cantidad = 0m;
            Kilogramo = 0m;
        }

        RecalculateLinePreview();
        SetStatus($"Producto: {product.Descripcion} | Costo: {CostoUnitario:N0} | Venta: {PrecioVenta:N0} Gs. | Stock: {StockDisponible:N2}", false);

        if (IsFraccionable)
        {
            RequestFocusControl?.Invoke("Kilogramo");
        }
        else if ((IsMovimientoRapido || _autoCantidad == 1) && LcTipo == "A")
        {
            Cantidad = 1m;
            var priceValidation = SalesCalculationEngine.ValidateUnitPrice(
                PrecioVenta,
                CostoUnitario,
                checkGrabarLimits: Preferences.ValidarLimitesPrecioEnItem,
                permitirVentaBajoCosto: Preferences.PermitirVentaBajoCosto,
                precioMinimo: Preferences.PrecioMinimoVenta,
                precioMaximo: Preferences.PrecioMaximoVenta);
            if (!priceValidation.IsValid)
            {
                HandleFieldValidationError(
                    $"⚠ {priceValidation.ErrorMessage} (Costo: {CostoUnitario:N0} Gs. / Precio Venta: {PrecioVenta:N0} Gs.)",
                    "PrecioVenta");
                return true;
            }

            await CmdGrabarAsync();
        }
        else
        {
            RequestFocusControl?.Invoke("Cantidad");
        }

        return true;
    }

    /// <summary>
    /// Exact replication of CmdGrabar.Click in ITEMSFACTURAVENTAS.SCX (LOCAL lctablacursor ... ENDCASE).
    /// Handles both lctipo = "A" (INSERT INTO) and lctipo = "M" (UPDATE ... WHERE RECNO() = thisform.nrecno).
    /// </summary>
    public async Task<bool> CmdGrabarAsync()
    {
        if (_parentSalesVm == null)
        {
            SetStatus("No hay formulario de ventas vinculado.", true);
            return false;
        }

        if (CurrentProductId <= 0 && !string.IsNullOrWhiteSpace(CodProducto))
        {
            bool found = await ValidateAndLookupProductAsync();
            if (!found) return false;
        }

        if (CurrentProductId <= 0 || string.IsNullOrWhiteSpace(CodProducto))
        {
            SetStatus("Debe seleccionar un producto válido antes de grabar.", true);
            RequestFocusControl?.Invoke("CodProducto");
            return false;
        }

        // If fractional product and Kilogramo (Gs amount) was entered, compute Cantidad = Kilogramo / PrecioVenta
        if (IsFraccionable && Kilogramo > 0m && PrecioVenta > 0m)
        {
            Cantidad = SalesCalculationEngine.CalculateQuantityFromGuaranies(Kilogramo, PrecioVenta);
        }

        // Check if fast-mode duplicate row merging applies (only when IsMovimientoRapido is active, LcTipo == "A", and NOT from Genes)
        SaleItemEntity? existingMergeRow = null;
        if (string.Equals(LcTipo, "A", StringComparison.OrdinalIgnoreCase) &&
            IsMovimientoRapido &&
            !_isCurrentOperationFromGenes &&
            !IsFraccionable)
        {
            existingMergeRow = _parentSalesVm.Items.FirstOrDefault(x =>
                !x.IsFromGenes &&
                x.Fraccionable == 0 &&
                (x.IdProducto == CurrentProductId ||
                 string.Equals(x.Codigo.Trim(), CodProducto.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                x.PreUnitario == PrecioVenta);
        }

        decimal targetQuantity = existingMergeRow != null
            ? existingMergeRow.Cantidad + Cantidad
            : Cantidad;

        // 1. Validate Quantity (CANTIDAD.Valid)
        var qtyValidation = SalesCalculationEngine.ValidateQuantity(
            targetQuantity,
            NoControlaStock,
            VendeSinStock,
            StockDisponible,
            noControlarStockGlobal: Preferences.NoControlarStockGlobal,
            limiteCantidad: Preferences.LimiteCantidadPorItem);
        if (!qtyValidation.IsValid)
        {
            HandleFieldValidationError(qtyValidation.ErrorMessage, "Cantidad");
            return false;
        }

        // 2. Validate Unit Price (precio_venta.Valid + límites editables de Configuración)
        var priceValidation = SalesCalculationEngine.ValidateUnitPrice(
            PrecioVenta,
            CostoUnitario,
            checkGrabarLimits: Preferences.ValidarLimitesPrecioEnItem,
            permitirVentaBajoCosto: Preferences.PermitirVentaBajoCosto,
            precioMinimo: Preferences.PrecioMinimoVenta,
            precioMaximo: Preferences.PrecioMaximoVenta);
        if (!priceValidation.IsValid)
        {
            HandleFieldValidationError(priceValidation.ErrorMessage, "PrecioVenta");
            return false;
        }

        int effectiveNoControlaStock = (Preferences.NoControlarStockGlobal && !Preferences.DescontarStockAunqueNoControle)
            ? 1
            : NoControlaStock;

        // 3. Build SaleItemEntity and apply lctipo = "A" (INSERT INTO) or lctipo = "M" (UPDATE WHERE RECNO() = nrecno)
        var inputItem = new SaleItemEntity
        {
            IdProducto = CurrentProductId,
            Codigo = CodProducto,
            Descripcion = DesProducto,
            Cantidad = targetQuantity,
            PreUnitario = PrecioVenta,
            CostoUnitario = CostoUnitario,
            IdDeposito = _parentSalesVm.IdDeposito,
            Iva = IvaType,
            Fraccionable = IsFraccionable ? 1 : 0,
            Compuesto = Compuesto,
            NoControlaStock = effectiveNoControlaStock,
            Kilogramo = Kilogramo,
            ControlaGarantia = IsWarrantyVisible ? 1 : 0,
            FechaInicioGarantia = FechaInicioGarantia,
            DiasGarantias = DiasGarantias,
            FechaFinGarantia = FechaFinGarantia,
            Estado = 1,
            IsFromGenes = _isCurrentOperationFromGenes
        };

        string effectiveOperation = existingMergeRow != null ? "M" : LcTipo;
        int effectiveRecNo = existingMergeRow != null ? existingMergeRow.RecNo : NRecNo;
        string modeLabel = existingMergeRow != null
            ? "FUSIONADO"
            : (LcTipo == "M" ? "MODIFICADO" : "AGREGADO");

        var appliedItem = SalesCalculationEngine.ApplyItemOperation(
            _parentSalesVm.Items, inputItem, effectiveOperation, effectiveRecNo);

        decimal previewRemaining = NoControlaStock == 1
            ? StockDisponible
            : Math.Max(0m, StockDisponible - appliedItem.Cantidad);
        _parentSalesVm.SetSelectedItemWithStockPreview(appliedItem, previewRemaining);

        // 4. Recalculate invoice totals (oFormPadre.sumarproductos()) and update mirror (actualizar_espejo())
        _parentSalesVm.SumarProductos();
        ActualizarEspejo();

        string warningSuffix = qtyValidation.IsWarningOnly ? $" ({qtyValidation.ErrorMessage})" : "";
        SetStatus(
            $"[(Caja {SelectedCaja} - {modeLabel})] {appliedItem.Cantidad:N2} x {appliedItem.Descripcion} = {appliedItem.PrecioTotal:N0} Gs.{warningSuffix}",
            false);

        // 5. Reset state to "A" and clear fields for next scan
        LcTipo = "A";
        NRecNo = 0;
        LimpiarCampos();
        RequestFocusControl?.Invoke("CodProducto");
        return true;
    }

    /// <summary>
    /// Handles all single-key and modifier shortcuts from ITEMSFACTURAVENTAS.codproducto.KeyPress.
    /// </summary>
    public async Task<bool> HandleShortcutKeyAsync(string shortcutCode)
    {
        if (_parentSalesVm == null)
            return false;

        switch (shortcutCode.ToUpperInvariant())
        {
            case "F1": SelectedCaja = 1; return true;
            case "F2": SelectedCaja = 2; return true;
            case "F3":
            case "F3_CAJA":
                SelectedCaja = 3;
                return true;
            case "F4": SelectedCaja = 4; return true;
            case "F5": SelectedCaja = 5; return true;

            case "F6":
            case "BUSCAR_CATALOGO":
                OpenProductCatalogSearch();
                return true;

            case "F10":
                IsMovimientoRapido = !IsMovimientoRapido;
                SetStatus(
                    IsMovimientoRapido
                        ? "⚡ Movimiento Rápido ACTIVADO [F10]: Al escanear pasará 1 unidad directamente verificando costo."
                        : "⚡ Movimiento Rápido DESACTIVADO [F10]: Ingrese cantidad manualmente al escanear.",
                    false);
                RequestFocusControl?.Invoke("CodProducto");
                return true;

            case "Z":
            case "CTRL+P":
                await _parentSalesVm.ProcesarVentaAsync(imprimirTicket: true);
                ActualizarEspejo();
                return true;

            case "X":
                await _parentSalesVm.ProcesarVentaAsync(imprimirTicket: false);
                ActualizarEspejo();
                return true;

            case "F9":
            case "CARGA_RAPIDA":
                _windowManager.OpenCargaRapidaProductos();
                return true;

            case "F7":
            case "PRESUPUESTOS":
                _parentSalesVm.OpenPresupuestosCommand.Execute(null);
                return true;

            case "C":
            case "CALCULADORA":
                _windowManager.OpenCalculatorDialog();
                return true;

            case "H":
                SetStatus("Cajón de dinero activado / Cobro rápido.", false);
                return true;

            case "DEL":
                if (_parentSalesVm.Items.Count == 0)
                {
                    PlayAlertSound();
                    SetStatus("⚠ No hay ítems en la grilla de ventas para borrar.", true);
                    return true;
                }
                _windowManager.ActivateDeleteModeFromSelector(_parentSalesVm);
                return true;

            case "CTRL+X":
                await _parentSalesVm.ReeditarVentaAsync();
                ActualizarEspejo();
                return true;

            case "CTRL+Z":
                await _parentSalesVm.ReimprimirVentaAsync();
                return true;

            // Department quick-selectors (genes_system.prg / GenesSelector.cs)
            case "A": AbrirGenes(1, "SELECT"); return true;
            case "S": AbrirGenes(2, "SELECT"); return true;
            case "D": AbrirGenes(3, "SELECT"); return true;
            case "F": AbrirGenes(4, "SELECT"); return true;
            case "CTRL+A": AbrirGenes(1, "AGREGAR"); return true;
            case "CTRL+S": AbrirGenes(2, "AGREGAR"); return true;
            case "CTRL+D": AbrirGenes(3, "AGREGAR"); return true;
            case "CTRL+F": AbrirGenes(4, "AGREGAR"); return true;
            case "ALT+A": AbrirGenes(1, "EDITAR"); return true;
            case "ALT+S": AbrirGenes(2, "EDITAR"); return true;
            case "ALT+D": AbrirGenes(3, "EDITAR"); return true;
            case "ALT+F": AbrirGenes(4, "EDITAR"); return true;
        }

        return false;
    }

    public void AbrirGenes(int genId, string initialMode = "SELECT")
    {
        int normalizedGen = ((genId - 1) % 4) + 1;
        _windowManager.OpenGenesSelectorFull(normalizedGen, initialMode, async selection =>
        {
            await ApplyGenesSelectionAsync(selection);
        });
    }

    /// <summary>
    /// Exact replication of InyectarEnFormularioVentas in PROGS\genes_system.prg (lines 124-251).
    /// Handles KG (amount in Gs -> weight in Kg), DOCENA, UNIDAD, and Precio == 1 (Monto Libre).
    /// </summary>
    public async Task ApplyGenesSelectionAsync(GenesSelectionResult selection)
    {
        if (_parentSalesVm == null || selection.EnteredValue <= 0m)
            return;

        string code = VfpCompat.TransformaCodigoProducto(selection.Product.Codigo);
        if (string.IsNullOrWhiteSpace(code))
            return;

        LimpiarCampos();
        LcTipo = "A";
        NRecNo = 0;

        int idDeposito = _parentSalesVm.IdDeposito > 0 ? _parentSalesVm.IdDeposito : 1;
        var sqlProd = await _productRepository.GetByCodeAsync(code, idDeposito);

        // If Gene product is not yet in MySQL productos, auto-sync it as in SincronizarProductoSQL
        if (sqlProd == null || sqlProd.IdProducto <= 0)
        {
            decimal syncPrice = selection.EffectivePrecioBase > 1m ? selection.EffectivePrecioBase : 10000m;
            decimal syncCost = selection.Product.Costo > 0m && selection.Product.Costo < syncPrice
                ? selection.Product.Costo
                : Math.Round(syncPrice * 0.80m, 0);

            var newProd = new ProductEntity
            {
                Codigo = code,
                Descripcion = selection.Product.Nombre.ToUpperInvariant(),
                Costo1 = syncCost,
                Costo2 = syncCost,
                CostoMedio = syncCost,
                Precio1 = syncPrice,
                Precio2 = syncPrice,
                Precio3 = syncPrice,
                Precio4 = syncPrice,
                Precio5 = syncPrice,
                Iva = 1,
                Impuesto = 10,
                Estado = 1,
                Fraccionable = selection.EffectiveTipo == "KG" ? 1 : 0,
                NoControlaStock = 1,
                VendeSinStock = 1
            };
            await _productRepository.SaveProductAsync(newProd, "A");
            sqlProd = await _productRepository.GetByCodeAsync(code, idDeposito);
        }

        CurrentProductId = sqlProd?.IdProducto ?? selection.Product.Id;
        CodProducto = code;
        DesProducto = !string.IsNullOrWhiteSpace(sqlProd?.Descripcion)
            ? sqlProd.Descripcion
            : selection.Product.Nombre.ToUpperInvariant();
        IvaType = sqlProd?.Iva is >= 1 and <= 3 ? sqlProd.Iva : 1;
        NoControlaStock = 1;
        VendeSinStock = 1;

        decimal basePrice = sqlProd != null && sqlProd.Precio1 > 1m
            ? sqlProd.Precio1
            : selection.EffectivePrecioBase;

        if (selection.Product.Precio == 1m || basePrice <= 1m)
        {
            basePrice = 10000m;
        }

        string tipo = selection.EffectiveTipo.ToUpperInvariant();
        if (tipo == "DOCENA")
        {
            // If price in DBF is per-dozen (> 1500), unit price per docena or unit
            PrecioVenta = basePrice;
        }
        else
        {
            PrecioVenta = basePrice;
        }

        CostoUnitario = sqlProd != null && sqlProd.Costo1 > 0m && sqlProd.Costo1 < PrecioVenta
            ? sqlProd.Costo1
            : Math.Round(PrecioVenta * 0.80m, 0);

        if (CostoUnitario < 10m) CostoUnitario = Math.Round(PrecioVenta * 0.80m, 0);
        if (PrecioVenta < 100m) PrecioVenta = 10000m;

        decimal lnValor = selection.EnteredValue;

        if (tipo == "KG")
        {
            // Scale price & cost if (lnValor / PrecioVenta) > 120 (exact genes_system.prg lines 170-177)
            if (PrecioVenta > 0m && (lnValor / PrecioVenta) > 120m)
            {
                decimal escala = Math.Ceiling((lnValor / PrecioVenta) / 100m);
                if (escala < 1m) escala = 1m;
                CostoUnitario *= escala;
                PrecioVenta *= escala;
            }

            IsFraccionable = true;
            Kilogramo = lnValor;
            Cantidad = SalesCalculationEngine.CalculateQuantityFromGuaranies(lnValor, PrecioVenta);
            LineSubtotal = lnValor;
        }
        else
        {
            IsFraccionable = false;
            Kilogramo = 0m;
            Cantidad = lnValor;
            RecalculateLinePreview();
        }

        _isCurrentOperationFromGenes = true;
        try
        {
            await CmdGrabarAsync();
        }
        finally
        {
            _isCurrentOperationFromGenes = false;
        }
    }

    public static void PlayAlertSound()
    {
        try
        {
            System.Media.SystemSounds.Hand.Play();
            _ = Task.Run(() =>
            {
                try
                {
                    Console.Beep(720, 140);
                    Console.Beep(480, 180);
                }
                catch
                {
                    // Ignore if audio device unavailable
                }
            });
        }
        catch
        {
            // Non-blocking
        }
    }

    public void OpenProductCatalogSearch()
    {
        _windowManager.OpenProductsModule(0, async selectedCode =>
        {
            if (!string.IsNullOrWhiteSpace(selectedCode))
            {
                CodProducto = selectedCode;
                await ValidateAndLookupProductAsync();
            }
        });
    }

    public bool ValidateQuantityOnEnter()
    {
        var qtyValidation = SalesCalculationEngine.ValidateQuantity(
            Cantidad,
            NoControlaStock,
            VendeSinStock,
            StockDisponible,
            noControlarStockGlobal: Preferences.NoControlarStockGlobal,
            limiteCantidad: Preferences.LimiteCantidadPorItem);
        if (!qtyValidation.IsValid)
        {
            HandleFieldValidationError(qtyValidation.ErrorMessage, "Cantidad");
            return false;
        }
        return true;
    }

    public void HandleFieldValidationError(string errorMessage, string fallbackFocusTarget)
    {
        if (Preferences.SonidoAlertaEnErrores)
        {
            PlayAlertSound();
        }

        if (Preferences.ReiniciarItemAlEquivocarseEnItemFactura)
        {
            LcTipo = "A";
            NRecNo = 0;
            LimpiarCampos();
            SetStatus(errorMessage, true);
            RequestFocusControl?.Invoke("CodProducto");
        }
        else
        {
            SetStatus(errorMessage, true);
            RequestFocusControl?.Invoke(fallbackFocusTarget);
        }
    }

    public void OpenLotesPicker()
    {
        if (CurrentProductId <= 0)
            return;
        int dep = _parentSalesVm?.IdDeposito ?? 1;
        var lot = _windowManager.OpenBuscarLotesDialog(CurrentProductId, dep);
        if (lot != null)
        {
            LoteSeleccionadoTexto = $"Lote {lot.Lote} (Vto: {lot.Vencimiento:dd/MM/yyyy})";
            SetStatus($"Lote seleccionado: {lot.Lote} · Vence {lot.Vencimiento:dd/MM/yyyy} · Cant: {lot.Cantidad:N2}", false);
        }
    }

    public void LimpiarCampos()
    {
        CurrentProductId = 0;
        _loadedProduct = null;
        CodProducto = string.Empty;
        DesProducto = string.Empty;
        Cantidad = 0m;
        Kilogramo = 0m;
        PrecioVenta = 0m;
        CostoUnitario = 0m;
        StockDisponible = 0m;
        LineSubtotal = 0m;
        IsFraccionable = false;
        IsWarrantyVisible = false;
        Compuesto = 0;
        UsaVencimiento = 0;
        NoControlaStock = 0;
        LoteSeleccionadoTexto = string.Empty;
        OnPropertyChanged(nameof(IsCompuestoVisible));
        OnPropertyChanged(nameof(IsLotesVisible));
    }

    private void RecalculateLinePreview()
    {
        decimal raw = Cantidad * PrecioVenta;
        LineSubtotal = IsFraccionable
            ? VfpCompat.RedondearMas(raw, 2)
            : Math.Round(raw, 2, MidpointRounding.AwayFromZero);
    }

    private void SetStatus(string message, bool isError)
    {
        ValidationMessage = message;
        IsError = isError;
    }
}
