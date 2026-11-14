using System.Windows.Media.Imaging;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Lisensi;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Login;

public sealed class LoginViewModel : ObservableObject
{
    private readonly LayananAkun _layananAkun = new();
    private readonly SqliteConnection _connection;
    private readonly IPenyediaWaktu _clock;
    private string _username = string.Empty;
    private string _sandi = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string? _pesanGalat;
    private AkunTersimpan? _pendingAccount;

    public LoginViewModel(SqliteConnection connection, string nomorVersi, string? namaSekolah, string? logoPath, StatusLisensi? statusLisensi,
        IPenyediaWaktu? clock = null)
    {
        _connection = connection;
        _clock = clock ?? new PenyediaWaktuSistem();
        NomorVersi = nomorVersi;
        NamaSekolah = string.IsNullOrWhiteSpace(namaSekolah) ? "Nama sekolah belum diatur" : namaSekolah;
        // PANDUAN-UI-UX.md §9 (layar Login): "Logo, nama sekolah, ... status
        // lisensi" — RENCANA-TUGAS.md Tugas 13 mengisi keduanya, sebelumnya
        // logo selalu placeholder lingkaran biru dan lisensi "(belum tersedia)".
        Logo = PemuatGambar.MuatDariFolder(LokasiData.FolderData, logoPath);
        TeksStatusLisensi = statusLisensi is { Aktif: true } ? "Lisensi: Aktif" : "Lisensi: (belum tersedia)";
        LoginCommand = new RelayCommand(Login, BisaLogin);
        ChangePasswordCommand = new RelayCommand(ChangePassword, CanChangePassword);
    }

    // P-21: after a reset by Kepala Sekolah/Admin the owner sets a new password
    // before the main window opens.
    public bool MustChangePassword => _pendingAccount is not null;

    public bool ShowLogin => !MustChangePassword;

    public RelayCommand ChangePasswordCommand { get; }

    public void SetNewPassword(string value)
    {
        _newPassword = value;
        ChangePasswordCommand.RaiseCanExecuteChanged();
    }

    public void SetConfirmPassword(string value)
    {
        _confirmPassword = value;
        ChangePasswordCommand.RaiseCanExecuteChanged();
    }

    public event EventHandler<AkunTersimpan>? LoginBerhasil;

    public string NomorVersi { get; }

    public string NamaSekolah { get; }

    public BitmapImage? Logo { get; }

    public bool TampilkanPlaceholderLogo => Logo is null;

    public string TeksStatusLisensi { get; }

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
        if (_layananAkun.RequiresPasswordChange(_connection, akun))
        {
            _pendingAccount = akun;
            OnPropertyChanged(nameof(MustChangePassword));
            OnPropertyChanged(nameof(ShowLogin));
            ChangePasswordCommand.RaiseCanExecuteChanged();
            return;
        }

        LoginBerhasil?.Invoke(this, akun);
    }

    private bool CanChangePassword() => _pendingAccount is not null && !string.IsNullOrEmpty(_newPassword);

    private void ChangePassword()
    {
        if (_pendingAccount is not { } account)
        {
            return;
        }

        if (_newPassword != _confirmPassword)
        {
            PesanGalat = "Ulangi sandi baru tidak sama.";
            return;
        }

        try
        {
            _layananAkun.ChangeOwnPassword(_connection, account, _clock.Sekarang, _sandi, _newPassword);
        }
        catch (InvalidOperationException ex)
        {
            PesanGalat = ex.Message;
            return;
        }
        catch (SqliteException)
        {
            PesanGalat = "Sandi belum dapat disimpan. Coba kembali.";
            return;
        }

        var refreshed = new RepositoriAkun().CariBerdasarkanId(_connection, account.Id) ?? account;
        _pendingAccount = null;
        PesanGalat = null;
        LoginBerhasil?.Invoke(this, refreshed);
    }
}
