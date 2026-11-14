using System.Globalization;

namespace Absensi.Domain.Akun;

// Password rule set by the owner (P-35, 15 November 2026): at least 12
// characters with an upper-case letter, a lower-case letter, a digit and a
// symbol. This is a recorded exception to ASVS 5.0 6.2.5 (PRD §22). It is
// checked when a password is set (new account, reset, installation wizard),
// not at sign-in, so existing passwords keep working.
public static class PasswordPolicy
{
    public const int MinimumLength = 12;

    public const string Hint =
        "Minimal 12 karakter, wajib berisi huruf kapital, huruf kecil, angka, dan simbol (mis. ! @ # $ % _ - .).";

    public static IReadOnlyList<string> MissingRequirements(string? password)
    {
        password ??= "";
        var missing = new List<string>(5);
        if (new StringInfo(password).LengthInTextElements < MinimumLength)
        {
            missing.Add($"minimal {MinimumLength} karakter");
        }

        if (!password.Any(char.IsUpper))
        {
            missing.Add("huruf kapital");
        }

        if (!password.Any(char.IsLower))
        {
            missing.Add("huruf kecil");
        }

        if (!password.Any(char.IsDigit))
        {
            missing.Add("angka");
        }

        if (!password.Any(IsSymbol))
        {
            missing.Add("simbol");
        }

        return missing;
    }

    // Any visible character that is not a letter or a digit; a space is
    // allowed in a password but does not count as the symbol.
    private static bool IsSymbol(char c) => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && !char.IsControl(c);
}
