using Absensi.Domain;

namespace Absensi.Telegram.Tests;

internal sealed class PenyediaWaktuTetap : IPenyediaWaktu
{
    public PenyediaWaktuTetap(DateTime sekarang) => Sekarang = sekarang;

    public DateTime Sekarang { get; }
}
