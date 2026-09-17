using Absensi.Domain.Kehadiran;

namespace Absensi.Domain.Tests;

public sealed class PenentuStatusKehadiranTests
{
    private static readonly DateOnly Tanggal = new(2026, 9, 17);
    private static readonly TimeSpan JedaTapMinimalBawaan = TimeSpan.FromSeconds(15);

    private readonly PenentuStatusKehadiran _penentu = new();

    private static DateTime Waktu(int jam, int menit, int detik = 0) =>
        Tanggal.ToDateTime(new TimeOnly(jam, menit, detik));

    private static KonteksTapSiswa BuatKonteksSiswa(
        bool aktif = true,
        DateTime? tapTerakhir = null,
        bool sudahMasuk = false,
        TimeOnly? jamMasuk = null,
        TimeOnly? batasAkhirTapMasuk = null,
        TimeOnly? mulaiAbsenPulang = null) => new(
            SiswaAktif: aktif,
            TapTerakhirKartuIni: tapTerakhir,
            JedaTapMinimal: JedaTapMinimalBawaan,
            SudahTapMasukHariIni: sudahMasuk,
            JamMasuk: jamMasuk ?? new TimeOnly(7, 30),
            BatasAkhirTapMasuk: batasAkhirTapMasuk ?? new TimeOnly(12, 0),
            MulaiAbsenPulang: mulaiAbsenPulang ?? new TimeOnly(13, 10));

    private static KonteksTapGuru BuatKonteksGuru(
        bool aktif = true,
        DateTime? tapTerakhir = null,
        StatusAbsensiGuru? statusDiMuka = null,
        bool sudahMasuk = false,
        TimeOnly? mulaiTapPulang = null) => new(
            GuruAktif: aktif,
            TapTerakhirKartuIni: tapTerakhir,
            JedaTapMinimal: JedaTapMinimalBawaan,
            StatusDitetapkanDiMuka: statusDiMuka,
            SudahTapMasukHariIni: sudahMasuk,
            MulaiTapPulang: mulaiTapPulang ?? new TimeOnly(13, 10));

    private static KonteksTap Bungkus(
        DateTime waktu,
        JenisPemilikKartu? pemilik,
        KonteksTapSiswa? siswa = null,
        KonteksTapGuru? guru = null,
        JenisHariKalender hari = JenisHariKalender.Sekolah,
        bool modeRegistrasi = false) =>
        new(waktu, modeRegistrasi, hari, pemilik, siswa, guru);

    // --- Kasus batas wajib CLAUDE.md §5 ---

    [Fact]
    public void Siswa_Tap0729_SebelumJamMasuk_Hadir()
    {
        var keputusan = _penentu.Tentukan(
            Bungkus(Waktu(7, 29), JenisPemilikKartu.Siswa, siswa: BuatKonteksSiswa()));

        Assert.Equal(JenisTindakanTap.IsiJamMasuk, keputusan.Tindakan);
        Assert.Equal(StatusAbsensiSiswa.Hadir, keputusan.StatusSiswaBaru);
    }

    [Fact]
    public void Siswa_Tap0730_TepatJamMasuk_Terlambat()
    {
        var keputusan = _penentu.Tentukan(
            Bungkus(Waktu(7, 30), JenisPemilikKartu.Siswa, siswa: BuatKonteksSiswa()));

        Assert.Equal(JenisTindakanTap.IsiJamMasuk, keputusan.Tindakan);
        Assert.Equal(StatusAbsensiSiswa.Terlambat, keputusan.StatusSiswaBaru);
    }

    [Fact]
    public void Siswa_Tap0731_SetelahJamMasuk_Terlambat()
    {
        var keputusan = _penentu.Tentukan(
            Bungkus(Waktu(7, 31), JenisPemilikKartu.Siswa, siswa: BuatKonteksSiswa()));

        Assert.Equal(JenisTindakanTap.IsiJamMasuk, keputusan.Tindakan);
        Assert.Equal(StatusAbsensiSiswa.Terlambat, keputusan.StatusSiswaBaru);
    }

    [Fact]
    public void Siswa_TapKedua10Detik_Ditolak()
    {
        var tapPertama = Waktu(7, 29);
        var keputusan = _penentu.Tentukan(Bungkus(
            tapPertama.AddSeconds(10),
            JenisPemilikKartu.Siswa,
            siswa: BuatKonteksSiswa(tapTerakhir: tapPertama, sudahMasuk: true)));

        Assert.Equal(JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(AlasanTap.JedaTapTerlaluCepat, keputusan.Alasan);
    }

    [Fact]
    public void Siswa_TapKedua20Detik_DiabaikanKarenaBelumJamPulang()
    {
        var tapPertama = Waktu(7, 29);
        var keputusan = _penentu.Tentukan(Bungkus(
            tapPertama.AddSeconds(20),
            JenisPemilikKartu.Siswa,
            siswa: BuatKonteksSiswa(tapTerakhir: tapPertama, sudahMasuk: true)));

        Assert.Equal(JenisTindakanTap.Diabaikan, keputusan.Tindakan);
    }

    [Fact]
    public void Siswa_Tap1201_TanpaCatatanMasuk_Ditolak()
    {
        var keputusan = _penentu.Tentukan(
            Bungkus(Waktu(12, 1), JenisPemilikKartu.Siswa, siswa: BuatKonteksSiswa(sudahMasuk: false)));

        Assert.Equal(JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(AlasanTap.LewatBatasTapMasuk, keputusan.Alasan);
    }

    [Fact]
    public void Siswa_Tap1315_SudahMasuk_MengisiJamPulang()
    {
        var keputusan = _penentu.Tentukan(
            Bungkus(Waktu(13, 15), JenisPemilikKartu.Siswa, siswa: BuatKonteksSiswa(sudahMasuk: true)));

        Assert.Equal(JenisTindakanTap.IsiJamPulang, keputusan.Tindakan);
    }

    [Fact]
    public void Siswa_JumatTap1030_TanpaCatatanMasuk_Ditolak()
    {
        var siswa = BuatKonteksSiswa(
            sudahMasuk: false,
            batasAkhirTapMasuk: new TimeOnly(10, 0),
            mulaiAbsenPulang: new TimeOnly(11, 30));

        var keputusan = _penentu.Tentukan(Bungkus(Waktu(10, 30), JenisPemilikKartu.Siswa, siswa: siswa));

        Assert.Equal(JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(AlasanTap.LewatBatasTapMasuk, keputusan.Alasan);
    }

    [Fact]
    public void Siswa_JumatTap1135_SudahMasuk_MengisiJamPulang()
    {
        var siswa = BuatKonteksSiswa(
            sudahMasuk: true,
            batasAkhirTapMasuk: new TimeOnly(10, 0),
            mulaiAbsenPulang: new TimeOnly(11, 30));

        var keputusan = _penentu.Tentukan(Bungkus(Waktu(11, 35), JenisPemilikKartu.Siswa, siswa: siswa));

        Assert.Equal(JenisTindakanTap.IsiJamPulang, keputusan.Tindakan);
    }

    [Fact]
    public void TanggalLibur_TapSiswa_Ditolak()
    {
        var keputusan = _penentu.Tentukan(Bungkus(
            Waktu(7, 29),
            JenisPemilikKartu.Siswa,
            siswa: BuatKonteksSiswa(),
            hari: JenisHariKalender.Libur));

        Assert.Equal(JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(AlasanTap.TanggalLibur, keputusan.Alasan);
    }

    [Fact]
    public void TanggalLibur_TapGuru_Ditolak()
    {
        var keputusan = _penentu.Tentukan(Bungkus(
            Waktu(7, 29),
            JenisPemilikKartu.Guru,
            guru: BuatKonteksGuru(),
            hari: JenisHariKalender.Libur));

        Assert.Equal(JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(AlasanTap.TanggalLibur, keputusan.Alasan);
    }

    [Fact]
    public void Guru_TapMasukSaja_StatusAkhirHariTidakLengkap()
    {
        var statusAkhir = _penentu.TentukanStatusAkhirHariGuru(StatusAbsensiGuru.Masuk);

        Assert.Equal(StatusAbsensiGuru.TidakLengkap, statusAkhir);
    }

    // --- Kasus tambahan per cabang keputusan ---

    [Fact]
    public void ModeRegistrasiAktif_TapApaPun_DiprosesSebagaiRegistrasi()
    {
        var keputusan = _penentu.Tentukan(Bungkus(
            Waktu(7, 29),
            JenisPemilikKartu.Siswa,
            siswa: BuatKonteksSiswa(),
            modeRegistrasi: true));

        Assert.Equal(JenisTindakanTap.Registrasi, keputusan.Tindakan);
    }

    [Fact]
    public void KartuTidakTerdaftar_Ditolak()
    {
        var keputusan = _penentu.Tentukan(Bungkus(Waktu(7, 29), pemilik: null));

        Assert.Equal(JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(AlasanTap.KartuTidakTerdaftar, keputusan.Alasan);
    }

    [Fact]
    public void Siswa_TidakAktif_Ditolak()
    {
        var keputusan = _penentu.Tentukan(
            Bungkus(Waktu(7, 29), JenisPemilikKartu.Siswa, siswa: BuatKonteksSiswa(aktif: false)));

        Assert.Equal(JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(AlasanTap.PemilikTidakAktif, keputusan.Alasan);
    }

    [Fact]
    public void Guru_TidakAktif_Ditolak()
    {
        var keputusan = _penentu.Tentukan(
            Bungkus(Waktu(7, 29), JenisPemilikKartu.Guru, guru: BuatKonteksGuru(aktif: false)));

        Assert.Equal(JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(AlasanTap.PemilikTidakAktif, keputusan.Alasan);
    }

    [Fact]
    public void Guru_TapKedua10Detik_Ditolak()
    {
        var tapPertama = Waktu(7, 0);
        var keputusan = _penentu.Tentukan(Bungkus(
            tapPertama.AddSeconds(10),
            JenisPemilikKartu.Guru,
            guru: BuatKonteksGuru(tapTerakhir: tapPertama, sudahMasuk: true)));

        Assert.Equal(JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(AlasanTap.JedaTapTerlaluCepat, keputusan.Alasan);
    }

    [Fact]
    public void Guru_StatusSudahDitetapkanDiMuka_TapTidakMengubahStatus()
    {
        var keputusan = _penentu.Tentukan(Bungkus(
            Waktu(7, 29),
            JenisPemilikKartu.Guru,
            guru: BuatKonteksGuru(statusDiMuka: StatusAbsensiGuru.Izin)));

        Assert.Equal(JenisTindakanTap.TidakBerubah, keputusan.Tindakan);
        Assert.Equal(StatusAbsensiGuru.Izin, keputusan.StatusGuruBaru);
    }

    [Fact]
    public void Guru_TapPulang_TanpaCatatanMasuk_Ditolak()
    {
        var keputusan = _penentu.Tentukan(Bungkus(
            Waktu(13, 15),
            JenisPemilikKartu.Guru,
            guru: BuatKonteksGuru(sudahMasuk: false)));

        Assert.Equal(JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(AlasanTap.BelumPunyaCatatanMasuk, keputusan.Alasan);
    }

    [Fact]
    public void Guru_TapKeduaSebelumJamPulang_SudahMasuk_Diabaikan()
    {
        var tapPertama = Waktu(7, 0);
        var keputusan = _penentu.Tentukan(Bungkus(
            tapPertama.AddSeconds(20),
            JenisPemilikKartu.Guru,
            guru: BuatKonteksGuru(tapTerakhir: tapPertama, sudahMasuk: true)));

        Assert.Equal(JenisTindakanTap.Diabaikan, keputusan.Tindakan);
    }

    [Fact]
    public void Guru_TapPertama_MenghasilkanMasuk()
    {
        var keputusan = _penentu.Tentukan(
            Bungkus(Waktu(7, 0), JenisPemilikKartu.Guru, guru: BuatKonteksGuru()));

        Assert.Equal(JenisTindakanTap.IsiJamMasuk, keputusan.Tindakan);
        Assert.Equal(StatusAbsensiGuru.Masuk, keputusan.StatusGuruBaru);
    }

    [Fact]
    public void Guru_TapPulang_SudahMasuk_MenghasilkanHadir()
    {
        var keputusan = _penentu.Tentukan(Bungkus(
            Waktu(13, 15),
            JenisPemilikKartu.Guru,
            guru: BuatKonteksGuru(sudahMasuk: true)));

        Assert.Equal(JenisTindakanTap.IsiJamPulang, keputusan.Tindakan);
        Assert.Equal(StatusAbsensiGuru.Hadir, keputusan.StatusGuruBaru);
    }

    [Fact]
    public void Siswa_TapPulang_TanpaCatatanMasuk_Ditolak()
    {
        var keputusan = _penentu.Tentukan(Bungkus(
            Waktu(13, 15),
            JenisPemilikKartu.Siswa,
            siswa: BuatKonteksSiswa(sudahMasuk: false)));

        Assert.Equal(JenisTindakanTap.Ditolak, keputusan.Tindakan);
        Assert.Equal(AlasanTap.BelumPunyaCatatanMasuk, keputusan.Alasan);
    }

    [Fact]
    public void Guru_StatusAkhirHari_SelainMasuk_TidakBerubah()
    {
        var statusAkhir = _penentu.TentukanStatusAkhirHariGuru(StatusAbsensiGuru.Hadir);

        Assert.Equal(StatusAbsensiGuru.Hadir, statusAkhir);
    }

    [Fact]
    public void PenyediaWaktuTetap_MengembalikanWaktuYangDitentukan()
    {
        var waktuTetap = Waktu(7, 29);
        var penyediaWaktu = new PenyediaWaktuTetap(waktuTetap);

        Assert.Equal(waktuTetap, penyediaWaktu.Sekarang);
    }
}
