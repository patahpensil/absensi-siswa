using System.Globalization;

namespace Absensi.Data.Repositori;

// Info satu bulan terkunci — dipakai LayananKalender/LayananJadwalHari
// untuk menolak perubahan dengan pesan yang menyebut bulan mana yang
// mengunci, kapan, dan oleh siapa (bukan hanya "sudah terkunci").
// DikunciOleh sudah nama tampil (guru.nama bila akun.guru_id terisi,
// kalau tidak akun.username — Kepala Sekolah/Admin biasanya tidak punya
// baris guru) — diselesaikan di RepositoriKuncimBulan, bukan di sini.
public sealed record KuncimBulanInfo(int Tahun, int Bulan, string DikunciOleh, DateTime DikunciPada)
{
    private static readonly CultureInfo BudayaIndonesia = new("id-ID");

    public string PesanGalat =>
        $"Tidak bisa mengubah. Bulan {new DateOnly(Tahun, Bulan, 1).ToString("MMMM yyyy", BudayaIndonesia)} " +
        $"sudah dikunci pada {DikunciPada.ToString("d MMMM", BudayaIndonesia)} oleh {DikunciOleh}.";
}
