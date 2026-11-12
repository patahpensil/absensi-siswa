using Absensi.App.Gerbang;

namespace Absensi.App.Tests;

public sealed class SerialUidFramesTests
{
    [Fact]
    public void Read_SeparateReadersKeepPartialFramesAndLeadingZeros()
    {
        var first = new SerialUidFrames();
        var second = new SerialUidFrames();
        var accepted = new List<string>();
        first.Read("0000", accepted.Add);
        second.Read("ABCD1234\r\n", accepted.Add);
        first.Read("1234\r\n", accepted.Add);
        Assert.Equal(new[] { "ABCD1234", "00001234" }, accepted);
    }

    [Fact]
    public void Read_DiscardsShortInvalidAndOverlongFramesUntilDelimiter()
    {
        var reader = new SerialUidFrames();
        var accepted = new List<string>();
        reader.Read("DR\rV\nAB!123456\r" + new string('1', 65), accepted.Add);
        reader.Read("12345678\r\n00000009\n", accepted.Add);
        Assert.Equal(new[] { "00000009" }, accepted);
    }
}
