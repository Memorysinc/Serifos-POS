using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FirewallControl.App.Views;

/// <summary>
/// Auxiliary calculator dialog replicating calculador.scx / VtAttachCalculador.
/// </summary>
public partial class CalculatorDialog : Window
{
    private readonly Action<decimal>? _onAcceptValue;
    private decimal _accumulator;
    private decimal _memory;
    private string _pendingOp = string.Empty;
    private bool _resetOnNextDigit = true;

    public decimal CurrentValue { get; private set; }

    public CalculatorDialog(Action<decimal>? onAcceptValue = null)
    {
        InitializeComponent();
        _onAcceptValue = onAcceptValue;
        if (_onAcceptValue != null)
            BtnUseValue.Visibility = Visibility.Visible;
    }

    private decimal ParseDisplay()
        => decimal.TryParse(TxtDisplay.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : 0m;

    private void SetDisplay(decimal value)
    {
        CurrentValue = value;
        TxtDisplay.Text = value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private void AppendDigit(string digit)
    {
        if (_resetOnNextDigit || TxtDisplay.Text == "0")
        {
            TxtDisplay.Text = digit;
            _resetOnNextDigit = false;
        }
        else
        {
            TxtDisplay.Text += digit;
        }
        CurrentValue = ParseDisplay();
    }

    private void AppendDot()
    {
        if (_resetOnNextDigit)
        {
            TxtDisplay.Text = "0.";
            _resetOnNextDigit = false;
        }
        else if (!TxtDisplay.Text.Contains('.'))
        {
            TxtDisplay.Text += ".";
        }
    }

    private void ApplyOperator(string op)
    {
        if (!_resetOnNextDigit && !string.IsNullOrEmpty(_pendingOp))
        {
            Evaluate();
        }
        else
        {
            _accumulator = ParseDisplay();
        }

        _pendingOp = op;
        LblExpression.Text = $"{_accumulator.ToString("0.######", CultureInfo.InvariantCulture)} {op}";
        _resetOnNextDigit = true;
    }

    private void Evaluate()
    {
        decimal rhs = ParseDisplay();
        decimal result = _pendingOp switch
        {
            "+" => _accumulator + rhs,
            "-" => _accumulator - rhs,
            "*" => _accumulator * rhs,
            "/" => rhs != 0m ? _accumulator / rhs : 0m,
            _ => rhs
        };

        LblExpression.Text = string.Empty;
        _pendingOp = string.Empty;
        _accumulator = result;
        SetDisplay(result);
        _resetOnNextDigit = true;
    }

    private void BtnDigit_Click(object sender, RoutedEventArgs e)
        => AppendDigit(((Button)sender).Content?.ToString() ?? "0");

    private void BtnDot_Click(object sender, RoutedEventArgs e) => AppendDot();

    private void BtnOp_Click(object sender, RoutedEventArgs e)
        => ApplyOperator(((Button)sender).Content?.ToString() ?? "+");

    private void BtnEquals_Click(object sender, RoutedEventArgs e) => Evaluate();

    private void BtnClearEntry_Click(object sender, RoutedEventArgs e)
    {
        SetDisplay(0m);
        _resetOnNextDigit = true;
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        _accumulator = 0m;
        _pendingOp = string.Empty;
        LblExpression.Text = string.Empty;
        SetDisplay(0m);
        _resetOnNextDigit = true;
    }

    private void BtnBackspace_Click(object sender, RoutedEventArgs e)
    {
        if (_resetOnNextDigit || TxtDisplay.Text.Length <= 1)
        {
            SetDisplay(0m);
            _resetOnNextDigit = true;
            return;
        }
        TxtDisplay.Text = TxtDisplay.Text[..^1];
        CurrentValue = ParseDisplay();
    }

    private void BtnSqrt_Click(object sender, RoutedEventArgs e)
    {
        decimal v = ParseDisplay();
        if (v >= 0m)
        {
            SetDisplay((decimal)Math.Sqrt((double)v));
            _resetOnNextDigit = true;
        }
    }

    private void BtnPercent_Click(object sender, RoutedEventArgs e)
    {
        decimal v = ParseDisplay();
        decimal result = _accumulator != 0m ? _accumulator * v / 100m : v / 100m;
        SetDisplay(result);
        _resetOnNextDigit = true;
    }

    private void BtnMem_Click(object sender, RoutedEventArgs e)
    {
        string cmd = ((Button)sender).Content?.ToString() ?? "";
        switch (cmd)
        {
            case "MC": _memory = 0m; break;
            case "MR": SetDisplay(_memory); _resetOnNextDigit = true; break;
            case "M+": _memory += ParseDisplay(); _resetOnNextDigit = true; break;
            case "M-": _memory -= ParseDisplay(); _resetOnNextDigit = true; break;
        }
        LblMemIndicator.Text = _memory != 0m ? $"M = {_memory:N2}" : string.Empty;
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(TxtDisplay.Text);
        }
        catch
        {
            // Ignore clipboard lock errors
        }
    }

    private void BtnUse_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_pendingOp)) Evaluate();
        _onAcceptValue?.Invoke(ParseDisplay());
        DialogResult = true;
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key >= Key.D0 && e.Key <= Key.D9)
        {
            e.Handled = true;
            AppendDigit(((int)(e.Key - Key.D0)).ToString());
        }
        else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
        {
            e.Handled = true;
            AppendDigit(((int)(e.Key - Key.NumPad0)).ToString());
        }
        else if (e.Key is Key.Decimal or Key.OemPeriod or Key.OemComma)
        {
            e.Handled = true;
            AppendDot();
        }
        else if (e.Key is Key.Add or Key.OemPlus)
        {
            e.Handled = true;
            ApplyOperator("+");
        }
        else if (e.Key is Key.Subtract or Key.OemMinus)
        {
            e.Handled = true;
            ApplyOperator("-");
        }
        else if (e.Key == Key.Multiply)
        {
            e.Handled = true;
            ApplyOperator("*");
        }
        else if (e.Key == Key.Divide)
        {
            e.Handled = true;
            ApplyOperator("/");
        }
        else if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            Evaluate();
        }
        else if (e.Key == Key.Back)
        {
            e.Handled = true;
            BtnBackspace_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            BtnClearAll_Click(this, new RoutedEventArgs());
        }
    }
}
