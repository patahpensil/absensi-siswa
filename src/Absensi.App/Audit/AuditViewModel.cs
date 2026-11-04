using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Audit;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Audit;

// Master Bug & UI/UX Register A-39 Tahap 2 + A-40 (keputusan pemilik
// proyek 2026-09-25): layar Audit MINIMUM -- baca-saja, gabungan riwayat
// database aktif + arsip pra-restore (A-39 Tahap 1). HANYA Admin. Tidak
// ada ekspor/cetak di versi ini -- keputusan sengaja (A-40: nilai audit
// tidak boleh keluar lewat jalur selain layar ini).
public sealed class AuditViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly string _folderArsipAudit;
    private readonly LayananAudit _layananAudit = new();

    private DateTime _dariTanggal;
    private DateTime _sampaiTanggal;
    private string? _pesanGalat;

    public AuditViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu, string folderArsipAudit)
    {
        _connection = connection;
        _akun = akun;
        _folderArsipAudit = folderArsipAudit;

        _sampaiTanggal = penyediaWaktu.Sekarang.Date;
        _dariTanggal = _sampaiTanggal.AddDays(-90); // PRD §12: 90 hari terakhir sebagai bawaan

        MuatUlangCommand = new RelayCommand(MuatUlang);

        if (BolehAkses)
        {
            MuatUlang();
        }
        else
        {
            PesanGalat = "Hanya Admin yang dapat membuka layar Audit.";
        }
    }

    // A-40: diperiksa juga di LayananAudit (PRD §24.3) --
    // properti ini cuma menyembunyikan konten DI DALAM layar ini sendiri.
    // Menu "Audit" di sidebar sudah difilter terpisah di MainWindowViewModel
    // supaya non-Admin tidak sampai membuka layar ini sama sekali.
    public bool BolehAkses => _akun.Peran == PeranAkun.Admin;

    public ObservableCollection<BarisAuditGabungan> Baris { get; } = new();

    public ObservableCollection<string> PesanArsipBermasalah { get; } = new();

    public bool AdaArsipBermasalah => PesanArsipBermasalah.Count > 0;

    public DateTime DariTanggal
    {
        get => _dariTanggal;
        set
        {
            if (SetField(ref _dariTanggal, value))
            {
                MuatUlang();
            }
        }
    }

    public DateTime SampaiTanggal
    {
        get => _sampaiTanggal;
        set
        {
            if (SetField(ref _sampaiTanggal, value))
            {
                MuatUlang();
            }
        }
    }

    public string? PesanGalat { get => _pesanGalat; private set => SetField(ref _pesanGalat, value); }

    public RelayCommand MuatUlangCommand { get; }

    public void MuatUlang()
    {
        if (!BolehAkses)
        {
            return;
        }

        try
        {
            // Akhir hari (23:59:59.999...) supaya SampaiTanggal yang cuma
            // tanggal (tanpa jam) tetap mencakup SELURUH hari itu.
            var akhirHari = _sampaiTanggal.Date.AddDays(1).AddTicks(-1);
            var hasil = _layananAudit.Baca(_connection, _akun, _folderArsipAudit, _dariTanggal.Date, akhirHari);

            Baris.Clear();
            foreach (var baris in hasil.Baris)
            {
                Baris.Add(baris);
            }

            PesanArsipBermasalah.Clear();
            foreach (var arsip in hasil.ArsipBermasalah)
            {
                PesanArsipBermasalah.Add(
                    $"Riwayat sekitar {arsip.WaktuRestore:d MMMM yyyy} tidak dapat ditampilkan ({arsip.Alasan}) — arsip \"{arsip.NamaBerkas}\".");
            }

            OnPropertyChanged(nameof(AdaArsipBermasalah));
            PesanGalat = null;
        }
        catch (InvalidOperationException ex)
        {
            PesanGalat = ex.Message;
        }
    }
}
