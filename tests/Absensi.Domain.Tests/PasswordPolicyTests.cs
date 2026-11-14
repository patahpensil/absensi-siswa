using Absensi.Domain.Akun;

namespace Absensi.Domain.Tests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("Sekolah#2026")]
    [InlineData("Kopi pagi 7!x")]
    [InlineData("Ägyptisch_99a")]
    public void ValidPasswords_HaveNoMissingRequirement(string password) =>
        Assert.Empty(PasswordPolicy.MissingRequirements(password));

    [Theory]
    [InlineData("Sekolah#202", "minimal 12 karakter")]
    [InlineData("sekolah#2026", "huruf kapital")]
    [InlineData("SEKOLAH#2026", "huruf kecil")]
    [InlineData("Sekolah#Pagi", "angka")]
    [InlineData("Sekolah 20261", "simbol")]
    public void EachMissingRequirement_IsNamed(string password, string expected) =>
        Assert.Equal(new[] { expected }, PasswordPolicy.MissingRequirements(password));

    [Fact]
    public void NullOrEmpty_ListsEveryRequirement()
    {
        var all = new[] { "minimal 12 karakter", "huruf kapital", "huruf kecil", "angka", "simbol" };
        Assert.Equal(all, PasswordPolicy.MissingRequirements(null));
        Assert.Equal(all, PasswordPolicy.MissingRequirements(""));
    }

    [Fact]
    public void Length_CountsVisibleCharacters_NotUtf16Units()
    {
        // 11 visible characters, one of them an emoji made of two UTF-16 units.
        Assert.Contains("minimal 12 karakter", PasswordPolicy.MissingRequirements("Sekolah#20\U0001F600"));
    }
}
