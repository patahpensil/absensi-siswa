using Absensi.Domain;

namespace Absensi.App.Tests;

internal sealed class PenyediaWaktuUji : IPenyediaWaktu
{
    public PenyediaWaktuUji(DateTime sekarang) => Sekarang = sekarang;

    public DateTime Sekarang { get; set; }
}
