using System;
using System.Collections.Generic;
using System.Linq;
using FirewallControl.Core.Entities;

namespace FirewallControl.Core.Business;

/// <summary>
/// Evaluates Visual FoxPro 9 `grupos.caccesos` permission matrices (8 prompt lines: L1..L8 formatted as `P.B1B2...`),
/// `grupos.btn*` toolbar visibility flags, and `usuarios.tipovendedor` toolbar restrictions.
/// </summary>
public static class VfpPermissionHelper
{
    public static bool IsAdmin(UserEntity? user, UserGroupEntity? group)
    {
        string grp = (group?.Cidgrupo ?? string.Empty).Trim();
        string usr = (user?.Cidusuario ?? string.Empty).Trim();
        return string.Equals(grp, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(usr, "ADMINISTRA", StringComparison.OrdinalIgnoreCase) ||
               string.IsNullOrWhiteSpace(grp);
    }

    public static bool IsVendorToolbarRestricted(UserEntity? user, UserGroupEntity? group)
    {
        if (IsAdmin(user, group) || user == null)
            return false;

        if (user.TipoVendedor == 2)
            return true;

        string grp = (group?.Cidgrupo ?? user.Cidgrupo ?? string.Empty).Trim();
        return user.TipoVendedor == 1 &&
               (string.Equals(grp, "VENDEDORES", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grp, "VENDEDOR", StringComparison.OrdinalIgnoreCase));
    }

    public static string[] SplitAccessLines(string? caccesos)
    {
        if (string.IsNullOrWhiteSpace(caccesos))
            return Array.Empty<string>();

        return caccesos
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .ToArray();
    }

    /// <summary>
    /// Evaluates whether a top-level prompt (1-based Referencia = promptOrder) is allowed.
    /// </summary>
    public static bool IsMenuPromptAllowed(UserEntity? user, UserGroupEntity? group, int promptOrder1Based)
    {
        if (IsAdmin(user, group))
            return true;

        var lines = SplitAccessLines(group?.Caccesos);
        if (lines.Length == 0 || promptOrder1Based < 1 || promptOrder1Based > lines.Length)
            return true;

        string line = lines[promptOrder1Based - 1];
        if (line.Length == 0)
            return true;

        return line[0] != '0' && line[0] != 'F' && line[0] != 'N';
    }

    /// <summary>
    /// Evaluates whether a menu bar item (1-based ParentNu within Prompt promptOrder1Based) is allowed.
    /// Matches VFP `SUBSTR(lamenus(i), j + 2, 1)` where string is `P.B1B2...`.
    /// </summary>
    public static bool IsMenuBarAllowed(UserEntity? user, UserGroupEntity? group, int promptOrder1Based, int barOrder1Based)
    {
        if (IsAdmin(user, group))
            return true;

        if (!IsMenuPromptAllowed(user, group, promptOrder1Based))
            return false;

        var lines = SplitAccessLines(group?.Caccesos);
        if (lines.Length == 0 || promptOrder1Based < 1 || promptOrder1Based > lines.Length)
            return true;

        string line = lines[promptOrder1Based - 1];
        int charIndex = barOrder1Based + 1; // index 0 = Prompt, index 1 = '.', index 2 = B1 (barOrder=1)
        if (barOrder1Based < 1 || charIndex >= line.Length)
            return true;

        char ch = line[charIndex];
        return ch != '0' && ch != 'F' && ch != 'N';
    }

    public static string BuildDefaultAccessMatrix(UserGroupEntity group)
    {
        char v = group.BtnVenta == 1 ? '1' : '0';
        char c = group.BtnCompra == 1 ? '1' : '0';
        char p = group.BtnProducto == 1 ? '1' : '0';
        char cl = group.BtnCliente == 1 ? '1' : '0';
        char pr = group.BtnProveedor == 1 ? '1' : '0';
        char cj = group.BtnMovCaja == 1 ? '1' : '0';
        char av = group.Avisos == 1 ? '1' : '0';

        return string.Join(Environment.NewLine, new[]
        {
            $"1.111111111",                 // L1 Archivo
            $"1.111111",                    // L2 Personales
            $"1.{cl}{p}111{pr}1111111",     // L3 Catalogos
            $"1.{v}{c}{c}1{v}111{cj}11",    // L4 Procesos
            $"1.11111111",                  // L5 Veterinaria
            $"1.{av}{av}{av}",              // L6 Informes
            $"1.1111111111",                // L7 Utilierias
            $"1.1"                          // L8 Ayuda
        });
    }
}
