using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Pengaturan;

// RENCANA-TUGAS.md Tugas 14 — dialog "Reset sandi" (Pengaturan → Akun).
// Hanya isi sandi baru — sandi lama tidak pernah ditampilkan (memang tidak
// tersimpan sebagai teks biasa, hanya hash-nya).
public sealed class ResetSandiViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _pembuat;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly int _akunIdTarget;
    private readonly LayananAkun _layananAkun = new();

    private string _sandiBaru = string.Empty;
    private string _konfirmasiSandi = string.Empty;
    private string? _pesanGalat;

    public ResetSandiViewModel(SqliteConnection connection, AkunTersimpan pembuat, IPenyediaWaktu penyediaWaktu, string usernameTarget, int akunIdTarget)
    {
        _connection = connection;
        _pembuat = pembuat;
        _penyediaWaktu = penyediaWaktu;
        _akunIdTarget = akunIdTarget;
        UsernameTarget = usernameTarget;

        SimpanCommand = new RelayCommand(Simpan, BisaSimpan);
    }

    public event EventHandler? Selesai;

    public string UsernameTarget { get; }

    public string? PesanGalat { get => _pesanGalat; private set => SetField(ref _pesanGalat, value); }

    public RelayCommand SimpanCommand { get; }

    public void AturSandiBaru(string sandi)
    {
        _sandiBaru = sandi;
        SimpanCommand.RaiseCanExecuteChanged();
    }

    public void AturKonfirmasiSandi(string konfirmasiSandi)
    {
        _konfirmasiSandi = konfirmasiSandi;
        SimpanCommand.RaiseCanExecuteChanged();
    }

    private bool BisaSimpan() => !string.IsNullOrEmpty(_sandiBaru) && _sandiBaru == _konfirmasiSandi;

    private void Simpan()
    {
        try
        {
            _layananAkun.ResetSandi(_connection, _pembuat, _penyediaWaktu.Sekarang, _akunIdTarget, _sandiBaru);
            PesanGalat = null;
            Selesai?.Invoke(this, EventArgs.Empty);
        }
        catch (InvalidOperationException ex)
        {
            PesanGalat = ex.Message;
        }
    }
}
