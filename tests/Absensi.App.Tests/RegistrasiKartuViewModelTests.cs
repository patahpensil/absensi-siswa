using Absensi.App.MasterData;
using Absensi.App.RegistrasiKartu;
using Absensi.Data;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// UAT Bug #11 (2026-09-23, REDESAIN FUNDAMENTAL): menggantikan alur
// antrean-otomatis lama. Operator memilih kelas, lalu memilih SATU siswa
// eksplisit dari dropdown baru, lalu tap kartu (ditahan sementara sampai
// Simpan), lalu Simpan — satu klik menghubungkan tepat satu siswa dengan
// tepat satu kartu. Lihat RegistrasiKartuViewModel.cs untuk detail
// keputusan desain dan docs/PRD.md §16.5 (direvisi) untuk keputusan produk.
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

    // TEST WAJIB #1: "Dua siswa dalam kelas yang sama muncul di dropdown
    // Nama siswa."
    [Fact]
    public void KelasTerpilih_DuaSiswaDalamKelasSama_KeduanyaMunculDiDaftarSiswaKelas()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);

        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");

        Assert.Equal(3, viewModel.DaftarSiswaKelas.Count);
        Assert.Contains(viewModel.DaftarSiswaKelas, s => s.Nama == "Siswa Sudah Kartu");
        Assert.Contains(viewModel.DaftarSiswaKelas, s => s.Nama == "Siswa Belum Satu");
        Assert.Contains(viewModel.DaftarSiswaKelas, s => s.Nama == "Siswa Belum Dua");
        // Siswa kelas LAIN tidak ikut tampil.
        Assert.DoesNotContain(viewModel.DaftarSiswaKelas, s => s.Nama == "Siswa Kelas Lain");
        Assert.False(viewModel.KelasBelumPunyaSiswa);
    }

    // TEST WAJIB #2: "Siswa yang baru ditambahkan muncul tanpa restart."
    // Simulasi: dialog dibuka ulang (instance ViewModel baru, koneksi
    // sama — persis App.BukaRegistrasiKartu() yang membuat ViewModel baru
    // setiap dialog dibuka, tanpa cache) SETELAH siswa baru ditambahkan
    // lewat panel Siswa.
    [Fact]
    public void KelasTerpilih_SiswaBaruDitambahkanSetelahDialogPertama_MunculDiDialogBaruTanpaRestart()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModelPertama = BuatViewModel(database);
        viewModelPertama.KelasTerpilih = viewModelPertama.DaftarKelas.Single(k => k.Nama == "7A");
        Assert.Equal(3, viewModelPertama.DaftarSiswaKelas.Count);

        database.Execute("""
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (5, '0005', 'Siswa Baru', 'L', 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (5, 1, 1);
            """);

        var viewModelKedua = BuatViewModel(database);
        viewModelKedua.KelasTerpilih = viewModelKedua.DaftarKelas.Single(k => k.Nama == "7A");

        Assert.Equal(4, viewModelKedua.DaftarSiswaKelas.Count);
        Assert.Contains(viewModelKedua.DaftarSiswaKelas, s => s.Nama == "Siswa Baru");
    }

    // TEST WAJIB #3: "Siswa tanpa kartu tampil 'Belum terdaftar'." — diuji
    // di dua lapisan: data model (KartuUid null) dan converter UI yang
    // sungguhan dipakai SiswaView.xaml.
    [Fact]
    public void DaftarSiswaKelas_SiswaTanpaKartu_KartuUidNull()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");

        var siswaBelumKartu = viewModel.DaftarSiswaKelas.Single(s => s.Nama == "Siswa Belum Satu");

        Assert.Null(siswaBelumKartu.KartuUid);
        Assert.Equal("Belum terdaftar", new Absensi.App.Converters.KartuUidKeTeksConverter()
            .Convert(siswaBelumKartu.KartuUid, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void DaftarSiswaKelas_SiswaSudahPunyaKartu_KartuUidTerisiUidSebenarnya()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");

        var siswaSudahKartu = viewModel.DaftarSiswaKelas.Single(s => s.Nama == "Siswa Sudah Kartu");

        Assert.Equal("UID-SISWA-LAMA", siswaSudahKartu.KartuUid);
    }

    // TEST WAJIB #4: "UID tersedia dapat disimpan kepada siswa terpilih."
    // Sekaligus membuktikan: (a) pesan "terdaftar" TIDAK tampil sebelum
    // Simpan (poin wajib "Jangan tampilkan pesan terdaftar sebelum
    // transaksi berhasil"), (b) verifikasi ulang pasca-commit lewat query
    // database langsung, (c) satu klik Simpan = satu baris kartu.
    [Fact]
    public void TapUidLaluSimpan_UidTersedia_TersimpanKeDatabaseDanPesanBerhasilTampil()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.SiswaTerpilih = viewModel.DaftarSiswaKelas.Single(s => s.Nama == "Siswa Belum Satu");

        viewModel.TapUid("UID-BARU-REDESAIN");

        // Sebelum Simpan: UID sementara tampil, TAPI belum ada pesan
        // "berhasil"/"terdaftar" apa pun.
        Assert.Equal("UID-BARU-REDESAIN", viewModel.UidSementara);
        Assert.Null(viewModel.PesanGalat);
        Assert.Null(viewModel.PesanStatus);
        Assert.True(viewModel.SimpanCommand.CanExecute(null));

        using (var commandSebelum = database.Connection.CreateCommand())
        {
            commandSebelum.CommandText = "SELECT COUNT(*) FROM kartu WHERE uid = 'UID-BARU-REDESAIN';";
            Assert.Equal(0, Convert.ToInt32(commandSebelum.ExecuteScalar()));
        }

        viewModel.SimpanCommand.Execute(null);

        // Sesudah Simpan: pesan berhasil BARU tampil sekarang, UID
        // sementara dikosongkan kembali (siap untuk siswa berikutnya).
        Assert.Null(viewModel.PesanGalat);
        Assert.Equal("Kartu RFID UID-BARU-REDESAIN berhasil disimpan untuk Siswa Belum Satu.", viewModel.PesanStatus);
        Assert.Null(viewModel.UidSementara);
        Assert.False(viewModel.SimpanCommand.CanExecute(null));

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM kartu WHERE uid = 'UID-BARU-REDESAIN' AND pemilik_tipe = 'siswa' AND aktif = 1;";
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }

    // TEST WAJIB #5: "UID duplikat ditolak." — tidak mengisi UID sementara,
    // tidak mengaktifkan Simpan (poin wajib Tap RFID).
    [Fact]
    public void TapUid_UidSudahDipakaiSiswaLain_DitolakUidSementaraTidakTerisi()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.SiswaTerpilih = viewModel.DaftarSiswaKelas.Single(s => s.Nama == "Siswa Belum Satu");

        viewModel.TapUid("UID-KELAS-LAIN");

        Assert.Equal("Kartu ini sudah dipakai Siswa Kelas Lain di kelas 7B.", viewModel.PesanGalat);
        Assert.Null(viewModel.UidSementara);
        Assert.False(viewModel.SimpanCommand.CanExecute(null));
    }

    // TEST WAJIB #6: "Siswa yang sudah memiliki kartu tidak menerima kartu
    // kedua." — dicek terhadap DATABASE (bukan properti KartuUid yang
    // dimuat di awal), UID sementara tidak terisi, Simpan tidak aktif.
    [Fact]
    public void TapUid_SiswaTerpilihSudahPunyaKartuAktif_DitolakUidSementaraTidakTerisi()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.SiswaTerpilih = viewModel.DaftarSiswaKelas.Single(s => s.Nama == "Siswa Sudah Kartu");

        viewModel.TapUid("UID-KARTU-KEDUA");

        Assert.Contains("sudah memiliki kartu aktif", viewModel.PesanGalat);
        Assert.Contains("UID-SISWA-LAMA", viewModel.PesanGalat);
        Assert.Null(viewModel.UidSementara);
        Assert.False(viewModel.SimpanCommand.CanExecute(null));

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM kartu WHERE uid = 'UID-KARTU-KEDUA';";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    // TEST WAJIB #7: "Data tetap ada setelah DbContext/aplikasi dibuka
    // ulang." — menutup koneksi yang menulis, membuka KONEKSI BARU ke
    // BERKAS yang sama (persis restart aplikasi), baru query. TEST WAJIB
    // #8 ("Panel Siswa menampilkan UID setelah refresh") digabung di sini
    // — SiswaViewModel BARU lewat koneksi baru itu juga harus menunjukkan
    // UID yang sama.
    [Fact]
    public void Simpan_SetelahKoneksiDitutupDanDibukaUlang_KartuTetapAdaDanPanelSiswaMenampilkanUid()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.SiswaTerpilih = viewModel.DaftarSiswaKelas.Single(s => s.Nama == "Siswa Belum Dua");
        viewModel.TapUid("UID-DURABEL-REDESAIN");
        viewModel.SimpanCommand.Execute(null);

        var pathBerkas = database.PathBerkas;
        database.Connection.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        using var koneksiBaru = SqliteConnectionFactory.Open(pathBerkas);
        using var command = koneksiBaru.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM kartu WHERE uid = 'UID-DURABEL-REDESAIN' AND aktif = 1;";
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));

        var siswaViewModel = new SiswaViewModel(koneksiBaru, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var siswa = siswaViewModel.Daftar.Single(s => s.Nama == "Siswa Belum Dua");
        Assert.Equal("UID-DURABEL-REDESAIN", siswa.KartuUid);
    }

    // TEST WAJIB #9: "Kegagalan database tidak menampilkan pesan berhasil."
    // Dipaksa gagal dengan menutup koneksi TEPAT sebelum Simpan (mensimulasikan
    // kegagalan database) — PesanStatus TIDAK BOLEH terisi, PesanGalat wajib
    // menjelaskan kegagalan.
    [Fact]
    public void Simpan_KoneksiDatabaseGagal_TidakMenampilkanPesanBerhasil()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.SiswaTerpilih = viewModel.DaftarSiswaKelas.Single(s => s.Nama == "Siswa Belum Satu");
        viewModel.TapUid("UID-GAGAL-SIMULASI");

        database.Connection.Close();

        var exception = Record.Exception(() => viewModel.SimpanCommand.Execute(null));

        Assert.Null(exception); // Simpan() sendiri menangkap exception, tidak melempar ke UI.
        Assert.Null(viewModel.PesanStatus);
        Assert.NotNull(viewModel.PesanGalat);
    }

    // TEST WAJIB #10 ("Tap registrasi tidak membuat absensi") SUDAH
    // dicakup GerbangViewModelTests.ProsesTap_ModeRegistrasiAktif_
    // TidakMenulisAbsensi — StatusModeRegistrasi.Aktifkan() di konstruktor
    // ViewModel ini TIDAK diubah oleh redesain (lihat Konstruksi_
    // MengaktifkanModeRegistrasi di atas), jadi perilaku itu tetap
    // terjamin tanpa duplikasi test.

    [Fact]
    public void TapUid_TanpaSiswaTerpilih_PesanMintaPilihSiswa()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");

        viewModel.TapUid("UID-TANPA-SISWA");

        Assert.Equal("Pilih siswa terlebih dahulu.", viewModel.PesanGalat);
        Assert.Null(viewModel.UidSementara);
    }

    [Fact]
    public void SiswaTerpilih_GantiSiswa_UidSementaraSebelumnyaDibatalkan()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.SiswaTerpilih = viewModel.DaftarSiswaKelas.Single(s => s.Nama == "Siswa Belum Satu");
        viewModel.TapUid("UID-AKAN-DIBATALKAN");
        Assert.Equal("UID-AKAN-DIBATALKAN", viewModel.UidSementara);

        viewModel.SiswaTerpilih = viewModel.DaftarSiswaKelas.Single(s => s.Nama == "Siswa Belum Dua");

        Assert.Null(viewModel.UidSementara);
        Assert.False(viewModel.SimpanCommand.CanExecute(null));
    }

    [Fact]
    public void Batal_TidakMenyimpanUidSementaraApaPun()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = BuatViewModel(database);
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7A");
        viewModel.SiswaTerpilih = viewModel.DaftarSiswaKelas.Single(s => s.Nama == "Siswa Belum Satu");
        viewModel.TapUid("UID-BATAL-TIDAK-TERSIMPAN");

        var selesaiDipicu = false;
        viewModel.Selesai += (_, _) => selesaiDipicu = true;
        viewModel.Batal();

        Assert.True(selesaiDipicu);

        using var command = database.Connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM kartu WHERE uid = 'UID-BATAL-TIDAK-TERSIMPAN';";
        Assert.Equal(0, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void KelasTerpilih_KelasTanpaSiswaSamaSekali_KelasBelumPunyaSiswaBenar()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        database.Execute("INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (3, 1, '7Z-Kosong', 7);");
        var viewModel = BuatViewModel(database);

        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single(k => k.Nama == "7Z-Kosong");

        Assert.True(viewModel.KelasBelumPunyaSiswa);
        Assert.Empty(viewModel.DaftarSiswaKelas);
    }

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
    public void Konstruksi_QueryKelasGagal_GagalMuatKelasBenarBukanDianggapKosong()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        database.Connection.Close();

        var viewModel = BuatViewModel(database);

        Assert.Empty(viewModel.DaftarKelas);
        Assert.True(viewModel.GagalMuatKelas);
        Assert.False(viewModel.TahunAjaranTanpaKelas);
        Assert.NotNull(viewModel.PesanMuatKelasGagal);
    }
}
