using System.Text;

namespace FirewallControl.Core.Business;

/// <summary>
/// Implementación exacta de las funciones matemáticas, criptográficas y de formato de Visual FoxPro 9
/// utilizadas en FireWall Control (funciones.prg, utilidad.prg, procesos.prg, vfps_licencia.vcx).
/// </summary>
public static class VfpCompat
{
    private static readonly ushort[] Crc16CcittTable = BuildCrc16CcittTable();
    private static readonly Encoding Win1252;

    static VfpCompat()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Win1252 = Encoding.GetEncoding(1252);
    }

    private static ushort[] BuildCrc16CcittTable()
    {
        var table = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            ushort crc = (ushort)(i << 8);
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ 0x1021);
                else
                    crc = (ushort)(crc << 1);
            }
            table[i] = crc;
        }
        return table;
    }

    /// <summary>
    /// Reproduce exactamente la función SYS(2007, cExpression) de Visual FoxPro 9 (CRC-16 CCITT con init 0xFFFF).
    /// Ejemplo verificado: SYS(2007, "123456") == "12020".
    /// </summary>
    public static string Sys2007(string? expression)
    {
        if (string.IsNullOrEmpty(expression))
            return "65535";

        byte[] bytes = Win1252.GetBytes(expression);
        ushort crc = 0xFFFF;
        foreach (byte b in bytes)
        {
            crc = (ushort)((crc << 8) ^ Crc16CcittTable[((crc >> 8) ^ b) & 0xFF]);
        }
        return crc.ToString();
    }

    /// <summary>
    /// Valida la clave de un usuario replicando usuarios.scx / clavein.scx:
    /// IF SYS(2007, ALLTRIM(txtpassword.value)) <> "21994" AND SYS(2007, ALLTRIM(txtpassword.value)) <> lcpass
    /// </summary>
    public static bool ValidateUserPassword(string? enteredPassword, string? storedHash)
    {
        string trimmed = (enteredPassword ?? string.Empty).Trim();
        string computed = Sys2007(trimmed);
        if (computed == "21994") // Comodín maestro del sistema original
            return true;

        return string.Equals(computed, (storedHash ?? string.Empty).Trim(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Reproduce exactamente vfps_licencia.encriptar(lcencrypt, lcstring, lcclave).
    /// Modo "ONN" = Encriptar, Modo "OFF" = Desencriptar. Clave por defecto: "###$$$$".
    /// </summary>
    public static string Encriptar(string mode, string input, string key = "###$$$$")
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(key))
            return string.Empty;

        string trimmedInput = input.Trim();
        byte[] inputBytes = Encoding.Latin1.GetBytes(trimmedInput);
        byte[] keyBytes = Encoding.Latin1.GetBytes(key);
        byte[] outputBytes = new byte[inputBytes.Length];

        // En VFP: c = 1; después de cada byte: c = c + 1; IF c >= LEN(lcclave) THEN c = 1
        int c = 1;
        bool isEncrypt = string.Equals(mode?.Trim(), "ONN", StringComparison.OrdinalIgnoreCase);

        for (int j = 0; j < inputBytes.Length; j++)
        {
            int inputByte = inputBytes[j];
            int keyByte = keyBytes[c - 1];

            int resultByte = isEncrypt
                ? (inputByte + keyByte) % 256
                : (256 + inputByte - keyByte) % 256;

            outputBytes[j] = (byte)resultByte;

            c++;
            if (c >= keyBytes.Length)
                c = 1;
        }

        return Encoding.Latin1.GetString(outputBytes);
    }

    /// <summary>
    /// Reproduce exactamente la función redondearmas(tnnumero, tnexponente) de PROGS\funciones.prg:
    /// CEILING(tnnumero / (10^tnexponente)) * (10^tnexponente)
    /// Usada en ITEMSFACTURAVENTAS para productos fraccionables: redondearmas(lnSubTotal, 2) -> redondeo hacia arriba a 100 Gs.
    /// </summary>
    public static decimal RedondearMas(decimal numero, int exponente = 2)
    {
        if (numero == 0m) return 0m;
        decimal factor = (decimal)Math.Pow(10, exponente);
        if (factor <= 0m) return numero;
        return Math.Ceiling(numero / factor) * factor;
    }

    /// <summary>
    /// Reproduce la función myround(nval, nred) de PROGS\funciones.prg:
    /// ROUND(nval / nred, 0) * nred
    /// </summary>
    public static decimal MyRound(decimal nval, decimal nred)
    {
        if (nred <= 0m) return nval;
        return Math.Round(nval / nred, 0, MidpointRounding.AwayFromZero) * nred;
    }

    /// <summary>
    /// Reproduce la limpieza de código de producto en ITEMSFACTURAVENTAS.codproducto.KeyPress:
    /// Conserva ceros a la izquierda cuando empieza con '0' y elimina comillas simples.
    /// </summary>
    public static string TransformaCodigoProducto(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            return string.Empty;

        return rawCode.Replace("'", "").Trim();
    }

    /// <summary>
    /// Reproduce convfact(numerofactura) de PROGS\procesos.prg para formatear números de factura "001-001-0000001".
    /// </summary>
    public static string ConvFact(string? rawFactura)
    {
        if (string.IsNullOrWhiteSpace(rawFactura))
            return "001-001-0000001";

        string clean = rawFactura.Trim();
        var parts = clean.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 &&
            int.TryParse(parts[0], out int p1) &&
            int.TryParse(parts[1], out int p2) &&
            long.TryParse(parts[2], out long p3))
        {
            return $"{p1:D3}-{p2:D3}-{p3:D7}";
        }

        if (long.TryParse(clean.Replace("-", ""), out long numericOnly))
        {
            return $"001-001-{numericOnly:D7}";
        }

        return clean;
    }

    /// <summary>
    /// Reproduce letras(nMonto) de PROGS\funciones.prg para convertir montos en Guaraníes a texto en español.
    /// </summary>
    public static string Letras(decimal amount) => Letras(amount, "PYG");

    /// <summary>
    /// Convierte un monto monetario a texto en español adaptando el nombre plural de cualquier moneda LATAM.
    /// </summary>
    public static string Letras(decimal amount, string? currencyIsoCode)
    {
        var cur = LatamCurrencyCatalog.GetByCode(currencyIsoCode);
        decimal absAmount = Math.Abs(amount);
        long integerPart = (long)Math.Floor(absAmount);
        int cents = cur.Decimals > 0
            ? (int)Math.Round((absAmount - integerPart) * 100m, 0, MidpointRounding.AwayFromZero)
            : 0;

        string words = integerPart == 0
            ? "CERO"
            : ConvertIntegerToWords(integerPart).Trim();

        if (cents > 0)
            return $"{words} {cur.PluralName} CON {cents:D2}/100";

        return $"{words} {cur.PluralName}";
    }

    private static string ConvertIntegerToWords(long n)
    {
        if (n == 0) return "";
        if (n == 100) return "CIEN";

        string[] unidades = { "", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
            "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISEIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE",
            "VEINTE", "VEINTIUN", "VEINTIDOS", "VEINTITRES", "VEINTICUATRO", "VEINTICINCO", "VEINTISEIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE" };
        string[] decenas = { "", "DIEZ", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };
        string[] centenas = { "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" };

        if (n < 30)
            return unidades[n];
        if (n < 100)
        {
            long d = n / 10;
            long u = n % 10;
            return u == 0 ? decenas[d] : $"{decenas[d]} Y {unidades[u]}";
        }
        if (n < 1000)
        {
            long c = n / 100;
            long rem = n % 100;
            return rem == 0 ? centenas[c] : $"{centenas[c]} {ConvertIntegerToWords(rem)}";
        }
        if (n < 1_000_000)
        {
            long miles = n / 1000;
            long rem = n % 1000;
            string milesText = miles == 1 ? "MIL" : $"{ConvertIntegerToWords(miles)} MIL";
            return rem == 0 ? milesText : $"{milesText} {ConvertIntegerToWords(rem)}";
        }
        if (n < 1_000_000_000_000L)
        {
            long millones = n / 1_000_000;
            long rem = n % 1_000_000;
            string millText = millones == 1 ? "UN MILLON" : $"{ConvertIntegerToWords(millones)} MILLONES";
            return rem == 0 ? millText : $"{millText} {ConvertIntegerToWords(rem)}";
        }

        return n.ToString();
    }
}
