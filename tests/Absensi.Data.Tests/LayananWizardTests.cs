using Absensi.Data.Akun;
using Absensi.Data.Lisensi;
using Absensi.Data.Repositori;
using Absensi.Data.Wizard;
using Absensi.Domain.Akun;

namespace Absensi.Data.Tests;

// RENCANA-TUGAS.md Tugas 13 — PRD §16.1: wizard instalasi pertama menulis
// sekolah, tahun ajaran, jadwal harian, lisensi, dan akun Kepala Sekolah
// sekaligus dalam satu transaksi.
public sealed class LayananWizardTests
{
    private static readonly DateTime Waktu = new(2026, 9, 18, 8, 0, 0);
    private readonly LayananWizard _layanan = new();

    private static DataWizardInstalasi BuatData() => new(
        KunciLisensi: "SEKOLAH-001-ABCD",
        NamaSekolah: "SMP Negeri Uji",
        Npsn: "12345678",
        Alamat: "Jalan Pendidikan No. 1",
        LogoPath: null,
        NamaTahunAjaran: "2026/2027",
        Semester: "ganjil",
        TglMulaiTahunAjaran: new DateOnly(2026, 7, 1),
        TglSelesaiTahunAjaran: new DateOnly(2026, 12, 19),
        JadwalHarian: Enumerable.Range(1, 7)
            .Select(hari => new JadwalHariBaris(
                hari,
                HariSekolah: hari != 7,
                JamMasuk: hari == 7 ? null : new TimeOnly(7, 30),
                JamRekap: hari == 7 ? null : new TimeOnly(8, 0),
                BatasTapMasuk: hari == 7 ? null : hari == 5 ? new TimeOnly(10, 0) : new TimeOnly(12, 0),
                MulaiPulang: hari == 7 ? null : hari == 5 ? new TimeOnly(11, 30) : new TimeOnly(13, 10)))
            .ToList(),
        UsernameKepalaSekolah: "kepsek",
        SandiKepalaSekolah: "Rahasia123!");

    [Fact]
    public void Selesaikan_MengembalikanAkunKepalaSekolahAktif()
    {
        using var database = new MigratedDatabase();

        var akun = _layanan.Selesaikan(database.Connection, Waktu, BuatData());

        Assert.Equal("kepsek", akun.Username);
        Assert.Equal(PeranAkun.KepalaSekolah, akun.Peran);
        Assert.True(akun.Aktif);
        Assert.True(HashSandi.Verifikasi("Rahasia123!", akun.SandiHash));
    }

    [Fact]
    public void Selesaikan_SekolahTersimpanDanBisaDibaca()
    {
        using var database = new MigratedDatabase();

        _layanan.Selesaikan(database.Connection, Waktu, BuatData());

        var sekolah = new RepositoriSekolah().CariSatuSatunya(database.Connection);
        Assert.NotNull(sekolah);
        Assert.Equal("SMP Negeri Uji", sekolah!.Nama);
        Assert.Equal("12345678", sekolah.Npsn);
    }

    [Fact]
    public void Selesaikan_TahunAjaranAktifDanJadwalTujuhHariTersimpan()
    {
        using var database = new MigratedDatabase();

        _layanan.Selesaikan(database.Connection, Waktu, BuatData());

        var tahunAjaranId = new RepositoriTahunAjaran().CariIdAktif(database.Connection);
        Assert.NotNull(tahunAjaranId);

        var jadwal = new RepositoriJadwalHari().DaftarUntukTahunAjaran(database.Connection, tahunAjaranId!.Value);
        Assert.Equal(7, jadwal.Count);
        Assert.False(jadwal.Single(h => h.Hari == 7).HariSekolah);
        Assert.Equal(new TimeOnly(10, 0), jadwal.Single(h => h.Hari == 5).BatasTapMasuk);
    }

    [Fact]
    public void Selesaikan_LisensiTersimpanAktif()
    {
        using var database = new MigratedDatabase();

        _layanan.Selesaikan(database.Connection, Waktu, BuatData());

        var status = new RepositoriLisensi().BacaStatus(database.Connection);
        Assert.NotNull(status);
        Assert.True(status!.Aktif);
        Assert.Equal("SEKOLAH-001-ABCD", status.Kunci);
    }

    // PRD §12: perubahan tetap tercatat di audit walau akun
    // pelakunya baru dibuat pada transaksi yang sama (lihat catatan di
    // LayananWizard).
    [Fact]
    public void Selesaikan_TercatatDiAuditDenganAktorAkunBaru()
    {
        using var database = new MigratedDatabase();

        var akun = _layanan.Selesaikan(database.Connection, Waktu, BuatData());

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT akun_id, aksi FROM audit WHERE entitas = 'akun' AND entitas_id = $id;";
        command.Parameters.AddWithValue("$id", akun.Id);
        using var reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(akun.Id, reader.GetInt32(0));
        Assert.Equal("wizard_selesai", reader.GetString(1));
    }

    // Aturan isian wizard diperiksa di layanan, sama untuk desktop dan web uji.
    [Theory]
    [InlineData("npsn")]
    [InlineData("tanggal")]
    [InlineData("jam")]
    [InlineData("sandi")]
    [InlineData("username")]
    public void Selesaikan_IsianTidakSah_DitolakTanpaMenulisSekolah(string kasus)
    {
        using var database = new MigratedDatabase();
        var data = BuatData();
        data = kasus switch
        {
            "npsn" => data with { Npsn = "123" },
            "tanggal" => data with { TglSelesaiTahunAjaran = data.TglMulaiTahunAjaran },
            "jam" => data with { JadwalHarian = data.JadwalHarian.Select(h => h.Hari == 1 ? h with { JamRekap = new TimeOnly(7, 0) } : h).ToList() },
            "sandi" => data with { SandiKepalaSekolah = "pendek" },
            _ => data with { UsernameKepalaSekolah = "Kepala Sekolah" },
        };

        Assert.Throws<InvalidOperationException>(() => _layanan.Selesaikan(database.Connection, Waktu, data));
        Assert.Null(new RepositoriSekolah().CariSatuSatunya(database.Connection));
    }
}
