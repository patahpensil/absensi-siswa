using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.Akun;
using Absensi.Data.Diagnostik;
using Absensi.Data.Kartu;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.App.RegistrasiKartu;

// RENCANA-TUGAS.md Tugas 8 — Registrasi Kartu per kelas (PRD §16.5).
// Kursor berjalan otomatis ke siswa berikutnya yang belum punya kartu
// (baik dari database maupun yang baru ditap di sesi ini); UID dicek
// lintas kedua master (§17.9) sebelum diterima. Sesi tidak menyentuh
// database sampai Simpan ditekan — satu transaksi utuh (langkah 5,
// dikonfirmasi pemilik proyek 2026-09-18). Mode Registrasi aktif selama
// jendela ini terbuka dan mati otomatis saat ditutup, disimpan ataupun
// dibatalkan (gagal-aman — lihat StatusModeRegistrasi).
public sealed class RegistrasiKartuViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly StatusModeRegistrasi _statusModeRegistrasi;
    private readonly int _tahunAjaranId;
    private readonly LayananRegistrasiKartu _layanan = new();
    private readonly Dictionary<string, PendaftaranKartu> _pendaftaranSesi = new();
    private readonly HashSet<int> _terdaftarSesiIni = new();

    private IReadOnlyList<SiswaDaftar> _daftarSiswaKelas = Array.Empty<SiswaDaftar>();
    private KelasDaftar? _kelasTerpilih;
    private SiswaDaftar? _siswaSekarang;
    private string? _progresTeks;
    private string? _pesanGalat;
    private string? _pesanStatus;

    public RegistrasiKartuViewModel(
        SqliteConnection connection,
        AkunTersimpan akun,
        IPenyediaWaktu penyediaWaktu,
        StatusModeRegistrasi statusModeRegistrasi,
        int tahunAjaranId)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _statusModeRegistrasi = statusModeRegistrasi;
        _tahunAjaranId = tahunAjaranId;

        // UAT Bug #11 (2026-09-22): dimuat ULANG di sini setiap kali
        // ViewModel ini dibuat — App.xaml.cs BukaRegistrasiKartu() membuat
        // ViewModel baru setiap dialog dibuka (tidak ada cache), jadi ini
        // otomatis mengambil kelas terbaru tanpa perlu restart aplikasi,
        // memakai _tahunAjaranId yang identik dengan yang dipakai panel
        // Kelas (MainWindowViewModel meneruskan nilai yang sama ke
        // keduanya). Dibungkus try/catch supaya kegagalan QUERY (mis.
        // database terkunci) dibedakan dari kelas yang MEMANG belum ada —
        // lihat GagalMuatKelas/TahunAjaranTanpaKelas di bawah, bukan
        // menyembunyikan exception (CLAUDE.md §5).
        try
        {
            foreach (var kelas in new RepositoriKelas().DaftarUntukTahunAjaran(connection, tahunAjaranId))
            {
                DaftarKelas.Add(kelas);
            }
        }
        catch (Exception galat)
        {
            PencatatGalat.Catat(galat, sumber: "RegistrasiKartuViewModel.MuatDaftarKelas", level: "ERROR");
            GagalMuatKelas = true;
        }

        SimpanCommand = new RelayCommand(Simpan, () => _pendaftaranSesi.Count > 0);

        // PRD §16.5 langkah 1: mengaktifkan Mode Registrasi adalah alasan
        // layar ini dibuka, jadi disatukan — bukan tombol terpisah.
        _statusModeRegistrasi.Aktifkan();
    }

    public ObservableCollection<KelasDaftar> DaftarKelas { get; } = new();

    // UAT Bug #11: true hanya kalau QUERY-nya sendiri gagal (exception),
    // bukan kalau kelasnya memang belum ada — dua kondisi ini butuh pesan
    // berbeda ke pengguna (§ "Hasil yang wajib dicapai" poin 9-11).
    public bool GagalMuatKelas { get; }

    public string? PesanMuatKelasGagal => GagalMuatKelas
        ? "Gagal memuat daftar kelas dari database. Tutup dan buka lagi Registrasi Kartu; bila terus terjadi, laporkan ke pengelola aplikasi."
        : null;

    // Dibedakan dari PesanGalat (dipicu tap kartu) — ini tampil begitu
    // dialog dibuka, sebelum operator sempat tap apa pun, supaya "kenapa
    // dropdown Kelas kosong" langsung terjawab di layar itu sendiri.
    public bool TahunAjaranTanpaKelas => !GagalMuatKelas && DaftarKelas.Count == 0;

    public KelasDaftar? KelasTerpilih
    {
        get => _kelasTerpilih;
        set
        {
            if (SetField(ref _kelasTerpilih, value))
            {
                MuatKelas();
            }
        }
    }

    public SiswaDaftar? SiswaSekarang
    {
        get => _siswaSekarang;
        private set => SetField(ref _siswaSekarang, value);
    }

    // UAT 2026-09-23: sebelumnya XAML memakai TargetNullValue statis
    // "Seluruh siswa di kelas ini sudah terdaftar" untuk SEMUA kondisi
    // SiswaSekarang null — salah/membingungkan untuk kelas yang MEMANG
    // belum punya siswa sama sekali (0 siswa, bukan "sudah terdaftar
    // semua"). Kelas itu sendiri TIDAK boleh hilang dari dropdown untuk
    // kasus ini (lihat DaftarKelas — tidak bergantung pada jumlah siswa).
    //
    // UAT 2026-09-23 (regresi putaran kedua, DIPERBAIKI): versi sebelumnya
    // HANYA memeriksa "SiswaSekarang is null" — true SEJAK DIALOG DIBUKA
    // (SiswaSekarang belum diisi sebelum kelas dipilih sama sekali),
    // sehingga pesan "Kelas ini belum punya siswa" tampil SEBELUM operator
    // sempat memilih kelas apa pun. Wajib mensyaratkan _kelasTerpilih tidak
    // null juga — kondisi "belum ada siswa" hanya bermakna SETELAH kelas
    // benar-benar dipilih.
    public bool TidakAdaSiswaSekarang => _kelasTerpilih is not null && SiswaSekarang is null;

    public string PesanSiswaKosong => _daftarSiswaKelas.Count == 0
        ? "Kelas ini belum punya siswa terdaftar di sistem."
        : "Seluruh siswa di kelas ini sudah terdaftar.";

    // PRD §16.5 langkah 4, format persis: "7A: 28 dari 30 terdaftar" —
    // bukan progress bar persen, operator perlu tahu sisa berapa siswa.
    public string? ProgresTeks
    {
        get => _progresTeks;
        private set => SetField(ref _progresTeks, value);
    }

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

    // View menutup jendela saat ini terpicu (setelah Simpan sukses).
    public event EventHandler? Selesai;

    public void TapUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return;
        }

        if (SiswaSekarang is not { } siswa)
        {
            PesanGalat = _kelasTerpilih is null
                ? "Pilih kelas terlebih dahulu."
                : "Seluruh siswa di kelas ini sudah terdaftar.";
            return;
        }

        PesanGalat = null;

        // PRD §16.5 langkah 3.4: duplikat DALAM sesi yang sama (belum
        // tersimpan ke database) juga wajib ditolak, bukan hanya yang
        // sudah ada di database.
        if (_pendaftaranSesi.TryGetValue(uid, out var dipakaiSesiIni))
        {
            PesanGalat = $"Kartu ini sudah dipakai {dipakaiSesiIni.NamaTampil} pada sesi ini.";
            return;
        }

        var pemilikAktif = _layanan.CariPemilikAktif(_connection, uid, _tahunAjaranId);
        if (pemilikAktif is not null)
        {
            PesanGalat = pemilikAktif.KelasNama is { } kelasNama
                ? $"Kartu ini sudah dipakai {pemilikAktif.Nama} di kelas {kelasNama}."
                : $"Kartu ini sudah dipakai {pemilikAktif.Nama}.";
            return;
        }

        _pendaftaranSesi[uid] = new PendaftaranKartu(uid, JenisPemilikKartu.Siswa, siswa.Id, siswa.Nama);
        _terdaftarSesiIni.Add(siswa.Id);
        PesanStatus = $"{siswa.Nama} terdaftar.";
        SimpanCommand.RaiseCanExecuteChanged();

        LompatKeSiswaBelumTerdaftar();
        PerbaruiProgres();
    }

    public void Batal() => Selesai?.Invoke(this, EventArgs.Empty);

    // Dipanggil Window.OnClosed apa pun cara jendela ditutup, memastikan
    // Mode Registrasi tidak pernah "menyala" setelah jendela ini hilang.
    public void MatikanModeRegistrasi() => _statusModeRegistrasi.Matikan();

    private void MuatKelas()
    {
        _pendaftaranSesi.Clear();
        _terdaftarSesiIni.Clear();
        PesanGalat = null;
        PesanStatus = null;
        SimpanCommand.RaiseCanExecuteChanged();

        if (_kelasTerpilih is not { } kelas)
        {
            _daftarSiswaKelas = Array.Empty<SiswaDaftar>();
            SiswaSekarang = null;
            ProgresTeks = null;
            OnPropertyChanged(nameof(TidakAdaSiswaSekarang));
            OnPropertyChanged(nameof(PesanSiswaKosong));
            return;
        }

        // UAT Bug #11: sama seperti pemuatan DaftarKelas di konstruktor —
        // kegagalan query dibedakan dari kelas yang memang belum punya
        // siswa, bukan ditelan diam-diam.
        try
        {
            _daftarSiswaKelas = new RepositoriSiswa().DaftarUntukTahunAjaran(_connection, _tahunAjaranId)
                .Where(s => s.Aktif && s.KelasNama == kelas.Nama)
                .ToList();
        }
        catch (Exception galat)
        {
            PencatatGalat.Catat(galat, sumber: "RegistrasiKartuViewModel.MuatKelas", level: "ERROR");
            _daftarSiswaKelas = Array.Empty<SiswaDaftar>();
            PesanGalat = "Gagal memuat daftar siswa kelas ini. Coba pilih ulang kelasnya.";
            SiswaSekarang = null;
            ProgresTeks = null;
            OnPropertyChanged(nameof(TidakAdaSiswaSekarang));
            OnPropertyChanged(nameof(PesanSiswaKosong));
            return;
        }

        LompatKeSiswaBelumTerdaftar();
        PerbaruiProgres();
    }

    private void LompatKeSiswaBelumTerdaftar()
    {
        SiswaSekarang = _daftarSiswaKelas.FirstOrDefault(s => !s.PunyaKartu && !_terdaftarSesiIni.Contains(s.Id));
        OnPropertyChanged(nameof(TidakAdaSiswaSekarang));
        OnPropertyChanged(nameof(PesanSiswaKosong));
    }

    private void PerbaruiProgres()
    {
        if (_kelasTerpilih is not { } kelas)
        {
            return;
        }

        var terdaftar = _daftarSiswaKelas.Count(s => s.PunyaKartu || _terdaftarSesiIni.Contains(s.Id));
        ProgresTeks = $"{kelas.Nama}: {terdaftar} dari {_daftarSiswaKelas.Count} terdaftar";
    }

    private void Simpan()
    {
        _layanan.DaftarkanSesi(_connection, _akun, _penyediaWaktu.Sekarang, _pendaftaranSesi.Values.ToList());
        Selesai?.Invoke(this, EventArgs.Empty);
    }
}
