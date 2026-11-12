using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Koreksi;

// RENCANA-TUGAS.md Tugas 11 — layar Guru→Kehadiran (PRD §16.17). PRD §17.10
// membatasi ini ke Kepala Sekolah SAJA — beda dari pola "kewenangan penuh"
// biasa (Admin/TU tidak berwenang di sini, padahal berwenang di hampir
// semua tempat lain). Tidak ada notifikasi di sini — PRD §16.17 tidak
// menyebutkannya (beda dari koreksi siswa, PRD §16.10 langkah 7.2-7.3).
public sealed class LayananStatusGuru
{
    private readonly RepositoriAbsensi _repositoriAbsensi = new();
    private readonly RepositoriKuncimBulan _repositoriKuncimBulan = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    public bool BulanTerkunci(SqliteConnection connection, DateOnly tanggal) =>
        _repositoriKuncimBulan.SudahDikunci(connection, tanggal.Year, tanggal.Month);

    // Satu jalur untuk DUA ARAH PRD §16.17: DI MUKA (tanggal mendatang,
    // belum ada baris) dan SETELAH (tanggal sudah punya baris) —
    // RepositoriAbsensi.TetapkanStatusGuru adalah upsert, jadi pemanggil
    // tidak perlu tahu bedanya.
    public void TetapkanStatus(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        int guruId,
        DateOnly tanggal,
        StatusAbsensiGuru status,
        string? keterangan)
    {
        if (akun.Peran != PeranAkun.KepalaSekolah)
        {
            throw new InvalidOperationException("Hanya kepala sekolah yang berwenang menetapkan status kehadiran guru (PRD §17.10).");
        }

        if (BulanTerkunci(connection, tanggal))
        {
            throw new InvalidOperationException($"Bulan {tanggal:MM/yyyy} sudah terkunci.");
        }

        // PRD §16.17 langkah 4: keterangan wajib, diperiksa di layanan.
        if (string.IsNullOrWhiteSpace(keterangan))
        {
            throw new InvalidOperationException("Keterangan status guru wajib diisi.");
        }

        var note = keterangan.Trim();

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "tetapkan_status_guru",
            entitas: "absensi_guru",
            perubahan: transaction =>
            {
                if (_repositoriKuncimBulan.SudahDikunci(connection, tanggal.Year, tanggal.Month, transaction))
                    throw new InvalidOperationException($"Bulan {tanggal:MM/yyyy} sudah terkunci.");

                var before = _repositoriAbsensi.CariHariIniGuru(connection, tanggal, guruId, transaction);
                _repositoriAbsensi.TetapkanStatusGuru(connection, transaction, tanggal, guruId, status, keterangan);
                LocalAttendanceClosure.InvalidateDay(connection, transaction, tanggal);
                // Audit memuat tanggal dan keterangan (PRD §16.17 langkah 5).
                return new CatatanPerubahan(guruId,
                    before is null ? $"tanggal={tanggal:yyyy-MM-dd}; status=(belum ada baris)" : $"tanggal={tanggal:yyyy-MM-dd}; status={PemetaanEnum.TeksDariStatusGuru(before.Status)}",
                    $"tanggal={tanggal:yyyy-MM-dd}; status={PemetaanEnum.TeksDariStatusGuru(status)}; keterangan={note}");
            });
    }
}
