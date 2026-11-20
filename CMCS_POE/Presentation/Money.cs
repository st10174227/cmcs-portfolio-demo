using System.Globalization;
namespace CMCS_POE.Presentation;
public static class Money
{
    public static string Zar(decimal value) => "R " + value.ToString("N2", CultureInfo.InvariantCulture);
}
