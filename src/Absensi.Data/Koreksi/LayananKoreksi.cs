using Absensi.Data.Akun;
using Absensi.Data.Notifikasi;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Koreksi;

// RENCANA-TUGAS.md Tugas 11 — layar Koreksi (PRD §16.10). Wali kelas boleh
// mengoreksi kelasnya sendiri secara langsung (LayananAkun.BolehMenyentuhKelas,
// dibangun Tugas 4 untuk kebutuhan ini persis); admin/kepala sekolah boleh
// kelas mana pun, dan mengisi "atas permintaan" saat mengeksekusi atas nama
// wali kelas (PRD §3.2 "Delegasi Koreksi").
public sealed class LayananKoreksi
{
    private readonly RepositoriAbsensi _repositoriAbsensi = new();
    private readonly RepositoriKuncimBulan _repositoriKuncimBulan = new();
    private readonly LayananAkun _layananAkun = new();
    private readonly LayananNotifikasi _layananNotifikasi = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    public bool BulanTerkunci(SqliteConnection connection, DateOnly tanggal) =>
        _repositoriKuncimBulan.SudahDikunci(connection, tanggal.Year, tanggal.Month);

    public void KoreksiStatusSiswa(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        int kelasId,
        int siswaId,
        string namaSiswa,
        DateOnly tanggal,
        StatusAbsensiSiswa statusBaru,
        string? keterangan,
        string? atasPermintaan)
    {
        // PRD §10: "Koreksi terbuka selama bulan bersangkutan belum
        // dikunci" — diperiksa di lapisan layanan, bukan hanya di UI
        // (Tugas 11, "Selesai bila").
        if (BulanTerkunci(connection, tanggal))
        {
            throw new InvalidOperationException($"Bulan {tanggal:MM/yyyy} sudah terkunci — koreksi ditolak.");
        }

        // PRD §10/§16.10 langkah 4: alasan koreksi wajib, diperiksa di layanan.
        if (string.IsNullOrWhiteSpace(keterangan))
        {
            throw new InvalidOperationException("Alasan koreksi wajib diisi.");
        }

        if (!_layananAkun.BolehMenyentuhKelas(connection, akun, kelasId))
        {
            throw new InvalidOperationException("Anda tidak berwenang mengoreksi absensi kelas ini.");
        }

        var sebelum = _repositoriAbsensi.CariHariIniSiswa(connection, tanggal, siswaId)
            ?? throw new InvalidOperationException("Data absensi siswa pada tanggal ini tidak ditemukan.");
        var statusLama = sebelum.Status;

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "koreksi_status_siswa",
            entitas: "absensi_siswa",
            perubahan: transaction =>
            {
                _repositoriAbsensi.KoreksiStatusSiswa(connection, transaction, tanggal, siswaId, statusBaru, keterangan);
                return siswaId;
            },
            nilaiLama: PemetaanEnum.TeksDariStatusSiswa(statusLama),
            nilaiBaru: PemetaanEnum.TeksDariStatusSiswa(statusBaru),
            atasPermintaan: atasPermintaan);

        // PRD §16.10 langkah 7.2-7.3: tidak ada notifikasi bila statusnya
        // tidak benar-benar berubah.
        if (statusLama == statusBaru)
        {
            return;
        }

        _layananNotifikasi.AntrekanNotifikasiKoreksi(connection, tanggal, siswaId, namaSiswa, statusBaru, sebelum.JamMasuk, waktu);
    }
}
