using System.Globalization;

namespace Shop.Web.Services;

/// <summary>Display only: does not change stored prices or monetary calculations.</summary>
public static class MoneyDisplay
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-BY");

    public static string Format(decimal amount) => $"{amount.ToString("F2", Culture)} BYN";
}
