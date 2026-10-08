using System.Globalization;

namespace OrgChart.Razor.Admin;

/// <summary>Persian digits for display, Latin digits for parsing.</summary>
public static class OrgFormat
{
    private const string PersianDigits = "۰۱۲۳۴۵۶۷۸۹";

    public static string Digits(string? text) => string.Create(text?.Length ?? 0, text ?? "", static (span, source) =>
    {
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            span[i] = c is >= '0' and <= '9' ? PersianDigits[c - '0'] : c;
        }
    });

    public static string Number(int value) => Digits(value.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '٬'));

    public static string LatinDigits(string text) => string.Create(text.Length, text, static (span, source) =>
    {
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            span[i] = c switch
            {
                >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
                >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
                _ => c,
            };
        }
    });
}
