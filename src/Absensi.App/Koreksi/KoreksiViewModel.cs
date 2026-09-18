using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Koreksi;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Koreksi;

// RENCANA-TUGAS.md Tugas 11 — layar Koreksi (PRD §16.10) + Kunci Bulan
// (PRD §10). Daftar kelas hanya memuat kelas yang boleh disentuh akun ini
// (LayananAkun.BolehMenyentuhKelas) — wali kelas hanya melihat kelasnya
// sendiri, tidak perlu memilih lalu ditolak.
public sealed class KoreksiViewModel : ObservableObject
{
    // PRD §16.10 langkah 5: "Ubah status siswa (Hadir / Terlambat / Izin /
    // Sakit / Alpa)" — persis lima ini, Bolos sengaja tidak termasuk
    // (ditetapkan sistem lewat LayananNotifikasi.JalankanFinalisasiBolos,
    // bukan manual).
    private static readonly StatusAbsensiSiswa[] StatusPilihan =
    {
        StatusAbsensiSiswa.Hadir, StatusAbsensiSiswa.Terlambat, StatusAbsensiSiswa.Izin,
        StatusAbsensiSiswa.Sakit, StatusAbsensiSiswa.Alpa,
    };

    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly RepositoriAbsensi _repositoriAbsensi = new();
    private readonly LayananKoreksi _layananKoreksi = new();
    private readonly LayananKuncimBulan _layananKuncimBulan = new();

    private DateTime _tanggalTerpilih;
    private KelasDaftar? _kelasTerpilih;
    private SiswaUntukKoreksi? _siswaTerpilih;
    private StatusAbsensiSiswa _statusBaru = StatusAbsensiSiswa.Hadir;
    private string? _keterangan;
    private string? _atasPermintaan;
    private string? _pesanGalat;
    private string? _pesanStatus;

    public KoreksiViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu, int tahunAjaranId)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _tanggalTerpilih = penyediaWaktu.Sekarang.Date;

        var layananAkun = new LayananAkun();
        foreach (var kelas in new RepositoriKelas().DaftarUntukTahunAjaran(connection, tahunAjaranId))
        {
            if (layananAkun.BolehMenyentuhKelas(connection, akun, kelas.Id))
            {
                DaftarKelas.Add(kelas);
            }
        }

        SimpanCommand = new RelayCommand(Simpan, BisaSimpan);
        KunciBulanCommand = new RelayCommand(BukaDialogKunciBulan, () => BolehKunciBulan && !BulanTerkunci);

        MuatDaftar();
    }

    // PRD §3.2 "Delegasi Koreksi": kolom ini hanya bermakna saat admin/
    // kepala sekolah mengeksekusi atas nama wali kelas — wali kelas
    // sendiri tidak perlu mengisinya (mereka bertindak atas nama sendiri).
    public bool TampilkanAtasPermintaan => _akun.Peran != PeranAkun.WaliKelas;

    // PRD §10 / Tugas 11: kunci bulan kewenangan penuh (kepala sekolah/admin).
    public bool BolehKunciBulan => _akun.Peran != PeranAkun.WaliKelas;

    public bool BulanTerkunci => _layananKoreksi.BulanTerkunci(_connection, DateOnly.FromDateTime(_tanggalTerpilih));

    public ObservableCollection<KelasDaftar> DaftarKelas { get; } = new();

    public IReadOnlyList<StatusAbsensiSiswa> DaftarStatus { get; } = StatusPilihan;

    public ObservableCollection<SiswaUntukKoreksi> Daftar { get; } = new();

    public DateTime TanggalTerpilih
    {
        get => _tanggalTerpilih;
        set
        {
            if (SetField(ref _tanggalTerpilih, value))
            {
                OnPropertyChanged(nameof(BulanTerkunci));
                MuatDaftar();
                SimpanCommand.RaiseCanExecuteChanged();
                KunciBulanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public KelasDaftar? KelasTerpilih
    {
        get => _kelasTerpilih;
        set
        {
            if (SetField(ref _kelasTerpilih, value))
            {
                MuatDaftar();
            }
        }
    }

    public SiswaUntukKoreksi? SiswaTerpilih
    {
        get => _siswaTerpilih;
        set
        {
            if (SetField(ref _siswaTerpilih, value))
            {
                StatusBaru = value?.Status ?? StatusAbsensiSiswa.Hadir;
                Keterangan = value?.Keterangan;
                SimpanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public StatusAbsensiSiswa StatusBaru { get => _statusBaru; set => SetField(ref _statusBaru, value); }

    public string? Keterangan { get => _keterangan; set => SetField(ref _keterangan, value); }

    public string? AtasPermintaan { get => _atasPermintaan; set => SetField(ref _atasPermintaan, value); }

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

    public RelayCommand SimpanCommand { get; }

    public RelayCommand KunciBulanCommand { get; }

    private bool BisaSimpan() => _siswaTerpilih is not null && _kelasTerpilih is not null && !BulanTerkunci;

    private void MuatDaftar()
    {
        Daftar.Clear();
        SiswaTerpilih = null;
        PesanStatus = null;

        if (_kelasTerpilih is null)
        {
            return;
        }

        foreach (var siswa in _repositoriAbsensi.DaftarUntukKoreksi(_connection, DateOnly.FromDateTime(_tanggalTerpilih), _kelasTerpilih.Id))
        {
            Daftar.Add(siswa);
        }
    }

    private void Simpan()
    {
        if (_siswaTerpilih is not { } siswa || _kelasTerpilih is not { } kelas)
        {
            return;
        }

        _layananKoreksi.KoreksiStatusSiswa(
            _connection, _akun, _penyediaWaktu.Sekarang,
            kelas.Id, siswa.SiswaId, siswa.Nama, DateOnly.FromDateTime(_tanggalTerpilih),
            StatusBaru, Keterangan, TampilkanAtasPermintaan ? AtasPermintaan : null);

        PesanGalat = null;
        PesanStatus = $"Status {siswa.Nama} tersimpan.";
        MuatDaftar();
    }

    private void BukaDialogKunciBulan()
    {
        var dialogViewModel = new KonfirmasiKunciBulanViewModel(_tanggalTerpilih.Year, _tanggalTerpilih.Month);
        var dialog = new KonfirmasiKunciBulanWindow(dialogViewModel);
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _layananKuncimBulan.KunciBulan(_connection, _akun, _penyediaWaktu.Sekarang, _tanggalTerpilih.Year, _tanggalTerpilih.Month);
        OnPropertyChanged(nameof(BulanTerkunci));
        SimpanCommand.RaiseCanExecuteChanged();
        KunciBulanCommand.RaiseCanExecuteChanged();
        PesanStatus = $"Bulan {_tanggalTerpilih:MM/yyyy} dikunci.";
    }
}
