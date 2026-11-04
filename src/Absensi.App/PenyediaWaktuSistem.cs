using Absensi.Domain;

namespace Absensi.App;

// Satu-satunya tempat aplikasi membaca jam sistem sungguhan (PRD §24.3:
// tidak menyebar DateTime.Now untuk keputusan bisnis — pakai antarmuka
// IPenyediaWaktu). Seluruh kode lain menerima waktu lewat parameter,
// bukan memanggil DateTime.Now sendiri.
public sealed class PenyediaWaktuSistem : IPenyediaWaktu
{
    public DateTime Sekarang => DateTime.Now;
}
