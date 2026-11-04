using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Diagnostik;
using Absensi.Data.Kartu;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.App.RegistrasiKartu;

// UAT Bug #11 (2026-09-23) — REDESAIN FUNDAMENTAL, menggantikan alur
// antrean-otomatis lama (RENCANA-TUGAS.md Tugas 8 / PRD §16.5 versi
// sebelumnya, lihat docs/PRD.md untuk revisi terbaru). Operator SEKARANG
// memilih kelas, lalu memilih SATU siswa secara eksplisit dari dropdown
// (bukan kursor yang lompat otomatis ke siswa berikutnya), lalu tap kartu,
// lalu Simpan — satu klik Simpan menghubungkan TEPAT satu siswa dengan
// TEPAT satu kartu. Dialog TIDAK menutup diri sendiri setelah Simpan —
// tetap terbuka supaya operator bisa langsung memilih siswa berikutnya di
// kelas yang sama tanpa membuka ulang dialog; hanya Batal/tombol X yang
// menutup jendela. Mode Registrasi aktif selama jendela ini terbuka dan
// mati otomatis saat ditutup (gagal-aman — lihat StatusModeRegistrasi).
public sealed class RegistrasiKartuViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly StatusModeRegistrasi _statusModeRegistrasi;
    private readonly int _tahunAjaranId;
    private readonly LayananRegistrasiKartu _layanan = new();

    private KelasDaftar? _kelasTerpilih;
    private SiswaDaftar? _siswaTerpilih;
    private string? _uidSementara;
    private string? _pesanGalat;
    private string? _pesanStatus;

    public RegistrasiKartuViewModel(
        SqliteConnection connection,
        AkunTersimpan akun,
        IPenyediaWaktu penyediaWaktu,
        StatusModeRegistrasi statusModeRegistrasi,
        int tahunAjaranId)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _statusModeRegistrasi = statusModeRegistrasi;
        _tahunAjaranId = tahunAjaranId;

        // UAT Bug #11 (2026-09-22, masih berlaku): dimuat ULANG di sini
        // setiap kali ViewModel ini dibuat — App.xaml.cs BukaRegistrasiKartu()
        // membuat ViewModel baru setiap dialog dibuka (tidak ada cache),
        // jadi ini otomatis mengambil kelas terbaru tanpa restart. Dibungkus
        // try/catch supaya kegagalan QUERY dibedakan dari kelas yang MEMANG
        // belum ada (bukan menyembunyikan exception, PRD §24.3).
        try
        {
            foreach (var kelas in new RepositoriKelas().DaftarUntukTahunAjaran(connection, tahunAjaranId))
            {
                DaftarKelas.Add(kelas);
            }
        }
        catch (Exception galat)
        {
            PencatatGalat.Catat(galat, sumber: "RegistrasiKartuViewModel.MuatDaftarKelas", level: "ERROR");
            GagalMuatKelas = true;
        }

        SimpanCommand = new RelayCommand(Simpan, BisaSimpan);

        // PRD §16.5 langkah 1: mengaktifkan Mode Registrasi adalah alasan
        // layar ini dibuka, jadi disatukan — bukan tombol terpisah.
        _statusModeRegistrasi.Aktifkan();
    }

    public ObservableCollection<KelasDaftar> DaftarKelas { get; } = new();

    public bool GagalMuatKelas { get; }

    public string? PesanMuatKelasGagal => GagalMuatKelas
        ? "Gagal memuat daftar kelas dari database. Tutup dan buka lagi Registrasi Kartu; bila terus terjadi, laporkan ke pengelola aplikasi."
        : null;

    public bool TahunAjaranTanpaKelas => !GagalMuatKelas && DaftarKelas.Count == 0;

    public KelasDaftar? KelasTerpilih
    {
        get => _kelasTerpilih;
        set
        {
            if (SetField(ref _kelasTerpilih, value))
            {
                MuatSiswaKelas();
            }
        }
    }

    public ObservableCollection<SiswaDaftar> DaftarSiswaKelas { get; } = new();

    public bool KelasBelumPunyaSiswa => _kelasTerpilih is not null && DaftarSiswaKelas.Count == 0;

    public SiswaDaftar? SiswaTerpilih
    {
        get => _siswaTerpilih;
        set
        {
            if (SetField(ref _siswaTerpilih, value))
            {
                // UAT poin wajib: memilih siswa BARU membatalkan UID
                // sementara yang mungkin masih tertahan dari siswa
                // sebelumnya — satu UID sementara hanya berlaku untuk satu
                // siswa yang sedang dipilih.
                UidSementara = null;
                PesanGalat = null;
                PesanStatus = null;
                SimpanCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(StatusKartuSiswaTerpilih));
            }
        }
    }

    // "Bagian Siswa sekarang menampilkan ... status kartu siswa terpilih"
    // — UID sebenarnya kalau sudah punya, bukan True/False (sama seperti
    // kolom Kartu panel Siswa, lihat SiswaView.xaml).
    public string StatusKartuSiswaTerpilih => SiswaTerpilih?.KartuUid ?? "Belum terdaftar";

    public string? UidSementara
    {
        get => _uidSementara;
        private set => SetField(ref _uidSementara, value);
    }

    public string? PesanGalat
    {
        get => _pesanGalat;
        private set => SetField(ref _pesanGalat, value);
    }

    public string? PesanStatus
    {
        get => _pesanStatus;
        private set => SetField(ref _pesanStatus, value);
    }

    public RelayCommand SimpanCommand { get; }

    // View menutup jendela saat ini terpicu — HANYA dari Batal/tombol X
    // sekarang (redesain 2026-09-23), TIDAK LAGI otomatis dipicu Simpan.
    // Panel Siswa di-refresh MainWindow.xaml.cs SETELAH dialog ini tertutup
    // (lihat MuatUlangSiswaSetelahRegistrasiKartu) — tidak perlu event
    // per-Simpan terpisah karena dialog ini modal (Owner tidak terlihat
    // selama dialog terbuka, jadi refresh mid-sesi tidak berguna secara
    // visual bagi operator).
    public event EventHandler? Selesai;

    public void TapUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return;
        }

        if (_kelasTerpilih is null)
        {
            PesanGalat = "Pilih kelas terlebih dahulu.";
            return;
        }

        if (SiswaTerpilih is not { } siswa)
        {
            PesanGalat = "Pilih siswa terlebih dahulu.";
            return;
        }

        // UAT poin wajib (Tap RFID): "Jika UID sudah terdaftar, tolak dan
        // tampilkan pemiliknya. Jangan isi UID sementara dan jangan
        // aktifkan Simpan." — dicek LANGSUNG ke database, bukan ke memori
        // sesi (redesain ini tidak lagi punya "sesi" multi-siswa).
        var pemilikAktif = _layanan.CariPemilikAktif(_connection, uid, _tahunAjaranId);
        if (pemilikAktif is not null)
        {
            PesanGalat = pemilikAktif.KelasNama is { } kelasNama
                ? $"Kartu ini sudah dipakai {pemilikAktif.Nama} di kelas {kelasNama}."
                : $"Kartu ini sudah dipakai {pemilikAktif.Nama}.";
            UidSementara = null;
            SimpanCommand.RaiseCanExecuteChanged();
            return;
        }

        // UAT poin wajib: "Jika siswa terpilih sudah memiliki kartu aktif,
        // tolak pendaftaran kartu baru. Penggantian kartu belum termasuk
        // scope ini." — dicek terhadap DATABASE (KartuUid dari SiswaDaftar
        // bisa basi kalau kartu itu baru saja dinonaktifkan/diganti lewat
        // layar lain selagi dialog ini terbuka), bukan cuma properti yang
        // sudah dimuat di memori.
        var kartuSiswaSaatIni = _layanan.CariUidAktifSiswa(_connection, siswa.Id);
        if (kartuSiswaSaatIni is not null)
        {
            PesanGalat = $"{siswa.Nama} sudah memiliki kartu aktif ({kartuSiswaSaatIni}). Penggantian kartu dilakukan lewat menu Siswa → Ganti kartu.";
            UidSementara = null;
            SimpanCommand.RaiseCanExecuteChanged();
            return;
        }

        // UAT poin wajib: "Jika UID tersedia, tampilkan UID yang terdeteksi
        // sebagai data sementara." — BUKAN pesan "terdaftar" (itu baru
        // benar setelah Simpan berhasil dan transaksi diverifikasi ulang).
        PesanGalat = null;
        UidSementara = uid;
        PesanStatus = null;
        SimpanCommand.RaiseCanExecuteChanged();
    }

    public void Batal() => Selesai?.Invoke(this, EventArgs.Empty);

    // Dipanggil Window.OnClosed apa pun cara jendela ditutup, memastikan
    // Mode Registrasi tidak pernah "menyala" setelah jendela ini hilang.
    public void MatikanModeRegistrasi() => _statusModeRegistrasi.Matikan();

    private bool BisaSimpan() => _kelasTerpilih is not null && SiswaTerpilih is not null && !string.IsNullOrWhiteSpace(UidSementara);

    private void MuatSiswaKelas()
    {
        DaftarSiswaKelas.Clear();
        SiswaTerpilih = null;
        UidSementara = null;
        PesanGalat = null;
        PesanStatus = null;
        SimpanCommand.RaiseCanExecuteChanged();

        if (_kelasTerpilih is not { } kelas)
        {
            OnPropertyChanged(nameof(KelasBelumPunyaSiswa));
            return;
        }

        // UAT Bug #11 (masih berlaku): kegagalan query dibedakan dari
        // kelas yang memang belum punya siswa, bukan ditelan diam-diam.
        try
        {
            foreach (var siswa in new RepositoriSiswa().DaftarUntukTahunAjaran(_connection, _tahunAjaranId)
                .Where(s => s.Aktif && s.KelasNama == kelas.Nama))
            {
                DaftarSiswaKelas.Add(siswa);
            }
        }
        catch (Exception galat)
        {
            PencatatGalat.Catat(galat, sumber: "RegistrasiKartuViewModel.MuatSiswaKelas", level: "ERROR");
            PesanGalat = "Gagal memuat daftar siswa kelas ini. Coba pilih ulang kelasnya.";
        }

        OnPropertyChanged(nameof(KelasBelumPunyaSiswa));
    }

    // UAT poin wajib (Simpan): CanExecute sudah menjamin kelas/siswa/UID
    // valid, tapi diperiksa ulang di sini juga (defensif — WPF command
    // requery tidak 100% real-time di semua skenario fokus).
    private void Simpan()
    {
        if (_kelasTerpilih is null || SiswaTerpilih is not { } siswa || string.IsNullOrWhiteSpace(UidSementara))
        {
            return;
        }

        var uid = UidSementara;

        try
        {
            _layanan.DaftarkanSatu(_connection, _akun, _penyediaWaktu.Sekarang, uid, siswa.Id, siswa.Nama);
        }
        catch (Exception galat)
        {
            // UAT poin wajib (Integritas): "Audit kegagalan ... dengan
            // jelas." — dicatat dengan detail teknis lengkap, ditampilkan
            // ke operator dengan pesan yang aman (InvalidOperationException
            // dari LayananRegistrasiKartu SUDAH berisi kalimat siap-tampil
            // untuk kegagalan integritas; exception lain memakai pesan
            // generik supaya tidak membocorkan detail teknis ke UI).
            PencatatGalat.Catat(galat, sumber: "RegistrasiKartuViewModel.Simpan", level: "ERROR");
            PesanGalat = galat is InvalidOperationException
                ? galat.Message
                : "Gagal menyimpan kartu ke database. Coba lagi; bila terus terjadi, laporkan ke pengelola aplikasi.";
            PesanStatus = null;
            return;
        }

        // UAT poin wajib: "Setelah berhasil, query ulang database dan
        // pastikan UID benar-benar tersimpan." — tidak menganggap "tidak
        // melempar exception" sebagai bukti cukup (ini persis skenario
        // yang menyebabkan Bug #11 putaran kelima: DaftarkanSesi versi
        // lama SELESAI TANPA EXCEPTION tapi tidak benar-benar menulis
        // apa pun kalau daftar pendaftarannya kosong).
        var kartuTersimpan = _layanan.CariUidAktifSiswa(_connection, siswa.Id);
        if (!string.Equals(kartuTersimpan, uid, StringComparison.Ordinal))
        {
            PencatatGalat.Catat(
                new Exception($"Simpan tidak melempar exception tapi verifikasi ulang gagal: uid={uid}, siswaId={siswa.Id}, kartuTersimpanSaatIni={kartuTersimpan ?? "(null)"}"),
                sumber: "RegistrasiKartuViewModel.Simpan.VerifikasiGagal",
                level: "ERROR");
            PesanGalat = "Penyimpanan tidak dapat diverifikasi. Coba lagi; bila terus terjadi, laporkan ke pengelola aplikasi.";
            PesanStatus = null;
            return;
        }

        // Muat ulang daftar siswa kelas ini supaya SiswaTerpilih (dan
        // seluruh dropdown) menunjukkan status kartu TERBARU tanpa perlu
        // menutup dialog — operator langsung lihat konfirmasi "sudah
        // terdaftar" pada siswa yang baru saja disimpan.
        var idSiswaTerpilih = siswa.Id;
        MuatSiswaKelas();
        SiswaTerpilih = DaftarSiswaKelas.FirstOrDefault(s => s.Id == idSiswaTerpilih);

        PesanGalat = null;
        PesanStatus = $"Kartu RFID {uid} berhasil disimpan untuk {siswa.Nama}.";
        UidSementara = null;
        SimpanCommand.RaiseCanExecuteChanged();
    }
}
