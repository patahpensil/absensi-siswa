using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data;

// Menyambungkan PenentuStatusKehadiran (Absensi.Domain) ke penyimpanan:
// memuat konteks dari database, meminta keputusan, lalu menuliskannya
// bersama tap_log dalam satu transaksi (RENCANA-TUGAS.md Tugas 3).
public sealed class PemrosesTap
{
    private readonly RepositoriTahunAjaran _repositoriTahunAjaran = new();
    private readonly RepositoriKalender _repositoriKalender = new();
    private readonly RepositoriJadwalHari _repositoriJadwalHari = new();
    private readonly RepositoriPengaturan _repositoriPengaturan = new();
    private readonly RepositoriKartu _repositoriKartu = new();
    private readonly RepositoriSiswa _repositoriSiswa = new();
    private readonly RepositoriGuru _repositoriGuru = new();
    private readonly RepositoriAbsensi _repositoriAbsensi = new();
    private readonly RepositoriTapLog _repositoriTapLog = new();
    private readonly PenentuStatusKehadiran _penentuStatusKehadiran = new();

    public KeputusanTap Proses(SqliteConnection connection, string uid, DateTime waktu, bool modeRegistrasiAktif)
    {
        var tanggal = DateOnly.FromDateTime(waktu);
        var tahunAjaranId = _repositoriTahunAjaran.CariIdAktif(connection)
            ?? throw new InvalidOperationException("Tidak ada tahun ajaran aktif; tap tidak bisa diproses.");

        var jenisHari = _repositoriKalender.TentukanJenisHari(connection, tanggal, tahunAjaranId);
        var kartu = _repositoriKartu.CariAktifBerdasarkanUid(connection, uid);
        var tapTerakhir = _repositoriTapLog.CariWaktuTapTerakhir(connection, uid);
        var jedaTapMinimal = TimeSpan.FromSeconds(_repositoriPengaturan.BacaInt(connection, "jeda_tap_detik", 15));
        var jadwal = _repositoriJadwalHari.CariUntukHari(connection, tahunAjaranId, HariKalender.DariTanggal(tanggal));

        var konteks = BuatKonteks(connection, tanggal, waktu, modeRegistrasiAktif, jenisHari, kartu, tapTerakhir, jedaTapMinimal, jadwal);
        var keputusan = _penentuStatusKehadiran.Tentukan(konteks);

        using var transaction = connection.BeginTransaction();

        if (kartu is not null)
        {
            SimpanKeputusan(connection, transaction, tanggal, waktu, kartu, keputusan);
        }

        _repositoriTapLog.Catat(
            connection,
            transaction,
            waktu,
            uid,
            TentukanHasilTapLog(keputusan, kartu?.PemilikTipe),
            kartu is null ? null : PemetaanEnum.TeksDariPemilikTipe(kartu.PemilikTipe),
            kartu?.PemilikId,
            catatan: keputusan.Alasan?.ToString());

        transaction.Commit();

        return keputusan;
    }

    private KonteksTap BuatKonteks(
        SqliteConnection connection,
        DateOnly tanggal,
        DateTime waktu,
        bool modeRegistrasiAktif,
        JenisHariKalender jenisHari,
        KartuAktif? kartu,
        DateTime? tapTerakhir,
        TimeSpan jedaTapMinimal,
        JadwalHariKonfigurasi? jadwal)
    {
        if (kartu?.PemilikTipe == JenisPemilikKartu.Siswa)
        {
            var absensiHariIni = _repositoriAbsensi.CariHariIniSiswa(connection, tanggal, kartu.PemilikId);
            var konteksSiswa = new KonteksTapSiswa(
                SiswaAktif: _repositoriSiswa.IsAktif(connection, kartu.PemilikId),
                TapTerakhirKartuIni: tapTerakhir,
                JedaTapMinimal: jedaTapMinimal,
                SudahTapMasukHariIni: absensiHariIni?.SudahTapMasuk ?? false,
                JamMasuk: jadwal?.JamMasuk ?? default,
                BatasAkhirTapMasuk: jadwal?.BatasTapMasuk ?? default,
                MulaiAbsenPulang: jadwal?.MulaiPulang ?? default);

            return new KonteksTap(waktu, modeRegistrasiAktif, jenisHari, kartu.PemilikTipe, Siswa: konteksSiswa);
        }

        if (kartu?.PemilikTipe == JenisPemilikKartu.Guru)
        {
            var absensiHariIni = _repositoriAbsensi.CariHariIniGuru(connection, tanggal, kartu.PemilikId);
            var statusDiMuka = absensiHariIni is not null && PemetaanEnum.AdalahStatusDitetapkanDiMuka(absensiHariIni.Status)
                ? absensiHariIni.Status
                : (StatusAbsensiGuru?)null;

            var konteksGuru = new KonteksTapGuru(
                GuruAktif: _repositoriGuru.IsAktif(connection, kartu.PemilikId),
                TapTerakhirKartuIni: tapTerakhir,
                JedaTapMinimal: jedaTapMinimal,
                StatusDitetapkanDiMuka: statusDiMuka,
                SudahTapMasukHariIni: absensiHariIni?.SudahTapMasuk ?? false,
                MulaiTapPulang: jadwal?.MulaiPulang ?? default);

            return new KonteksTap(waktu, modeRegistrasiAktif, jenisHari, kartu.PemilikTipe, Guru: konteksGuru);
        }

        return new KonteksTap(waktu, modeRegistrasiAktif, jenisHari, PemilikTerdaftar: null);
    }

    private void SimpanKeputusan(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateOnly tanggal,
        DateTime waktu,
        KartuAktif kartu,
        KeputusanTap keputusan)
    {
        if (kartu.PemilikTipe == JenisPemilikKartu.Siswa)
        {
            _repositoriAbsensi.SimpanKeputusanSiswa(connection, transaction, tanggal, kartu.PemilikId, waktu, keputusan);
        }
        else
        {
            _repositoriAbsensi.SimpanKeputusanGuru(connection, transaction, tanggal, kartu.PemilikId, waktu, keputusan);
        }
    }

    // Memetakan keputusan ke salah satu nilai CHECK(hasil) tap_log
    // (docs/SKEMA-DATABASE.md §6). Daftar itu tidak punya nilai khusus untuk
    // "pemilik tidak aktif" atau "tap pulang tanpa catatan masuk" — dipetakan
    // ke nilai ditolak terdekat; alasan lengkapnya tetap tersimpan di kolom
    // catatan. Layak dipertimbangkan menambah nilai baru lewat migrasi bila
    // pemilik proyek ingin laporan yang lebih rinci.
    private static string TentukanHasilTapLog(KeputusanTap keputusan, JenisPemilikKartu? pemilikTipe) =>
        keputusan.Tindakan switch
        {
            JenisTindakanTap.Registrasi => "registrasi",
            JenisTindakanTap.Diabaikan => "diabaikan",
            JenisTindakanTap.TidakBerubah => "diabaikan",
            JenisTindakanTap.Ditolak => keputusan.Alasan switch
            {
                AlasanTap.TanggalLibur => "ditolak_libur",
                AlasanTap.KartuTidakTerdaftar => "ditolak_tidak_terdaftar",
                AlasanTap.PemilikTidakAktif => "ditolak_tidak_terdaftar",
                AlasanTap.JedaTapTerlaluCepat => "ditolak_jeda",
                AlasanTap.LewatBatasTapMasuk => "ditolak_lewat_batas",
                AlasanTap.BelumPunyaCatatanMasuk => "ditolak_lewat_batas",
                _ => throw new ArgumentOutOfRangeException(nameof(keputusan), keputusan.Alasan, null),
            },
            JenisTindakanTap.IsiJamMasuk => pemilikTipe == JenisPemilikKartu.Guru
                ? "masuk_guru"
                : keputusan.StatusSiswaBaru == StatusAbsensiSiswa.Hadir ? "hadir" : "terlambat",
            JenisTindakanTap.IsiJamPulang => pemilikTipe == JenisPemilikKartu.Guru ? "pulang_guru" : "pulang",
            _ => throw new ArgumentOutOfRangeException(nameof(keputusan), keputusan.Tindakan, null),
        };
}
