using Absensi.App.Mvvm;
using Absensi.Data.Kartu;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.App.RegistrasiKartu;

// Dialog satu-kartu dipakai dari layar Siswa/Guru untuk dua kasus di luar
// alur per-kelas Tugas 8: kartu pertama untuk satu orang (dipakai guru,
// karena PRD §16.5 hanya menjabarkan alur per kelas untuk siswa) dan
// penggantian kartu hilang (PRD §16.11). Mode Registrasi aktif selama
// jendela ini terbuka, mati otomatis saat ditutup (gagal-aman, sama
// seperti RegistrasiKartuViewModel).
public sealed class TapKartuTunggalViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly StatusModeRegistrasi _statusModeRegistrasi;
    private readonly LayananRegistrasiKartu _layanan = new();
    private readonly int _tahunAjaranId;
    private readonly JenisPemilikKartu _pemilikTipe;
    private readonly int _pemilikId;
    private readonly bool _modeGanti;

    private string? _pesanGalat;

    public TapKartuTunggalViewModel(
        SqliteConnection connection,
        AkunTersimpan akun,
        IPenyediaWaktu penyediaWaktu,
        StatusModeRegistrasi statusModeRegistrasi,
        int tahunAjaranId,
        JenisPemilikKartu pemilikTipe,
        int pemilikId,
        string namaPemilik,
        bool modeGanti)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _statusModeRegistrasi = statusModeRegistrasi;
        _tahunAjaranId = tahunAjaranId;
        _pemilikTipe = pemilikTipe;
        _pemilikId = pemilikId;
        _modeGanti = modeGanti;
        NamaPemilik = namaPemilik;

        _statusModeRegistrasi.Aktifkan();
    }

    public string NamaPemilik { get; }

    public string Judul => _modeGanti ? $"Ganti kartu — {NamaPemilik}" : $"Daftarkan kartu — {NamaPemilik}";

    public string Instruksi => _modeGanti
        ? "Tempelkan kartu baru pada reader. Kartu lama otomatis dinonaktifkan."
        : "Tempelkan kartu pada reader untuk mendaftarkan.";

    public string? PesanGalat
    {
        get => _pesanGalat;
        private set => SetField(ref _pesanGalat, value);
    }

    // View menutup jendela saat ini terpicu (setelah tap berhasil disimpan).
    public event EventHandler? Selesai;

    public void TapUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return;
        }

        PesanGalat = null;

        var pemilikAktif = _layanan.CariPemilikAktif(_connection, uid, _tahunAjaranId);
        if (pemilikAktif is not null)
        {
            PesanGalat = pemilikAktif.KelasNama is { } kelasNama
                ? $"Kartu ini sudah dipakai {pemilikAktif.Nama} di kelas {kelasNama}."
                : $"Kartu ini sudah dipakai {pemilikAktif.Nama}.";
            return;
        }

        var waktu = _penyediaWaktu.Sekarang;
        // Penolakan layanan (peran, integritas kartu) atau galat database
        // ditampilkan sebagai PesanGalat; jendela tetap terbuka dan tidak
        // menutup seolah kartu tersimpan.
        var saved = PenangananGalat.Coba(
            () =>
            {
                if (_modeGanti)
                {
                    _layanan.GantiKartuHilang(_connection, _akun, waktu, _pemilikTipe, _pemilikId, uid);
                }
                else
                {
                    _layanan.DaftarkanSesi(_connection, _akun, waktu, new[] { new PendaftaranKartu(uid, _pemilikTipe, _pemilikId, NamaPemilik) });
                }
            },
            message => PesanGalat = message,
            "TapKartuTunggalViewModel.TapUid");

        if (!saved)
        {
            return;
        }

        Selesai?.Invoke(this, EventArgs.Empty);
    }

    public void Batal() => Selesai?.Invoke(this, EventArgs.Empty);

    // Dipanggil Window.OnClosed apa pun cara jendela ditutup — gagal-aman
    // sesuai StatusModeRegistrasi.
    public void MatikanModeRegistrasi() => _statusModeRegistrasi.Matikan();
}
