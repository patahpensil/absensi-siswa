namespace Absensi.Domain.Notifikasi;

// PRD P-20: the WhatsApp channel uses the parent phone number stored on the
// student record directly, without a confirmation message. Numbers are
// normalised to the international form without '+' (e.g. 6281234567890) so
// the same parent is not queued under two spellings. Only Indonesian numbers
// are accepted; anything else is treated as "no usable number" and no
// WhatsApp message is created, rather than guessing a destination.
public static class ParentPhoneNumber
{
    private const int MinimumDigitsAfterCountryCode = 8;
    private const int MaximumDigitsAfterCountryCode = 13;

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        var hasPlus = trimmed.StartsWith('+');
        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());

        // Only spaces, dashes, dots and brackets may separate digits; any
        // other character means the value is not a phone number.
        if (trimmed.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '-' or '.' or '(' or ')' or '+')))
        {
            return null;
        }

        string national;
        if (digits.StartsWith("62", StringComparison.Ordinal))
        {
            national = digits[2..];
        }
        else if (!hasPlus && digits.StartsWith('0'))
        {
            national = digits[1..];
        }
        else
        {
            return null;
        }

        if (national.StartsWith('0')
            || national.Length < MinimumDigitsAfterCountryCode
            || national.Length > MaximumDigitsAfterCountryCode)
        {
            return null;
        }

        return "62" + national;
    }
}
