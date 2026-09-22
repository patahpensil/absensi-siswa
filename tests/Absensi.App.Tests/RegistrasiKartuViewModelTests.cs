using Absensi.App.RegistrasiKartu;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 8, "Selesai bila": tidak ada satu pun baris
// absensi tertulis selama mode registrasi aktif — dicek lewat
// GerbangViewModelTests. Di sini: kursor per kelas, penolakan UID
// lintas kelas/master, duplikat dalam sesi yang sama, dan simpan atomik
// (PRD §16.5, dikonfirmasi pemilik proyek 2026-09-18).
public sealed class RegistrasiKartuViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    private static RegistrasiKartuViewModel BuatViewModel(MigratedDatabase database, StatusModeRegistrasi? status = null) =>
        new(database.Connection, Admin, new PenyediaWaktuUji(Waktu), status ?? new StatusModeRegistrasi(), tahunAjaranId: 1);

    [Fact]
    public void Konstruksi_MengaktifkanModeRegistrasi()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var status = new StatusModeRegistrasi();

        BuatViewModel(database, status);

        Assert.True(status.Aktif);
    }

    [Fact]
    public void KelasTerpilih_LompatKeSiswaPertamaYangBelumPunyaKartu()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);

        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");

        // Diurutkan per nama (RepositoriSiswa.DaftarUntukTahunAjaran, sama
        // seperti layar Siswa) — "Dua" < "Satu" secara alfabetis.
        Assert.Equal("Siswa Belum Dua", viewModel.SiswaSekarang?.Nama);
        Assert.Equal("7A: 1 dari 3 terdaftar", viewModel.ProgresTeks);
    }

    [Fact]
    public void TapUid_UidBebas_TerdaftarDanKursorLompatKeBerikutnya()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");

        viewModel.TapUid("UID-BARU-1");

        Assert.Equal("Siswa Belum Satu", viewModel.SiswaSekarang?.Nama);
        Assert.Equal("7A: 2 dari 3 terdaftar", viewModel.ProgresTeks);
        Assert.Null(viewModel.PesanGalat);
        Assert.True(viewModel.SimpanCommand.CanExecute(null));
    }

    [Fact]
    public void TapUid_UidSudahDipakaiKelasLain_DitolakDenganNamaDanKelasPemilik()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        var siswaSebelumTap = viewModel.SiswaSekarang;

        viewModel.TapUid("UID-KELAS-LAIN");

        Assert.Equal("Kartu ini sudah dipakai Siswa Kelas Lain di kelas 7B.", viewModel.PesanGalat);
        Assert.Equal(siswaSebelumTap, viewModel.SiswaSekarang);
        Assert.False(viewModel.SimpanCommand.CanExecute(null));
    }

    [Fact]
    public void TapUid_UidSudahDipakaiSesiIni_DitolakTanpaMenyentuhDatabase()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");

        viewModel.TapUid("UID-BARU-1");
        var siswaSetelahTapPertama = viewModel.SiswaSekarang;
        viewModel.TapUid("UID-BARU-1");

        Assert.Equal("Kartu ini sudah dipakai Siswa Belum Dua pada sesi ini.", viewModel.PesanGalat);
        Assert.Equal(siswaSetelahTapPertama, viewModel.SiswaSekarang);
    }

    [Fact]
    public void Simpan_MenyimpanSeluruhSesiDanMematikanModeRegistrasi()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var status = new StatusModeRegistrasi();
        var viewModel = BuatViewModel(database, status);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.TapUid("UID-BARU-1");
        viewModel.TapUid("UID-BARU-2");

        var selesaiDipicu = false;
        viewModel.Selesai += (_, _) => selesaiDipicu = true;
        viewModel.SimpanCommand.Execute(null);
        viewModel.MatikanModeRegistrasi();

        Assert.True(selesaiDipicu);
        Assert.False(status.Aktif);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM kartu WHERE uid IN ('UID-BARU-1', 'UID-BARU-2') AND aktif = 1;";
        Assert.Equal(2, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void Batal_TidakMenyimpanApaPunKeDatabase()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.TapUid("UID-BARU-1");

        viewModel.Batal();

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM kartu WHERE uid = 'UID-BARU-1';";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    // UAT Bug #11 (2026-09-22): dropdown Kelas kosong di UAT meski data
    // kelas sudah ada di panel Kelas. Audit kode membuktikan konstruktor
    // SUDAH memuat ulang dari database setiap ViewModel dibuat (tidak ada
    // cache) memakai tahunAjaranId yang identik dengan panel Kelas — tes
    // di bawah membuktikan itu langsung, plus 3 kondisi yang sebelumnya
    // tidak dibedakan sama sekali: isolasi antar tahun ajaran, kelas baru
    // tanpa restart, dan tahun ajaran aktif tanpa kelas sama sekali.

    [Fact]
    public void Konstruksi_TahunAjaranAktifDenganKelas_DaftarKelasBenarDanTidakMenandakanKosong()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();

        var viewModel = BuatViewModel(database);

        Assert.Equal(new[] { "7A", "7B" }, viewModel.DaftarKelas.Select(k => k.Nama));
        Assert.False(viewModel.TahunAjaranTanpaKelas);
        Assert.False(viewModel.GagalMuatKelas);
        Assert.Null(viewModel.PesanMuatKelasGagal);
    }

    [Fact]
    public void Konstruksi_KelasDariTahunAjaranLain_TidakIkutTampil()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        // Tahun ajaran KEDUA dengan kelasnya sendiri — bukan aktif, tapi
        // ada di database yang sama (skenario nyata: sekolah sudah lewat
        // satu pergantian tahun ajaran).
        database.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (2, '2025/2026', 'genap', '2026-01-01', '2026-06-19', 0);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (99, 2, '9Z-LamaTahun', 9);
            """);

        var viewModel = BuatViewModel(database);

        Assert.DoesNotContain(viewModel.DaftarKelas, k => k.Nama == "9Z-LamaTahun");
        Assert.Equal(2, viewModel.DaftarKelas.Count);
    }

    [Fact]
    public void BukaUlang_KelasBaruDitambahkanSetelahDialogPertama_LangsungTampilTanpaRestart()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModelPertama = BuatViewModel(database);
        Assert.Equal(2, viewModelPertama.DaftarKelas.Count);

        // Simulasi: operator membuka menu Kelas, menambah kelas baru,
        // TANPA merestart aplikasi — RegistrasiKartuWindow di App.xaml.cs
        // membuat RegistrasiKartuViewModel baru setiap dialog dibuka, jadi
        // simulasinya di sini adalah membuat instance BARU dengan koneksi
        // yang sama (bukan reload di ViewModel yang sama).
        database.Execute("""
            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (3, 1, '7C-Baru', 7);
            """);

        var viewModelKedua = BuatViewModel(database);

        Assert.Equal(3, viewModelKedua.DaftarKelas.Count);
        Assert.Contains(viewModelKedua.DaftarKelas, k => k.Nama == "7C-Baru");
    }

    [Fact]
    public void Konstruksi_TahunAjaranAktifTanpaKelas_TahunAjaranTanpaKelasBenarBukanGagalMuat()
    {
        using var database = new MigratedDatabase();
        // Sengaja HANYA tahun_ajaran, tanpa satu pun baris kelas — beda
        // dari "gagal query" (harus tetap bisa dibedakan, lihat tes di
        // bawah).
        database.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);
            """);

        var viewModel = BuatViewModel(database);

        Assert.Empty(viewModel.DaftarKelas);
        Assert.True(viewModel.TahunAjaranTanpaKelas);
        Assert.False(viewModel.GagalMuatKelas);
        Assert.Null(viewModel.PesanMuatKelasGagal);
    }

    // UAT 2026-09-23 (putaran KETIGA Bug #11): regresi nyata ditemukan —
    // TidakAdaSiswaSekarang sebelumnya hanya memeriksa "SiswaSekarang is
    // null", yang TRUE sejak ViewModel dikonstruksi (sebelum kelas apa pun
    // dipilih) — pesan "Kelas ini belum punya siswa" tampil PREMATUR
    // sebelum operator sempat memilih kelas, membingungkan operator dan
    // dilaporkan sebagai "ComboBox kosong/tidak bisa pilih kelas".
    [Fact]
    public void Konstruksi_SebelumKelasDipilih_TidakAdaSiswaSekarangHarusFalse()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();

        var viewModel = BuatViewModel(database);

        Assert.False(viewModel.TidakAdaSiswaSekarang, "Pesan 'kelas belum punya siswa' tidak boleh tampil sebelum kelas dipilih.");
        Assert.Null(viewModel.KelasTerpilih);
        Assert.Null(viewModel.SiswaSekarang);
    }

    [Fact]
    public void KelasTerpilih_MengesetProperti_MemicuTidakAdaSiswaSekarangBerubah()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        database.Execute("INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (3, 1, '7Z-Kosong', 7);");
        var viewModel = BuatViewModel(database);
        Assert.False(viewModel.TidakAdaSiswaSekarang);

        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7Z-Kosong");

        Assert.True(viewModel.TidakAdaSiswaSekarang, "Setelah kelas kosong benar-benar dipilih, pesan wajib tampil.");
        Assert.Equal("7Z-Kosong", viewModel.KelasTerpilih.Nama);
    }

    // UAT 2026-09-23 (putaran kedua Bug #11): "Registrasi harus tetap
    // menampilkan kelas walaupun kelas tersebut belum memiliki siswa" +
    // "memilih kelas tanpa siswa menampilkan kondisi 'belum ada siswa',
    // bukan menghilangkan kelas".
    [Fact]
    public void KelasTerpilih_KelasTanpaSiswaSamaSekali_TetapMunculDiDropdownDanPesanBerbedaDariSudahTerdaftarSemua()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        // Kelas KETIGA, sengaja tanpa satu pun baris siswa/siswa_kelas.
        database.Execute("INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (3, 1, '7Z-Kosong', 7);");
        var viewModel = BuatViewModel(database);

        Assert.Contains(viewModel.DaftarKelas, k => k.Nama == "7Z-Kosong");

        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7Z-Kosong");

        Assert.Null(viewModel.SiswaSekarang);
        Assert.True(viewModel.TidakAdaSiswaSekarang);
        Assert.Equal("Kelas ini belum punya siswa terdaftar di sistem.", viewModel.PesanSiswaKosong);
        Assert.Equal("7Z-Kosong: 0 dari 0 terdaftar", viewModel.ProgresTeks);
        // Kelas lain tidak ikut hilang dari dropdown gara-gara memilih kelas kosong.
        Assert.Equal(3, viewModel.DaftarKelas.Count);
    }

    [Fact]
    public void KelasTerpilih_SeluruhSiswaSudahTerdaftar_PesanBerbedaDariKelasKosong()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.TapUid("UID-BARU-1");
        viewModel.TapUid("UID-BARU-2");

        Assert.Null(viewModel.SiswaSekarang);
        Assert.True(viewModel.TidakAdaSiswaSekarang);
        Assert.Equal("Seluruh siswa di kelas ini sudah terdaftar.", viewModel.PesanSiswaKosong);
    }

    [Fact]
    public void Konstruksi_QueryKelasGagal_GagalMuatKelasBenarBukanDianggapKosong()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        // Paksa query kelas di konstruktor GAGAL (bukan kembali kosong)
        // supaya bisa membuktikan dua kondisi ini menghasilkan sinyal
        // berbeda ke UI (CLAUDE.md §5: jangan menelan galat diam-diam).
        database.Connection.Close();

        var viewModel = BuatViewModel(database);

        Assert.Empty(viewModel.DaftarKelas);
        Assert.True(viewModel.GagalMuatKelas);
        Assert.False(viewModel.TahunAjaranTanpaKelas);
        Assert.NotNull(viewModel.PesanMuatKelasGagal);
    }
}
