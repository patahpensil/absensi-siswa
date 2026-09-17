using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Gerbang;

// RENCANA-TUGAS.md Tugas 6 (dikoreksi): "Layar tidak bisa ditutup tanpa
// sandi", dibatasi ke kepala sekolah dan admin — wali kelas ditolak walau
// sandinya benar (PRD §3: wali kelas hanya berwenang di kelasnya sendiri,
// tidak termasuk mengendalikan gerbang). Memeriksa akun.Peran langsung,
// bukan lewat PenentuAksesKelas — pertanyaannya bukan "kelas mana" tapi
// "peran apa", jadi tidak butuh id kelas apa pun.
public sealed class KonfirmasiTutupViewModel : ObservableObject
{
    private readonly LayananAkun _layananAkun = new();
    private readonly SqliteConnection _connection;
    private string _username = string.Empty;
    private string _sandi = string.Empty;
    private string? _pesanGalat;

    public KonfirmasiTutupViewModel(SqliteConnection connection)
    {
        _connection = connection;
        KonfirmasiCommand = new RelayCommand(Konfirmasi, BisaKonfirmasi);
    }

    public event EventHandler? DiizinkanTutup;

    public string Username
    {
        get => _username;
        set
        {
            if (SetField(ref _username, value))
            {
                KonfirmasiCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? PesanGalat
    {
        get => _pesanGalat;
        private set => SetField(ref _pesanGalat, value);
    }

    public RelayCommand KonfirmasiCommand { get; }

    public void AturSandi(string sandi)
    {
        _sandi = sandi;
        KonfirmasiCommand.RaiseCanExecuteChanged();
    }

    private bool BisaKonfirmasi() => !string.IsNullOrWhiteSpace(_username) && !string.IsNullOrEmpty(_sandi);

    private void Konfirmasi()
    {
        var akun = _layananAkun.Autentikasi(_connection, _username, _sandi);
        if (akun is null)
        {
            PesanGalat = "Username atau sandi salah.";
            return;
        }

        if (akun.Peran is not (PeranAkun.KepalaSekolah or PeranAkun.Admin))
        {
            PesanGalat = "Hanya kepala sekolah atau admin yang bisa menutup layar gerbang.";
            return;
        }

        PesanGalat = null;
        DiizinkanTutup?.Invoke(this, EventArgs.Empty);
    }
}
