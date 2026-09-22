using System.IO;
using System.Windows.Media.Imaging;
using Absensi.App;
using Absensi.App.MasterData;
using Absensi.Data;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// Tugas 7: hanya jalur yang tidak membuka dialog WPF (ShowDialog) yang
// diuji otomatis di sini — Impor Excel/Foto dan Nonaktifkan butuh jendela
// nyata, sama seperti keterbatasan pengujian layar gerbang di Tugas 6.
public sealed class MasterDataViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    [Fact]
    public void SiswaViewModel_Tambah_MunculDiDaftar()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var jumlahAwal = viewModel.Daftar.Count;

        viewModel.Nisn = "0099999999";
        viewModel.Nama = "Siswa Baru";
        viewModel.JenisKelamin = "L";
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single();
        viewModel.SimpanCommand.Execute(null);

        Assert.Equal(jumlahAwal + 1, viewModel.Daftar.Count);
        Assert.Contains(viewModel.Daftar, s => s.Nama == "Siswa Baru");
        Assert.Equal(string.Empty, viewModel.Nama);
    }

    [Fact]
    public void SiswaViewModel_TanpaKelasTerpilih_SimpanCommandTidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1)
        {
            Nisn = "0099999999",
            Nama = "Siswa Baru",
        };

        Assert.False(viewModel.SimpanCommand.CanExecute(null));
    }

    // A-2 (Bug #1, docs/BUG-REPORT-UAT-2026-09-20.md): urutan pengisian field
    // di UAT adalah NISN -> Nama -> Kelas (Kelas terakhir). KelasTerpilih
    // sebelumnya tidak memicu RaiseCanExecuteChanged, sehingga WPF tidak
    // pernah requery tombol Simpan walau BisaSimpan() sudah true -- tombol
    // tetap tampak nonaktif. Test ini menutup celah yang membuat 389 test
    // lama tetap hijau meski bug ini nyata (test lain memanggil
    // Execute/CanExecute langsung, tidak lewat siklus CanExecuteChanged).
    [Fact]
    public void SiswaViewModel_KelasDipilihTerakhir_SimpanCommandCanExecuteChangedTerpicu()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);

        var terpicu = false;
        viewModel.SimpanCommand.CanExecuteChanged += (_, _) => terpicu = true;

        viewModel.Nisn = "0099999999";
        viewModel.Nama = "Siswa Baru";
        // Kelas dipilih terakhir -- persis urutan reproduksi UAT.
        viewModel.KelasTerpilih = viewModel.DaftarKelas.Single();

        Assert.True(terpicu, "KelasTerpilih setter harus memicu SimpanCommand.RaiseCanExecuteChanged().");
        Assert.True(viewModel.SimpanCommand.CanExecute(null));
    }

    // Tahap E-2 (docs/INVESTIGASI-CRASH-2026-09-21.md §9): GantiKartuCommand
    // melempar NullReferenceException saat WPF memanggil CanExecute(null)
    // sesaat DataGrid Siswa re-layout — predikat sekarang menjaga s is not
    // null sebelum mengakses PunyaKartu.
    [Fact]
    public void SiswaViewModel_GantiKartuCommand_CanExecuteNull_KembalikanFalseTanpaMelempar()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);

        var exception = Record.Exception(() => viewModel.GantiKartuCommand.CanExecute(null));

        Assert.Null(exception);
        Assert.False(viewModel.GantiKartuCommand.CanExecute(null));
    }

    [Fact]
    public void SiswaViewModel_GantiKartuCommand_SiswaPunyaKartu_CanExecuteTrue()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var siswaPunyaKartu = viewModel.Daftar.Single(s => s.Id == 1);

        Assert.True(siswaPunyaKartu.PunyaKartu);
        Assert.True(viewModel.GantiKartuCommand.CanExecute(siswaPunyaKartu));
    }

    [Fact]
    public void SiswaViewModel_GantiKartuCommand_SiswaBelumPunyaKartu_CanExecuteFalse()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var siswaBelumKartu = viewModel.Daftar.Single(s => s.Nama == "Siswa Belum Satu");

        Assert.False(siswaBelumKartu.PunyaKartu);
        Assert.False(viewModel.GantiKartuCommand.CanExecute(siswaBelumKartu));
    }

    [Fact]
    public void SiswaViewModel_GantiKartuCommand_TransisiValidNullValid_TidakMelemparDanHasilKonsisten()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var siswaPunyaKartu = viewModel.Daftar.Single(s => s.Id == 1);

        Assert.True(viewModel.GantiKartuCommand.CanExecute(siswaPunyaKartu));
        Assert.False(viewModel.GantiKartuCommand.CanExecute(null));
        Assert.True(viewModel.GantiKartuCommand.CanExecute(siswaPunyaKartu));
    }

    [Fact]
    public void GuruViewModel_Tambah_MunculDiDaftar()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new GuruViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var jumlahAwal = viewModel.Daftar.Count;

        viewModel.Nip = "999999";
        viewModel.Nama = "Guru Baru";
        viewModel.SimpanCommand.Execute(null);

        Assert.Equal(jumlahAwal + 1, viewModel.Daftar.Count);
        Assert.Contains(viewModel.Daftar, g => g.Nama == "Guru Baru");
    }

    // Bug #3 (Master Bug & UI/UX Register A-20 / UAT 2026-09-22 dan 2026-09-23):
    // SiswaViewModel di-cache lewat "??=" di MainWindowViewModel — constructor
    // (dan pemuatan DaftarKelas di dalamnya) hanya jalan SEKALI per sesi.
    // MuatUlangDaftarKelas() (dipanggil MainWindowViewModel setiap menu
    // "Siswa" dipilih) memperbaiki ini. Tes di bawah memverifikasi method
    // itu langsung, tanpa perlu membangun ulang seluruh MainWindow/App.

    [Fact]
    public void SiswaViewModel_MuatUlangDaftarKelas_KelasBaruMunculTanpaKonstruksiUlang()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        Assert.DoesNotContain(viewModel.DaftarKelas, k => k.Nama == "7Z-Baru");

        // Simulasi: operator menambah kelas lewat panel Kelas TANPA
        // menutup/membuka ulang panel Siswa (yang tetap dipakai dari cache).
        database.Execute("INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (2, 1, '7Z-Baru', 7);");

        viewModel.MuatUlangDaftarKelas();

        Assert.Contains(viewModel.DaftarKelas, k => k.Nama == "7Z-Baru");
        Assert.Contains(viewModel.DaftarKelasFilter, k => k.Nama == "7Z-Baru");
    }

    [Fact]
    public void SiswaViewModel_MuatUlangDaftarKelasBerulang_TidakMenghasilkanDuplikat()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);

        viewModel.MuatUlangDaftarKelas();
        viewModel.MuatUlangDaftarKelas();
        viewModel.MuatUlangDaftarKelas();

        Assert.Equal(1, viewModel.DaftarKelas.Count(k => k.Nama == "7A"));
        // DaftarKelasFilter = DaftarKelas + 1 sentinel "Semua kelas".
        Assert.Equal(1, viewModel.DaftarKelasFilter.Count(k => k.Nama == "7A"));
        Assert.Equal(1, viewModel.DaftarKelasFilter.Count(k => k.Id == 0));
    }

    [Fact]
    public void SiswaViewModel_MuatUlangDaftarKelas_FilterYangSedangDipilihTetapDipertahankan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        viewModel.KelasFilterTerpilih = viewModel.DaftarKelasFilter.Single(k => k.Nama == "7A");

        database.Execute("INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (2, 1, '7Z-Baru', 7);");
        viewModel.MuatUlangDaftarKelas();

        Assert.Equal("7A", viewModel.KelasFilterTerpilih.Nama);
    }

    [Fact]
    public void KelasViewModel_TambahLaluUbah_DaftarMencerminkanPerubahan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new KelasViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);

        viewModel.Nama = "9Z";
        viewModel.Tingkat = 9;
        viewModel.SimpanCommand.Execute(null);

        var baru = Assert.Single(viewModel.Daftar, k => k.Nama == "9Z");

        viewModel.EditCommand.Execute(baru);
        viewModel.Nama = "9Y";
        viewModel.SimpanCommand.Execute(null);

        Assert.Contains(viewModel.Daftar, k => k.Nama == "9Y");
        Assert.DoesNotContain(viewModel.Daftar, k => k.Nama == "9Z");
    }

    // UAT 2026-09-23 (Panel Siswa — perbaikan Unggah foto): sebelumnya
    // UnggahFoto sama sekali tidak punya try/catch -- kegagalan menyalin
    // atau mengubah ukuran gambar melempar exception tak tertangani sampai
    // handler global, dan tidak ada bukti eksplisit bahwa database BENAR
    // diperbarui sebelum pesan berhasil ditampilkan. UnggahFotoDariBerkas
    // dipisah dari UnggahFoto (yang membuka OpenFileDialog) supaya bisa
    // diuji langsung, pola sama seperti SimpanTemplateKeBerkas (A-8).
    private static string BuatBerkasGambarSementara()
    {
        var bitmap = new WriteableBitmap(8, 8, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
        var path = Path.Combine(Path.GetTempPath(), $"foto-uji-{Guid.NewGuid():N}.png");
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(stream);
        return path;
    }

    [Fact]
    public void SiswaViewModel_UnggahFotoDariBerkas_Berhasil_PesanJelasDanFotoTersimpanDiDatabase()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var siswa = viewModel.Daftar.Single(s => s.Id == 1);
        var pathSumber = BuatBerkasGambarSementara();
        var pathTujuan = Path.Combine(LokasiData.FolderData, "foto", $"{siswa.Nisn}.png");

        try
        {
            viewModel.UnggahFotoDariBerkas(siswa, pathSumber);

            Assert.Null(viewModel.PesanGalat);
            Assert.Equal($"Foto {siswa.Nama} berhasil diunggah.", viewModel.PesanStatus);
            Assert.True(File.Exists(pathTujuan));

            // Query ULANG database (bukan cuma percaya pesan sukses) --
            // memastikan foto_path benar-benar ditulis, bukan cuma tampil
            // berhasil di UI.
            var fotoPathDb = new RepositoriSiswa().CariFotoPath(database.Connection, siswa.Id);
            Assert.Equal($"{siswa.Nisn}.png", fotoPathDb);
        }
        finally
        {
            File.Delete(pathSumber);
            File.Delete(pathTujuan);
        }
    }

    [Fact]
    public void SiswaViewModel_UnggahFotoDariBerkas_BerkasSumberBukanGambarValid_PesanGagalJelasTanpaSukses()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var siswa = viewModel.Daftar.Single(s => s.Id == 1);
        var pathSumber = Path.Combine(Path.GetTempPath(), $"foto-uji-rusak-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(pathSumber, new byte[] { 1, 2, 3, 4, 5 });
        var pathTujuan = Path.Combine(LokasiData.FolderData, "foto", $"{siswa.Nisn}.png");
        var fotoPathSebelum = new RepositoriSiswa().CariFotoPath(database.Connection, siswa.Id);

        try
        {
            viewModel.UnggahFotoDariBerkas(siswa, pathSumber);

            Assert.NotNull(viewModel.PesanGalat);
            Assert.Null(viewModel.PesanStatus);

            // Database TIDAK boleh berubah kalau pengolahan gambar gagal.
            var fotoPathSesudah = new RepositoriSiswa().CariFotoPath(database.Connection, siswa.Id);
            Assert.Equal(fotoPathSebelum, fotoPathSesudah);
        }
        finally
        {
            File.Delete(pathSumber);
            if (File.Exists(pathTujuan))
            {
                File.Delete(pathTujuan);
            }
        }
    }

    // "Pastikan foto tetap tersimpan setelah aplikasi ditutup dan dibuka
    // kembali" -- menutup koneksi yang menulis, membuka KONEKSI BARU ke
    // BERKAS yang sama (persis restart aplikasi), baru query ulang. Pola
    // sama seperti durabilitas Registrasi Kartu
    // (RegistrasiKartuViewModelTests.Simpan_SetelahKoneksiDitutupDanDibukaUlang...).
    [Fact]
    public void SiswaViewModel_UnggahFotoDariBerkas_Berhasil_BertahanSetelahKoneksiDitutupDanDibukaUlang()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);
        var siswa = viewModel.Daftar.Single(s => s.Id == 1);
        var pathSumber = BuatBerkasGambarSementara();
        var pathTujuan = Path.Combine(LokasiData.FolderData, "foto", $"{siswa.Nisn}.png");

        try
        {
            viewModel.UnggahFotoDariBerkas(siswa, pathSumber);

            var pathBerkas = database.PathBerkas;
            database.Connection.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            using var koneksiBaru = SqliteConnectionFactory.Open(pathBerkas);
            var fotoPathDb = new RepositoriSiswa().CariFotoPath(koneksiBaru, siswa.Id);

            Assert.Equal($"{siswa.Nisn}.png", fotoPathDb);
            Assert.True(File.Exists(pathTujuan));
        }
        finally
        {
            File.Delete(pathSumber);
            if (File.Exists(pathTujuan))
            {
                File.Delete(pathTujuan);
            }
        }
    }
}
