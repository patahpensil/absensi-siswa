using Absensi.Data.Akun;
using Absensi.Data.Kartu;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Tests;

// PRD §12 (Jejak Audit): perubahan master dan penggantian kartu wajib
// dicatat "nilai lama -> nilai baru". Verifikasi 2026-09-24 menemukan dua
// jalur tulis yang belum lengkap:
//  - ganti_kartu hanya menulis UID baru (nilai_lama kosong walau ada kartu
//    lama yang dinonaktifkan);
//  - perbarui_siswa/guru/kelas, nonaktifkan_*, dan unggah_foto_* hanya
//    menulis nilai baru (nama) tanpa nilai lama.
// Nilai lama sekarang dibaca DI DALAM transaksi perubahan (bukan ditebak) dan
// hanya kolom yang benar-benar berubah yang ditulis. Baris audit yang sudah
// ada tidak pernah diubah.
public sealed class KelengkapanAuditTests
{
    private static readonly DateTime Waktu = new(2026, 9, 24, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    private sealed record BarisAudit(int? EntitasId, string? Lama, string? Baru, string Entitas);

    private static MigratedDatabase BuatDatabase()
    {
        var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        // Siswa 1 = "Siswa Sudah Kartu" (UID-SISWA-LAMA), 2 dan 3 tanpa kartu; guru 1 tanpa kartu,
        // guru 2 = "Guru Sudah Kartu" (UID-GURU-LAMA); kelas 7A tanpa wali.
        database.Execute("INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat, wali_guru_id) VALUES (2, 1, '7B', 7, NULL);");
        return database;
    }

    private static List<BarisAudit> BacaAudit(MigratedDatabase database, string aksi)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT entitas_id, nilai_lama, nilai_baru, entitas FROM audit WHERE aksi = $aksi ORDER BY id;";
        command.Parameters.AddWithValue("$aksi", aksi);

        using var reader = command.ExecuteReader();
        var hasil = new List<BarisAudit>();
        while (reader.Read())
        {
            hasil.Add(new BarisAudit(
                reader.IsDBNull(0) ? null : reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3)));
        }

        return hasil;
    }

    private static int HitungAudit(MigratedDatabase database)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM audit;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string? Skalar(MigratedDatabase database, string sql)
    {
        using var command = database.Connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() is { } nilai and not DBNull ? Convert.ToString(nilai) : null;
    }

    // ---------------- ganti kartu ----------------

    [Fact]
    public void GantiKartu_Siswa_UidLamaDanBaruTercatatDenganNamaPemilik()
    {
        using var database = BuatDatabase();

        new LayananRegistrasiKartu().GantiKartuHilang(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, 1, "UID-SISWA-BARU");

        var baris = Assert.Single(BacaAudit(database, "ganti_kartu"));
        Assert.Equal("UID-SISWA-LAMA->Siswa Sudah Kartu", baris.Lama);
        Assert.Equal("UID-SISWA-BARU->Siswa Sudah Kartu", baris.Baru);
        Assert.Equal(1, baris.EntitasId);
        Assert.Equal("kartu", baris.Entitas);

        // Pengaman: kartu lama memang nonaktif, kartu baru aktif.
        Assert.Equal("0", Skalar(database, "SELECT aktif FROM kartu WHERE uid = 'UID-SISWA-LAMA'"));
        Assert.Equal("1", Skalar(database, "SELECT aktif FROM kartu WHERE uid = 'UID-SISWA-BARU'"));
    }

    [Fact]
    public void GantiKartu_Guru_UidLamaDanBaruTercatat()
    {
        using var database = BuatDatabase();

        new LayananRegistrasiKartu().GantiKartuHilang(database.Connection, Admin, Waktu, JenisPemilikKartu.Guru, 2, "UID-GURU-BARU");

        var baris = Assert.Single(BacaAudit(database, "ganti_kartu"));
        Assert.Equal("UID-GURU-LAMA->Guru Sudah Kartu", baris.Lama);
        Assert.Equal("UID-GURU-BARU->Guru Sudah Kartu", baris.Baru);
    }

    [Fact]
    public void GantiKartu_PemilikTanpaKartuAktif_NilaiLamaNullBukanTebakan()
    {
        using var database = BuatDatabase();

        new LayananRegistrasiKartu().GantiKartuHilang(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, 2, "UID-SISWA-DUA");

        var baris = Assert.Single(BacaAudit(database, "ganti_kartu"));
        Assert.Null(baris.Lama);
        Assert.Equal("UID-SISWA-DUA->Siswa Belum Satu", baris.Baru);
    }

    [Fact]
    public void GantiKartu_Gagal_TidakAdaBarisAuditDanKartuLamaTetapAktif()
    {
        using var database = BuatDatabase();
        var sebelum = HitungAudit(database);

        // UID-GURU-LAMA sudah dipakai guru 2 -> indeks unik kartu aktif menolak.
        Assert.ThrowsAny<Exception>(() =>
            new LayananRegistrasiKartu().GantiKartuHilang(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, 1, "UID-GURU-LAMA"));

        Assert.Equal(sebelum, HitungAudit(database));
        Assert.Equal("1", Skalar(database, "SELECT aktif FROM kartu WHERE uid = 'UID-SISWA-LAMA'"));
    }

    // ---------------- perubahan master ----------------

    [Fact]
    public void PerbaruiSiswa_HanyaKolomBerubahDicatat_DenganKelasLamaDanBaru()
    {
        using var database = BuatDatabase();
        var data = new DataSiswa("0002", "Siswa Belum Satu B", "L", null, null, "Jl. Baru 5", null, null);

        new LayananSiswa().Perbarui(database.Connection, Admin, Waktu, 2, data, (TahunAjaranId: 1, KelasId: 2));

        var baris = Assert.Single(BacaAudit(database, "perbarui_siswa"));
        Assert.Equal("nama=Siswa Belum Satu; alamat=(kosong); kelas=7A", baris.Lama);
        Assert.Equal("nama=Siswa Belum Satu B; alamat=Jl. Baru 5; kelas=7B", baris.Baru);
        Assert.Equal(2, baris.EntitasId);
        Assert.Equal("Siswa Belum Satu B", Skalar(database, "SELECT nama FROM siswa WHERE id = 2"));
    }

    [Fact]
    public void PerbaruiSiswa_TanpaPerubahan_TercatatJelasTanpaNilaiLamaPalsu()
    {
        using var database = BuatDatabase();
        // Sama dengan isi database; string kosong dan null dianggap sama.
        var data = new DataSiswa("0002", "Siswa Belum Satu", "L", "", null, "  ", null, null);

        new LayananSiswa().Perbarui(database.Connection, Admin, Waktu, 2, data, (1, 1));

        var baris = Assert.Single(BacaAudit(database, "perbarui_siswa"));
        Assert.Null(baris.Lama);
        Assert.Equal(RingkasanPerubahan.TidakAdaPerubahan, baris.Baru);
    }

    [Fact]
    public void PerbaruiSiswa_IdTidakAda_NilaiLamaNullBukanDitebak()
    {
        using var database = BuatDatabase();

        new LayananSiswa().Perbarui(database.Connection, Admin, Waktu, 999, new DataSiswa("9999", "Hantu", "L", null, null, null, null, null));

        var baris = Assert.Single(BacaAudit(database, "perbarui_siswa"));
        Assert.Null(baris.Lama);
        Assert.StartsWith("nama=Hantu", baris.Baru);
    }

    [Fact]
    public void PerbaruiGuru_PangkatDanJabatanBerubah_LamaDanBaruTercatat_NamaTakBerubahTidakDicatat()
    {
        using var database = BuatDatabase();
        database.Execute("UPDATE guru SET pangkat = 'Penata' WHERE id = 1;");

        new LayananGuru().Perbarui(database.Connection, Admin, Waktu, 1, new DataGuru("111", "Guru Belum Kartu", "Penata Tk. I", "Wali Kelas"));

        var baris = Assert.Single(BacaAudit(database, "perbarui_guru"));
        Assert.Equal("pangkat=Penata; jabatan=(kosong)", baris.Lama);
        Assert.Equal("pangkat=Penata Tk. I; jabatan=Wali Kelas", baris.Baru);
    }

    [Fact]
    public void PerbaruiKelas_WaliDicatatDenganNamaGuru_NamaDanTingkat()
    {
        using var database = BuatDatabase();

        new LayananKelas().Perbarui(database.Connection, Admin, Waktu, 1, "7A Melati", 7, waliGuruId: 2);

        var baris = Assert.Single(BacaAudit(database, "perbarui_kelas"));
        Assert.Equal("nama=7A; wali_kelas=(kosong)", baris.Lama);
        Assert.Equal("nama=7A Melati; wali_kelas=Guru Sudah Kartu", baris.Baru);

        new LayananKelas().Perbarui(database.Connection, Admin, Waktu, 1, "7A Melati", 7, waliGuruId: 1);
        var kedua = BacaAudit(database, "perbarui_kelas").Last();
        Assert.Equal("wali_kelas=Guru Sudah Kartu", kedua.Lama);
        Assert.Equal("wali_kelas=Guru Belum Kartu", kedua.Baru);
    }

    [Fact]
    public void Nonaktifkan_SiswaDanGuru_StatusLamaAktifDanBaruDenganAlasan_DiulangTanpaPerubahan()
    {
        using var database = BuatDatabase();
        var siswa = new LayananSiswa();
        var guru = new LayananGuru();

        siswa.TandaiTidakAktif(database.Connection, Admin, Waktu, 2, "pindah");
        guru.TandaiTidakAktif(database.Connection, Admin, Waktu, 1, "pensiun");
        siswa.TandaiTidakAktif(database.Connection, Admin, Waktu, 2, "pindah");

        var barisSiswa = BacaAudit(database, "nonaktifkan_siswa");
        Assert.Equal("status=aktif", barisSiswa[0].Lama);
        Assert.Equal("status=nonaktif (pindah)", barisSiswa[0].Baru);
        Assert.Null(barisSiswa[1].Lama);
        Assert.Equal(RingkasanPerubahan.TidakAdaPerubahan, barisSiswa[1].Baru);

        var barisGuru = Assert.Single(BacaAudit(database, "nonaktifkan_guru"));
        Assert.Equal("status=aktif", barisGuru.Lama);
        Assert.Equal("status=nonaktif (pensiun)", barisGuru.Baru);
    }

    [Fact]
    public void UnggahFoto_SiswaDanGuru_PathLamaDanBaruTercatat()
    {
        using var database = BuatDatabase();
        var siswa = new LayananSiswa();

        siswa.SimpanFoto(database.Connection, Admin, Waktu, 2, "0002.png");
        siswa.SimpanFoto(database.Connection, Admin, Waktu, 2, "0002-baru.png");
        new LayananGuru().SimpanFoto(database.Connection, Admin, Waktu, 1, "111.png");

        var barisSiswa = BacaAudit(database, "unggah_foto_siswa");
        Assert.Equal("foto_path=(kosong)", barisSiswa[0].Lama);
        Assert.Equal("foto_path=0002.png", barisSiswa[0].Baru);
        Assert.Equal("foto_path=0002.png", barisSiswa[1].Lama);
        Assert.Equal("foto_path=0002-baru.png", barisSiswa[1].Baru);

        var barisGuru = Assert.Single(BacaAudit(database, "unggah_foto_guru"));
        Assert.Equal("foto_path=(kosong)", barisGuru.Lama);
        Assert.Equal("foto_path=111.png", barisGuru.Baru);
    }

    // ---------------- audit lama tidak disentuh ----------------

    [Fact]
    public void BarisAuditLama_TidakBerubahSetelahOperasiBaru()
    {
        using var database = BuatDatabase();
        // audit_uid diisi manual (bukan lewat RepositoriAudit.Catat) --
        // meniru baris LAMA format pra-migrasi 003, yang di dunia nyata
        // sudah dibackfill jadi fingerprint unik oleh migrasi itu sendiri,
        // bukan dibiarkan kosong (kosong akan bentrok UNIQUE INDEX
        // ix_audit_uid kalau lebih dari satu baris memakainya).
        database.Execute("""
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan, audit_uid)
            VALUES ('2026-09-01T08:00:00.0000000', 1, 'perbarui_siswa', 'siswa', 2, NULL, 'Siswa Belum Satu', NULL, 'uji-lama-1');
            INSERT INTO audit (waktu, akun_id, aksi, entitas, entitas_id, nilai_lama, nilai_baru, atas_permintaan, audit_uid)
            VALUES ('2026-09-02T08:00:00.0000000', 1, 'ganti_kartu', 'kartu', 1, NULL, 'UID-LAMA-SEKALI', NULL, 'uji-lama-2');
            """);
        var snapshot = Skalar(database, "SELECT group_concat(id || '|' || waktu || '|' || akun_id || '|' || aksi || '|' || entitas || '|' || IFNULL(entitas_id,'') || '|' || IFNULL(nilai_lama,'~') || '|' || IFNULL(nilai_baru,'~') || '|' || IFNULL(atas_permintaan,'~'), ';') FROM (SELECT * FROM audit ORDER BY id)");

        new LayananSiswa().Perbarui(database.Connection, Admin, Waktu, 2, new DataSiswa("0002", "Nama Lain", "L", null, null, null, null, null));
        new LayananRegistrasiKartu().GantiKartuHilang(database.Connection, Admin, Waktu, JenisPemilikKartu.Siswa, 1, "UID-X");

        var sesudah = Skalar(database, "SELECT group_concat(id || '|' || waktu || '|' || akun_id || '|' || aksi || '|' || entitas || '|' || IFNULL(entitas_id,'') || '|' || IFNULL(nilai_lama,'~') || '|' || IFNULL(nilai_baru,'~') || '|' || IFNULL(atas_permintaan,'~'), ';') FROM (SELECT * FROM audit WHERE id <= 2 ORDER BY id)");
        Assert.Equal(snapshot, sesudah);
        Assert.Equal(4, HitungAudit(database));
    }

    // ---------------- jalur tulis dan pembanding ----------------

    [Fact]
    public void JalankanDenganCatatan_PerubahanGagal_TidakMenulisAudit()
    {
        using var database = BuatDatabase();
        var sebelum = HitungAudit(database);

        Assert.Throws<InvalidOperationException>(() =>
            new JalurPerubahanData().JalankanDenganCatatan(
                database.Connection, Admin, Waktu, "uji", "uji",
                _ => throw new InvalidOperationException("gagal di tengah")));

        Assert.Equal(sebelum, HitungAudit(database));
    }

    [Fact]
    public void JalankanDenganCatatan_Berhasil_MenulisEntitasLamaBaruDanAtasPermintaan()
    {
        using var database = BuatDatabase();

        new JalurPerubahanData().JalankanDenganCatatan(
            database.Connection, Admin, Waktu, "uji", "uji",
            _ => new CatatanPerubahan(7, "a", "b"), atasPermintaan: "Bu Wali");

        var baris = Assert.Single(BacaAudit(database, "uji"));
        Assert.Equal((7, "a", "b"), (baris.EntitasId, baris.Lama, baris.Baru));
        Assert.Equal("Bu Wali", Skalar(database, "SELECT atas_permintaan FROM audit WHERE aksi = 'uji'"));
    }

    [Fact]
    public void RingkasanPerubahan_Bandingkan_HanyaKolomBerubah_NormalisasiKosong()
    {
        var hasil = RingkasanPerubahan.Bandingkan(
            5,
            new RingkasanPerubahan.Kolom("a", "x", "x"),
            new RingkasanPerubahan.Kolom("b", null, ""),
            new RingkasanPerubahan.Kolom("c", " 1 ", "2"),
            new RingkasanPerubahan.Kolom("d", "lama", null));

        Assert.Equal(5, hasil.EntitasId);
        Assert.Equal("c=1; d=lama", hasil.NilaiLama);
        Assert.Equal("c=2; d=(kosong)", hasil.NilaiBaru);

        var sama = RingkasanPerubahan.Bandingkan(5, new RingkasanPerubahan.Kolom("a", "x", " x "));
        Assert.Null(sama.NilaiLama);
        Assert.Equal(RingkasanPerubahan.TidakAdaPerubahan, sama.NilaiBaru);

        var hanyaBaru = RingkasanPerubahan.HanyaBaru(5, new RingkasanPerubahan.Kolom("a", "tak dipakai", "baru"));
        Assert.Null(hanyaBaru.NilaiLama);
        Assert.Equal("a=baru", hanyaBaru.NilaiBaru);
    }
}
