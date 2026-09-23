using System.Collections.Concurrent;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Absensi.App.MasterData;

namespace Absensi.App.Tests;

// UAT Bug #12 (2026-09-22): setelah navigasi Siswa/Guru <-> panel lain,
// lebar/urutan kolom DataGrid berantakan. MainWindowViewModel.PerbaruiKontenAktif
// membuat instance View BARU (lewat DataTemplate implisit) setiap menu
// diklik — bukan menggunakan kembali visual yang sama — jadi navigasi di
// aplikasi nyata setara dengan mengonstruksi SiswaView/GuruView berkali-kali
// dari awal. Tes ini MEMBUKTIKAN (bukan cuma memeriksa konstanta) bahwa
// InitializeComponent() menghasilkan definisi kolom yang SAMA dan TIDAK
// GANDA setiap kali dijalankan ulang, dan bahwa MinWidth (perbaikan Bug #12
// di SiswaView.xaml/GuruView.xaml) benar-benar terpasang di objek kolom
// yang dihasilkan compiler — bukan cuma teks XAML yang terlihat benar.
//
// Konstruksi FrameworkElement (Control) WAJIB thread STA, dan StaticResource
// (dipakai di seluruh SiswaView.xaml/GuruView.xaml) butuh Application.Current
// dengan GayaAplikasi.xaml termuat — keduanya dikonfirmasi lewat percobaan
// langsung sebelum kode ini ditulis (StaHost di bawah). Tidak menambah
// paket NuGet (mis. Xunit.StaFact) — satu thread STA persisten dibangun
// manual dari System.Threading (BCL) dan dipakai bersama oleh semua tes
// di berkas ini, supaya Application.Current dan seluruh FrameworkElement
// yang dibuat konsisten berada di thread yang sama (WPF melarang mengakses
// DispatcherObject lintas thread).
//
// YANG TIDAK diverifikasi tes ini (tetap manual, lihat PANDUAN-TESTER.md
// Skenario B): perilaku pengukuran "*" WPF DataGrid yang sesungguhnya saat
// container mendapat ActualWidth secara bertahap selama navigasi nyata —
// itu butuh Window sungguhan dengan siklus Measure/Arrange penuh, di luar
// jangkauan xUnit biasa (sama seperti batasan yang didokumentasikan
// berulang kali di PANDUAN-TESTER.md untuk siklus hidup Window WPF lain).
public sealed class SiswaGuruTabelKolomTests
{
    // UAT 2026-09-23: 8 -> 6 setelah kolom TG dan Alasan dihapus dari
    // tampilan Panel Siswa (field TertautTelegram/AlasanNonaktif TETAP ada
    // di database/SiswaDaftar, dipakai modul lain seperti Notifikasi — lihat
    // MASTER-BUG-UIUX-REGISTER.md).
    private const int KolomSiswaDiharapkan = 6;
    // 2026-09-24: 6 -> 5 setelah kolom TG dihapus dari Panel Guru (sama seperti
    // Panel Siswa; TertautTelegram TETAP di GuruDaftar untuk menu Notifikasi).
    private const int KolomGuruDiharapkan = 5;

    private static DataGrid AmbilTabel(FrameworkElement view, string namaElemen)
    {
        var tabel = view.FindName(namaElemen) as DataGrid;
        Assert.NotNull(tabel);
        return tabel!;
    }

    [Fact]
    public void SiswaView_DibuatUlangDuaKali_JumlahKolomIdentikDanTidakGanda() => StaHost.Jalankan(() =>
    {
        // Konstruksi ganda mensimulasikan navigasi Siswa -> panel lain ->
        // Siswa: MainWindowViewModel membuat SiswaView baru setiap kali,
        // bukan mendaur ulang instance sebelumnya.
        var viewPertama = new SiswaView();
        var viewKedua = new SiswaView();

        var tabelPertama = AmbilTabel(viewPertama, "TabelSiswa");
        var tabelKedua = AmbilTabel(viewKedua, "TabelSiswa");

        Assert.Equal(KolomSiswaDiharapkan, tabelPertama.Columns.Count);
        Assert.Equal(KolomSiswaDiharapkan, tabelKedua.Columns.Count);
        Assert.Equal(tabelPertama.Columns.Count, tabelKedua.Columns.Count);
    });

    [Fact]
    public void SiswaView_KolomHeaderUrutanDeterministik() => StaHost.Jalankan(() =>
    {
        var view = new SiswaView();
        var tabel = AmbilTabel(view, "TabelSiswa");

        var header = tabel.Columns.Select(k => k.Header?.ToString() ?? string.Empty).ToArray();

        Assert.Equal(new[] { "Nama", "NISN", "JK", "Kelas", "Kartu", "" }, header);
    });

    [Fact]
    public void SiswaView_SetiapKolomPunyaMinWidthPositif_TidakBisaMenyempitKeNol() => StaHost.Jalankan(() =>
    {
        var view = new SiswaView();
        var tabel = AmbilTabel(view, "TabelSiswa");

        foreach (var kolom in tabel.Columns)
        {
            Assert.True(kolom.MinWidth > 0, $"Kolom '{kolom.Header}' tidak punya MinWidth positif — bisa menyempit ke 0 seperti Bug #12.");
        }

        // Kolom tindakan (Ubah/Unggah foto/Ganti kartu/Mutasi) butuh ruang
        // paling besar — kalau MinWidth-nya ikut menyempit, tombol-tombol
        // itu yang paling dulu tidak terbaca/tidak bisa diklik (laporan UAT).
        // UAT 2026-09-23: 290 -> 400, empat tombol teks tidak muat penuh di
        // 290px (lihat komentar di SiswaView.xaml).
        var kolomTindakan = tabel.Columns.Last();
        // 2026-09-24: 400 -> 340 (formulir pindah ke dialog; empat slot aksi berlebar tetap).
        Assert.Equal(340, kolomTindakan.MinWidth);
    });

    [Fact]
    public void GuruView_DibuatUlangDuaKali_JumlahKolomIdentikDanTidakGanda() => StaHost.Jalankan(() =>
    {
        var viewPertama = new GuruView();
        var viewKedua = new GuruView();

        var tabelPertama = AmbilTabel(viewPertama, "TabelGuru");
        var tabelKedua = AmbilTabel(viewKedua, "TabelGuru");

        Assert.Equal(KolomGuruDiharapkan, tabelPertama.Columns.Count);
        Assert.Equal(KolomGuruDiharapkan, tabelKedua.Columns.Count);
        Assert.Equal(tabelPertama.Columns.Count, tabelKedua.Columns.Count);
    });

    [Fact]
    public void GuruView_KolomHeaderUrutanDeterministik() => StaHost.Jalankan(() =>
    {
        var view = new GuruView();
        var tabel = AmbilTabel(view, "TabelGuru");

        var header = tabel.Columns.Select(k => k.Header?.ToString() ?? string.Empty).ToArray();

        Assert.Equal(new[] { "Nama", "NIP", "Kartu", "Status", "" }, header);
    });

    [Fact]
    public void GuruView_SetiapKolomPunyaMinWidthPositif_TidakBisaMenyempitKeNol() => StaHost.Jalankan(() =>
    {
        var view = new GuruView();
        var tabel = AmbilTabel(view, "TabelGuru");

        foreach (var kolom in tabel.Columns)
        {
            Assert.True(kolom.MinWidth > 0, $"Kolom '{kolom.Header}' tidak punya MinWidth positif — bisa menyempit ke 0 seperti Bug #12.");
        }

        var kolomTindakan = tabel.Columns.Last();
        // 2026-09-24: 290 -> 380, empat slot aksi berlebar tetap (Ubah, Unggah
        // foto, Daftarkan/Ganti kartu, Nonaktifkan) — lihat GuruView.xaml.
        Assert.Equal(380, kolomTindakan.MinWidth);
    });
}

// Thread STA tunggal, dibagi oleh seluruh tes di berkas ini (lihat komentar
// di atas untuk alasannya). Statis dan lazy — dibuat sekali per proses tes,
// dimatikan otomatis karena IsBackground=true.
internal static class StaHost
{
    private static readonly BlockingCollection<Action> Antrian = new();
    private static readonly Thread Thread;

    static StaHost()
    {
        Thread = new Thread(Loop) { IsBackground = true };
        Thread.SetApartmentState(ApartmentState.STA);
        Thread.Start();
    }

    private static void Loop()
    {
        // Application minimal (tidak pernah Run()/OnStartup) hanya supaya
        // StaticResource di SiswaView.xaml/GuruView.xaml (via GayaAplikasi.xaml)
        // bisa diselesaikan — SiswaView/GuruView TIDAK dibuat lewat App
        // sungguhan (yang akan membuka koneksi database dkk., di luar
        // cakupan tes ini).
        if (Application.Current is null)
        {
            _ = new Application
            {
                // Bawaan WPF (OnLastWindowClose) mematikan Application
                // begitu Window SUNGGUHAN yang di-Show() (RegistrasiKartuWindowTests,
                // PengaturanViewLayoutTests, dst.) di-Close() — StaHost ini
                // dipakai ULANG oleh BANYAK berkas tes sepanjang satu proses
                // xUnit, jadi Window kedua yang ditutup akan mematikan
                // Application untuk SISA seluruh proses tes (ditemukan lewat
                // percobaan langsung: tes lain gagal acak tergantung urutan
                // eksekusi). OnExplicitShutdown memastikan StaHost tetap
                // hidup sampai proses tes berakhir sendiri (thread
                // IsBackground=true), berapa pun Window yang dibuka/ditutup.
                ShutdownMode = ShutdownMode.OnExplicitShutdown,
                Resources = new ResourceDictionary
                {
                    MergedDictionaries =
                    {
                        new ResourceDictionary
                        {
                            Source = new Uri("pack://application:,,,/Absensi.App;component/Resources/GayaAplikasi.xaml"),
                        },
                    },
                },
            };
        }

        foreach (var aksi in Antrian.GetConsumingEnumerable())
        {
            aksi();
        }
    }

    public static void Jalankan(Action aksi)
    {
        Exception? galat = null;
        using var selesai = new ManualResetEventSlim(false);

        Antrian.Add(() =>
        {
            try
            {
                aksi();
            }
            catch (Exception ex)
            {
                galat = ex;
            }
            finally
            {
                selesai.Set();
            }
        });

        selesai.Wait();

        if (galat is not null)
        {
            // Pertahankan stack trace asli dari thread STA ("throw galat" menimpanya).
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(galat).Throw();
        }
    }
}
