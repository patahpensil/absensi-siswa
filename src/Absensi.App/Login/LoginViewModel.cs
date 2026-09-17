using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Login;

public sealed class LoginViewModel : ObservableObject
{
    private readonly LayananAkun _layananAkun = new();
    private readonly SqliteConnection _connection;
    private string _username = string.Empty;
    private string _sandi = string.Empty;
    private string? _pesanGalat;

    public LoginViewModel(SqliteConnection connection, string nomorVersi, string? namaSekolah)
    {
        _connection = connection;
        NomorVersi = nomorVersi;
        NamaSekolah = string.IsNullOrWhiteSpace(namaSekolah) ? "Nama sekolah belum diatur" : namaSekolah;
        LoginCommand = new RelayCommand(Login, BisaLogin);
    }

    public event EventHandler<AkunTersimpan>? LoginBerhasil;

    public string NomorVersi { get; }

    public string NamaSekolah { get; }

    public string Username
    {
        get => _username;
        set
        {
            if (SetField(ref _username, value))
            {
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? PesanGalat
    {
        get => _pesanGalat;
        private set => SetField(ref _pesanGalat, value);
    }

    public RelayCommand LoginCommand { get; }

    // PasswordBox.Password bukan DependencyProperty (alasan keamanan), jadi
    // tidak bisa dua-arah lewat binding biasa — code-behind memanggil ini
    // dari event PasswordChanged.
    public void AturSandi(string sandi)
    {
        _sandi = sandi;
        LoginCommand.RaiseCanExecuteChanged();
    }

    private bool BisaLogin() => !string.IsNullOrWhiteSpace(_username) && !string.IsNullOrEmpty(_sandi);

    private void Login()
    {
        var akun = _layananAkun.Autentikasi(_connection, _username, _sandi);
        if (akun is null)
        {
            PesanGalat = "Username atau sandi salah.";
            return;
        }

        PesanGalat = null;
        LoginBerhasil?.Invoke(this, akun);
    }
}
