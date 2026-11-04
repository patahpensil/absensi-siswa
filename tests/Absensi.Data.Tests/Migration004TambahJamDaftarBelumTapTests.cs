using Absensi.Data.Migrations;
using Absensi.Data.Repositori;

namespace Absensi.Data.Tests;

// PRD §7 P-06: kolom jam_daftar_belum_tap ditambahkan tanpa mengubah perilaku
// instalasi lama — baris yang sudah ada diisi dari jam_rekap tersimpan.
public sealed class Migration004TambahJamDaftarBelumTapTests
{
    [Fact]
    public void Migrasi_BarisLama_JamDaftarBelumTapDiisiDariJamRekapDanJamRekapTidakBerubah()
    {
        using var database = new MigratedDatabase(applyAllMigrations: false);
        foreach (var migrasi in new IMigration[] { new Migration001CreateSchema(), new Migration002CegahUbahHapusAudit(), new Migration003TambahAuditUid() })
        {
            database.Execute(migrasi.Sql);
            database.Execute($"INSERT INTO skema_versi (versi, diterapkan) VALUES ({migrasi.Number}, '2026-09-01T00:00:00');");
        }

        database.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);
            INSERT INTO jadwal_hari (tahun_ajaran_id, hari, hari_sekolah, jam_masuk, jam_rekap, batas_tap_masuk, mulai_pulang)
            VALUES (1, 4, 1, '07:30', '08:00', '12:00', '13:10');
            INSERT INTO jadwal_hari (tahun_ajaran_id, hari, hari_sekolah) VALUES (1, 7, 0);
            """);

        new MigrationRunner().Run(database.Connection);

        var kamis = new RepositoriJadwalHari().CariUntukHari(database.Connection, tahunAjaranId: 1, hari: 4);
        Assert.Equal(new TimeOnly(8, 0), kamis!.JamDaftarBelumTap);
        Assert.Equal(new TimeOnly(8, 0), kamis.JamRekap);

        var minggu = new RepositoriJadwalHari().CariUntukHari(database.Connection, tahunAjaranId: 1, hari: 7);
        Assert.Null(minggu!.JamDaftarBelumTap);
    }

    [Fact]
    public void RepositoriJadwalHari_SimpanDanBacaUlang_KeduaJamTersimpanTerpisah()
    {
        using var database = new MigratedDatabase();
        database.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);
            """);
        var repositori = new RepositoriJadwalHari();

        using (var transaction = database.Connection.BeginTransaction())
        {
            repositori.Perbarui(database.Connection, transaction, tahunAjaranId: 1, new JadwalHariBaris(
                4, true, new TimeOnly(7, 30), new TimeOnly(8, 10), new TimeOnly(12, 0), new TimeOnly(13, 10), new TimeOnly(8, 0)));
            transaction.Commit();
        }

        var baris = repositori.DaftarUntukTahunAjaran(database.Connection, tahunAjaranId: 1).Single(b => b.Hari == 4);
        Assert.Equal(new TimeOnly(8, 0), baris.JamDaftarBelumTap);
        Assert.Equal(new TimeOnly(8, 10), baris.JamRekap);
    }
}
