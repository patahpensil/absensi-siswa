using System.Globalization;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using Absensi.Domain.Notifikasi;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Notifikasi;

// RENCANA-TUGAS.md Tugas 10 — seluruh titik pengiriman batch di PRD §9.2
// dan §9.3 (yang real-time, tap Terlambat setelah jam rekap, ditangani
// langsung di Absensi.Data.PemrosesTap — lihat catatan di sana). Dipanggil
// oleh penjadwal (Absensi.App) pada jam yang dikonfigurasi jadwal_hari,
// bukan mengirim sendiri (CLAUDE.md §4.4 aturan 15) — hanya mengantre ke
// `outbox`, lewat RepositoriOutbox.Antrekan yang aman dipanggil berulang.
public sealed class LayananNotifikasi
{
    private static readonly CultureInfo BudayaIndonesia = new("id-ID");

    private readonly RepositoriAbsensi _repositoriAbsensi = new();
    private readonly RepositoriPairing _repositoriPairing = new();
    private readonly RepositoriKelas _repositoriKelas = new();
    private readonly RepositoriOutbox _repositoriOutbox = new();
    private readonly PenentuStatusKehadiran _penentuStatusKehadiran = new();

    // PRD §16.7 08.00 + §9.3 baris 1: rekap Hadir/Terlambat untuk siswa
    // yang sudah tap sebelum jam rekap, plus daftar "belum tap" per kelas
    // ke wali kelas (dikonfirmasi pemilik proyek 2026-09-18: Terlambat
    // SEBELUM jam rekap ikut batch ini, bukan real-time).
    public void JalankanRekapPagi(SqliteConnection connection, DateOnly tanggal, DateTime waktu)
    {
        var daftarSiswa = _repositoriAbsensi.DaftarSiswaUntukNotifikasi(connection, tanggal);

        foreach (var siswa in daftarSiswa)
        {
            if (siswa.Status is StatusAbsensiSiswa.Hadir or StatusAbsensiSiswa.Terlambat)
            {
                AntrekanNotifikasiOrangTua(connection, tanggal, siswa, waktu);
            }
        }

        foreach (var kelompok in daftarSiswa.Where(s => s.Status == StatusAbsensiSiswa.Alpa).GroupBy(s => (s.KelasId, s.NamaKelas)))
        {
            var namaSiswa = kelompok.Select(s => s.NamaSiswa).ToList();
            var isi = $"Rekap pagi kelas {kelompok.Key.NamaKelas} ({FormatTanggal(tanggal)}): {namaSiswa.Count} siswa belum tap — {string.Join(", ", namaSiswa)}.";
            AntrekanPesanWaliKelas(connection, tanggal, kelompok.Key.KelasId, JenisNotifikasi.RekapWaliBelumTap, isi, waktu);
        }
    }

    // PRD §16.7 12.00/Jumat 10.00 (batas akhir tap masuk) + §17.6: siswa
    // masih Alpa → notifikasi tidak hadir tanpa keterangan ke orang tua;
    // guru yang sama sekali belum tap dan tidak berstatus di muka →
    // pengingat ke akun Telegram guru itu sendiri.
    public void JalankanBatasAkhirTapMasuk(SqliteConnection connection, DateOnly tanggal, DateTime waktu)
    {
        foreach (var siswa in _repositoriAbsensi.DaftarSiswaUntukNotifikasi(connection, tanggal))
        {
            if (siswa.Status == StatusAbsensiSiswa.Alpa)
            {
                AntrekanNotifikasiOrangTua(connection, tanggal, siswa, waktu);
            }
        }

        foreach (var guru in _repositoriAbsensi.DaftarGuruUntukNotifikasi(connection, tanggal))
        {
            if (guru.Status != StatusAbsensiGuru.Alpa)
            {
                continue;
            }

            var chatId = _repositoriPairing.CariChatIdAktif(connection, JenisPemilikKartu.Guru, guru.GuruId);
            if (chatId is null)
            {
                continue;
            }

            var isi = $"Anda belum tercatat tap hari ini, {FormatTanggal(tanggal)}. Mohon segera konfirmasi ke Kepala Sekolah bila berhalangan.";
            _repositoriOutbox.Antrekan(connection, null, tanggal, JenisSubjekOutbox.Guru, guru.GuruId, JenisNotifikasi.GuruTanpaKabar, chatId, isi, waktu);
        }
    }

    // PRD §16.7 14.10/Jumat 12.30: siswa tercatat masuk tapi belum tap
    // pulang ditetapkan Bolos, satu pesan per kelas ke wali kelas.
    public void JalankanFinalisasiBolos(SqliteConnection connection, DateOnly tanggal, DateTime waktu)
    {
        var bolosPerKelas = new Dictionary<(int KelasId, string NamaKelas), List<string>>();

        foreach (var siswa in _repositoriAbsensi.DaftarSiswaUntukNotifikasi(connection, tanggal))
        {
            var statusAkhir = _penentuStatusKehadiran.TentukanStatusAkhirHariSiswa(siswa.Status, siswa.JamPulang is not null);
            if (statusAkhir != StatusAbsensiSiswa.Bolos)
            {
                continue;
            }

            using (var transaction = connection.BeginTransaction())
            {
                _repositoriAbsensi.TandaiBolos(connection, transaction, tanggal, siswa.SiswaId);
                transaction.Commit();
            }

            var kelasKey = (siswa.KelasId, siswa.NamaKelas);
            if (!bolosPerKelas.TryGetValue(kelasKey, out var daftarNama))
            {
                daftarNama = new List<string>();
                bolosPerKelas[kelasKey] = daftarNama;
            }

            daftarNama.Add(siswa.NamaSiswa);
        }

        foreach (var (kelas, namaSiswa) in bolosPerKelas)
        {
            var isi = $"Siswa berstatus BOLOS di kelas {kelas.NamaKelas} hari ini ({FormatTanggal(tanggal)}): {string.Join(", ", namaSiswa)}.";
            AntrekanPesanWaliKelas(connection, tanggal, kelas.KelasId, JenisNotifikasi.BolosWali, isi, waktu);
        }
    }

    private void AntrekanNotifikasiOrangTua(SqliteConnection connection, DateOnly tanggal, AbsensiSiswaUntukNotifikasi siswa, DateTime waktu)
    {
        var chatId = _repositoriPairing.CariChatIdAktif(connection, JenisPemilikKartu.Siswa, siswa.SiswaId);
        if (chatId is null)
        {
            return;
        }

        (JenisNotifikasi Jenis, string Isi)? pesan = siswa.Status switch
        {
            StatusAbsensiSiswa.Hadir => (JenisNotifikasi.Hadir,
                $"Ananda {siswa.NamaSiswa} tercatat HADIR hari ini, {FormatTanggal(tanggal)} pukul {siswa.JamMasuk?.ToString("HH:mm")}."),
            StatusAbsensiSiswa.Terlambat => (JenisNotifikasi.Terlambat,
                $"Ananda {siswa.NamaSiswa} tercatat TERLAMBAT hari ini, {FormatTanggal(tanggal)} pukul {siswa.JamMasuk?.ToString("HH:mm")}."),
            StatusAbsensiSiswa.Alpa => (JenisNotifikasi.Alpa,
                $"Ananda {siswa.NamaSiswa} belum tercatat hadir tanpa keterangan hari ini, {FormatTanggal(tanggal)}."),
            _ => null,
        };

        if (pesan is not { } p)
        {
            return;
        }

        _repositoriOutbox.Antrekan(connection, null, tanggal, JenisSubjekOutbox.Siswa, siswa.SiswaId, p.Jenis, chatId, p.Isi, waktu);
    }

    private void AntrekanPesanWaliKelas(SqliteConnection connection, DateOnly tanggal, int kelasId, JenisNotifikasi jenis, string isi, DateTime waktu)
    {
        if (_repositoriKelas.CariWaliGuruId(connection, kelasId) is not { } waliGuruId)
        {
            return;
        }

        var chatId = _repositoriPairing.CariChatIdAktif(connection, JenisPemilikKartu.Guru, waliGuruId);
        if (chatId is null)
        {
            return;
        }

        _repositoriOutbox.Antrekan(connection, null, tanggal, JenisSubjekOutbox.Kelas, kelasId, jenis, chatId, isi, waktu);
    }

    private static string FormatTanggal(DateOnly tanggal) => tanggal.ToString("d MMMM yyyy", BudayaIndonesia);
}
