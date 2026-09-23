using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using Absensi.App.Mvvm;
using Absensi.Data;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Gerbang;

// RENCANA-TUGAS.md Tugas 6 — Layar Gerbang. Logika penerimaan/penampilan
// tap dipisah dari DispatcherTimer dan pemutar bunyi sungguhan (lewat
// IPenyediaWaktu/IPemutarBunyi) supaya bisa diuji tanpa jendela WPF hidup.
public sealed class GerbangViewModel : ObservableObject
{
    public const int MaksimalKartuAktif = 4;
    private static readonly TimeSpan DurasiTampil = TimeSpan.FromSeconds(8);

    private readonly SqliteConnection _connection;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly IPemutarBunyi _pemutarBunyi;
    private readonly CacheTampilanGerbang _cache;
    private readonly StatusModeRegistrasi _statusModeRegistrasi;
    private readonly RepositoriKartu _repositoriKartu = new();
    private readonly RepositoriAbsensi _repositoriAbsensi = new();
    private readonly PemrosesTap _pemrosesTap = new();

    private string _jamSekarangTeks = string.Empty;
    private int _ringkasanHadir;
    private int _ringkasanTerlambat;
    private int _ringkasanBelumTap;
    private int _ringkasanGuru;

    public GerbangViewModel(
        SqliteConnection connection,
        IPenyediaWaktu penyediaWaktu,
        IPemutarBunyi pemutarBunyi,
        CacheTampilanGerbang cache,
        StatusModeRegistrasi statusModeRegistrasi)
    {
        _connection = connection;
        _penyediaWaktu = penyediaWaktu;
        _pemutarBunyi = pemutarBunyi;
        _cache = cache;
        _statusModeRegistrasi = statusModeRegistrasi;
        PerbaruiJam();
        PerbaruiRingkasanHarian();
    }

    // PRD §14.1: "nama sekolah tampil di header" + logo — dimuat sekali
    // lewat CacheTampilanGerbang.Muat saat layar dibuka.
    public string NamaSekolah => _cache.NamaSekolah;

    public BitmapImage? Logo => _cache.Logo;

    // Sampai empat, sesuai PANDUAN-UI-UX.md §8.2. Kartu tertua turun ke
    // Ringkasan saat penuh atau saat sudah 8 detik (EvaluasiKartuKedaluwarsa).
    public ObservableCollection<KartuTapViewModel> KartuAktif { get; } = new();

    public ObservableCollection<KartuTapViewModel> Ringkasan { get; } = new();

    private TipografiGerbang _tipografi = TipografiGerbang.PresetBesar;

    // Ukuran teks dan jarak sesuai tinggi layar (lihat TipografiGerbang).
    // Diatur GerbangWindow setiap ukuran jendela berubah; bawaan = ukuran
    // panduan 1920x1080.
    public TipografiGerbang Tipografi
    {
        get => _tipografi;
        private set => SetField(ref _tipografi, value);
    }

    public void SesuaikanDenganTinggi(double tinggiArea) => Tipografi = TipografiGerbang.Untuk(tinggiArea);

    public string JamSekarangTeks
    {
        get => _jamSekarangTeks;
        private set => SetField(ref _jamSekarangTeks, value);
    }

    // Ringkasan harian layar gerbang (PRD §14.1): Hadir / Terlambat / Belum
    // tap / Guru, dihitung ulang setiap tap masuk.
    public int RingkasanHadir
    {
        get => _ringkasanHadir;
        private set => SetField(ref _ringkasanHadir, value);
    }

    public int RingkasanTerlambat
    {
        get => _ringkasanTerlambat;
        private set => SetField(ref _ringkasanTerlambat, value);
    }

    public int RingkasanBelumTap
    {
        get => _ringkasanBelumTap;
        private set => SetField(ref _ringkasanBelumTap, value);
    }

    public int RingkasanGuru
    {
        get => _ringkasanGuru;
        private set => SetField(ref _ringkasanGuru, value);
    }

    public void ProsesTap(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return;
        }

        var waktu = _penyediaWaktu.Sekarang;
        var kartu = _repositoriKartu.CariAktifBerdasarkanUid(_connection, uid);
        var keputusan = _pemrosesTap.Proses(_connection, uid, waktu, _statusModeRegistrasi.Aktif);
        var info = _cache.Cari(kartu?.PemilikTipe, kartu?.PemilikId);
        var (kategori, teksStatus, berhasil) = PemetaanTampilanTap.Petakan(keputusan);

        var kartuTampil = new KartuTapViewModel(
            kartu?.PemilikTipe,
            info?.Nama ?? "Kartu tidak dikenal",
            info?.NisnAtauNip ?? string.Empty,
            info?.Foto,
            teksStatus,
            waktu,
            kategori);

        TambahKartuAktif(kartuTampil);
        PerbaruiRingkasanHarian();

        if (berhasil)
        {
            _pemutarBunyi.Berhasil();
        }
        else
        {
            _pemutarBunyi.Gagal();
        }
    }

    private void PerbaruiRingkasanHarian()
    {
        var tanggal = DateOnly.FromDateTime(_penyediaWaktu.Sekarang);
        RingkasanHadir = _repositoriAbsensi.HitungSiswaBerdasarkanStatus(_connection, tanggal, StatusAbsensiSiswa.Hadir);
        RingkasanTerlambat = _repositoriAbsensi.HitungSiswaBerdasarkanStatus(_connection, tanggal, StatusAbsensiSiswa.Terlambat);
        RingkasanBelumTap = _repositoriAbsensi.HitungSiswaBerdasarkanStatus(_connection, tanggal, StatusAbsensiSiswa.Alpa);
        RingkasanGuru = _repositoriAbsensi.HitungGuruSudahTapMasuk(_connection, tanggal);
    }

    // Dipanggil dari DispatcherTimer milik View setiap detik.
    public void EvaluasiKartuKedaluwarsa()
    {
        var sekarang = _penyediaWaktu.Sekarang;

        var kedaluwarsa = KartuAktif.Where(kartu => sekarang - kartu.Waktu >= DurasiTampil).ToList();
        foreach (var kartu in kedaluwarsa)
        {
            KartuAktif.Remove(kartu);
            Ringkasan.Insert(0, kartu);
        }

        PerbaruiJam();
    }

    private void PerbaruiJam() => JamSekarangTeks = _penyediaWaktu.Sekarang.ToString("HH:mm:ss");

    private void TambahKartuAktif(KartuTapViewModel kartu)
    {
        if (KartuAktif.Count >= MaksimalKartuAktif)
        {
            var tertua = KartuAktif[0];
            KartuAktif.RemoveAt(0);
            Ringkasan.Insert(0, tertua);
        }

        KartuAktif.Add(kartu);
    }
}
