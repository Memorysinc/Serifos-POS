using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FirewallControl.App.Services;
using FirewallControl.Core.Business;
using FirewallControl.Core.Entities;
using FirewallControl.Core.Interfaces;
using Microsoft.Win32;

namespace FirewallControl.App.Views;

/// <summary>
/// Ventana unificada de Carga Rapida de Productos (PROGS\carga_rapida_productos.prg),
/// compartida entre ITEMSFACTURAVENTAS [F9] y el modulo de Productos.
/// Usa ICargaRapidaIaService (singleton en segundo plano) con Gemini Files API hasta 50 MB.
/// </summary>
public partial class CargaRapidaProductosWindow : Window
{
    private readonly IProductRepository _productRepository;
    private readonly IConfigService _configService;
    private readonly ICargaRapidaIaService _iaService;
    private readonly IWindowManagerService? _windowManager;

    private bool _esEdicion;
    private decimal _costoAnteriorBd;
    private decimal _ventaAnteriorBd;
    private string _lastLookupCode = string.Empty;
    private string _lastFocusedControl = "TxtFastCod";
    private bool _modelsRefreshedThisSession;
    private bool _isUpdatingModels;
    private bool _allowPermanentClose;

    public bool AllowPermanentClose
    {
        get => _allowPermanentClose;
        set => _allowPermanentClose = value;
    }

    public CargaRapidaProductosWindow(
        IProductRepository productRepository,
        IConfigService configService,
        ICargaRapidaIaService iaService,
        IWindowManagerService? windowManager = null)
    {
        InitializeComponent();
        AppIconHelper.ApplySefirosIcon(this);
        _productRepository = productRepository;
        _configService = configService;
        _iaService = iaService;
        _windowManager = windowManager;

        _iaService.StateChanged += OnIaStateChanged;
        _iaService.QueueReady += OnIaQueueReady;
        _iaService.ErrorOccurred += OnIaErrorOccurred;

        LoadApiConfigIntoControls();
        SetModoBadge("IDLE", "");
        UpdateComparacionCosto();
        UpdateMargenPreview();

        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                ResumeOrSyncState();
            }
        };
    }

    public void ResumeOrSyncState()
    {
        _iaService.IsHidden = false;
        LoadApiConfigIntoControls();
        RefreshQueuePanelUi();

        if (_iaService.IsProcessingIa)
        {
            if (_iaService.Snapshot != null)
            {
                RestoreSnapshot(_iaService.Snapshot);
            }
            SetStatus(_iaService.GetStageStatusMessage(ObtenerModeloSeleccionado()));
        }
        else if (_iaService.PendingLoadCurrent)
        {
            _iaService.PendingLoadCurrent = false;
            _iaService.ClearSnapshot();
            _ = LoadCurrentQueueItemAsync();
        }
        else if (_iaService.Snapshot != null)
        {
            var snap = _iaService.Snapshot;
            RestoreSnapshot(snap);
            if (!string.IsNullOrWhiteSpace(_iaService.PendingStatusMessage))
            {
                SetStatus(_iaService.PendingStatusMessage);
                _iaService.PendingStatusMessage = string.Empty;
            }
            FocusByName(snap.LastFocusedControl);
            return;
        }
        else if (!string.IsNullOrWhiteSpace(_iaService.PendingStatusMessage))
        {
            SetStatus(_iaService.PendingStatusMessage);
            _iaService.PendingStatusMessage = string.Empty;
        }
        else if (_iaService.IsQueueActive && _iaService.QueueTotal > 0)
        {
            _ = LoadCurrentQueueItemAsync();
        }
        else
        {
            PosWindowBehaviorHelper.FocusAndSelect(TxtFastCod);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowPermanentClose)
        {
            e.Cancel = true;
            OcultarVentana();
            return;
        }

        _iaService.StateChanged -= OnIaStateChanged;
        _iaService.QueueReady -= OnIaQueueReady;
        _iaService.ErrorOccurred -= OnIaErrorOccurred;
        base.OnClosing(e);
    }

    private void OnIaStateChanged()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            RefreshQueuePanelUi();
            if (_iaService.IsProcessingIa && IsVisible)
            {
                SetStatus(_iaService.GetStageStatusMessage(ObtenerModeloSeleccionado()) + " Puedes pulsar [Ocultar F2].");
            }
        });
    }

    private void OnIaQueueReady(int count)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        Dispatcher.BeginInvoke(async () =>
        {
            RefreshQueuePanelUi();
            if (IsVisible && !_iaService.IsHidden)
            {
                _iaService.PendingLoadCurrent = false;
                await LoadCurrentQueueItemAsync();
            }
        });
    }

    private void OnIaErrorOccurred(string msg, string detail)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            RefreshQueuePanelUi();
            SetStatus(msg);
            if (IsVisible)
            {
                string full = string.IsNullOrWhiteSpace(detail) ? msg : $"{msg}\n\n{detail[..Math.Min(detail.Length, 800)]}";
                MessageBox.Show(this, full, "Carga Rápida IA", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        });
    }

    // =========================================================================
    // CONFIG API & MODELOS
    // =========================================================================
    private void LoadApiConfigIntoControls()
    {
        var cfg = _configService.Config;
        TxtApiKey.Text = cfg.FastCargaApiKey ?? string.Empty;

        string currentModel = string.IsNullOrWhiteSpace(cfg.FastCargaApiModel)
            ? FastCargaParser.DefaultApiModel
            : cfg.FastCargaApiModel.Trim();

        CboApiModel.Items.Clear();
        foreach (string predef in FastCargaParser.PredefinedModels)
        {
            AddModelToCombo(predef, insertAtTop: false);
        }

        if (!string.IsNullOrWhiteSpace(cfg.FastCargaApiModelsCatalog))
        {
            foreach (string extra in cfg.FastCargaApiModelsCatalog.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddModelToCombo(extra, insertAtTop: false);
            }
        }

        AddModelToCombo(currentModel, insertAtTop: false);
        CboApiModel.Text = currentModel;
    }

    private bool AddModelToCombo(string modelName, bool insertAtTop)
    {
        string clean = (modelName ?? string.Empty).Replace("\r", "").Replace("\n", "").Replace("\t", "").Trim();
        if (string.IsNullOrWhiteSpace(clean))
            return false;

        foreach (object item in CboApiModel.Items)
        {
            if (string.Equals(item?.ToString(), clean, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (insertAtTop && CboApiModel.Items.Count > 0)
            CboApiModel.Items.Insert(0, clean);
        else
            CboApiModel.Items.Add(clean);

        return true;
    }

    private string ObtenerModeloSeleccionado()
    {
        string txt = (CboApiModel.Text ?? string.Empty).Replace("\r", "").Replace("\n", "").Replace("\t", "").Trim();
        return string.IsNullOrWhiteSpace(txt) ? FastCargaParser.DefaultApiModel : txt;
    }

    private void GuardarConfigApi(bool silent)
    {
        string apiKey = (TxtApiKey.Text ?? string.Empty).Trim();
        string model = ObtenerModeloSeleccionado();
        AddModelToCombo(model, insertAtTop: false);
        CboApiModel.Text = model;

        var catalogItems = new List<string>();
        foreach (object item in CboApiModel.Items)
        {
            string s = item?.ToString()?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(s) &&
                !s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                catalogItems.Add(s);
            }
        }

        string catalogCsv = string.Join(",", catalogItems);
        if (catalogCsv.Length > 980) catalogCsv = catalogCsv[..980];

        _configService.SaveFastCargaSettings(apiKey, model, catalogCsv);
        if (!silent)
        {
            SetStatus($"API y modelo guardados en FireWall.Ini ({model}).");
        }
    }

    private async Task ActualizarModelosApiAsync(bool silent)
    {
        if (_isUpdatingModels)
            return;

        string key = (TxtApiKey.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            SetStatus("Ingresa una API Key válida antes de actualizar modelos.");
            if (!silent)
                MessageBox.Show(this, "Ingresa una API Key válida en el campo API KEY.", "API requerida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string curModel = ObtenerModeloSeleccionado();
        _isUpdatingModels = true;
        BtnActModelos.IsEnabled = false;
        BtnActModelos.Content = "Buscando...";
        SetStatus("Consultando modelos Gemini disponibles en la API...");

        try
        {
            var discovered = await _iaService.FetchAvailableModelsAsync(key);
            if (discovered.Count == 0)
            {
                SetStatus("No se encontraron modelos generativos Gemini en la respuesta.");
            }
            else
            {
                int added = 0;
                foreach (string m in discovered)
                {
                    if (AddModelToCombo(m, insertAtTop: true))
                        added++;
                }

                AddModelToCombo(curModel, insertAtTop: false);
                CboApiModel.Text = curModel;
                _modelsRefreshedThisSession = true;
                GuardarConfigApi(silent: true);
                SetStatus($"Modelos actualizados ({CboApiModel.Items.Count} disponibles{(added > 0 ? $", +{added} nuevo/s" : "")}).");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error al consultar modelos: {ex.Message}");
            if (!silent)
                MessageBox.Show(this, $"No se pudo actualizar la lista de modelos:\n{ex.Message}", "Modelos Gemini", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isUpdatingModels = false;
            BtnActModelos.IsEnabled = true;
            BtnActModelos.Content = "Act. Modelos";
        }
    }

    // =========================================================================
    // FLUJO IA (FILES API) & COLA DE PRODUCTOS
    // =========================================================================
    private async Task IniciarProcesoIaAsync()
    {
        if (_iaService.IsProcessingIa)
            return;

        GuardarConfigApi(silent: true);
        string apiKey = (TxtApiKey.Text ?? string.Empty).Trim();
        string model = ObtenerModeloSeleccionado();

        if (string.IsNullOrWhiteSpace(apiKey) && !model.Contains("key=", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("Ingresa tu API Key de Gemini antes de analizar facturas.");
            MessageBox.Show(this, "Ingresa una API Key válida en el campo API KEY.", "API requerida", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtApiKey.Focus();
            return;
        }

        var ofd = new OpenFileDialog
        {
            Title = "Selecciona las fotos o PDFs de la factura (hasta 50 MB total)",
            Filter = "Imágenes y PDF (*.jpg;*.jpeg;*.png;*.webp;*.pdf)|*.jpg;*.jpeg;*.png;*.webp;*.pdf|Todos (*.*)|*.*",
            Multiselect = true
        };

        if (ofd.ShowDialog(this) != true || ofd.FileNames.Length == 0)
            return;

        try
        {
            await _iaService.StartAnalyzeInvoicesAsync(ofd.FileNames, apiKey, model);
            RefreshQueuePanelUi();
            SetStatus(_iaService.GetStageStatusMessage(model) + " Puedes pulsar [Ocultar F2].");
        }
        catch (Exception ex)
        {
            RefreshQueuePanelUi();
            SetStatus(ex.Message);
            MessageBox.Show(this, ex.Message, "Carga Rápida IA", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task LoadCurrentQueueItemAsync()
    {
        var item = _iaService.GetCurrentQueueItem();
        if (item == null)
        {
            FinalizarColaIa(showDialog: false);
            return;
        }

        LimpiarCampos();
        TxtFastCod.Text = item.Codigo;
        await BuscarPorCodigoAsync(moveFocus: false);

        if (_esEdicion)
        {
            if (item.Costo > 0m || _costoAnteriorBd == 0m)
            {
                TxtFastCosto.Text = FormatInt(item.Costo);
            }
            SetStatus($"Cola IA [{_iaService.QueueIndex}/{_iaService.QueueTotal}] · EDICIÓN Ant: {FormatInt(_costoAnteriorBd)} -> Nuevo: {TxtFastCosto.Text} · Ajusta Venta y [Enter].");
        }
        else
        {
            TxtFastDesc.Text = item.Nombre.ToUpperInvariant();
            TxtFastCosto.Text = FormatInt(item.Costo);
            SetStatus($"Cola IA [{_iaService.QueueIndex}/{_iaService.QueueTotal}] · NUEVO Nombre y costo ({FormatInt(item.Costo)} Gs.) · Ajusta Venta y [Enter].");
        }

        UpdateComparacionCosto();
        UpdateMargenPreview();
        RefreshQueuePanelUi();
        _lastFocusedControl = "TxtFastVenta";
        PosWindowBehaviorHelper.FocusAndSelect(TxtFastVenta);
    }

    private void AvanzarCola(bool skipped)
    {
        var next = _iaService.AdvanceQueue(skipped);
        RefreshQueuePanelUi();
        if (next != null)
        {
            _ = LoadCurrentQueueItemAsync();
        }
        else
        {
            FinalizarColaIa(showDialog: true);
        }
    }

    private void FinalizarColaIa(bool showDialog)
    {
        var items = _iaService.QueueItems.ToList();
        int total = items.Count;
        int guardados = items.Count(i => string.Equals(i.Estado, "GUARDADO", StringComparison.OrdinalIgnoreCase));
        int saltados = items.Count(i => string.Equals(i.Estado, "SALTADO", StringComparison.OrdinalIgnoreCase));

        _iaService.CancelAndResetAll();
        LimpiarCampos();
        RefreshQueuePanelUi();

        string msg = total > 0
            ? $"¡Todos los productos fueron procesados! Guardados: {guardados}/{total}{(saltados > 0 ? $" (Saltados: {saltados})" : "")}"
            : "Cola IA finalizada. Listo para ingreso manual.";
        SetStatus(msg);

        if (showDialog && total > 0)
        {
            MessageBox.Show(this, msg, "Carga Rápida IA", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        PosWindowBehaviorHelper.FocusAndSelect(TxtFastCod);
    }

    // =========================================================================
    // BÚSQUEDA Y GUARDADO EN MYSQL
    // =========================================================================
    private async Task<bool> BuscarPorCodigoAsync(bool moveFocus)
    {
        string cod = (TxtFastCod.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cod))
            return false;

        try
        {
            var prod = await _productRepository.GetByCodeIncludingInactiveAsync(cod, 1);
            _lastLookupCode = cod;

            if (prod != null && prod.IdProducto > 0)
            {
                _esEdicion = true;
                _costoAnteriorBd = prod.Costo1;
                _ventaAnteriorBd = prod.Precio1;

                TxtFastDesc.Text = prod.Descripcion;
                TxtFastCostoAnt.Text = FormatInt(prod.Costo1);
                TxtFastCosto.Text = FormatInt(prod.Costo1);
                TxtFastVenta.Text = FormatInt(prod.Precio1);
                ChkFastFraccionable.IsChecked = prod.Fraccionable == 1;
                ChkFastNoControlaStock.IsChecked = prod.NoControlaStock == 1;

                SetModoBadge("EDIT", cod);
                UpdateComparacionCosto();
                UpdateMargenPreview();
                SetStatus($"Producto encontrado: {cod} · Costo ant: {FormatInt(prod.Costo1)} Gs. · Venta: {FormatInt(prod.Precio1)} Gs.");

                if (moveFocus)
                {
                    _lastFocusedControl = "TxtFastCosto";
                    PosWindowBehaviorHelper.FocusAndSelect(TxtFastCosto);
                }
            }
            else
            {
                _esEdicion = false;
                _costoAnteriorBd = 0m;
                _ventaAnteriorBd = 0m;

                TxtFastDesc.Text = string.Empty;
                TxtFastCostoAnt.Text = "0";
                TxtFastCosto.Text = "0";
                TxtFastVenta.Text = "0";
                ChkFastFraccionable.IsChecked = false;
                ChkFastNoControlaStock.IsChecked = true;

                SetModoBadge("NEW", cod);
                UpdateComparacionCosto();
                UpdateMargenPreview();
                SetStatus($"Código {cod} disponible · ingresa descripción, costo y precio de venta.");

                if (moveFocus)
                {
                    _lastFocusedControl = "TxtFastDesc";
                    PosWindowBehaviorHelper.FocusAndSelect(TxtFastDesc);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Error al consultar MySQL: {ex.Message}");
            return false;
        }
    }

    private async Task GuardarProductoAsync()
    {
        string cod = (TxtFastCod.Text ?? string.Empty).Trim();
        string desc = (TxtFastDesc.Text ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(cod))
        {
            SetStatus("Ingresa el código del producto antes de guardar.");
            PosWindowBehaviorHelper.FocusAndSelect(TxtFastCod);
            return;
        }

        if (!string.Equals(cod, _lastLookupCode, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(desc))
        {
            await BuscarPorCodigoAsync(moveFocus: false);
            desc = (TxtFastDesc.Text ?? string.Empty).Trim().ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(desc))
        {
            SetStatus("Ingresa la descripción del producto antes de guardar.");
            PosWindowBehaviorHelper.FocusAndSelect(TxtFastDesc);
            return;
        }

        decimal costo = ParseAmount(TxtFastCosto.Text);
        decimal venta = ParseAmount(TxtFastVenta.Text);
        bool fraccionable = ChkFastFraccionable.IsChecked == true;
        bool noControlaStock = ChkFastNoControlaStock.IsChecked == true;
        bool wasEdit = _esEdicion;

        try
        {
            await _productRepository.SaveFastProductAsync(cod, desc, costo, venta, fraccionable, wasEdit, noControlaStock);
            LimpiarCampos();
            SetStatus($"{(wasEdit ? "Actualizado" : "Guardado (Nuevo)")}: {cod} · {desc} · Venta: {FormatInt(venta)} Gs. · {(noControlaStock ? "Sin control stock" : "Con control stock")}");

            if (_iaService.IsQueueActive)
            {
                AvanzarCola(skipped: false);
            }
            else
            {
                PosWindowBehaviorHelper.FocusAndSelect(TxtFastCod);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error al guardar producto: {ex.Message}");
            MessageBox.Show(this, $"Error al guardar producto en MySQL:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // =========================================================================
    // SNAPSHOT, VISUAL FEEDBACK & SHORTCUTS
    // =========================================================================
    private void OcultarVentana()
    {
        _iaService.SaveSnapshot(new FastCargaSnapshot
        {
            Codigo = TxtFastCod.Text,
            Descripcion = TxtFastDesc.Text,
            CostoAnterior = ParseAmount(TxtFastCostoAnt.Text),
            CostoNuevo = ParseAmount(TxtFastCosto.Text),
            PrecioVenta = ParseAmount(TxtFastVenta.Text),
            Fraccionable = ChkFastFraccionable.IsChecked == true,
            NoControlaStock = ChkFastNoControlaStock.IsChecked == true,
            EsEdicion = _esEdicion,
            CostoAnteriorBd = _costoAnteriorBd,
            VentaAnteriorBd = _ventaAnteriorBd,
            LastLookupCode = _lastLookupCode,
            StatusMessage = LblStatus.Text,
            LastFocusedControl = _lastFocusedControl
        });
        if (_windowManager != null)
            _windowManager.SafeHideAndRestoreFocus(this);
        else
            Hide();
    }

    private void CerrarYCancelarTodo()
    {
        _iaService.CancelAndResetAll();
        LimpiarCampos();
        RefreshQueuePanelUi();
        if (_windowManager != null)
            _windowManager.SafeHideAndRestoreFocus(this);
        else
            Hide();
    }

    private void RestoreSnapshot(FastCargaSnapshot snap)
    {
        TxtFastCod.Text = snap.Codigo;
        TxtFastDesc.Text = snap.Descripcion;
        TxtFastCostoAnt.Text = FormatInt(snap.CostoAnterior);
        TxtFastCosto.Text = FormatInt(snap.CostoNuevo);
        TxtFastVenta.Text = FormatInt(snap.PrecioVenta);
        ChkFastFraccionable.IsChecked = snap.Fraccionable;
        ChkFastNoControlaStock.IsChecked = snap.NoControlaStock;
        _esEdicion = snap.EsEdicion;
        _costoAnteriorBd = snap.CostoAnteriorBd;
        _ventaAnteriorBd = snap.VentaAnteriorBd;
        _lastLookupCode = snap.LastLookupCode;
        _lastFocusedControl = snap.LastFocusedControl;

        SetModoBadge(string.IsNullOrWhiteSpace(snap.Codigo) ? "IDLE" : (snap.EsEdicion ? "EDIT" : "NEW"), snap.Codigo);
        UpdateComparacionCosto();
        UpdateMargenPreview();
        RefreshQueuePanelUi();
        if (!string.IsNullOrWhiteSpace(snap.StatusMessage))
            SetStatus(snap.StatusMessage);
    }

    private void RefreshQueuePanelUi()
    {
        if (_iaService.IsProcessingIa)
        {
            BtnAnalizarIa.IsEnabled = false;
            BtnAnalizarIa.Content = _iaService.CurrentStage == "GENERATE" ? "Analizando..." : "Subiendo...";
            ShpCola.Background = BrushFromHex("#E8F1FD");
            ShpCola.BorderBrush = BrushFromHex("#A8C8F0");
            LblCola.Foreground = BrushFromHex("#184E92");
            LblCola.Text = _iaService.GetStageBadge();
        }
        else
        {
            BtnAnalizarIa.IsEnabled = true;
            BtnAnalizarIa.Content = "Analizar Factura [F3]";
            if (_iaService.IsQueueActive && _iaService.QueueTotal > 0)
            {
                ShpCola.Background = BrushFromHex("#E8F1FD");
                ShpCola.BorderBrush = BrushFromHex("#A8C8F0");
                LblCola.Foreground = BrushFromHex("#184E92");
                LblCola.Text = _iaService.GetStageBadge();
            }
            else
            {
                ShpCola.Background = BrushFromHex("#F4F1EB");
                ShpCola.BorderBrush = BrushFromHex("#D6D0C6");
                LblCola.Foreground = BrushFromHex("#524E48");
                LblCola.Text = "COLA IA · Sin productos en espera";
            }
        }

        BtnSaltarCola.IsEnabled = _iaService.IsQueueActive && !_iaService.IsProcessingIa;
        BtnVaciarCola.IsEnabled = _iaService.IsQueueActive || _iaService.IsProcessingIa;
    }

    private void SetModoBadge(string mode, string cod)
    {
        switch (mode)
        {
            case "EDIT":
                ShpModo.Background = BrushFromHex("#E8F1FD");
                ShpModo.BorderBrush = BrushFromHex("#B4D0F2");
                LblModo.Foreground = BrushFromHex("#184E92");
                LblModo.Text = $"EDICIÓN · Producto existente ({cod})";
                break;
            case "NEW":
                ShpModo.Background = BrushFromHex("#ECFAF2");
                ShpModo.BorderBrush = BrushFromHex("#A6DABC");
                LblModo.Foreground = BrushFromHex("#157642");
                LblModo.Text = $"NUEVO · Alta rápida ({cod})";
                break;
            default:
                ShpModo.Background = BrushFromHex("#EDEAE3");
                ShpModo.BorderBrush = BrushFromHex("#D6D0C6");
                LblModo.Foreground = BrushFromHex("#48443F");
                LblModo.Text = "LISTO · Esperando código de producto";
                break;
        }
    }

    private void UpdateComparacionCosto()
    {
        if (string.IsNullOrWhiteSpace(TxtFastCod.Text) && string.IsNullOrWhiteSpace(_lastLookupCode))
        {
            ShpCompCosto.Background = BrushFromHex("#F7F5F1");
            ShpCompCosto.BorderBrush = BrushFromHex("#DED9D0");
            LblCompCosto.Foreground = BrushFromHex("#5C5852");
            LblCompCosto.Text = "Costo anterior: — · Escanea un código o analiza factura para comparar variación";
            return;
        }

        decimal nuevo = ParseAmount(TxtFastCosto.Text);
        if (!_esEdicion)
        {
            ShpCompCosto.Background = BrushFromHex("#ECFAF2");
            ShpCompCosto.BorderBrush = BrushFromHex("#A6DABC");
            LblCompCosto.Foreground = BrushFromHex("#157642");
            LblCompCosto.Text = $"PRODUCTO NUEVO · Sin costo anterior registrado en BD · Costo cargado: {FormatInt(nuevo)} Gs.";
            return;
        }

        decimal ant = _costoAnteriorBd;
        decimal dif = nuevo - ant;
        string pctTxt = ant > 0m && dif != 0m ? $" / {(dif > 0 ? "+" : "")}{(dif / ant) * 100m:0.0}%" : string.Empty;

        if (dif > 0m)
        {
            ShpCompCosto.Background = BrushFromHex("#FEF2F2");
            ShpCompCosto.BorderBrush = BrushFromHex("#F4B6B6");
            LblCompCosto.Foreground = BrushFromHex("#B22424");
            LblCompCosto.Text = $"SUBIÓ EL COSTO: Anterior {FormatInt(ant)} Gs. -> Nuevo {FormatInt(nuevo)} Gs. (+{FormatInt(dif)} Gs.{pctTxt}) · Venta actual: {FormatInt(_ventaAnteriorBd)} Gs.";
        }
        else if (dif < 0m)
        {
            ShpCompCosto.Background = BrushFromHex("#ECFAF2");
            ShpCompCosto.BorderBrush = BrushFromHex("#A6DABC");
            LblCompCosto.Foreground = BrushFromHex("#157642");
            LblCompCosto.Text = $"BAJÓ EL COSTO: Anterior {FormatInt(ant)} Gs. -> Nuevo {FormatInt(nuevo)} Gs. ({FormatInt(dif)} Gs.{pctTxt}) · Venta actual: {FormatInt(_ventaAnteriorBd)} Gs.";
        }
        else
        {
            ShpCompCosto.Background = BrushFromHex("#EFF4FC");
            ShpCompCosto.BorderBrush = BrushFromHex("#C2D2EC");
            LblCompCosto.Foreground = BrushFromHex("#244A84");
            LblCompCosto.Text = $"COSTO SIN CAMBIO: {FormatInt(ant)} Gs. (igual al registrado en BD) · Venta actual: {FormatInt(_ventaAnteriorBd)} Gs.";
        }
    }

    private void UpdateMargenPreview()
    {
        decimal costo = ParseAmount(TxtFastCosto.Text);
        decimal venta = ParseAmount(TxtFastVenta.Text);

        if (costo > 0m && venta > 0m)
        {
            decimal dif = venta - costo;
            decimal pct = (dif / costo) * 100m;
            LblMargen.Text = $"Ganancia: {(dif >= 0 ? "+" : "")}{FormatInt(dif)} Gs. ({(pct >= 0 ? "+" : "")}{pct:0.0}%)";
            LblMargen.Foreground = BrushFromHex(dif > 0 ? "#157642" : "#B22A2A");
        }
        else if (venta > 0m)
        {
            LblMargen.Text = $"Venta: {FormatInt(venta)} Gs.";
            LblMargen.Foreground = BrushFromHex("#48443F");
        }
        else
        {
            LblMargen.Text = "Margen: —";
            LblMargen.Foreground = BrushFromHex("#736E66");
        }
    }

    private void LimpiarCampos()
    {
        TxtFastCod.Text = string.Empty;
        TxtFastDesc.Text = string.Empty;
        TxtFastCostoAnt.Text = "0";
        TxtFastCosto.Text = "0";
        TxtFastVenta.Text = "0";
        ChkFastFraccionable.IsChecked = false;
        ChkFastNoControlaStock.IsChecked = true;
        _costoAnteriorBd = 0m;
        _ventaAnteriorBd = 0m;
        _esEdicion = false;
        _lastLookupCode = string.Empty;
        _lastFocusedControl = "TxtFastCod";
        SetModoBadge("IDLE", "");
        UpdateComparacionCosto();
        UpdateMargenPreview();
    }

    private void SetStatus(string msg) => LblStatus.Text = msg;

    private void FocusByName(string name)
    {
        TextBox target = name switch
        {
            "TxtFastVenta" => TxtFastVenta,
            "TxtFastCosto" => TxtFastCosto,
            "TxtFastDesc" => TxtFastDesc,
            _ => TxtFastCod
        };
        PosWindowBehaviorHelper.FocusAndSelect(target);
    }

    private static decimal ParseAmount(string? raw)
        => PosNumericParser.TryParseGuaraniAmount(raw, out decimal val) ? val : 0m;

    private static string FormatInt(decimal val)
        => Math.Round(val, 0, MidpointRounding.AwayFromZero).ToString("N0");

    private static SolidColorBrush BrushFromHex(string hex)
        => new((Color)ColorConverter.ConvertFromString(hex));

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================
    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                CerrarYCancelarTodo();
                break;
            case Key.F2:
                e.Handled = true;
                OcultarVentana();
                break;
            case Key.F3:
                e.Handled = true;
                await IniciarProcesoIaAsync();
                break;
            case Key.F4:
                e.Handled = true;
                if (_iaService.IsQueueActive) AvanzarCola(skipped: true);
                else BtnAbortar_Click(this, new RoutedEventArgs());
                break;
            case Key.F8:
            case Key.F9:
                e.Handled = true;
                await GuardarProductoAsync();
                break;
        }
    }

    private void BtnOcultar_Click(object sender, RoutedEventArgs e) => OcultarVentana();
    private void BtnCerrar_Click(object sender, RoutedEventArgs e) => CerrarYCancelarTodo();
    private async void BtnAnalizarIa_Click(object sender, RoutedEventArgs e) => await IniciarProcesoIaAsync();
    private void BtnSaltarCola_Click(object sender, RoutedEventArgs e) => AvanzarCola(skipped: true);
    private void BtnVaciarCola_Click(object sender, RoutedEventArgs e)
    {
        int total = _iaService.QueueTotal;
        _iaService.CancelAndResetAll();
        LimpiarCampos();
        RefreshQueuePanelUi();
        SetStatus($"Cola IA cancelada ({total} ítem/s). Listo para ingreso manual.");
        PosWindowBehaviorHelper.FocusAndSelect(TxtFastCod);
    }

    private async void BtnActModelos_Click(object sender, RoutedEventArgs e) => await ActualizarModelosApiAsync(silent: false);
    private void BtnGuardarApi_Click(object sender, RoutedEventArgs e) => GuardarConfigApi(silent: false);

    private void BtnAbortar_Click(object sender, RoutedEventArgs e)
    {
        LimpiarCampos();
        SetStatus("Carga cancelada. Listo para ingresar otro código.");
        PosWindowBehaviorHelper.FocusAndSelect(TxtFastCod);
    }

    private async void BtnGuardar_Click(object sender, RoutedEventArgs e) => await GuardarProductoAsync();

    private void TxtApiKey_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; CboApiModel.Focus(); }
    }

    private void CboApiModel_DropDownOpened(object? sender, EventArgs e)
    {
        if (!_modelsRefreshedThisSession && !string.IsNullOrWhiteSpace(TxtApiKey.Text))
        {
            _ = ActualizarModelosApiAsync(silent: true);
        }
    }

    private void CboApiModel_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; GuardarConfigApi(silent: false); PosWindowBehaviorHelper.FocusAndSelect(TxtFastCod); }
    }

    private async void TxtFastCod_LostFocus(object sender, RoutedEventArgs e)
    {
        string cod = (TxtFastCod.Text ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(cod) && !string.Equals(cod, _lastLookupCode, StringComparison.OrdinalIgnoreCase))
        {
            await BuscarPorCodigoAsync(moveFocus: false);
        }
    }

    private async void TxtFastCod_KeyDown(object sender, KeyEventArgs e)
    {
        _lastFocusedControl = "TxtFastCod";
        if (e.Key == Key.Enter) { e.Handled = true; await BuscarPorCodigoAsync(moveFocus: true); }
    }

    private void TxtFastDesc_KeyDown(object sender, KeyEventArgs e)
    {
        _lastFocusedControl = "TxtFastDesc";
        if (e.Key == Key.Enter) { e.Handled = true; PosWindowBehaviorHelper.FocusAndSelect(TxtFastCosto); }
    }

    private void TxtFastCosto_KeyDown(object sender, KeyEventArgs e)
    {
        _lastFocusedControl = "TxtFastCosto";
        if (e.Key == Key.Enter) { e.Handled = true; PosWindowBehaviorHelper.FocusAndSelect(TxtFastVenta); }
    }

    private async void TxtFastVenta_KeyDown(object sender, KeyEventArgs e)
    {
        _lastFocusedControl = "TxtFastVenta";
        if (e.Key == Key.Enter) { e.Handled = true; await GuardarProductoAsync(); }
    }

    private void OnCostOrPriceChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateComparacionCosto();
        UpdateMargenPreview();
    }

    private void OnCostOrPriceLostFocus(object sender, RoutedEventArgs e)
    {
        UpdateComparacionCosto();
        UpdateMargenPreview();
    }
}
