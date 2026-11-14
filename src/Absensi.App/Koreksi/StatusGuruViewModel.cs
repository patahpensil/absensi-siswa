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

// RENCANA-TUGAS.md Tugas 11 — layar Guru→Kehadiran (PRD §16.17). Hanya
// Kepala Sekolah berwenang (PRD §17.10) — beda dari kebanyakan layar lain
// yang mengizinkan Admin juga; wali kelas dan admin sama-sama tidak boleh
// di sini.
public sealed class StatusGuruViewModel : ObservableObject
{
    // Masuk/TidakLengkap sengaja tidak ditawarkan — keduanya hasil tap
    // otomatis (PRD §17.3/§16.16), bukan sesuatu yang Kepala Sekolah
    // tetapkan manual. PRD §16.17 langkah 3: DI MUKA menawarkan Izin/Dinas
    // Luar/Cuti, SETELAH mengizinkan "kategori lain" — Hadir dan Alpa
    // disertakan supaya koreksi "setelah" bisa mengembalikan ke keduanya.
    private static readonly StatusAbsensiGuru[] StatusPilihan =
    {
        StatusAbsensiGuru.Alpa, StatusAbsensiGuru.Hadir, StatusAbsensiGuru.Izin,
        StatusAbsensiGuru.Sakit, StatusAbsensiGuru.DinasLuar, StatusAbsensiGuru.Cuti,
    };

    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly RepositoriAbsensi _repositoriAbsensi = new();
    private readonly LayananStatusGuru _layananStatusGuru = new();

    private DateTime _tanggalTerpilih;
    private GuruUntukKehadiran? _guruTerpilih;
    private StatusAbsensiGuru _statusBaru = StatusAbsensiGuru.Izin;
    private string? _keterangan;
    private string? _pesanGalat;
    private string? _pesanStatus;

    public StatusGuruViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _tanggalTerpilih = penyediaWaktu.Sekarang.Date;

        SimpanCommand = new RelayCommand(Simpan, BisaSimpan);

        MuatDaftar();
    }

    public bool BolehMengubah => _akun.Peran is PeranAkun.KepalaSekolah or PeranAkun.Admin;

    public bool TidakBolehMengubah => !BolehMengubah;

    public bool BulanTerkunci => _layananStatusGuru.BulanTerkunci(_connection, DateOnly.FromDateTime(_tanggalTerpilih));

    public ObservableCollection<GuruUntukKehadiran> Daftar { get; } = new();

    public IReadOnlyList<StatusAbsensiGuru> DaftarStatus { get; } = StatusPilihan;

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
            }
        }
    }

    public GuruUntukKehadiran? GuruTerpilih
    {
        get => _guruTerpilih;
        set
        {
            if (SetField(ref _guruTerpilih, value))
            {
                StatusBaru = value?.Status ?? StatusAbsensiGuru.Izin;
                Keterangan = value?.Keterangan;
                SimpanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public StatusAbsensiGuru StatusBaru { get => _statusBaru; set => SetField(ref _statusBaru, value); }

    public string? Keterangan { get => _keterangan; set => SetField(ref _keterangan, value); }

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

    private bool BisaSimpan() => BolehMengubah && _guruTerpilih is not null && !BulanTerkunci;

    private void MuatDaftar()
    {
        Daftar.Clear();
        GuruTerpilih = null;
        PesanStatus = null;

        foreach (var guru in _repositoriAbsensi.DaftarGuruUntukKehadiran(_connection, DateOnly.FromDateTime(_tanggalTerpilih)))
        {
            Daftar.Add(guru);
        }
    }

    private void Simpan()
    {
        if (_guruTerpilih is not { } guru)
        {
            return;
        }

        PesanStatus = null;
        if (!PenangananGalat.Coba(
                () => _layananStatusGuru.TetapkanStatus(_connection, _akun, _penyediaWaktu.Sekarang, guru.GuruId, DateOnly.FromDateTime(_tanggalTerpilih), StatusBaru, Keterangan),
                pesan => PesanGalat = pesan,
                "StatusGuruViewModel.Simpan"))
        {
            return;
        }

        PesanGalat = null;
        PesanStatus = $"Status {guru.Nama} tersimpan.";
        MuatDaftar();
    }
}
