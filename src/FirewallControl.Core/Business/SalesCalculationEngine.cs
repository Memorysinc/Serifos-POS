using FirewallControl.Core.Entities;

namespace FirewallControl.Core.Business;

public record ValidationResult(bool IsValid, string ErrorMessage = "", bool IsWarningOnly = false)
{
    public static ValidationResult Success() => new(true);
    public static ValidationResult Fail(string message) => new(false, message, false);
    public static ValidationResult Warn(string message) => new(true, message, true);
}

public record SaleTotalsResult(
    decimal TotalExenta,
    decimal TotalIva5,
    decimal TotalIva10,
    decimal Subtotal,
    decimal Descuento,
    decimal TotalVenta,
    decimal LiquidacionIva5,
    decimal LiquidacionIva10,
    decimal TotalImpuesto,
    decimal MonedaGuarani,
    decimal MonedaDolar,
    decimal MonedaReal,
    decimal MonedaPeso,
    decimal MonedaEuro);

/// <summary>
/// Motor de reglas de negocio, validaciones y cálculos para ITEMSFACTURAVENTAS.SCX y ABM_VENTAS_AUTOSERVICE_COBRO_CONTADO.SCX.
/// Conserva íntegramente las validaciones, mensajes y fórmulas del código Visual FoxPro 9 original.
/// </summary>
public static class SalesCalculationEngine
{
    /// <summary>
    /// En ITEMSFACTURAVENTAS.Kilogramo.KeyPress:
    /// thisform.cantidad.value = this.value / thisform.precio_venta.value
    /// </summary>
    public static decimal CalculateQuantityFromGuaranies(decimal montoGuaranies, decimal precioVenta)
    {
        if (precioVenta <= 0m || montoGuaranies <= 0m)
            return 0m;

        return Math.Round(montoGuaranies / precioVenta, 6, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Validación de cantidad en ITEMSFACTURAVENTAS:
    /// - <= 0: "La cantidad debe ser mayor que cero."
    /// - > LimiteCantidadPorItem (si > 0): alerta de cantidad máxima por ítem
    /// - nocontrolastock == 0 y stockactual < cantidad: alerta de stock insuficiente
    /// - Si noControlarStockGlobal == true, omite el control de stock en todos los ítems.
    /// </summary>
    public static ValidationResult ValidateQuantity(
        decimal cantidad,
        int noControlaStock,
        int vendeSinStock,
        decimal stockActual,
        bool noControlarStockGlobal = false,
        decimal limiteCantidad = 120m)
    {
        if (cantidad <= 0m)
            return ValidationResult.Fail("La cantidad debe ser mayor que cero.");

        if (limiteCantidad > 0m && cantidad > limiteCantidad)
            return ValidationResult.Fail($"La cantidad supera el límite máximo permitido por ítem ({limiteCantidad:0.##}).");

        if (!noControlarStockGlobal && noControlaStock == 0 && stockActual < cantidad)
        {
            const string msg = "Producto sin stock suficiente en el depósito seleccionado.";
            return vendeSinStock == 1
                ? ValidationResult.Warn(msg)
                : ValidationResult.Fail(msg);
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validación de precio unitario en ITEMSFACTURAVENTAS (con límites editables desde Configuración):
    /// - <= 0: "El precio de venta debe ser mayor que cero."
    /// - <= costo (cuando no se permite venta bajo costo): "El precio de venta no puede ser igual ni inferior al costo."
    /// - < precioMinimo (si > 0) o >= precioMaximo (si > 0): alerta de rango permitido.
    /// </summary>
    public static ValidationResult ValidateUnitPrice(
        decimal precioVenta,
        decimal costoUnitario,
        bool checkGrabarLimits = true,
        bool permitirVentaBajoCosto = false,
        decimal precioMinimo = 99m,
        decimal precioMaximo = 1000000m)
    {
        if (precioVenta <= 0m)
            return ValidationResult.Fail("El precio de venta debe ser mayor que cero.");

        if (!permitirVentaBajoCosto && costoUnitario > 0m && precioVenta <= costoUnitario)
            return ValidationResult.Fail("El precio de venta no puede ser igual ni inferior al costo.");

        if (checkGrabarLimits)
        {
            if (precioMinimo > 0m && precioVenta < precioMinimo)
                return ValidationResult.Fail($"El precio unitario es inferior al mínimo permitido ({precioMinimo:N0} Gs.).");

            if (precioMaximo > 0m && precioVenta >= precioMaximo)
                return ValidationResult.Fail($"El precio unitario supera el máximo permitido ({precioMaximo:N0} Gs.).");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Calcula el costo promedio ponderado exacto de itemsfacturacompras.scx:
    /// Si stockActual > 0: ((stockActual * costoAnterior) + (cantidadCompra * costoCompra)) / (stockActual + cantidadCompra)
    /// Si stockActual <= 0: costoCompra
    /// </summary>
    public static decimal CalculateWeightedAverageCost(
        decimal stockActual,
        decimal costoAnterior,
        decimal cantidadCompra,
        decimal costoCompra)
    {
        if (costoCompra <= 0m)
            return costoAnterior;
        if (stockActual > 0m && costoAnterior > 0m && (stockActual + cantidadCompra) > 0m)
        {
            return Math.Round(
                ((stockActual * costoAnterior) + (cantidadCompra * costoCompra)) / (stockActual + cantidadCompra),
                2,
                MidpointRounding.AwayFromZero);
        }
        return Math.Round(costoCompra, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Calcula el precio de venta a partir del costo base y porcentaje de utilidad (itemsfacturacompras.scx / abm_productos.scx).
    /// </summary>
    public static decimal CalculateSalePriceFromUtility(decimal costoBase, decimal porcentajeUtilidad)
    {
        if (costoBase <= 0m)
            return 0m;
        return Math.Round(costoBase + (costoBase * porcentajeUtilidad / 100m), 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Calcula el porcentaje de utilidad a partir del costo base y el precio de venta (itemsfacturacompras.scx / abm_productos.scx).
    /// </summary>
    public static decimal CalculateUtilityFromSalePrice(decimal costoBase, decimal precioVenta)
    {
        if (costoBase <= 0m || precioVenta <= 0m)
            return 0m;
        return Math.Round(((precioVenta - costoBase) * 100m) / costoBase, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Calcula el subtotal e impuestos de una línea de producto exactamente como en ITEMSFACTURAVENTAS:
    /// lnSubTotal = lnCantidad * lnPrecio
    /// Si fraccionable == 1: total = redondearmas(lnSubTotal, 2)
    /// Si iva == 1 -> iva10 = total; iva == 2 -> iva5 = total; iva == 3 -> exenta = total
    /// </summary>
    public static void CalculateItemAmounts(SaleItemEntity item)
    {
        decimal rawSubTotal = item.Cantidad * item.PreUnitario;
        decimal lineTotal;
        if (item.Fraccionable == 1 && item.Kilogramo > 0m)
        {
            lineTotal = Math.Round(item.Kilogramo, 0, MidpointRounding.AwayFromZero);
        }
        else if (item.Fraccionable == 1)
        {
            lineTotal = VfpCompat.RedondearMas(rawSubTotal, 2);
        }
        else
        {
            lineTotal = Math.Round(rawSubTotal, 2, MidpointRounding.AwayFromZero);
        }

        item.PrecioTotal = lineTotal;
        switch (item.Iva)
        {
            case 3: // Exenta
                item.PreExenta = lineTotal;
                item.PreIva5 = 0m;
                item.PreIva10 = 0m;
                break;
            case 2: // IVA 5%
                item.PreExenta = 0m;
                item.PreIva5 = lineTotal;
                item.PreIva10 = 0m;
                break;
            default: // 1 = IVA 10%
                item.PreExenta = 0m;
                item.PreIva5 = 0m;
                item.PreIva10 = lineTotal;
                break;
        }
    }

    /// <summary>
    /// Ejecuta la lógica real de CmdGrabar.Click en ITEMSFACTURAVENTAS.SCX:
    /// - lctipo = "A": Agrega un producto nuevo al cursor (INSERT INTO)
    /// - lctipo = "M": Modifica el producto existente en WHERE RECNO() = thisform.nrecno (UPDATE)
    /// </summary>
    public static SaleItemEntity ApplyItemOperation(
        IList<SaleItemEntity> cursorItems,
        SaleItemEntity inputItem,
        string lcTipo,
        int nRecNo)
    {
        CalculateItemAmounts(inputItem);

        if (string.Equals(lcTipo, "M", StringComparison.OrdinalIgnoreCase))
        {
            var existing = cursorItems.FirstOrDefault(x => x.RecNo == nRecNo);
            if (existing != null)
            {
                existing.IdProducto = inputItem.IdProducto;
                existing.Codigo = inputItem.Codigo;
                existing.Descripcion = inputItem.Descripcion;
                existing.Cantidad = inputItem.Cantidad;
                existing.PreUnitario = inputItem.PreUnitario;
                existing.PreExenta = inputItem.PreExenta;
                existing.PreIva5 = inputItem.PreIva5;
                existing.PreIva10 = inputItem.PreIva10;
                existing.PrecioTotal = inputItem.PrecioTotal;
                existing.ControlaGarantia = inputItem.ControlaGarantia;
                existing.FechaInicioGarantia = inputItem.FechaInicioGarantia;
                existing.DiasGarantias = inputItem.DiasGarantias;
                existing.FechaFinGarantia = inputItem.FechaFinGarantia;
                existing.IdDeposito = inputItem.IdDeposito;
                existing.Iva = inputItem.Iva;
                existing.Fraccionable = inputItem.Fraccionable;
                existing.Compuesto = inputItem.Compuesto;
                existing.NoControlaStock = inputItem.NoControlaStock;
                existing.CostoUnitario = inputItem.CostoUnitario;
                existing.Kilogramo = inputItem.Kilogramo;
                return existing;
            }
        }

        // Caso lctipo = "A" (INSERT INTO)
        var newItem = inputItem.Clone();
        int nextRecNo = cursorItems.Count == 0 ? 1 : cursorItems.Max(x => x.RecNo) + 1;
        newItem.RecNo = nextRecNo;
        cursorItems.Add(newItem);
        return newItem;
    }

    /// <summary>
    /// Reproduce exactamente sumarproductos() de ABM_VENTAS_AUTOSERVICE_COBRO_CONTADO.SCX.
    /// </summary>
    public static SaleTotalsResult SumarProductos(
        IEnumerable<SaleItemEntity> items,
        decimal descuento = 0m,
        int decimales = 0,
        ExchangeRateEntity? cotizacion = null)
    {
        decimal totalExenta = 0m;
        decimal totalIva5 = 0m;
        decimal totalIva10 = 0m;

        foreach (var item in items)
        {
            totalExenta += item.PreExenta;
            totalIva5 += item.PreIva5;
            totalIva10 += item.PreIva10;
        }

        decimal subtotal = totalExenta + totalIva5 + totalIva10;
        decimal descuentoAplicado = Math.Clamp(descuento, 0m, subtotal);
        decimal totalVenta = subtotal - descuentoAplicado;

        decimal liqIva5 = Math.Round(totalIva5 / 21m, decimales, MidpointRounding.AwayFromZero);
        decimal liqIva10 = Math.Round(totalIva10 / 11m, decimales, MidpointRounding.AwayFromZero);
        decimal totalImpuesto = liqIva5 + liqIva10;

        decimal monGuarani = totalVenta;
        decimal monDolar = cotizacion is { VentaDolar: > 0 } ? Math.Round(totalVenta / cotizacion.VentaDolar, 2) : 0m;
        decimal monReal = cotizacion is { VentaReal: > 0 } ? Math.Round(totalVenta / cotizacion.VentaReal, 2) : 0m;
        decimal monPeso = cotizacion is { VentaPeso: > 0 } ? Math.Round(totalVenta / cotizacion.VentaPeso, 2) : 0m;
        decimal monEuro = cotizacion is { VentaEuro: > 0 } ? Math.Round(totalVenta / cotizacion.VentaEuro, 2) : 0m;

        return new SaleTotalsResult(
            totalExenta,
            totalIva5,
            totalIva10,
            subtotal,
            descuentoAplicado,
            totalVenta,
            liqIva5,
            liqIva10,
            totalImpuesto,
            monGuarani,
            monDolar,
            monReal,
            monPeso,
            monEuro);
    }
}
