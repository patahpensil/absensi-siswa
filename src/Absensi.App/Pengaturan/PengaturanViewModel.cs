using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Backup;
using Absensi.Data.Pengaturan;
using Absensi.Data.Repositori;
using Absensi.Data.Waktu;
using Absensi.Domain;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Pengaturan;

// RENCANA-TUGAS.md Tugas 9 — bagian "jadwal harian", Tugas 10 — bagian
// "token bot", dan Tugas 13 — bagian "backup" dari layar Pengaturan
// (PANDUAN-UI-UX.md §9: "Pengaturan | Identitas sekolah, jadwal harian,
// tahun ajaran, token bot, backup"). Identitas sekolah/tahun ajaran hanya
// diisi lewat wizard (Tugas 13) — belum ada layar untuk mengubahnya lagi
// setelahnya, di luar cakupan Tugas 13 (lihat RENCANA-TUGAS.md).
public sealed class PengaturanViewModel : ObservableObject
{
    // hari: 1=Senin..7=Minggu (docs/SKEMA-DATABASE.md §4).
    private static readonly string[] NamaHari = { "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu", "Minggu" };

    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly int _tahunAjaranId;
    private readonly RepositoriJadwalHari _repositoriJadwalHari = new();
    private readonly LayananJadwalHari _layananJadwalHari = new();
    private readonly RepositoriPengaturan _repositoriPengaturan = new();
    private readonly LayananPengaturan _layananPengaturan = new();
    private readonly LayananBackup _layananBackup;

    private string? _pesanGalat;
    private string? _pesanStatus;
    private string? _tokenBot;
    private string? _usernameBot;
    private string? _pesanStatusBot;
    private string? _pesanGalatBot;
    private string? _pesanGalatBackup;

    public PengaturanViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu, int tahunAjaranId, LayananBackup layananBackup)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _tahunAjaranId = tahunAjaranId;
        _layananBackup = layananBackup;

        SimpanJadwalCommand = new RelayCommand(SimpanJadwal, () => BolehMengubah);
        SimpanBotCommand = new RelayCommand(SimpanBot, () => BolehMengubah);
        RestoreCommand = new RelayCommand<BerkasBackupTampilan>(Restore, _ => BolehMengubah);
        Akun = new AkunViewModel(connection, akun, penyediaWaktu);

        MuatJadwal();
        MuatBot();
        MuatDaftarBackup();
    }

    // RENCANA-TUGAS.md Tugas 14 — sub-tab "Akun" (Pengaturan → Akun).
    // Wali kelas tidak diberi tab ini sama sekali (PengaturanView.xaml
    // menyembunyikan TabItem-nya lewat AkunViewModel.BolehAkses) — bukan
    // hanya menonaktifkan tombol di dalamnya.
    public AkunViewModel Akun { get; }

    // PRD §18.4 langkah 2: "restore hanya dapat dilakukan akun berwenang
    // penuh" — pemanggil (App.xaml.cs) menutup koneksi dan aplikasi setelah
    // ini terpicu, karena berkas database diganti di bawah koneksi yang
    // sedang berjalan.
    public event EventHandler<byte[]>? RestoreSelesaiPerluTutupAplikasi;

    public ObservableCollection<BerkasBackupTampilan> DaftarBackup { get; } = new();

    public RelayCommand<BerkasBackupTampilan> RestoreCommand { get; }

    private void MuatDaftarBackup()
    {
        DaftarBackup.Clear();
        var sekarang = _penyediaWaktu.Sekarang;
        foreach (var berkas in _layananBackup.DaftarBackupLokal())
        {
            DaftarBackup.Add(new BerkasBackupTampilan(berkas, berkas.HariDataYangHilangJikaDipulihkan(sekarang)));
        }
    }

    private void Restore(BerkasBackupTampilan tampilan)
    {
        var dialogViewModel = new KonfirmasiRestoreViewModel(tampilan.Berkas, tampilan.HariDataHilang);
        var dialog = new KonfirmasiRestoreWindow(dialogViewModel);
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        // Kegagalan yang bisa dipulihkan (berkas backup hilang/terkunci, wali kelas
        // tidak berwenang) tampil sebagai pesan di tab Backup — bukan galat global
        // yang menutup aplikasi. Pesan berhasil tidak ada di sini: restore yang
        // sukses langsung menutup aplikasi dengan pemberitahuan (App.xaml.cs).
        byte[]? data = null;
        PesanGalatBackup = null;
        if (!PenangananGalat.Coba(
                () => data = _layananBackup.SiapkanRestore(_connection, _akun, _penyediaWaktu.Sekarang, tampilan.Berkas),
                pesan => PesanGalatBackup = pesan,
                "PengaturanViewModel.Restore"))
        {
            return;
        }

        RestoreSelesaiPerluTutupAplikasi?.Invoke(this, data!);
    }

    public ObservableCollection<JadwalHariBarisViewModel> JadwalHarian { get; } = new();

    // PRD §16.6 langkah 2: "Admin memasukkan token bot dan username bot di
    // menu Pengaturan" — username bot dipakai LayananPairing.BuatUrlQr,
    // token bot dipakai Absensi.Telegram (PengirimOutbox/
    // PemrosesPembaruanPairing) lewat RepositoriPengaturan langsung, bukan
    // lewat ViewModel ini.
    public string? TokenBot { get => _tokenBot; set => SetField(ref _tokenBot, value); }

    public string? UsernameBot { get => _usernameBot; set => SetField(ref _usernameBot, value); }

    public string? PesanStatusBot { get => _pesanStatusBot; private set => SetField(ref _pesanStatusBot, value); }

    public string? PesanGalatBot { get => _pesanGalatBot; private set => SetField(ref _pesanGalatBot, value); }

    public string? PesanGalatBackup { get => _pesanGalatBackup; private set => SetField(ref _pesanGalatBackup, value); }

    public RelayCommand SimpanBotCommand { get; }

    // PRD §3: pengaturan adalah kewenangan penuh (Kepala Sekolah/Admin) —
    // wali kelas tidak berwenang. Ditegakkan juga (dan sebenarnya oleh)
    // LayananJadwalHari; ini sekadar menyembunyikan tombol Simpan.
    public bool BolehMengubah => _akun.Peran != PeranAkun.WaliKelas;

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

    public RelayCommand SimpanJadwalCommand { get; }

    private void MuatBot()
    {
        TokenBot = _repositoriPengaturan.BacaString(_connection, "token_bot");
        UsernameBot = _repositoriPengaturan.BacaString(_connection, "username_bot");
    }

    // Token bot: angka, titik dua, lalu karakter huruf/angka/_/- (format BotFather).
    // Username bot: 5-32 huruf/angka/garis bawah; tanda @ di depan dibuang.
    // Pesan galat TIDAK PERNAH memuat nilai token yang diketik (kredensial).
    private static readonly System.Text.RegularExpressions.Regex FormatToken = new(@"^\d+:[A-Za-z0-9_-]+$");
    private static readonly System.Text.RegularExpressions.Regex FormatUsername = new(@"^[A-Za-z0-9_]{5,32}$");

    private void SimpanBot()
    {
        PesanStatusBot = null;
        var token = (TokenBot ?? string.Empty).Trim();
        var username = (UsernameBot ?? string.Empty).Trim().TrimStart('@');

        if (token.Length > 0 && !FormatToken.IsMatch(token))
        {
            PesanGalatBot = "Format token bot tidak dikenali. Salin ulang token utuh dari BotFather (contoh bentuknya: angka, titik dua, lalu huruf dan angka).";
            return;
        }

        if (username.Length > 0 && !FormatUsername.IsMatch(username))
        {
            PesanGalatBot = "Username bot hanya boleh huruf, angka, dan garis bawah (5 sampai 32 karakter), tanpa tanda @ dan tanpa spasi.";
            return;
        }

        var waktu = _penyediaWaktu.Sekarang;
        if (!PenangananGalat.Coba(
                () =>
                {
                    _layananPengaturan.Simpan(_connection, _akun, waktu, "token_bot", token);
                    _layananPengaturan.Simpan(_connection, _akun, waktu, "username_bot", username);
                },
                pesan => PesanGalatBot = pesan,
                "PengaturanViewModel.SimpanBot"))
        {
            return;
        }

        TokenBot = token;
        UsernameBot = username;
        PesanGalatBot = null;
        PesanStatusBot = "Pengaturan bot Telegram tersimpan.";
    }

    private void MuatJadwal()
    {
        JadwalHarian.Clear();
        foreach (var baris in _repositoriJadwalHari.DaftarUntukTahunAjaran(_connection, _tahunAjaranId))
        {
            JadwalHarian.Add(new JadwalHariBarisViewModel(
                baris.Hari,
                NamaHari[baris.Hari - 1],
                baris.HariSekolah,
                FormatJam(baris.JamMasuk),
                FormatJam(baris.JamRekap),
                FormatJam(baris.BatasTapMasuk),
                FormatJam(baris.MulaiPulang)));
        }
    }

    private static string? FormatJam(TimeOnly? jam) => jam?.ToString("HH:mm");

    private void SimpanJadwal()
    {
        var hasil = new List<JadwalHariBaris>();

        foreach (var baris in JadwalHarian)
        {
            if (!CobaUraikanJam(baris.NamaHari, "jam masuk", baris.JamMasuk, baris.HariSekolah, out var jamMasuk)
                || !CobaUraikanJam(baris.NamaHari, "jam rekap", baris.JamRekap, baris.HariSekolah, out var jamRekap)
                || !CobaUraikanJam(baris.NamaHari, "batas akhir tap masuk", baris.BatasTapMasuk, baris.HariSekolah, out var batasTapMasuk)
                || !CobaUraikanJam(baris.NamaHari, "mulai absen pulang", baris.MulaiPulang, baris.HariSekolah, out var mulaiPulang))
            {
                PesanStatus = null;
                return;
            }

            hasil.Add(new JadwalHariBaris(baris.Hari, baris.HariSekolah, jamMasuk, jamRekap, batasTapMasuk, mulaiPulang));
        }

        PesanGalat = null;
        PesanStatus = null;
        if (!PenangananGalat.Coba(
                () => _layananJadwalHari.Perbarui(_connection, _akun, _penyediaWaktu.Sekarang, _tahunAjaranId, hasil),
                pesan => PesanGalat = pesan,
                "PengaturanViewModel.SimpanJadwal"))
        {
            return;
        }

        PesanStatus = "Jadwal harian tersimpan.";
    }

    // UAT 2026-09-23: sebelumnya kosong diterima sebagai valid untuk baris
    // apa pun, termasuk hari sekolah — dikombinasikan dengan grid yang saat
    // itu tidak bisa diedit sama sekali (lihat PengaturanView.xaml), Simpan
    // menuliskan NULL ke jadwal_hari sambil tetap menampilkan "Jadwal
    // harian tersimpan." seolah berhasil. Sekarang hari sekolah WAJIB diisi
    // (pola sama seperti WizardInstalasiViewModel.CobaUraikanJam) — hari
    // non-sekolah tetap boleh kosong sesuai PRD §7.
    private bool CobaUraikanJam(string namaHari, string namaKolom, string? teks, bool wajib, out TimeOnly? hasil)
    {
        hasil = null;
        if (string.IsNullOrWhiteSpace(teks))
        {
            if (wajib)
            {
                PesanGalat = $"{namaHari}: {namaKolom} wajib diisi untuk hari sekolah.";
                return false;
            }

            return true;
        }

        if (!TimeOnly.TryParse(teks, out var jam))
        {
            PesanGalat = $"{namaHari}: format {namaKolom} tidak valid. Gunakan format 24 jam HH:mm.";
            return false;
        }

        hasil = jam;
        return true;
    }
}
