using Absensi.Domain.Notifikasi;

namespace Absensi.Domain.Tests;

// PRD P-20: WhatsApp uses the stored parent number directly, so the same
// number must normalise to one form and unusable values must be rejected.
public sealed class ParentPhoneNumberTests
{
    [Theory]
    [InlineData("081234567890", "6281234567890")]
    [InlineData("0812-3456-7890", "6281234567890")]
    [InlineData("0812 3456 7890", "6281234567890")]
    [InlineData("+6281234567890", "6281234567890")]
    [InlineData("6281234567890", "6281234567890")]
    [InlineData("+62 (812) 3456.7890", "6281234567890")]
    [InlineData("080000000001", "6280000000001")]
    public void Normalize_IndonesianNumber_ReturnsInternationalFormWithoutPlus(string input, string expected)
    {
        Assert.Equal(expected, ParentPhoneNumber.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0812")]
    [InlineData("08123456789012345")]
    [InlineData("+6581234567")]
    [InlineData("81234567890")]
    [InlineData("620812345678")]
    [InlineData("0812abc4567")]
    [InlineData("-")]
    public void Normalize_UnusableValue_ReturnsNull(string? input)
    {
        Assert.Null(ParentPhoneNumber.Normalize(input));
    }
}
