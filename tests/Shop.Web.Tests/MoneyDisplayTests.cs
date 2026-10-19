using System.Globalization;
using Shop.Web.Services;

namespace Shop.Web.Tests;

public class MoneyDisplayTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("ru-RU")]
    [InlineData("ru-BY")]
    public void CurrencyDoesNotDependOnBrowserCulture(string culture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal("1199,99 BYN", MoneyDisplay.Format(1199.99m));
            Assert.Equal("0,00 BYN", MoneyDisplay.Format(0m));
            Assert.Equal("1000000,01 BYN", MoneyDisplay.Format(1000000.01m));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }
}
