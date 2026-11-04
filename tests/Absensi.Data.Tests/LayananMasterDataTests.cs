using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.Data.Tests;

// LayananSiswa/LayananGuru/LayananKelas: setiap perubahan lewat
// JalurPerubahanData (PRD §12) — satu baris audit per
// operasi, mencakup penandaan tidak aktif (§4.2 aturan 8: tidak pernah
// dihapus).
public sealed class LayananMasterDataTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    [Fact]
    public void LayananSiswa_Tambah_TercatatDiAuditDanBisaDicariLewatNisn()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananSiswa();
        var data = new DataSiswa("0011122233", "Siswa Uji", "L", null, null, null, null, null);

        var id = layanan.Tambah(database.Connection, Admin, Waktu, data);

        Assert.Equal(id, new RepositoriSiswa().CariIdBerdasarkanNisn(database.Connection, "0011122233"));
        AssertAdaBarisAudit(database, "tambah_siswa");
    }

    [Fact]
    public void LayananSiswa_TandaiTidakAktif_AktifJadiNolBukanDihapus()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananSiswa();
        var id = layanan.Tambah(database.Connection, Admin, Waktu, new DataSiswa("0011122233", "Siswa Uji", "L", null, null, null, null, null));

        layanan.TandaiTidakAktif(database.Connection, Admin, Waktu, id, "pindah");

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT aktif, alasan_nonaktif FROM siswa WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.Equal("pindah", reader.GetString(1));
    }

    [Fact]
    public void LayananGuru_Tambah_TercatatDiAuditDanBisaDicariLewatNip()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananGuru();
        var data = new DataGuru("999", "Guru Uji", null, null);

        var id = layanan.Tambah(database.Connection, Admin, Waktu, data);

        Assert.Equal(id, new RepositoriGuru().CariIdBerdasarkanNip(database.Connection, "999"));
        AssertAdaBarisAudit(database, "tambah_guru");
    }

    [Fact]
    public void LayananGuru_TandaiTidakAktif_AktifJadiNolBukanDihapus()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananGuru();
        var id = layanan.Tambah(database.Connection, Admin, Waktu, new DataGuru("999", "Guru Uji", null, null));

        layanan.TandaiTidakAktif(database.Connection, Admin, Waktu, id, "pensiun");

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT aktif, alasan_nonaktif FROM guru WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.Equal("pensiun", reader.GetString(1));
    }

    [Fact]
    public void LayananKelas_TambahLaluPerbarui_DaftarMencerminkanPerubahan()
    {
        using var database = new MigratedDatabase();
        database.SeedMasterDataUntukUji();
        var layanan = new LayananKelas();

        var id = layanan.Tambah(database.Connection, Admin, Waktu, tahunAjaranId: 1, nama: "8B", tingkat: 8, waliGuruId: null);
        layanan.Perbarui(database.Connection, Admin, Waktu, id, nama: "8C", tingkat: 8, waliGuruId: null);

        var daftar = new RepositoriKelas().DaftarUntukTahunAjaran(database.Connection, 1);
        Assert.Contains(daftar, k => k.Id == id && k.Nama == "8C");
    }

    private static void AssertAdaBarisAudit(MigratedDatabase database, string aksi)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit WHERE aksi = $aksi;";
        command.Parameters.AddWithValue("$aksi", aksi);
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }
}
