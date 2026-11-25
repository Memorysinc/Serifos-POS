using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FirewallControl.App.Services;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;

namespace FirewallControl.App.ViewModels;

/// <summary>
/// Priority 2: Complete MVVM implementation of ABM_VENTAS_AUTOSERVICE_COBRO_CONTADO.SCX.
/// Supports multi-caja instances (Cajas 1..5), bidirectional synchronization with ITEMSFACTURAVENTAS,
/// sumarproductos(), grabar/grabaritems with stock deduction & compound product decomposition,
/// cash collection (Command9 / abm_cobroclientesmontos_contado), delete mode (DEL),
/// reeditar_ventas (Ctrl+X), reimprimir_ventas (Ctrl+Z), and thermal/legal invoice printing.
/// </summary>
public class AbmVentasAutoserviceViewModel : ViewModelBase
{
    private readonly ISalesRepository _salesRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ICashRepository _cashRepository;
    private readonly ISystemRepository _systemRepository;
    private readonly ITicketPrintService _ticketPrintService;
    private readonly IWindowManagerService _windowManager;
    private readonly IProductRepository? _productRepository;

    private int _idVenta;
    private int _nroVenta;
    private string _nroFactura = "001-001-0000001";
    private string _timbrado = string.Empty;
    private DateTime _fechaVenta = DateTime.Today;
    private string _horaVenta = DateTime.Now.ToString("HH:mm:ss");
    private int _idCaja = 1;
    private int _idDeposito = 1;
    private int _idSucursal = 1;
    private int _idMoneda = 1;
    private int _formaPago = 1; // 1 = Contado, 2 = Crédito
    private int _tipoBoleta = 1; // 1 = Ticket, 2 = Factura Legal
    private int _listaPrecios = 1;

    private int _idCliente = 132;
    private string _rucCliente = "X";
    private string _nombreCliente = "CLIENTE OCASIONAL";
    private string _direccionCliente = string.Empty;
    private string _telefonoCliente = string.Empty;

    private int _idVendedor = 10;
    private string _nombreVendedor = "VENDEDOR MOSTRADOR";

    private decimal _totalExenta;
    private decimal _totalIva5;
    private decimal _totalIva10;
    private decimal _subtotal;
    private decimal _descuento;
    private decimal _totalVenta;
    private decimal _liquidacionIva5;
    private decimal _liquidacionIva10;
    private decimal _totalImpuesto;

    private decimal _monedaGuarani;
    private decimal _monedaDolar;
    private decimal _monedaReal;
    private decimal _monedaPeso;
    private decimal _monedaEuro;

    private decimal _entregaGuarani;
    private decimal _vueltoGuarani;

    private string _lcTipo = "A"; // "A" = Alta, "M" = Modificación (Reeditar)
    private bool _modoBorradoGrilla;
    private bool _isProcessingSale;
    private SaleItemEntity? _selectedItem;
    private decimal _stockActualValor;
    private string _stockActualProducto = "Sin ítem";
    private bool _tieneStockActual;
    private SaleItemEntity? _skipAsyncRefreshItem;
    private string _statusMessage = "Listo - Presione F2 para abrir el selector de productos.";
    private string _letrasTotal = "CERO GUARANIES";
    private string _observacion = string.Empty;
    private string _nroPresupuestoTexto = string.Empty;
    private bool _buscarPorRuc;
    private bool _buscarPresupuestoPorNumero;
    private string _nombreSupervisor = string.Empty;
    private decimal _lineaCreditoCliente;
    private decimal _saldoDisponibleCliente;
    private decimal _cotDolar;
    private decimal _cotReal;
    private decimal _cotPeso;
    private decimal _cotEuro;
    private List<SaleInstallmentEntity> _pendingInstallments = new();
    private decimal _pendingInstallmentEntrega;

    public ObservableCollection<SaleItemEntity> Items { get; } = new();
    public ObservableCollection<SellerEntity> Vendedores { get; } = new();
    public ObservableCollection<DepositEntity> Deposits { get; } = new();
    public ObservableCollection<BranchEntity> Branches { get; } = new();
    public ObservableCollection<CurrencyEntity> Currencies { get; } = new();
    public ObservableCollection<CashRegisterEntity> CashRegisters { get; } = new();
    public ObservableCollection<SupervisorEntity> Supervisors { get; } = new();

    public ExchangeRateEntity CurrentExchangeRate { get; private set; } = new();
    public CompanyDataEntity CompanyData { get; private set; } = new();
    public TimbradoLocalEntity CurrentTimbrado { get; private set; } = new();

    public event Action? RequestFocusGrid;
    public AppPreferencesEntity Preferences => _windowManager.ConfigService.Preferences;

    public AbmVentasAutoserviceViewModel(
        ISalesRepository salesRepository,
        ICustomerRepository customerRepository,
        ICashRepository cashRepository,
        ISystemRepository systemRepository,
        ITicketPrintService ticketPrintService,
        IWindowManagerService windowManager,
        int idCaja = 1,
        IProductRepository? productRepository = null)
    {
        _salesRepository = salesRepository;
        _customerRepository = customerRepository;
        _cashRepository = cashRepository;
        _systemRepository = systemRepository;
        _ticketPrintService = ticketPrintService;
        _windowManager = windowManager;
        _productRepository = productRepository;
        _idCaja = Math.Clamp(idCaja, 1, 5);

        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ItemsCountText));

        OpenSelectorCommand = new RelayCommand(() => MantenimientoItems("A"));
        EditSelectedItemCommand = new RelayCommand(() =>
        {
            if (SelectedItem != null)
                MantenimientoItems("M", SelectedItem);
        });
        DeleteSelectedItemCommand = new RelayCommand(() => DeleteSelectedItem(SelectedItem));
        SaveOnlyCommand = new AsyncRelayCommand(() => ProcesarVentaAsync(false));
        SaveAndPrintCommand = new AsyncRelayCommand(() => ProcesarVentaAsync(true));
        CancelSaleCommand = new AsyncRelayCommand(InitializeNewSaleAsync);
        SearchCustomerCommand = new RelayCommand(OpenCustomerLookup);
        LookupCustomerByRucCommand = new AsyncRelayCommand(LookupCustomerByRucAsync);
        ReeditarVentaCommand = new AsyncRelayCommand(() => ReeditarVentaAsync());
        ReimprimirVentaCommand = new AsyncRelayCommand(() => ReimprimirVentaAsync());
        ToggleModoBorradoCommand = new RelayCommand(() =>
        {
            if (ModoBorradoGrilla) SalirModoBorrado();
            else ActivarModoBorrado();
        });

        SelectPriceListCommand = new RelayCommand(p =>
        {
            if (p != null && int.TryParse(p.ToString(), out int listNo) && listNo is >= 1 and <= 7)
                ListaPrecios = listNo;
        });
        SetFormaPagoCommand = new RelayCommand(p =>
        {
            if (p != null && int.TryParse(p.ToString(), out int fp) && fp is 1 or 2)
                FormaPago = fp;
        });
        ResetToCliente1Command = new AsyncRelayCommand(ResetToCliente1Async);
        QuickCreateCustomerCommand = new RelayCommand(QuickCreateCustomer);
        QuickCreateSellerCommand = new RelayCommand(QuickCreateSeller);
        OpenPresupuestosCommand = new RelayCommand(OpenPresupuestosImport);
        LookupPresupuestoByNumberCommand = new AsyncRelayCommand(LookupPresupuestoByNumberAsync);
        OpenFacturaLegalCommand = new RelayCommand(() => _windowManager.OpenFacturaLegalWindow(BuildSaleEntity()));
        OpenAjusteStockCommand = new RelayCommand(() => _windowManager.OpenAjusteStockWindow(IdDeposito));
        OpenCuotasCommand = new RelayCommand(OpenCuotasDialog);
        OpenCalculatorCommand = new RelayCommand(() => _windowManager.OpenCalculatorDialog());
        OpenCargaRapidaCommand = new RelayCommand(() => _windowManager.OpenCargaRapidaProductos());
        OpenCombinarSelectedCommand = new RelayCommand(OpenCombinarSelectedItem);
    }

    public string WindowTitle => $"PUNTO DE VENTA AUTOSERVICE - CAJA {IdCaja} {(LcTipo == "M" ? $"[REEDITANDO VENTA #{NroVenta}]" : "")}";

    public string ClassicWindowTitle => LcTipo == "M"
        ? $"Caja {IdCaja} - Facturas Ventas [REEDITANDO #{NroVenta}]"
        : $"Caja {IdCaja} - Facturas Ventas";

    public string CompanyBannerTitle => string.IsNullOrWhiteSpace(CompanyData?.Descripcion)
        ? "..:: SIN DATOS ::.."
        : CompanyData.Descripcion.Trim();

    public int IdVenta
    {
        get => _idVenta;
        set => SetProperty(ref _idVenta, value);
    }

    public int NroVenta
    {
        get => _nroVenta;
        set
        {
            if (SetProperty(ref _nroVenta, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(ClassicWindowTitle));
            }
        }
    }

    public string NroFactura
    {
        get => _nroFactura;
        set => SetProperty(ref _nroFactura, value);
    }

    public string Timbrado
    {
        get => _timbrado;
        set => SetProperty(ref _timbrado, value);
    }

    public DateTime FechaVenta
    {
        get => _fechaVenta;
        set => SetProperty(ref _fechaVenta, value);
    }

    public string HoraVenta
    {
        get => _horaVenta;
        set => SetProperty(ref _horaVenta, value);
    }

    public int IdCaja
    {
        get => _idCaja;
        set
        {
            if (SetProperty(ref _idCaja, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(ClassicWindowTitle));
            }
        }
    }

    public int IdDeposito
    {
        get => _idDeposito;
        set => SetProperty(ref _idDeposito, value);
    }

    public int IdSucursal
    {
        get => _idSucursal;
        set => SetProperty(ref _idSucursal, value);
    }

    public int IdMoneda
    {
        get => _idMoneda;
        set => SetProperty(ref _idMoneda, value);
    }

    public int FormaPago
    {
        get => _formaPago;
        set => SetProperty(ref _formaPago, value);
    }

    public int TipoBoleta
    {
        get => _tipoBoleta;
        set => SetProperty(ref _tipoBoleta, value);
    }

    public int ListaPrecios
    {
        get => _listaPrecios;
        set
        {
            int clamped = Math.Clamp(value, 1, 7);
            if (SetProperty(ref _listaPrecios, clamped))
            {
                var selVm = _windowManager.GetItemsSelectorViewModel();
                if (selVm != null && selVm.SelectedPriceList != clamped)
                {
                    selVm.SelectedPriceList = clamped;
                }
            }
        }
    }

    public int IdCliente
    {
        get => _idCliente;
        set => SetProperty(ref _idCliente, value);
    }

    public string RucCliente
    {
        get => _rucCliente;
        set => SetProperty(ref _rucCliente, value);
    }

    public string NombreCliente
    {
        get => _nombreCliente;
        set => SetProperty(ref _nombreCliente, value);
    }

    public string DireccionCliente
    {
        get => _direccionCliente;
        set => SetProperty(ref _direccionCliente, value);
    }

    public string TelefonoCliente
    {
        get => _telefonoCliente;
        set => SetProperty(ref _telefonoCliente, value);
    }

    public int IdVendedor
    {
        get => _idVendedor;
        set => SetProperty(ref _idVendedor, value);
    }

    public string NombreVendedor
    {
        get => _nombreVendedor;
        set => SetProperty(ref _nombreVendedor, value);
    }

    public decimal TotalExenta
    {
        get => _totalExenta;
        set => SetProperty(ref _totalExenta, value);
    }

    public decimal TotalIva5
    {
        get => _totalIva5;
        set => SetProperty(ref _totalIva5, value);
    }

    public decimal TotalIva10
    {
        get => _totalIva10;
        set => SetProperty(ref _totalIva10, value);
    }

    public decimal Subtotal
    {
        get => _subtotal;
        set => SetProperty(ref _subtotal, value);
    }

    public decimal Descuento
    {
        get => _descuento;
        set
        {
            if (SetProperty(ref _descuento, value))
                SumarProductos();
        }
    }

    public decimal TotalVenta
    {
        get => _totalVenta;
        set
        {
            if (SetProperty(ref _totalVenta, value))
            {
                string iso = _windowManager.ConfigService.Preferences.MonedaBaseIso;
                LetrasTotal = VfpCompat.Letras(value, iso);
            }
        }
    }

    public decimal LiquidacionIva5
    {
        get => _liquidacionIva5;
        set => SetProperty(ref _liquidacionIva5, value);
    }

    public decimal LiquidacionIva10
    {
        get => _liquidacionIva10;
        set => SetProperty(ref _liquidacionIva10, value);
    }

    public decimal TotalImpuesto
    {
        get => _totalImpuesto;
        set => SetProperty(ref _totalImpuesto, value);
    }

    public decimal MonedaGuarani
    {
        get => _monedaGuarani;
        set => SetProperty(ref _monedaGuarani, value);
    }

    public decimal MonedaDolar
    {
        get => _monedaDolar;
        set => SetProperty(ref _monedaDolar, value);
    }

    public decimal MonedaReal
    {
        get => _monedaReal;
        set => SetProperty(ref _monedaReal, value);
    }

    public decimal MonedaPeso
    {
        get => _monedaPeso;
        set => SetProperty(ref _monedaPeso, value);
    }

    public decimal MonedaEuro
    {
        get => _monedaEuro;
        set => SetProperty(ref _monedaEuro, value);
    }

    public decimal EntregaGuarani
    {
        get => _entregaGuarani;
        set
        {
            if (SetProperty(ref _entregaGuarani, value))
            {
                VueltoGuarani = Math.Max(0m, value - TotalVenta);
            }
        }
    }

    public decimal VueltoGuarani
    {
        get => _vueltoGuarani;
        set => SetProperty(ref _vueltoGuarani, value);
    }

    public string LcTipo
    {
        get => _lcTipo;
        set
        {
            if (SetProperty(ref _lcTipo, value))
            {
                OnPropertyChanged(nameof(WindowTitle));
                OnPropertyChanged(nameof(ClassicWindowTitle));
            }
        }
    }

    public bool ModoBorradoGrilla
    {
        get => _modoBorradoGrilla;
        set => SetProperty(ref _modoBorradoGrilla, value);
    }

    public SaleItemEntity? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                if (value != null && value.IdProducto > 0 && !ReferenceEquals(value, _skipAsyncRefreshItem))
                    _ = RefreshSelectedItemStockAsync(value);
                _skipAsyncRefreshItem = null;
            }
        }
    }

    public decimal StockActualValor
    {
        get => _stockActualValor;
        private set
        {
            if (SetProperty(ref _stockActualValor, value))
            {
                OnPropertyChanged(nameof(EsStockBajoOCero));
                OnPropertyChanged(nameof(StockActualTooltip));
            }
        }
    }

    public string StockActualProducto
    {
        get => _stockActualProducto;
        private set
        {
            if (SetProperty(ref _stockActualProducto, value))
                OnPropertyChanged(nameof(StockActualTooltip));
        }
    }

    public bool TieneStockActual
    {
        get => _tieneStockActual;
        private set
        {
            if (SetProperty(ref _tieneStockActual, value))
            {
                OnPropertyChanged(nameof(EsStockBajoOCero));
                OnPropertyChanged(nameof(StockActualTooltip));
            }
        }
    }

    public bool EsStockBajoOCero => TieneStockActual && StockActualValor <= 0m;

    public string StockActualTooltip => TieneStockActual
        ? $"{StockActualProducto}: {StockActualValor:N2} en depósito"
        : "Stock actual del producto activo en depósito";

    public void UpdateStockActualPreview(decimal stock, string? productLabel = null)
    {
        StockActualValor = stock;
        StockActualProducto = string.IsNullOrWhiteSpace(productLabel) ? "Producto activo" : productLabel.Trim();
        TieneStockActual = true;
    }

    public void SetSelectedItemWithStockPreview(SaleItemEntity item, decimal previewStock)
    {
        _skipAsyncRefreshItem = item;
        SelectedItem = item;
        UpdateStockActualPreview(previewStock, item.Descripcion);
    }

    public void ClearStockActualPreview()
    {
        StockActualValor = 0m;
        StockActualProducto = "Sin ítem";
        TieneStockActual = false;
    }

    private async Task RefreshSelectedItemStockAsync(SaleItemEntity item)
    {
        if (_productRepository == null || item.IdProducto <= 0)
            return;

        try
        {
            decimal stock = await _productRepository.GetDepositStockAsync(item.IdProducto, IdDeposito);
            if (ReferenceEquals(SelectedItem, item))
                UpdateStockActualPreview(stock, item.Descripcion);
        }
        catch
        {
            // Keep current preview if DB query fails
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string LetrasTotal
    {
        get => _letrasTotal;
        set => SetProperty(ref _letrasTotal, value);
    }

    public bool IsProcessingSale
    {
        get => _isProcessingSale;
        private set => SetProperty(ref _isProcessingSale, value);
    }

    public string ItemsCountText => Items.Count == 1 ? "1 ítem" : $"{Items.Count} ítems";

    public string Observacion
    {
        get => _observacion;
        set => SetProperty(ref _observacion, value);
    }

    public string NroPresupuestoTexto
    {
        get => _nroPresupuestoTexto;
        set => SetProperty(ref _nroPresupuestoTexto, value);
    }

    public bool BuscarPorRuc
    {
        get => _buscarPorRuc;
        set => SetProperty(ref _buscarPorRuc, value);
    }

    public bool BuscarPresupuestoPorNumero
    {
        get => _buscarPresupuestoPorNumero;
        set => SetProperty(ref _buscarPresupuestoPorNumero, value);
    }

    public string NombreSupervisor
    {
        get => _nombreSupervisor;
        set => SetProperty(ref _nombreSupervisor, value);
    }

    public decimal LineaCreditoCliente
    {
        get => _lineaCreditoCliente;
        set => SetProperty(ref _lineaCreditoCliente, value);
    }

    public decimal SaldoDisponibleCliente
    {
        get => _saldoDisponibleCliente;
        set => SetProperty(ref _saldoDisponibleCliente, value);
    }

    public decimal CotDolar
    {
        get => _cotDolar;
        set => SetProperty(ref _cotDolar, value);
    }

    public decimal CotReal
    {
        get => _cotReal;
        set => SetProperty(ref _cotReal, value);
    }

    public decimal CotPeso
    {
        get => _cotPeso;
        set => SetProperty(ref _cotPeso, value);
    }

    public decimal CotEuro
    {
        get => _cotEuro;
        set => SetProperty(ref _cotEuro, value);
    }

    public ICommand OpenSelectorCommand { get; }
    public ICommand EditSelectedItemCommand { get; }
    public ICommand DeleteSelectedItemCommand { get; }
    public ICommand SaveOnlyCommand { get; }
    public ICommand SaveAndPrintCommand { get; }
    public ICommand CancelSaleCommand { get; }
    public ICommand SearchCustomerCommand { get; }
    public ICommand LookupCustomerByRucCommand { get; }
    public ICommand ReeditarVentaCommand { get; }
    public ICommand ReimprimirVentaCommand { get; }
    public ICommand ToggleModoBorradoCommand { get; }
    public ICommand SelectPriceListCommand { get; }
    public ICommand SetFormaPagoCommand { get; }
    public ICommand ResetToCliente1Command { get; }
    public ICommand QuickCreateCustomerCommand { get; }
    public ICommand QuickCreateSellerCommand { get; }
    public ICommand OpenPresupuestosCommand { get; }
    public ICommand LookupPresupuestoByNumberCommand { get; }
    public ICommand OpenFacturaLegalCommand { get; }
    public ICommand OpenAjusteStockCommand { get; }
    public ICommand OpenCuotasCommand { get; }
    public ICommand OpenCalculatorCommand { get; }
    public ICommand OpenCargaRapidaCommand { get; }
    public ICommand OpenCombinarSelectedCommand { get; }

    public async Task InitializeNewSaleAsync()
    {
        try
        {
            LcTipo = "A";
            IdVenta = 0;
            ModoBorradoGrilla = false;
            Items.Clear();
            ClearStockActualPreview();

            CompanyData = await _systemRepository.GetCompanyDataAsync();
            OnPropertyChanged(nameof(CompanyBannerTitle));
            CurrentExchangeRate = await _cashRepository.GetLatestExchangeRateAsync();
            CotDolar = CurrentExchangeRate.VentaDolar;
            CotReal = CurrentExchangeRate.VentaReal;
            CotPeso = CurrentExchangeRate.VentaPeso;
            CotEuro = CurrentExchangeRate.VentaEuro;

            CurrentTimbrado = await _salesRepository.GetTimbradoLocalAsync(IdSucursal, IdCaja);
            NroVenta = await _salesRepository.GetNextNroVentaAsync();

            Timbrado = !string.IsNullOrWhiteSpace(CurrentTimbrado.Timbrado) ? CurrentTimbrado.Timbrado : "12345678";
            NroFactura = !string.IsNullOrWhiteSpace(CurrentTimbrado.Factura)
                ? CurrentTimbrado.Factura
                : CurrentTimbrado.FormatNextInvoice();

            FechaVenta = DateTime.Today;
            HoraVenta = DateTime.Now.ToString("HH:mm:ss");
            FormaPago = 1;
            TipoBoleta = 1;
            Descuento = 0m;
            EntregaGuarani = 0m;
            VueltoGuarani = 0m;
            Observacion = string.Empty;
            NroPresupuestoTexto = string.Empty;
            _pendingInstallments.Clear();
            _pendingInstallmentEntrega = 0m;

            int cfgPriceList = _windowManager.ConfigService?.Config?.ListaPrecios ?? 1;
            ListaPrecios = Math.Clamp(cfgPriceList, 1, 7);

            int defaultCustomerId = CompanyData.IdCliente > 0 ? CompanyData.IdCliente : 132;
            var defaultCustomer = await _customerRepository.GetByIdAsync(defaultCustomerId);
            if (defaultCustomer != null)
            {
                ApplyCustomer(defaultCustomer);
            }
            else
            {
                IdCliente = 132;
                RucCliente = "X";
                NombreCliente = "CLIENTE OCASIONAL";
                DireccionCliente = "";
                TelefonoCliente = "";
                LineaCreditoCliente = 0m;
                SaldoDisponibleCliente = 0m;
            }

            if (Vendedores.Count == 0)
            {
                var sellers = await _systemRepository.GetSellersAsync();
                foreach (var s in sellers)
                    Vendedores.Add(s);
            }

            if (Deposits.Count == 0 && _productRepository != null)
            {
                var deps = await _productRepository.GetDepositsAsync();
                foreach (var d in deps) Deposits.Add(d);
            }
            if (Branches.Count == 0)
            {
                var brs = await _systemRepository.GetBranchesAsync();
                foreach (var b in brs) Branches.Add(b);
            }
            if (Currencies.Count == 0)
            {
                try
                {
                    var curs = await _cashRepository.GetCurrenciesAsync();
                    foreach (var c in curs) Currencies.Add(c);
                }
                catch { }

                if (Currencies.Count == 0)
                {
                    string baseIso = _windowManager?.ConfigService?.Preferences?.MonedaBaseIso ?? "PYG";
                    foreach (var c in LatamCurrencyCatalog.ToCurrencyEntities(baseIso))
                        Currencies.Add(c);
                }
            }
            if (CashRegisters.Count == 0)
            {
                var regs = await _cashRepository.GetCashRegistersAsync();
                foreach (var r in regs) CashRegisters.Add(r);
            }
            if (Supervisors.Count == 0)
            {
                var sups = await _systemRepository.GetSupervisorsAsync();
                foreach (var sp in sups) Supervisors.Add(sp);
                if (Supervisors.Count > 0 && string.IsNullOrWhiteSpace(NombreSupervisor))
                    NombreSupervisor = Supervisors[0].Nombres;
            }

            IdVendedor = CompanyData.IdVendedor > 0 ? CompanyData.IdVendedor : 10;
            var seller = Vendedores.FirstOrDefault(v => v.IdVendedor == IdVendedor);
            if (seller != null)
                NombreVendedor = seller.Nombres;

            SumarProductos();
            StatusMessage = $"Caja {IdCaja} lista - Venta #{NroVenta}. Presione F2 para agregar productos.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Advertencia al inicializar venta: {ex.Message}";
        }
    }

    /// <summary>
    /// Exact replication of sumarproductos() in ABM_VENTAS_AUTOSERVICE_COBRO_CONTADO.SCX.
    /// </summary>
    public void SumarProductos()
    {
        var totals = SalesCalculationEngine.SumarProductos(Items, Descuento, 0, CurrentExchangeRate);
        TotalExenta = totals.TotalExenta;
        TotalIva5 = totals.TotalIva5;
        TotalIva10 = totals.TotalIva10;
        Subtotal = totals.Subtotal;
        TotalVenta = totals.TotalVenta;
        LiquidacionIva5 = totals.LiquidacionIva5;
        LiquidacionIva10 = totals.LiquidacionIva10;
        TotalImpuesto = totals.TotalImpuesto;
        MonedaGuarani = totals.MonedaGuarani;
        MonedaDolar = totals.MonedaDolar;
        MonedaReal = totals.MonedaReal;
        MonedaPeso = totals.MonedaPeso;
        MonedaEuro = totals.MonedaEuro;

        if (EntregaGuarani > 0)
            VueltoGuarani = Math.Max(0m, EntregaGuarani - TotalVenta);
    }

    /// <summary>
    /// Exact replication of mantenimientoitems(lctipo) in ABM_VENTAS_AUTOSERVICE_COBRO_CONTADO.SCX.
    /// Opens or activates the single reusable instance of ITEMSFACTURAVENTAS linked to this Caja.
    /// </summary>
    public void MantenimientoItems(string lcTipo, SaleItemEntity? item = null)
    {
        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase))
        {
            var target = item ?? SelectedItem;
            if (target == null)
            {
                StatusMessage = "Seleccione un ítem de la grilla para modificar.";
                return;
            }
            _windowManager.OpenOrActivateItemsSelector(this, "M", target.RecNo, target);
        }
        else
        {
            _windowManager.OpenOrActivateItemsSelector(this, "A", 0, null);
        }
    }

    public void DeleteSelectedItem(SaleItemEntity? item = null)
    {
        var target = item ?? SelectedItem;
        if (target == null)
        {
            StatusMessage = "No hay producto seleccionado para eliminar.";
            if (ModoBorradoGrilla)
                SalirModoBorrado();
            return;
        }

        bool wasModoBorrado = ModoBorradoGrilla;
        int index = Items.IndexOf(target);
        Items.Remove(target);

        // Re-index RecNo 1..Count
        for (int i = 0; i < Items.Count; i++)
        {
            Items[i].RecNo = i + 1;
        }

        if (Items.Count > 0)
        {
            SelectedItem = Items[Math.Clamp(index, 0, Items.Count - 1)];
        }
        else
        {
            SelectedItem = null;
        }

        SumarProductos();
        StatusMessage = $"Producto '{target.Descripcion}' eliminado. Total: {TotalVenta:N0} Gs.";

        // If triggered from DEL mode, automatically return to ITEMSFACTURAVENTAS selector after deleting the chosen item
        if (wasModoBorrado)
        {
            SalirModoBorrado();
        }
    }

    public void ActivarModoBorrado()
    {
        if (Items.Count == 0)
        {
            StatusMessage = "No hay ítems en la grilla para borrar.";
            return;
        }

        ModoBorradoGrilla = true;
        SelectedItem ??= Items.LastOrDefault();
        StatusMessage = "MODO BORRADO ACTIVO: Use Flechas y ENTER/DEL para borrar un ítem, o ESC para volver al selector.";
        RequestFocusGrid?.Invoke();
    }

    public void SalirModoBorrado()
    {
        ModoBorradoGrilla = false;
        StatusMessage = "Modo borrado desactivado.";
        _windowManager.OpenOrActivateItemsSelector(this, "A", 0, null);
    }

    /// <summary>
    /// Limpia todo el carrito y datos de esta caja al cerrar la ventana con X/Salir
    /// (minimizar o cambiar entre Cajas 1..5 conserva el contenido).
    /// </summary>
    public void ResetSaleForWindowClose()
    {
        LcTipo = "A";
        IdVenta = 0;
        ModoBorradoGrilla = false;
        Items.Clear();
        SelectedItem = null;
        ClearStockActualPreview();
        Descuento = 0m;
        EntregaGuarani = 0m;
        VueltoGuarani = 0m;
        Observacion = string.Empty;
        NroPresupuestoTexto = string.Empty;
        _pendingInstallments.Clear();
        _pendingInstallmentEntrega = 0m;
        SumarProductos();
        StatusMessage = $"Caja {IdCaja} reiniciada.";
        _ = InitializeNewSaleAsync();
    }

    public void HandleWindowClosedByUser(System.Windows.Window salesWindow)
        => _windowManager.HandleSalesWindowClosedByUser(IdCaja, salesWindow);

    /// <summary>
    /// Exact replication of procesarventa(tlImprimir) in ABM_VENTAS_AUTOSERVICE_COBRO_CONTADO.SCX.
    /// Z / Ctrl+P -> imprimirTicket = true
    /// X / Ctrl+S / F9 -> imprimirTicket = false
    /// </summary>
    public async Task<bool> ProcesarVentaAsync(bool imprimirTicket)
    {
        if (Items.Count == 0)
        {
            StatusMessage = "No existen productos cargados en la venta.";
            return false;
        }

        SumarProductos();
        if (TotalVenta <= 0m)
        {
            StatusMessage = "El total de la venta debe ser mayor a cero.";
            return false;
        }

        IsProcessingSale = true;
        try
        {
            if (LcTipo == "A")
            {
                // Multi-PC synchronization: always fetch the latest sequential NroVenta right before saving
                NroVenta = await _salesRepository.GetNextNroVentaAsync();
            }

            HoraVenta = DateTime.Now.ToString("HH:mm:ss");
            var sale = BuildSaleEntity();

            // Cobro contado en efectivo (conservando flujo actual C#)
            if (FormaPago == 1 && CompanyData.CobroCaja == 1)
            {
                bool confirmed = _windowManager.ShowCobroContadoDialog(sale, CurrentExchangeRate);
                if (!confirmed)
                {
                    StatusMessage = "Cobro cancelado por el usuario.";
                    return false;
                }
                EntregaGuarani = sale.EntregaGuarani;
                VueltoGuarani = sale.VueltoGuarani;
            }
            else
            {
                if (sale.EntregaGuarani <= 0)
                {
                    sale.EntregaGuarani = sale.TotalVenta;
                    sale.VueltoGuarani = 0m;
                }
            }

            int savedId = await _salesRepository.ProcessSaleTransactionAsync(sale, LcTipo);
            sale.IdVenta = savedId;

            if (FormaPago == 2 && _pendingInstallments.Count > 0)
            {
                await _salesRepository.SaveSaleInstallmentsAsync(
                    sale.IdVenta,
                    sale.NroVenta,
                    sale.NroFactura,
                    sale.IdCliente,
                    sale.FechaVenta,
                    _pendingInstallmentEntrega,
                    _pendingInstallments,
                    sale.IdCaja,
                    sale.IdSucursal,
                    sale.CidUsuario);
                _pendingInstallments.Clear();
                _pendingInstallmentEntrega = 0m;
            }

            if (imprimirTicket)
            {
                await _ticketPrintService.PrintSaleAsync(sale, CompanyData);
            }

            string actionDesc = imprimirTicket ? "grabada e impresa" : "grabada";
            int completedNro = sale.NroVenta;
            decimal completedTotal = sale.TotalVenta;

            await InitializeNewSaleAsync();
            StatusMessage = $"Venta #{completedNro} ({completedTotal:N0} Gs.) {actionDesc} exitosamente.";

            // Return focus to ITEMSFACTURAVENTAS selector just like VFP Cmdcancelar1.Click()
            _windowManager.OpenOrActivateItemsSelector(this, "A", 0, null);
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al procesar venta: {ex.Message}";
            return false;
        }
        finally
        {
            IsProcessingSale = false;
        }
    }

    /// <summary>
    /// Replicates reeditar_ventas.scx (Ctrl+X): loads a completed sale into the form with LcTipo = "M".
    /// </summary>
    public async Task<bool> ReeditarVentaAsync(int? nroVentaParam = null)
    {
        int? targetNro = nroVentaParam ?? _windowManager.ShowReeditarReimprimirDialog(isReprint: false);
        if (!targetNro.HasValue || targetNro.Value <= 0)
            return false;

        var sale = await _salesRepository.GetSaleByNroVentaAsync(targetNro.Value);
        if (sale == null)
        {
            StatusMessage = $"No se encontró la venta Nro. {targetNro.Value}.";
            return false;
        }

        LoadSaleIntoForm(sale, "M");
        StatusMessage = $"Reeditando Venta #{sale.NroVenta} (ID {sale.IdVenta}). Al grabar se actualizará el stock.";
        return true;
    }

    /// <summary>
    /// Replicates reimprimir_ventas.scx (Ctrl+Z): reprints a completed sale by NroVenta.
    /// </summary>
    public async Task<bool> ReimprimirVentaAsync(int? nroVentaParam = null)
    {
        int? targetNro = nroVentaParam ?? _windowManager.ShowReeditarReimprimirDialog(isReprint: true);
        if (!targetNro.HasValue || targetNro.Value <= 0)
            return false;

        var sale = await _salesRepository.GetSaleByNroVentaAsync(targetNro.Value);
        if (sale == null)
        {
            StatusMessage = $"No se encontró la venta Nro. {targetNro.Value} para reimprimir.";
            return false;
        }

        await _ticketPrintService.PrintSaleAsync(sale, CompanyData);
        StatusMessage = $"Ticket de Venta #{sale.NroVenta} reimpreso correctamente.";
        return true;
    }

    public void LoadSaleIntoForm(SaleEntity sale, string lcTipo)
    {
        LcTipo = lcTipo;
        IdVenta = sale.IdVenta;
        NroVenta = sale.NroVenta;
        NroFactura = sale.NroFactura;
        Timbrado = sale.Timbrado;
        FechaVenta = sale.FechaVenta;
        HoraVenta = sale.HoraVenta;
        FormaPago = sale.FormaPago;
        TipoBoleta = sale.TipoBoleta;
        IdCliente = sale.IdCliente;
        NombreCliente = sale.NombreCliente;
        RucCliente = sale.RucCliente;
        IdVendedor = sale.IdVendedor;
        Descuento = sale.Descuento;

        Items.Clear();
        int recNo = 1;
        foreach (var item in sale.Items)
        {
            item.RecNo = recNo++;
            Items.Add(item);
        }

        SumarProductos();
    }

    public void ApplyCustomer(CustomerEntity customer)
    {
        IdCliente = customer.IdCliente;
        RucCliente = string.IsNullOrWhiteSpace(customer.Dv)
            ? customer.Ruc
            : $"{customer.Ruc}-{customer.Dv}";
        NombreCliente = customer.Nombres;
        DireccionCliente = customer.Direccion;
        TelefonoCliente = customer.Telefono;
        LineaCreditoCliente = customer.LineaCredito;
        SaldoDisponibleCliente = customer.LineaCredito;
        ListaPrecios = customer.GetAssignedPriceList(ListaPrecios);
    }

    public async Task ResetToCliente1Async()
    {
        int defaultId = CompanyData.IdCliente > 0 ? CompanyData.IdCliente : 132;
        var c = await _customerRepository.GetByIdAsync(defaultId);
        if (c != null)
        {
            ApplyCustomer(c);
        }
        else
        {
            IdCliente = 132;
            RucCliente = "X";
            NombreCliente = "CLIENTE OCASIONAL";
            LineaCreditoCliente = 0m;
            SaldoDisponibleCliente = 0m;
        }
        StatusMessage = $"Cliente restablecido a {NombreCliente} (Cliente 1).";
    }

    private void QuickCreateCustomer()
    {
        var created = _windowManager.OpenQuickCreateCustomerDialog();
        if (created != null)
        {
            ApplyCustomer(created);
            StatusMessage = $"Cliente '{created.Nombres}' creado y asignado a la venta.";
        }
    }

    private void QuickCreateSeller()
    {
        var created = _windowManager.OpenQuickCreateSellerDialog();
        if (created != null)
        {
            Vendedores.Add(created);
            IdVendedor = created.IdVendedor;
            NombreVendedor = created.Nombres;
            StatusMessage = $"Vendedor '{created.Nombres}' creado y asignado.";
        }
    }

    private void OpenPresupuestosImport()
    {
        var header = _windowManager.OpenPresupuestosImportDialog(out var budgetItems);
        if (header == null)
            return;

        ApplyBudgetToSale(header, budgetItems);
    }

    private async Task LookupPresupuestoByNumberAsync()
    {
        if (!int.TryParse(NroPresupuestoTexto?.Trim(), out int nro) || nro <= 0)
        {
            OpenPresupuestosImport();
            return;
        }

        var loaded = await _salesRepository.GetBudgetByNumberOrIdAsync(nro, byNumber: true);
        if (loaded == null)
        {
            StatusMessage = $"No se encontró el Presupuesto #{nro}.";
            return;
        }

        ApplyBudgetToSale(loaded.Value.Header, loaded.Value.Items);
    }

    public void ApplyBudgetToSale(BudgetSummaryEntity header, IReadOnlyList<BudgetItemEntity> budgetItems)
    {
        NroPresupuestoTexto = header.NroPresupuesto.ToString();
        IdCliente = header.IdCliente > 0 ? header.IdCliente : IdCliente;
        NombreCliente = !string.IsNullOrWhiteSpace(header.NombreCliente) ? header.NombreCliente : NombreCliente;
        RucCliente = !string.IsNullOrWhiteSpace(header.RucCliente) ? header.RucCliente : RucCliente;
        if (header.IdVendedor > 0) IdVendedor = header.IdVendedor;
        Descuento = header.Descuento;
        if (!string.IsNullOrWhiteSpace(header.Observacion))
            Observacion = header.Observacion;

        Items.Clear();
        foreach (var bItem in budgetItems)
        {
            var saleItem = new SaleItemEntity
            {
                IdProducto = bItem.IdProducto,
                Codigo = bItem.Codigo,
                Descripcion = bItem.Descripcion,
                Cantidad = bItem.Cantidad,
                PreUnitario = bItem.PreUnitario,
                CostoUnitario = bItem.CostoUnitario,
                IdDeposito = IdDeposito,
                Iva = bItem.Iva is >= 1 and <= 3 ? bItem.Iva : 1,
                Fraccionable = bItem.Fraccionable,
                Compuesto = bItem.Compuesto,
                NoControlaStock = bItem.NoControlaStock,
                Estado = 1
            };
            SalesCalculationEngine.ApplyItemOperation(Items, saleItem, "A", 0);
        }

        SelectedItem = Items.LastOrDefault();
        SumarProductos();
        _windowManager.GetItemsSelectorViewModel()?.ActualizarEspejo();
        StatusMessage = $"Presupuesto #{header.NroPresupuesto} importado ({Items.Count} ítems).";
    }

    private void OpenCuotasDialog()
    {
        if (TotalVenta <= 0m)
        {
            StatusMessage = "Cargue al menos un ítem antes de generar plan de cuotas.";
            return;
        }

        var installments = _windowManager.OpenCuotaVentaDialog(BuildSaleEntity(), out decimal entregaInicial);
        if (installments != null && installments.Count > 0)
        {
            _pendingInstallments = installments;
            _pendingInstallmentEntrega = entregaInicial;
            FormaPago = 2;
            StatusMessage = $"Plan de {installments.Count} cuota(s) generado (Entrega: {entregaInicial:N0} Gs.).";
        }
    }

    private void OpenCombinarSelectedItem()
    {
        var target = SelectedItem;
        if (target == null)
        {
            StatusMessage = "Seleccione un producto de la grilla para editar su receta compuesta.";
            return;
        }
        _windowManager.OpenCombinarCompuestoDialog(target.IdProducto, target.Descripcion);
    }

    private void OpenCustomerLookup()
    {
        _windowManager.OpenCustomersModule(0, customer =>
        {
            ApplyCustomer(customer);
            StatusMessage = $"Cliente seleccionado: {customer.Nombres} ({RucCliente})";
        });
    }

    private async Task LookupCustomerByRucAsync()
    {
        if (string.IsNullOrWhiteSpace(RucCliente))
            return;

        string cleanRuc = RucCliente.Split('-')[0].Trim();
        var matches = await _customerRepository.SearchCustomersAsync(cleanRuc, 5);
        var match = matches.FirstOrDefault(c => c.Ruc.Equals(cleanRuc, StringComparison.OrdinalIgnoreCase))
                    ?? matches.FirstOrDefault();
        if (match != null)
        {
            ApplyCustomer(match);
            StatusMessage = $"Cliente cargado: {match.Nombres}";
        }
    }

    private SaleEntity BuildSaleEntity()
    {
        return new SaleEntity
        {
            IdVenta = IdVenta,
            NroVenta = NroVenta,
            NroFactura = NroFactura,
            FechaVenta = FechaVenta,
            HoraVenta = HoraVenta,
            FormaPago = FormaPago,
            IdDeposito = IdDeposito,
            IdCaja = IdCaja,
            IdCliente = IdCliente,
            NombreCliente = NombreCliente,
            RucCliente = RucCliente,
            IdVendedor = IdVendedor,
            NombreVendedor = NombreVendedor,
            Timbrado = Timbrado,
            TotalExenta = TotalExenta,
            TotalIva5 = TotalIva5,
            TotalIva10 = TotalIva10,
            Subtotal = Subtotal,
            Descuento = Descuento,
            TotalVenta = TotalVenta,
            LiquidacionIva5 = LiquidacionIva5,
            LiquidacionIva10 = LiquidacionIva10,
            TotalImpuesto = TotalImpuesto,
            CidUsuario = _windowManager.CurrentUser?.Cidusuario ?? "ADMINISTRA",
            Concepto = FormaPago == 1 ? "VENTA CONTADO" : "VENTA CREDITO",
            Estado = 1,
            TipoBoleta = TipoBoleta,
            NroBoleta = NroVenta,
            CotDolar = CurrentExchangeRate.VentaDolar,
            CotReal = CurrentExchangeRate.VentaReal,
            CotPeso = CurrentExchangeRate.VentaPeso,
            CotEuro = CurrentExchangeRate.VentaEuro,
            CotGuarani = 1m,
            MonedaGuarani = MonedaGuarani,
            MonedaDolar = MonedaDolar,
            MonedaReal = MonedaReal,
            MonedaPeso = MonedaPeso,
            MonedaEuro = MonedaEuro,
            IdMoneda = IdMoneda,
            TipoVenta = FormaPago,
            EntregaGuarani = EntregaGuarani,
            VueltoGuarani = VueltoGuarani,
            CobroCaja = CompanyData.CobroCaja,
            IdSucursal = IdSucursal,
            Observacion = Observacion ?? string.Empty,
            Items = Items.Select(x => x.Clone()).ToList()
        };
    }

    /// <summary>
    /// Live multi-PC synchronization: when another PC on the LAN registers a sale,
    /// any open POS Caja in Alta ("A") mode automatically advances its NroVenta.
    /// </summary>
    public void SyncFromMultiTerminal(int latestMaxNroVenta)
    {
        if (LcTipo == "A" && latestMaxNroVenta + 1 > NroVenta)
        {
            NroVenta = latestMaxNroVenta + 1;
            StatusMessage = $"[Multiconexión LAN] Sincronizado en vivo con red — Próxima Venta #{NroVenta}.";
        }
    }
}
