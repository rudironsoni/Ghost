namespace Ghost.Utilities;

public static class SalaryFormatter
{
    public static string FormatCurrency(decimal amount, string currency = "USD")
    {
        try
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1:N2}", currency, amount);
        }
        catch
        {
            return amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
