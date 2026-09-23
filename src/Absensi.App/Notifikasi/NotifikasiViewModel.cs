using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Notifikasi;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Notifikasi;

// RENCANA-TUGAS.md Tugas 10 — layar Notifikasi (PANDUAN-UI-UX.md §9:
// "Status bot, antrean, riwayat kirim, pemantau pairing").
public sealed class NotifikasiViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly int _tahunAjaranId;
    private readonly RepositoriOutbox _repositoriOutbox = new();
    private readonly RepositoriSiswa _repositoriSiswa = new();
    private readonly RepositoriGuru _repositoriGuru = new();
    private readonly LayananPairing _layananPairing = new();

    private IReadOnlyList<SiswaDaftar> _semuaSiswa = Array.Empty<SiswaDaftar>();
    private KelasDaftar? _kelasTerpilih;
    private string? _ringkasanPairingKelas;
    private string? _pesanStatus;
    private string? _pesanGalat;

    public NotifikasiViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu, int tahunAjaranId)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _tahunAjaranId = tahunAjaranId;

        var repositoriPengaturan = new RepositoriPengaturan();
        BotTerkonfigurasi = !string.IsNullOrWhiteSpace(repositoriPengaturan.BacaString(connection, "token_bot"));
        UsernameBot = repositoriPengaturan.BacaString(connection, "username_bot");

        foreach (var kelas in new RepositoriKelas().DaftarUntukTahunAjaran(connection, tahunAjaranId))
        {
            DaftarKelas.Add(kelas);
        }

        LihatQrSiswaCommand = new RelayCommand<SiswaDaftar>(LihatQrSiswa, _ => BolehBuatQr);
        LihatQrGuruCommand = new RelayCommand<GuruDaftar>(LihatQrGuru, guru => BolehBuatQr && _akun.Peran != PeranAkun.WaliKelas);
        CetakMassalCommand = new RelayCommand(CetakMassal, () => BolehBuatQr && _kelasTerpilih is not null);
        MuatUlangCommand = new RelayCommand(MuatSemua);

        MuatSemua();
    }

    // PRD §16.6 langkah 1-2: tautan QR butuh username bot; tanpa itu tombol
    // buat/cetak QR dinonaktifkan daripada menghasilkan tautan rusak.
    public bool BotTerkonfigurasi { get; }

    public bool BotBelumDikonfigurasi => !BotTerkonfigurasi;

    public string? UsernameBot { get; }

    private bool BolehBuatQr => !string.IsNullOrWhiteSpace(UsernameBot);

    public ObservableCollection<KelasDaftar> DaftarKelas { get; } = new();

    public KelasDaftar? KelasTerpilih
    {
        get => _kelasTerpilih;
        set
        {
            if (SetField(ref _kelasTerpilih, value))
            {
                TerapkanFilterKelas();
                CetakMassalCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<SiswaDaftar> SiswaTampil { get; } = new();

    public string? RingkasanPairingKelas
    {
        get => _ringkasanPairingKelas;
        private set => SetField(ref _ringkasanPairingKelas, value);
    }

    public ObservableCollection<GuruDaftar> DaftarGuru { get; } = new();

    public ObservableCollection<OutboxBaris> Antrean { get; } = new();

    public ObservableCollection<OutboxBaris> Riwayat { get; } = new();

    // Membuat QR baru untuk siapa pun yang SUDAH tertaut memutus tautan lamanya
    // (LayananPairing.BuatTokenBaru) — View memasang dialog Ya/Tidak di sini; tanpa
    // pemasangan (mis. tes) dianggap disetujui.
    public Func<string, bool>? MintaKonfirmasi { get; set; }

    private bool Setuju(string pesan) => MintaKonfirmasi?.Invoke(pesan) ?? true;

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

    public RelayCommand<SiswaDaftar> LihatQrSiswaCommand { get; }

    public RelayCommand<GuruDaftar> LihatQrGuruCommand { get; }

    public RelayCommand CetakMassalCommand { get; }

    public RelayCommand MuatUlangCommand { get; }

    // View membuka jendela QR tunggal saat ini terpicu.
    public event EventHandler<QrTampil>? TampilkanQrDiminta;

    // View membuka pratinjau cetak massal saat ini terpicu.
    public event EventHandler<IReadOnlyList<QrTampil>>? CetakMassalSiap;

    private void LihatQrSiswa(SiswaDaftar siswa)
    {
        if (siswa.TertautTelegram
            && !Setuju($"{siswa.Nama} sudah tertaut Telegram. Membuat QR baru MEMUTUS tautan yang lama — orang tua harus memindai QR baru. Lanjutkan?"))
        {
            return;
        }

        PesanGalat = null;
        string? token = null;
        if (!PenangananGalat.Coba(
                () => token = _layananPairing.BuatTokenBaru(_connection, _akun, _penyediaWaktu.Sekarang, JenisPemilikKartu.Siswa, siswa.Id),
                pesan => PesanGalat = pesan,
                "NotifikasiViewModel.LihatQrSiswa"))
        {
            return;
        }

        TampilkanQrDiminta?.Invoke(this, new QrTampil(siswa.Nama, siswa.Nisn, LayananPairing.BuatUrlQr(UsernameBot!, token!)));
        MuatSemua();
    }

    private void LihatQrGuru(GuruDaftar guru)
    {
        if (guru.TertautTelegram
            && !Setuju($"{guru.Nama} sudah tertaut Telegram. Membuat QR baru MEMUTUS tautan yang lama — guru harus memindai QR baru. Lanjutkan?"))
        {
            return;
        }

        PesanGalat = null;
        string? token = null;
        if (!PenangananGalat.Coba(
                () => token = _layananPairing.BuatTokenBaru(_connection, _akun, _penyediaWaktu.Sekarang, JenisPemilikKartu.Guru, guru.Id),
                pesan => PesanGalat = pesan,
                "NotifikasiViewModel.LihatQrGuru"))
        {
            return;
        }

        TampilkanQrDiminta?.Invoke(this, new QrTampil(guru.Nama, guru.Nip, LayananPairing.BuatUrlQr(UsernameBot!, token!)));
        MuatSemua();
    }

    // PRD §16.6 langkah 4: "Admin mencetak QR massal per kelas" — siswa
    // yang sudah tertaut dilewati (LayananPairing.PastikanTokenUntukCetakMassal).
    private void CetakMassal()
    {
        var daftar = new List<QrTampil>();
        PesanGalat = null;
        PesanStatus = null;
        if (!PenangananGalat.Coba(
                () =>
                {
                    foreach (var siswa in SiswaTampil)
                    {
                        var token = _layananPairing.PastikanTokenUntukCetakMassal(_connection, _akun, _penyediaWaktu.Sekarang, JenisPemilikKartu.Siswa, siswa.Id);
                        if (token is not null)
                        {
                            daftar.Add(new QrTampil(siswa.Nama, siswa.Nisn, LayananPairing.BuatUrlQr(UsernameBot!, token)));
                        }
                    }
                },
                pesan => PesanGalat = pesan,
                "NotifikasiViewModel.CetakMassal"))
        {
            return;
        }

        if (daftar.Count == 0)
        {
            PesanStatus = "Seluruh siswa di kelas ini sudah tertaut Telegram.";
            return;
        }

        CetakMassalSiap?.Invoke(this, daftar);
        PesanStatus = $"{daftar.Count} QR siap dicetak.";
        MuatSemua();
    }

    private void MuatSemua()
    {
        _semuaSiswa = _repositoriSiswa.DaftarUntukTahunAjaran(_connection, _tahunAjaranId);
        TerapkanFilterKelas();

        DaftarGuru.Clear();
        foreach (var guru in _repositoriGuru.DaftarLengkap(_connection))
        {
            DaftarGuru.Add(guru);
        }

        Antrean.Clear();
        foreach (var baris in _repositoriOutbox.DaftarAntre(_connection))
        {
            Antrean.Add(baris);
        }

        Riwayat.Clear();
        foreach (var baris in _repositoriOutbox.DaftarRiwayat(_connection))
        {
            Riwayat.Add(baris);
        }
    }

    private void TerapkanFilterKelas()
    {
        var tersaring = _kelasTerpilih is null
            ? _semuaSiswa
            : _semuaSiswa.Where(s => s.KelasNama == _kelasTerpilih.Nama).ToList();

        SiswaTampil.Clear();
        foreach (var siswa in tersaring)
        {
            SiswaTampil.Add(siswa);
        }

        if (_kelasTerpilih is null)
        {
            RingkasanPairingKelas = null;
            return;
        }

        var tertaut = tersaring.Count(s => s.TertautTelegram);
        RingkasanPairingKelas = $"Kelas {_kelasTerpilih.Nama}: {tertaut} dari {tersaring.Count} tertaut Telegram";
    }
}
