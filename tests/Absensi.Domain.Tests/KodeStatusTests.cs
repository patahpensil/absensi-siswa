using Absensi.Domain.Kehadiran;
using Absensi.Domain.Laporan;

namespace Absensi.Domain.Tests;

// RENCANA-TUGAS.md Tugas 12, koreksi 2026-09-18: Bolos (siswa) dan
// TidakLengkap (guru) sempat dipetakan ke huruf "H" yang sama dengan
// Hadir di grid bulanan — pemilik proyek meminta huruf sendiri ("B"/"L")
// supaya polanya terlihat sekali pandang, TANPA mengubah bagaimana
// keduanya dihitung untuk persentase kehadiran (PRD §11.5).
public sealed class KodeStatusTests
{
    [Fact]
    public void Huruf_BolosSiswa_HurufBBukanH()
    {
        Assert.Equal("B", KodeStatus.Huruf(StatusAbsensiSiswa.Bolos));
        Assert.NotEqual(KodeStatus.Huruf(StatusAbsensiSiswa.Hadir), KodeStatus.Huruf(StatusAbsensiSiswa.Bolos));
    }

    [Fact]
    public void Huruf_TidakLengkapGuru_HurufLBukanH()
    {
        Assert.Equal("L", KodeStatus.Huruf(StatusAbsensiGuru.TidakLengkap));
        Assert.NotEqual(KodeStatus.Huruf(StatusAbsensiGuru.Hadir), KodeStatus.Huruf(StatusAbsensiGuru.TidakLengkap));
    }

    // PRD §11.5: "Terlambat dihitung sebagai hadir ... Bolos juga
    // dihitung sebagai hadir" — pembedaan huruf grid tidak boleh mengubah
    // pembilang persentase kehadiran.
    [Fact]
    public void DihitungHadir_BolosSiswaDanTidakLengkapGuru_TetapDihitungHadirWalauHurufBerbeda()
    {
        Assert.True(KodeStatus.DihitungHadir(StatusAbsensiSiswa.Bolos));
        Assert.True(KodeStatus.DihitungHadir(StatusAbsensiGuru.TidakLengkap));
    }

    [Theory]
    [InlineData(StatusAbsensiSiswa.Alpa, "A")]
    [InlineData(StatusAbsensiSiswa.Hadir, "H")]
    [InlineData(StatusAbsensiSiswa.Terlambat, "T")]
    [InlineData(StatusAbsensiSiswa.Bolos, "B")]
    [InlineData(StatusAbsensiSiswa.Izin, "I")]
    [InlineData(StatusAbsensiSiswa.Sakit, "S")]
    public void Huruf_Siswa_SeluruhStatusMenghasilkanKodeYangDiharapkan(StatusAbsensiSiswa status, string kodeDiharapkan)
    {
        Assert.Equal(kodeDiharapkan, KodeStatus.Huruf(status));
    }

    [Theory]
    [InlineData(StatusAbsensiGuru.Alpa, "A")]
    [InlineData(StatusAbsensiGuru.Masuk, "H")]
    [InlineData(StatusAbsensiGuru.Hadir, "H")]
    [InlineData(StatusAbsensiGuru.TidakLengkap, "L")]
    [InlineData(StatusAbsensiGuru.Izin, "I")]
    [InlineData(StatusAbsensiGuru.Sakit, "S")]
    [InlineData(StatusAbsensiGuru.DinasLuar, "DL")]
    [InlineData(StatusAbsensiGuru.Cuti, "C")]
    public void Huruf_Guru_SeluruhStatusMenghasilkanKodeYangDiharapkan(StatusAbsensiGuru status, string kodeDiharapkan)
    {
        Assert.Equal(kodeDiharapkan, KodeStatus.Huruf(status));
    }
}
