using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Waktu;

// RENCANA-TUGAS.md Tugas 9 — bagian "jadwal harian" dari layar Pengaturan
// (PRD §7, PANDUAN-UI-UX.md §9: "jadwal harian" ada di dalam Pengaturan,
// bukan menu sendiri). Setiap perubahan lewat JalurPerubahanData
// (CLAUDE.md §4.1 aturan 4).
public sealed class LayananJadwalHari
{
    private readonly RepositoriJadwalHari _repositoriJadwalHari = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    // PRD §7: "seluruh nilai dapat diubah per hari" — ketujuh baris
    // disimpan sekaligus, satu transaksi (dikonfirmasi pemilik proyek
    // 2026-09-18, pola sama seperti Registrasi Kartu Tugas 8 dan impor
    // Excel — CLAUDE.md §4.2 aturan 10).
    public void Perbarui(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int tahunAjaranId, IReadOnlyList<JadwalHariBaris> hari)
    {
        // PRD §3: pengaturan termasuk "kewenangan penuh" (Kepala
        // Sekolah/Admin) — wali kelas tidak disebutkan berwenang di sini.
        if (akun.Peran == PeranAkun.WaliKelas)
        {
            throw new InvalidOperationException("Wali kelas tidak berwenang mengubah jadwal harian (PRD §3).");
        }

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "perbarui_jadwal_hari",
            entitas: "jadwal_hari",
            perubahan: transaction =>
            {
                foreach (var baris in hari)
                {
                    _repositoriJadwalHari.Perbarui(connection, transaction, tahunAjaranId, baris);
                }

                return tahunAjaranId;
            });
    }
}
