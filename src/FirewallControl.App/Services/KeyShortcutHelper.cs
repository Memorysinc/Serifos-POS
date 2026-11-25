using System.Windows.Input;

namespace FirewallControl.App.Services;

public static class KeyShortcutHelper
{
    /// <summary>
    /// Converts a WPF KeyEventArgs into a normalized shortcut string such as "F1", "DEL", "A", "CTRL+X", "ALT+F".
    /// Returns empty string if only a modifier key (Ctrl, Shift, Alt) is being pressed by itself.
    /// </summary>
    public static string NormalizeKeyEvent(KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        return FormatKeyCombo(key, Keyboard.Modifiers);
    }

    /// <summary>
    /// Shared canonical key formatter used by both runtime shortcut dispatching and ConfiguracionPreferenciasWindow capture.
    /// </summary>
    public static string FormatKeyCombo(Key key, ModifierKeys mods)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin or Key.None or
            Key.Enter or Key.Return or Key.Tab or Key.Escape)
        {
            return string.Empty;
        }

        string baseKey = FormatBaseKey(key);
        if (string.IsNullOrEmpty(baseKey))
            return string.Empty;

        string prefix = string.Empty;
        if ((mods & ModifierKeys.Control) != 0)
            prefix += "CTRL+";
        if ((mods & ModifierKeys.Alt) != 0)
            prefix += "ALT+";
        if ((mods & ModifierKeys.Shift) != 0 && baseKey.Length > 1 && !baseKey.StartsWith("F"))
            prefix += "SHIFT+";

        return prefix + baseKey;
    }

    public static string FormatBaseKey(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
            return key.ToString().ToUpperInvariant();

        if (key >= Key.F1 && key <= Key.F12)
            return key.ToString().ToUpperInvariant();

        if (key >= Key.D0 && key <= Key.D9)
            return ((int)(key - Key.D0)).ToString();

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return ((int)(key - Key.NumPad0)).ToString();

        return key switch
        {
            Key.Delete => "DEL",
            Key.Back => "BACKSPACE",
            Key.Space => "ESPACIO",
            Key.Insert => "INS",
            Key.Home => "INICIO",
            Key.End => "FIN",
            Key.PageUp => "REPAG",
            Key.PageDown => "AVPAG",
            Key.Up => "UP",
            Key.Down => "DOWN",
            Key.Left => "LEFT",
            Key.Right => "RIGHT",
            Key.Add or Key.OemPlus => "+",
            Key.Subtract or Key.OemMinus => "-",
            Key.Multiply => "*",
            Key.Divide => "/",
            _ => string.Empty
        };
    }
}
