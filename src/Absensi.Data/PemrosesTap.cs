using System.Globalization;
using Absensi.Data.Repositori;
using Absensi.Data.Koreksi;
using Absensi.Data.Notifikasi;
using Absensi.Domain.Kehadiran;
using Absensi.Domain.Notifikasi;
using Microsoft.Data.Sqlite;

namespace Absensi.Data;

// Menyambungkan PenentuStatusKehadiran (Absensi.Domain) ke penyimpanan:
// memuat konteks dari database, meminta keputusan, lalu menuliskannya
// bersama tap_log dalam satu transaksi (RENCANA-TUGAS.md Tugas 3).
public sealed class PemrosesTap
{
    private static readonly CultureInfo BudayaIndonesia = new("id-ID");

    private readonly RepositoriTahunAjaran _repositoriTahunAjaran = new();
    private readonly RepositoriKalender _repositoriKalender = new();
    private readonly RepositoriJadwalHari _repositoriJadwalHari = new();
    private readonly RepositoriPengaturan _repositoriPengaturan = new();
    private readonly RepositoriKartu _repositoriKartu = new();
    private readonly RepositoriSiswa _repositoriSiswa = new();
    private readonly RepositoriGuru _repositoriGuru = new();
    private readonly RepositoriAbsensi _repositoriAbsensi = new();
    private readonly RepositoriTapLog _repositoriTapLog = new();
    private readonly ParentRecipientResolver _parentRecipient = new();
    private readonly RepositoriOutbox _repositoriOutbox = new();
    private readonly PenentuStatusKehadiran _penentuStatusKehadiran = new();

    public KeputusanTap Proses(SqliteConnection connection, string uid, DateTime waktu, bool modeRegistrasiAktif)
    {
        var tanggal = DateOnly.FromDateTime(waktu);
        if (!modeRegistrasiAktif)
        {
            EnsureAttendanceRow(connection, uid, tanggal);
        }

        // PRD §8.3: koreksi tidak boleh disela oleh tap dengan konteks yang sudah usang.
        using var transaction = connection.BeginTransaction(deferred: false);

        var tahunAjaranId = _repositoriTahunAjaran.CariIdAktif(connection)
            ?? throw new InvalidOperationException("Tidak ada tahun ajaran aktif; tap tidak bisa diproses.");

        var jenisHari = _repositoriTahunAjaran.FindForDate(connection, tanggal, transaction) == tahunAjaranId
            ? _repositoriKalender.TentukanJenisHari(connection, tanggal, tahunAjaranId, transaction)
            : JenisHariKalender.Libur;
        var kartu = _repositoriKartu.CariAktifBerdasarkanUid(connection, uid, transaction);
        var tapTerakhir = _repositoriTapLog.CariWaktuTapTerakhir(connection, uid);
        var jedaTapMinimal = TimeSpan.FromSeconds(_repositoriPengaturan.BacaInt(connection, "jeda_tap_detik", 15));
        var jadwal = _repositoriJadwalHari.CariUntukHari(connection, tahunAjaranId, HariKalender.DariTanggal(tanggal), transaction, tanggal);

        // Bug jadwal kosong (docs/MASTER-BUG-UIUX-REGISTER.md, analisis
        // penolakan Gerbang): sebelumnya jam jadwal yang NULL diam-diam
        // dijadikan 00:00 lewat "?? default" di BuatKonteks, membuat
        // PenentuStatusKehadiran salah menyimpulkan "sudah lewat jam
        // mulai pulang" untuk tap PERTAMA hari itu, kapan pun jam
        // sebenarnya. Ditangani DI SINI, secara eksplisit, SEBELUM
        // PenentuStatusKehadiran pernah dipanggil — kelas itu (satu-
        // satunya penentu status kehadiran, PRD §24.1) dan
        // KonteksTapSiswa/KonteksTapGuru TIDAK disentuh, aturannya tidak
        // berubah sama sekali. Gerbang tetap dicek lebih dulu (mode
        // registrasi, kartu tidak terdaftar) supaya penolakan ini tidak
        // pernah membocorkan/mengganggu alur registrasi RFID atau hari
        // libur — keduanya sudah benar menentukan hasil TANPA pernah
        // membaca jam jadwal sama sekali.
        KeputusanTap keputusan;
        if (!modeRegistrasiAktif && new RepositoriKuncimBulan().SudahDikunci(connection, tanggal.Year, tanggal.Month, transaction))
        {
            keputusan = KeputusanTap.Ditolak(AlasanTap.BulanTerkunci);
        }
        else if (!modeRegistrasiAktif && new LocalAttendanceClosure().IsClosed(connection, tanggal, transaction))
        {
            keputusan = KeputusanTap.Ditolak(AlasanTap.HariDitutup);
        }
        else if (!modeRegistrasiAktif
            && jenisHari == JenisHariKalender.Sekolah
            && kartu is not null
            && JadwalBelumLengkapUntuk(kartu.PemilikTipe, jadwal))
        {
            keputusan = KeputusanTap.Ditolak(AlasanTap.JadwalBelumDiatur);
        }
        else
        {
            var konteks = BuatKonteks(connection, transaction, tanggal, waktu, modeRegistrasiAktif, jenisHari, kartu, tapTerakhir, jedaTapMinimal, jadwal);
            keputusan = _penentuStatusKehadiran.Tentukan(konteks);
        }

        // PRD §16.8.a langkah 9 / §16.9: tap yang menghasilkan TERLAMBAT
        // SETELAH jam rekap diantre real-time di sini. Tap sebelum jam
        // rekap (Hadir maupun Terlambat) ditangani batch oleh
        // Absensi.Data.Notifikasi.LayananNotifikasi.RunParentRecap —
        // dikonfirmasi pemilik proyek 2026-09-18. Pesan hanya diANTRE, tak
        // pernah dikirim di sini (PRD §9.4).
        var notifikasiTerlambatRealtime = SiapkanNotifikasiTerlambatRealtime(connection, transaction, tanggal, waktu, tahunAjaranId, kartu, keputusan, jadwal);

        if (kartu is not null)
        {
            SimpanKeputusan(connection, transaction, tanggal, waktu, kartu, keputusan);
        }

        if (notifikasiTerlambatRealtime is { } notifikasi)
        {
            _repositoriOutbox.Antrekan(
                connection, transaction, tanggal, JenisSubjekOutbox.Siswa, kartu!.PemilikId,
                JenisNotifikasi.Terlambat, notifikasi.Penerima.Address, notifikasi.Isi, waktu, notifikasi.Penerima.Channel);
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

    // Jam yang dipakai Proses() untuk memutuskan gerbang harus ditolak
    // eksplisit SEBELUM BuatKonteks/PenentuStatusKehadiran dipanggil sama
    // sekali — lihat komentar di Proses(). Guru hanya butuh MulaiTapPulang
    // (KonteksTapGuru tidak pernah membaca JamMasuk/BatasAkhirTapMasuk,
    // lihat PenentuStatusKehadiran.TentukanUntukGuru); siswa butuh
    // ketiganya. jadwal bisa null kalau baris jadwal_hari hari ini belum
    // ada sama sekali — kasus itu sebenarnya sudah ditolak lebih dulu
    // sebagai TanggalLibur oleh RepositoriKalender.TentukanJenisHari (baris
    // tanpa jadwal dianggap bukan hari sekolah), jadi pemeriksaan `jadwal is
    // null` di sini murni jaring pengaman, bukan jalur yang diharapkan
    // tercapai untuk jenisHari == Sekolah.
    private static bool JadwalBelumLengkapUntuk(JenisPemilikKartu pemilikTipe, JadwalHariKonfigurasi? jadwal) =>
        pemilikTipe == JenisPemilikKartu.Siswa
            ? jadwal is not { JamMasuk: not null, BatasTapMasuk: not null, MulaiPulang: not null }
            : jadwal is not { MulaiPulang: not null };

    private KonteksTap BuatKonteks(
        SqliteConnection connection,
        SqliteTransaction transaction,
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
            // "?? default" (00:00) di bawah TIDAK PERNAH memengaruhi
            // keputusan nyata lagi: untuk hari sekolah dengan kartu
            // terdaftar dan mode registrasi mati, Proses() sudah menolak
            // eksplisit lewat JadwalBelumLengkapUntuk SEBELUM sampai ke sini
            // kalau salah satu field ini null. Jalur yang MASIH bisa sampai
            // ke sini dengan jam null adalah mode registrasi aktif atau
            // hari libur — keduanya membuat PenentuStatusKehadiran berhenti
            // di pengecekan paling awal (ModeRegistrasiAktif / JenisHariIni)
            // tanpa pernah membaca field KonteksTapSiswa ini. Default di
            // sini sekadar memenuhi tipe TimeOnly non-nullable-nya, bukan
            // nilai yang pernah dipakai untuk memutuskan apa pun.
            var absensiHariIni = _repositoriAbsensi.CariHariIniSiswa(connection, tanggal, kartu.PemilikId, transaction);
            var konteksSiswa = new KonteksTapSiswa(
                SiswaAktif: _repositoriSiswa.IsAktif(connection, kartu.PemilikId),
                TapTerakhirKartuIni: tapTerakhir,
                JedaTapMinimal: jedaTapMinimal,
                SudahTapMasukHariIni: absensiHariIni?.SudahTapMasuk ?? false,
                JamMasuk: jadwal?.JamMasuk ?? default,
                BatasAkhirTapMasuk: jadwal?.BatasTapMasuk ?? default,
                MulaiAbsenPulang: jadwal?.MulaiPulang ?? default,
                StatusSaatIni: absensiHariIni?.Status ?? StatusAbsensiSiswa.Alpa);

            return new KonteksTap(waktu, modeRegistrasiAktif, jenisHari, kartu.PemilikTipe, Siswa: konteksSiswa);
        }

        if (kartu?.PemilikTipe == JenisPemilikKartu.Guru)
        {
            // "?? default" di bawah: catatan yang sama seperti cabang Siswa
            // di atas berlaku persis — sudah dijaga JadwalBelumLengkapUntuk
            // di Proses() sebelum sampai ke sini untuk hari sekolah.
            var absensiHariIni = _repositoriAbsensi.CariHariIniGuru(connection, tanggal, kartu.PemilikId, transaction);
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

    // Keputusan pemilik 14 November 2026: siswa/guru yang ditambahkan setelah
    // hari dibuka (mis. siswa pindahan didaftarkan pukul 09.00) belum punya
    // baris absensi hari itu. Pembukaan hari dijalankan lagi untuk melengkapi
    // daftar hadir dengan aturan yang sama persis (idempoten: INSERT OR IGNORE,
    // libur/bulan terkunci/hari ditutup tetap tidak membuat baris).
    private void EnsureAttendanceRow(SqliteConnection connection, string uid, DateOnly tanggal)
    {
        var kartu = _repositoriKartu.CariAktifBerdasarkanUid(connection, uid);
        if (kartu is null || _repositoriTahunAjaran.CariIdAktif(connection) is null)
        {
            return;
        }

        var hasRow = kartu.PemilikTipe == JenisPemilikKartu.Siswa
            ? _repositoriAbsensi.CariHariIniSiswa(connection, tanggal, kartu.PemilikId) is not null
            : _repositoriAbsensi.CariHariIniGuru(connection, tanggal, kartu.PemilikId) is not null;
        if (!hasRow)
        {
            new PembukaanHari().Jalankan(connection, tanggal);
        }
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

    private (NotificationRecipient Penerima, string Isi)? SiapkanNotifikasiTerlambatRealtime(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateOnly tanggal,
        DateTime waktu,
        int tahunAjaranId,
        KartuAktif? kartu,
        KeputusanTap keputusan,
        JadwalHariKonfigurasi? jadwal)
    {
        if (kartu?.PemilikTipe != JenisPemilikKartu.Siswa
            || keputusan.Tindakan != JenisTindakanTap.IsiJamMasuk
            || keputusan.StatusSiswaBaru != StatusAbsensiSiswa.Terlambat
            || jadwal?.JamRekap is not { } jamRekap
            || TimeOnly.FromDateTime(waktu) < jamRekap)
        {
            return null;
        }

        // PRD P-20: parent's chosen channel (Telegram or WhatsApp).
        if (_parentRecipient.Find(connection, kartu.PemilikId, transaction) is not { } penerima)
        {
            return null;
        }

        var (namaSiswa, _) = _repositoriSiswa.CariNamaDanKelas(connection, kartu.PemilikId, tahunAjaranId);
        var isi = $"Ananda {namaSiswa} tercatat TERLAMBAT hari ini, {tanggal.ToString("d MMMM yyyy", BudayaIndonesia)} pukul {TimeOnly.FromDateTime(waktu).ToString("HH:mm")}.";
        return (penerima, isi);
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
                // Sama seperti dua alasan di atas: tidak ada nilai CHECK
                // khusus untuk "jadwal belum diatur" (menambah satu berarti
                // migrasi skema baru, di luar cakupan perbaikan ini — lihat
                // AlasanTap.JadwalBelumDiatur). Alasan lengkapnya tetap apa
                // adanya di tap_log.catatan lewat keputusan.Alasan?.ToString().
                AlasanTap.JadwalBelumDiatur or AlasanTap.BulanTerkunci or AlasanTap.HariDitutup => "ditolak_lewat_batas",
                _ => throw new ArgumentOutOfRangeException(nameof(keputusan), keputusan.Alasan, null),
            },
            JenisTindakanTap.IsiJamMasuk => pemilikTipe == JenisPemilikKartu.Guru
                ? "masuk_guru"
                : keputusan.StatusSiswaBaru == StatusAbsensiSiswa.Hadir ? "hadir" : "terlambat",
            JenisTindakanTap.IsiJamPulang => pemilikTipe == JenisPemilikKartu.Guru ? "pulang_guru" : "pulang",
            _ => throw new ArgumentOutOfRangeException(nameof(keputusan), keputusan.Tindakan, null),
        };
}
