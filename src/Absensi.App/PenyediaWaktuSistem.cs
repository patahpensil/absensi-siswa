using Absensi.Domain;

namespace Absensi.App;

// Satu-satunya tempat aplikasi membaca jam sistem sungguhan (CLAUDE.md §5:
// "Tidak ada DateTime.Now tersebar di mana-mana — pakai antarmuka
// IPenyediaWaktu"). Seluruh kode lain menerima waktu lewat parameter,
// bukan memanggil DateTime.Now sendiri.
public sealed class PenyediaWaktuSistem : IPenyediaWaktu
{
    public DateTime Sekarang => DateTime.Now;
}
