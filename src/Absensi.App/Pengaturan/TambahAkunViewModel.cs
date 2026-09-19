using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Pengaturan;

public sealed record PeranPilihan(PeranAkun Peran, string Teks);

// RENCANA-TUGAS.md Tugas 14 — dialog "Tambah akun" (Pengaturan → Akun).
// PRD §3.1: "Kepala Sekolah → akun Admin", "Admin → akun Wali Kelas" —
// dropdown Peran hanya menawarkan pilihan yang memang berwenang dibuat
// pembuatnya, supaya tidak menampilkan pilihan yang pasti ditolak
// LayananAkun.BuatAkun (yang tetap memvalidasi ulang, CLAUDE.md §4.5
// aturan 19 — dropdown ini sekadar kenyamanan, bukan satu-satunya penjaga).
public sealed class TambahAkunViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _pembuat;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly LayananAkun _layananAkun = new();

    private PeranPilihan _peranTerpilih;
    private string _username = string.Empty;
    private string _sandi = string.Empty;
    private string _konfirmasiSandi = string.Empty;
    private GuruRingkas? _guruTerpilih;
    private string? _pesanGalat;

    public TambahAkunViewModel(SqliteConnection connection, AkunTersimpan pembuat, IPenyediaWaktu penyediaWaktu, IReadOnlyList<GuruRingkas> daftarGuru)
    {
        _connection = connection;
        _pembuat = pembuat;
        _penyediaWaktu = penyediaWaktu;
        DaftarGuru = daftarGuru;

        PeranTersedia = pembuat.Peran == PeranAkun.KepalaSekolah
            ? new[] { new PeranPilihan(PeranAkun.Admin, "Admin"), new PeranPilihan(PeranAkun.WaliKelas, "Wali Kelas") }
            : new[] { new PeranPilihan(PeranAkun.WaliKelas, "Wali Kelas") };
        _peranTerpilih = PeranTersedia[0];

        SimpanCommand = new RelayCommand(Simpan, BisaSimpan);
    }

    public event EventHandler<AkunTersimpan>? Tersimpan;

    public IReadOnlyList<PeranPilihan> PeranTersedia { get; }

    public IReadOnlyList<GuruRingkas> DaftarGuru { get; }

    public PeranPilihan PeranTerpilih
    {
        get => _peranTerpilih;
        set
        {
            if (SetField(ref _peranTerpilih, value))
            {
                OnPropertyChanged(nameof(GuruWajib));
                SimpanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // PRD (instruksi Tugas 14): "Pilih guru dari daftar (wajib untuk Wali
    // Kelas, opsional untuk Admin)".
    public bool GuruWajib => PeranTerpilih.Peran == PeranAkun.WaliKelas;

    public string Username
    {
        get => _username;
        set
        {
            if (SetField(ref _username, value))
            {
                SimpanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public GuruRingkas? GuruTerpilih
    {
        get => _guruTerpilih;
        set
        {
            if (SetField(ref _guruTerpilih, value))
            {
                SimpanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? PesanGalat { get => _pesanGalat; private set => SetField(ref _pesanGalat, value); }

    public RelayCommand SimpanCommand { get; }

    // PasswordBox.Password bukan DependencyProperty — code-behind
    // memanggil ini dari event PasswordChanged (pola sama seperti
    // LoginWindow/WizardInstalasiWindow).
    public void AturSandi(string sandi)
    {
        _sandi = sandi;
        SimpanCommand.RaiseCanExecuteChanged();
    }

    public void AturKonfirmasiSandi(string konfirmasiSandi)
    {
        _konfirmasiSandi = konfirmasiSandi;
        SimpanCommand.RaiseCanExecuteChanged();
    }

    private bool BisaSimpan() =>
        !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrEmpty(_sandi)
        && _sandi == _konfirmasiSandi
        && (!GuruWajib || GuruTerpilih is not null);

    private void Simpan()
    {
        try
        {
            var akunBaru = _layananAkun.BuatAkun(
                _connection, _pembuat, _penyediaWaktu.Sekarang, Username, _sandi, PeranTerpilih.Peran, GuruTerpilih?.Id);

            PesanGalat = null;
            Tersimpan?.Invoke(this, akunBaru);
        }
        catch (InvalidOperationException ex)
        {
            PesanGalat = ex.Message;
        }
    }
}
