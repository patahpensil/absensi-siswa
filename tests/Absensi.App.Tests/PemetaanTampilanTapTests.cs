using Absensi.App.Gerbang;
using Absensi.Domain.Kehadiran;

namespace Absensi.App.Tests;

// Sebelum perbaikan ini, JenisTindakanTap.Ditolak selalu dipetakan ke satu
// kata "Ditolak" tanpa alasan (lihat MASTER-BUG-UIUX-REGISTER.md, analisis
// penolakan layar Gerbang) — operator tidak bisa membedakan UID tidak
// dikenal dari jadwal yang belum lewat. Test ini mengunci teks spesifik
// untuk TIAP nilai AlasanTap yang benar-benar bisa dihasilkan
// PenentuStatusKehadiran lewat KeputusanTap.Ditolak, plus alasan null
// sebagai jaring pengaman.
public sealed class PemetaanTampilanTapTests
{
    [Theory]
    [InlineData(StatusAbsensiSiswa.Izin, "Izin")]
    [InlineData(StatusAbsensiSiswa.Sakit, "Sakit")]
    public void Petakan_KoreksiSiswaTetapBerlaku_MenampilkanStatusDanArahan(
        StatusAbsensiSiswa status, string teksStatus)
    {
        var keputusan = KeputusanTap.TidakBerubahSiswa(status);

        var (kategori, teks, _) = PemetaanTampilanTap.Petakan(keputusan);

        Assert.Equal(KategoriWarnaTap.Info, kategori);
        Assert.Equal(teksStatus, teks);
        Assert.Equal($"Status hari ini: {teksStatus}. Hubungi wali kelas atau admin untuk koreksi.",
            PemetaanTampilanTap.PetunjukUntuk(keputusan));
    }

    [Fact]
    public void PetunjukUntuk_TapNormalAtauGuru_TidakMenampilkanArahanKoreksiSiswa()
    {
        Assert.Null(PemetaanTampilanTap.PetunjukUntuk(KeputusanTap.IsiJamMasukSiswa(StatusAbsensiSiswa.Hadir)));
        Assert.Null(PemetaanTampilanTap.PetunjukUntuk(KeputusanTap.TidakBerubahGuru(StatusAbsensiGuru.Izin)));
    }

    [Theory]
    [InlineData(AlasanTap.TanggalLibur, "Hari libur")]
    [InlineData(AlasanTap.KartuTidakTerdaftar, "Tidak terdaftar")]
    [InlineData(AlasanTap.PemilikTidakAktif, "Nonaktif")]
    [InlineData(AlasanTap.JedaTapTerlaluCepat, "Tap terlalu cepat")]
    [InlineData(AlasanTap.BelumPunyaCatatanMasuk, "Belum tap masuk")]
    [InlineData(AlasanTap.LewatBatasTapMasuk, "Lewat batas tap")]
    [InlineData(AlasanTap.JadwalBelumDiatur, "Jadwal belum diatur")]
    public void Petakan_Ditolak_MenampilkanTeksSpesifikSesuaiAlasan(AlasanTap alasan, string teksDiharapkan)
    {
        var keputusan = KeputusanTap.Ditolak(alasan);

        var (kategori, teks, berhasil) = PemetaanTampilanTap.Petakan(keputusan);

        Assert.Equal(KategoriWarnaTap.Ditolak, kategori);
        Assert.Equal(teksDiharapkan, teks);
        Assert.False(berhasil);
    }

    [Fact]
    public void Petakan_DitolakTanpaAlasan_JatuhKeTeksGenerikBukanCrash()
    {
        var keputusan = new KeputusanTap(JenisTindakanTap.Ditolak, Alasan: null);

        var (kategori, teks, berhasil) = PemetaanTampilanTap.Petakan(keputusan);

        Assert.Equal(KategoriWarnaTap.Ditolak, kategori);
        Assert.Equal("Ditolak", teks);
        Assert.False(berhasil);
    }

    [Fact]
    public void Petakan_SeluruhNilaiAlasanTapYangDitolakPunyaTeksBerbeda()
    {
        // Menutup celah "lupa menambah cabang baru saat AlasanTap
        // bertambah": setiap alasan yang benar-benar dipakai
        // KeputusanTap.Ditolak — baik oleh PenentuStatusKehadiran.cs
        // (enam alasan pertama) maupun oleh PemrosesTap.cs sendiri
        // (JadwalBelumDiatur, lihat PemrosesTap.JadwalBelumLengkapUntuk) —
        // wajib menghasilkan teks unik, bukan ikut jatuh ke "Ditolak" generik.
        var alasanDitolak = new[]
        {
            AlasanTap.TanggalLibur,
            AlasanTap.KartuTidakTerdaftar,
            AlasanTap.PemilikTidakAktif,
            AlasanTap.JedaTapTerlaluCepat,
            AlasanTap.BelumPunyaCatatanMasuk,
            AlasanTap.LewatBatasTapMasuk,
            AlasanTap.JadwalBelumDiatur,
        };

        var teks = alasanDitolak
            .Select(alasan => PemetaanTampilanTap.Petakan(KeputusanTap.Ditolak(alasan)).Teks)
            .ToList();

        Assert.Equal(alasanDitolak.Length, teks.Distinct().Count());
        Assert.DoesNotContain("Ditolak", teks);
    }
}
