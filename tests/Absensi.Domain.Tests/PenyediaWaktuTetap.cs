using Absensi.Domain;

namespace Absensi.Domain.Tests;

// Implementasi IPenyediaWaktu untuk pengujian: waktu tidak pernah diambil
// dari DateTime.Now, selalu dari nilai tetap yang ditentukan pengujian.
internal sealed class PenyediaWaktuTetap : IPenyediaWaktu
{
    public PenyediaWaktuTetap(DateTime sekarang) => Sekarang = sekarang;

    public DateTime Sekarang { get; }
}
