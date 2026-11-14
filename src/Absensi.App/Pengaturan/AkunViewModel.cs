using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Pengaturan;

// RENCANA-TUGAS.md Tugas 14 — layar Manajemen Akun (Pengaturan → Akun).
// Backend (LayananAkun.BuatAkun) sudah ada sejak Tugas 4; sebelumnya tidak
// ada satu pun layar yang memanggilnya, jadi sekolah tidak bisa membuat
// akun Admin/Wali Kelas lewat aplikasi sama sekali — celah yang baru
// ketahuan lewat panduan pemasangan Tugas 14. PRD §3: wali kelas "tidak
// bisa akses menu ini sama sekali" — bukan sekadar tombol disembunyikan,
// LayananAkun sendiri menolak (PRD §24.3); BolehAkses di
// sini hanya menentukan apakah tab ini ditampilkan sama sekali.
public sealed class AkunViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly LayananAkun _layananAkun = new();
    private readonly RepositoriGuru _repositoriGuru = new();

    private string? _pesanGalat;
    private string? _pesanStatus;

    public AkunViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;

        TambahAkunCommand = new RelayCommand(TambahAkun, () => BolehAkses);
        NonaktifkanCommand = new RelayCommand<AkunDaftarTampilan>(Nonaktifkan, _ => BolehAkses);
        ResetSandiCommand = new RelayCommand<AkunDaftarTampilan>(ResetSandi, _ => BolehAkses);

        if (BolehAkses)
        {
            MuatDaftar();
        }
    }

    public bool BolehAkses => _akun.Peran != PeranAkun.WaliKelas;

    public ObservableCollection<AkunDaftarTampilan> DaftarAkun { get; } = new();

    public string? PesanGalat { get => _pesanGalat; private set => SetField(ref _pesanGalat, value); }

    public string? PesanStatus { get => _pesanStatus; private set => SetField(ref _pesanStatus, value); }

    public RelayCommand TambahAkunCommand { get; }

    public RelayCommand<AkunDaftarTampilan> NonaktifkanCommand { get; }

    public RelayCommand<AkunDaftarTampilan> ResetSandiCommand { get; }

    private void MuatDaftar()
    {
        DaftarAkun.Clear();
        foreach (var akun in _layananAkun.DaftarAkun(_connection, _akun))
        {
            DaftarAkun.Add(new AkunDaftarTampilan(akun,
                akun.Aktif && LayananAkun.MayManageAccount(_akun, akun.Id, akun.Peran)));
        }
    }

    private void TambahAkun()
    {
        var daftarGuru = _repositoriGuru.DaftarAktifRingkas(_connection);
        var dialogViewModel = new TambahAkunViewModel(_connection, _akun, _penyediaWaktu, daftarGuru);
        var dialog = new TambahAkunWindow(dialogViewModel);

        if (dialog.ShowDialog() != true || dialog.AkunBaru is null)
        {
            return;
        }

        PesanGalat = null;
        PesanStatus = $"Akun {dialog.AkunBaru.Username} berhasil dibuat.";
        MuatDaftar();
    }

    private void Nonaktifkan(AkunDaftarTampilan tampilan)
    {
        try
        {
            _layananAkun.NonaktifkanAkun(_connection, _akun, _penyediaWaktu.Sekarang, tampilan.Akun.Id);
            PesanGalat = null;
            PesanStatus = $"Akun {tampilan.Username} dinonaktifkan.";
            MuatDaftar();
        }
        catch (InvalidOperationException ex)
        {
            PesanStatus = null;
            PesanGalat = ex.Message;
        }
    }

    private void ResetSandi(AkunDaftarTampilan tampilan)
    {
        var dialogViewModel = new ResetSandiViewModel(_connection, _akun, _penyediaWaktu, tampilan.Username, tampilan.Akun.Id);
        var dialog = new ResetSandiWindow(dialogViewModel);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        PesanGalat = null;
        PesanStatus = $"Sandi {tampilan.Username} berhasil diganti.";
    }
}
