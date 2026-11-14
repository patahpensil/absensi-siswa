using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using Absensi.App.Mvvm;
using Absensi.Data.Laporan;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Akun;
using Absensi.Domain.Laporan;
using Absensi.Laporan;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace Absensi.App.Laporan;

// RENCANA-TUGAS.md Tugas 12 (PRD §11, §17.8; PANDUAN-UI-UX.md: "Pilih
// jenis, rentang, kelas; pratinjau; ekspor Excel dan PDF"). Satu layar
// untuk kelima jenis laporan dan kedua subjek (siswa/guru) — kontrol yang
// relevan saja yang tampil, lewat properti Tampilkan* (pola sama seperti
// Tugas 6/8: computed bool property, bukan converter berparameter).
public sealed class LaporanViewModel : ObservableObject
{
    private static readonly CultureInfo BudayaIndonesia = new("id-ID");

    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _akun;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly int _tahunAjaranId;
    private readonly LayananLaporan _layananLaporan = new();
    private readonly RepositoriSekolah _repositoriSekolah = new();
    private readonly RepositoriTahunAjaran _repositoriTahunAjaran = new();
    private readonly RepositoriGuru _repositoriGuru = new();
    private readonly PenulisExcelLaporan _penulisExcel = new();
    private readonly PenulisPdfLaporan _penulisPdf = new();
    private readonly ILayananCetakLaporan _layananCetak;

    private JenisLaporan _jenisTerpilih = JenisLaporan.Harian;
    private JenisSubjekLaporan _subjekTerpilih = JenisSubjekLaporan.Siswa;
    private int? _kelasTerpilihId;
    private bool _cetakSatuOrang;
    private int? _orangTerpilihId;
    private DateTime _tanggalAwal;
    private DateTime _tanggalAkhir;
    private DataTable _pratinjauBulanan = new();
    private KartuOrang? _kartuTerpilih;
    private string? _pesanGalat;
    private string? _pesanStatus;

    public LaporanViewModel(SqliteConnection connection, AkunTersimpan akun, IPenyediaWaktu penyediaWaktu, int tahunAjaranId, ILayananCetakLaporan layananCetak)
    {
        _connection = connection;
        _akun = akun;
        _penyediaWaktu = penyediaWaktu;
        _tahunAjaranId = tahunAjaranId;
        _layananCetak = layananCetak;
        _tanggalAwal = penyediaWaktu.Sekarang.Date;
        _tanggalAkhir = penyediaWaktu.Sekarang.Date;

        // P-22: wali kelas hanya melihat kelas yang sedang ia walikan dan
        // hanya laporan siswa; LayananLaporan tetap memeriksa ulang.
        var isHomeroom = akun.Peran == PeranAkun.WaliKelas;
        DaftarSubjek = isHomeroom
            ? new[] { new OpsiSubjekLaporan(JenisSubjekLaporan.Siswa, "Siswa") }
            : new[] { new OpsiSubjekLaporan(JenisSubjekLaporan.Siswa, "Siswa"), new OpsiSubjekLaporan(JenisSubjekLaporan.Guru, "Guru") };
        if (!isHomeroom)
        {
            DaftarKelas.Add(new OpsiKelas(null, "Semua kelas"));
        }

        foreach (var kelas in new RepositoriKelas().DaftarUntukTahunAjaran(connection, tahunAjaranId))
        {
            if (!isHomeroom || _layananLaporan.MayOpenClassReport(connection, akun, kelas.Id))
            {
                DaftarKelas.Add(new OpsiKelas(kelas.Id, kelas.Nama));
            }
        }

        if (isHomeroom)
        {
            _kelasTerpilihId = DaftarKelas.FirstOrDefault()?.Id;
        }

        EksporExcelCommand = new RelayCommand(() => Ekspor(keExcel: true), () => BolehAkses);
        EksporPdfCommand = new RelayCommand(() => Ekspor(keExcel: false), () => BolehAkses);
        CetakCommand = new RelayCommand(Cetak, () => BolehAkses);

        if (BolehAkses)
        {
            MuatDaftarOrang();
            Muat();
        }
        else
        {
            PesanGalat = "Belum ada kelas yang Anda walikan pada periode ini.";
        }
    }

    // PRD §3 P-22: Kepala Sekolah/Admin membuka semua laporan; wali kelas
    // hanya laporan siswa kelasnya. Diperiksa juga di LayananLaporan (PRD §24.3).
    public bool BolehAkses => _akun.Peran != PeranAkun.WaliKelas || _kelasTerpilihId is not null;

    public IReadOnlyList<OpsiJenisLaporan> DaftarJenis { get; } =
    [
        new(JenisLaporan.Harian, "Harian"),
        new(JenisLaporan.Mingguan, "Mingguan"),
        new(JenisLaporan.Bulanan, "Bulanan"),
        new(JenisLaporan.PerOrang, "Per orang"),
        new(JenisLaporan.Semester, "Semester"),
    ];

    public IReadOnlyList<OpsiSubjekLaporan> DaftarSubjek { get; }

    public ObservableCollection<OpsiKelas> DaftarKelas { get; } = new();

    public ObservableCollection<OpsiOrang> DaftarOrang { get; } = new();

    public ObservableCollection<BarisKejadian> PratinjauKejadian { get; } = new();

    public ObservableCollection<BarisSemester> PratinjauSemester { get; } = new();

    public DataTable PratinjauBulanan { get => _pratinjauBulanan; private set => SetField(ref _pratinjauBulanan, value); }

    public KartuOrang? KartuTerpilih { get => _kartuTerpilih; private set => SetField(ref _kartuTerpilih, value); }

    public JenisLaporan JenisTerpilih
    {
        get => _jenisTerpilih;
        set
        {
            if (SetField(ref _jenisTerpilih, value))
            {
                OnPropertyChanged(nameof(TampilkanTanggalAkhir));
                OnPropertyChanged(nameof(TampilkanCetakSatuOrang));
                OnPropertyChanged(nameof(TampilkanOrang));
                OnPropertyChanged(nameof(TampilkanTanggal));
                OnPropertyChanged(nameof(TampilkanPratinjauKejadian));
                OnPropertyChanged(nameof(TampilkanPratinjauBulanan));
                OnPropertyChanged(nameof(TampilkanPratinjauSemester));
                OnPropertyChanged(nameof(TampilkanPratinjauOrang));
                Muat();
            }
        }
    }

    public JenisSubjekLaporan SubjekTerpilih
    {
        get => _subjekTerpilih;
        set
        {
            if (SetField(ref _subjekTerpilih, value))
            {
                OnPropertyChanged(nameof(TampilkanKelas));
                MuatDaftarOrang();
                Muat();
            }
        }
    }

    public int? KelasTerpilihId
    {
        get => _kelasTerpilihId;
        set
        {
            if (SetField(ref _kelasTerpilihId, value))
            {
                MuatDaftarOrang();
                Muat();
            }
        }
    }

    public bool CetakSatuOrang
    {
        get => _cetakSatuOrang;
        set
        {
            if (SetField(ref _cetakSatuOrang, value))
            {
                Muat();
            }
        }
    }

    public int? OrangTerpilihId
    {
        get => _orangTerpilihId;
        set
        {
            if (SetField(ref _orangTerpilihId, value))
            {
                Muat();
            }
        }
    }

    public DateTime TanggalAwal
    {
        get => _tanggalAwal;
        set
        {
            if (SetField(ref _tanggalAwal, value))
            {
                Muat();
            }
        }
    }

    public DateTime TanggalAkhir
    {
        get => _tanggalAkhir;
        set
        {
            if (SetField(ref _tanggalAkhir, value))
            {
                Muat();
            }
        }
    }

    // PRD §3: kalender/pengaturan/laporan tidak disebut eksplisit di tabel
    // kewenangan §3 — laporan dibiarkan terbuka untuk ketiga peran (mirip
    // pertimbangan notifikasi/pairing di Tugas 10), operator hanya melihat
    // datanya sendiri lewat filter kelas biasa.
    public bool TampilkanKelas => _subjekTerpilih == JenisSubjekLaporan.Siswa;

    public bool TampilkanTanggalAkhir => _jenisTerpilih == JenisLaporan.PerOrang;

    public bool TampilkanTanggal => _jenisTerpilih != JenisLaporan.Semester;

    public bool TampilkanCetakSatuOrang => _jenisTerpilih == JenisLaporan.PerOrang;

    public bool TampilkanOrang => _jenisTerpilih == JenisLaporan.PerOrang && _cetakSatuOrang;

    public bool TampilkanPratinjauKejadian => _jenisTerpilih is JenisLaporan.Harian or JenisLaporan.Mingguan;

    public bool TampilkanPratinjauBulanan => _jenisTerpilih == JenisLaporan.Bulanan;

    public bool TampilkanPratinjauSemester => _jenisTerpilih == JenisLaporan.Semester
        || (_jenisTerpilih == JenisLaporan.PerOrang && !_cetakSatuOrang);

    public bool TampilkanPratinjauOrang => _jenisTerpilih == JenisLaporan.PerOrang && _cetakSatuOrang;

    public string? PesanGalat { get => _pesanGalat; private set => SetField(ref _pesanGalat, value); }

    public string? PesanStatus { get => _pesanStatus; private set => SetField(ref _pesanStatus, value); }

    public RelayCommand EksporExcelCommand { get; }

    public RelayCommand EksporPdfCommand { get; }

    public RelayCommand CetakCommand { get; }

    private void MuatDaftarOrang()
    {
        DaftarOrang.Clear();
        if (_subjekTerpilih == JenisSubjekLaporan.Guru)
        {
            foreach (var guru in _repositoriGuru.DaftarAktifRingkas(_connection))
            {
                DaftarOrang.Add(new OpsiOrang(guru.Id, guru.Nama));
            }

            return;
        }

        var siswaAktif = new RepositoriSiswa().DaftarAktifRingkas(_connection);
        if (_kelasTerpilihId is not { } kelasId)
        {
            foreach (var siswa in siswaAktif)
            {
                DaftarOrang.Add(new OpsiOrang(siswa.Id, siswa.Nama));
            }

            return;
        }

        var siswaDiKelasIni = new RepositoriKelas().DaftarSiswaAktifPerKelas(_connection, _tahunAjaranId)
            .Where(anggota => anggota.KelasId == kelasId)
            .Select(anggota => anggota.SiswaId)
            .ToHashSet();
        foreach (var siswa in siswaAktif.Where(s => siswaDiKelasIni.Contains(s.Id)))
        {
            DaftarOrang.Add(new OpsiOrang(siswa.Id, siswa.Nama));
        }
    }

    private void Muat()
    {
        PesanGalat = null;
        try
        {
            switch (_jenisTerpilih)
            {
                case JenisLaporan.Harian:
                    MuatKejadian(DateOnly.FromDateTime(_tanggalAwal), DateOnly.FromDateTime(_tanggalAwal));
                    break;
                case JenisLaporan.Mingguan:
                    var senin = AwalMinggu(DateOnly.FromDateTime(_tanggalAwal));
                    MuatKejadian(senin, senin.AddDays(5));
                    break;
                case JenisLaporan.Bulanan:
                    MuatBulanan();
                    break;
                case JenisLaporan.PerOrang:
                    MuatPerOrang();
                    break;
                case JenisLaporan.Semester:
                    MuatSemester();
                    break;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            PesanGalat = PesanAman(ex);
        }
    }

    // ArgumentException berasal dari kerangka .NET (pesan teknis berbahasa Inggris,
    // bisa memuat nama parameter) — tidak pernah ditampilkan mentah ke pengguna.
    private static string PesanAman(Exception ex) => ex is InvalidOperationException
        ? ex.Message
        : "Data laporan tidak valid untuk pilihan ini. Periksa jenis, rentang tanggal, dan kelas, lalu coba lagi.";

    private static DateOnly AwalMinggu(DateOnly tanggal)
    {
        var selisih = ((int)tanggal.DayOfWeek + 6) % 7;
        return tanggal.AddDays(-selisih);
    }

    private void MuatKejadian(DateOnly awal, DateOnly akhir)
    {
        PratinjauKejadian.Clear();
        foreach (var baris in _layananLaporan.Kejadian(_connection, _akun, awal, akhir, _kelasTerpilihId, _subjekTerpilih))
        {
            PratinjauKejadian.Add(baris);
        }
    }

    private void MuatBulanan()
    {
        var laporan = _layananLaporan.Bulanan(_connection, _akun, _tahunAjaranId, _tanggalAwal.Year, _tanggalAwal.Month, _kelasTerpilihId, _subjekTerpilih);
        PratinjauBulanan = BangunTabelBulanan(laporan, _subjekTerpilih);
    }

    private static DataTable BangunTabelBulanan(LaporanBulanan laporan, JenisSubjekLaporan subjek)
    {
        var tabel = new DataTable();
        tabel.Columns.Add("Nama");
        tabel.Columns.Add("Identitas");
        if (subjek == JenisSubjekLaporan.Siswa)
        {
            tabel.Columns.Add("Kelas");
        }

        foreach (var kolom in laporan.Kolom)
        {
            tabel.Columns.Add(kolom.Tanggal.Day.ToString());
        }

        foreach (var judul in KolomRekap(subjek))
        {
            tabel.Columns.Add(judul);
        }

        tabel.Columns.Add("%");

        foreach (var baris in laporan.Baris)
        {
            var nilai = new List<object?> { baris.Nama, baris.Identitas };
            if (subjek == JenisSubjekLaporan.Siswa)
            {
                nilai.Add(baris.Kelas ?? "-");
            }

            foreach (var kolom in laporan.Kolom)
            {
                nilai.Add(baris.KodePerTanggal.TryGetValue(kolom.Tanggal, out var kode) ? kode : "");
            }

            nilai.AddRange(NilaiRekap(baris.Rekap, subjek).Cast<object?>());
            nilai.Add($"{baris.Rekap.Persentase:0.0}");
            tabel.Rows.Add(nilai.ToArray());
        }

        return tabel;
    }

    // Sama seperti PenulisPdfLaporan/PenulisExcelLaporan — kolom rekap
    // guru punya urutan sendiri (Hadir·TidakLengkap·Sakit·Izin·DinasLuar·
    // Cuti·Alpa, PRD §17.3), bukan siswa+tambahan, supaya pratinjau
    // layar dan berkas yang diekspor selalu menampilkan hal yang sama.
    private static string[] KolomRekap(JenisSubjekLaporan subjek) => subjek == JenisSubjekLaporan.Guru
        ? ["H", "L", "S", "I", "DL", "C", "A"]
        : ["H", "S", "I", "A", "T", "Bolos"];

    private static int[] NilaiRekap(RekapKehadiran rekap, JenisSubjekLaporan subjek) => subjek == JenisSubjekLaporan.Guru
        ? [rekap.Hadir, rekap.TidakLengkap, rekap.Sakit, rekap.Izin, rekap.DinasLuar, rekap.Cuti, rekap.Alpa]
        : [rekap.Hadir, rekap.Sakit, rekap.Izin, rekap.Alpa, rekap.Terlambat, rekap.Bolos];

    private void MuatPerOrang()
    {
        if (_cetakSatuOrang)
        {
            KartuTerpilih = _orangTerpilihId is { } id
                ? _layananLaporan.PerOrang(_connection, _akun, id, _subjekTerpilih, _tahunAjaranId, DateOnly.FromDateTime(_tanggalAwal), DateOnly.FromDateTime(_tanggalAkhir))
                : null;
            return;
        }

        PratinjauSemester.Clear();
        foreach (var kartu in _layananLaporan.PerKelasAtauSemua(
            _connection, _akun, _subjekTerpilih, _kelasTerpilihId, _tahunAjaranId, DateOnly.FromDateTime(_tanggalAwal), DateOnly.FromDateTime(_tanggalAkhir)))
        {
            PratinjauSemester.Add(new BarisSemester(kartu.Nama, kartu.Identitas, kartu.Kelas, kartu.Rekap));
        }
    }

    private void MuatSemester()
    {
        PratinjauSemester.Clear();
        foreach (var baris in _layananLaporan.Semester(_connection, _akun, _tahunAjaranId, _kelasTerpilihId, _subjekTerpilih))
        {
            PratinjauSemester.Add(baris);
        }
    }

    private void Ekspor(bool keExcel)
    {
        PesanGalat = null;
        PesanStatus = null;
        try
        {
            var kepala = BangunKepala();
            byte[] berkas;
            string namaBerkas;

            switch (_jenisTerpilih)
            {
                case JenisLaporan.Harian or JenisLaporan.Mingguan:
                    var tampilkanKelas = _subjekTerpilih == JenisSubjekLaporan.Siswa;
                    berkas = keExcel
                        ? _penulisExcel.TulisKejadian(kepala, PratinjauKejadian, tampilkanKelas)
                        : _penulisPdf.TulisKejadian(kepala, PratinjauKejadian, tampilkanKelas);
                    namaBerkas = kepala.Judul;
                    break;
                case JenisLaporan.Bulanan:
                    var bulanan = _layananLaporan.Bulanan(_connection, _akun, _tahunAjaranId, _tanggalAwal.Year, _tanggalAwal.Month, _kelasTerpilihId, _subjekTerpilih);
                    berkas = keExcel ? _penulisExcel.TulisBulanan(kepala, bulanan, _subjekTerpilih) : _penulisPdf.TulisBulanan(kepala, bulanan, _subjekTerpilih);
                    namaBerkas = kepala.Judul;
                    break;
                case JenisLaporan.PerOrang when _cetakSatuOrang:
                    if (KartuTerpilih is not { } satu)
                    {
                        PesanGalat = "Pilih orang yang akan dicetak terlebih dahulu.";
                        return;
                    }

                    berkas = keExcel ? _penulisExcel.TulisKartuOrang(kepala, LengkapiFoto([satu]), _subjekTerpilih) : _penulisPdf.TulisKartuOrang(kepala, LengkapiFoto([satu]), _subjekTerpilih);
                    namaBerkas = $"{kepala.Judul} - {satu.Nama}";
                    break;
                case JenisLaporan.PerOrang:
                    var batch = _layananLaporan.PerKelasAtauSemua(
                        _connection, _akun, _subjekTerpilih, _kelasTerpilihId, _tahunAjaranId, DateOnly.FromDateTime(_tanggalAwal), DateOnly.FromDateTime(_tanggalAkhir));
                    berkas = keExcel ? _penulisExcel.TulisKartuOrang(kepala, LengkapiFoto(batch), _subjekTerpilih) : _penulisPdf.TulisKartuOrang(kepala, LengkapiFoto(batch), _subjekTerpilih);
                    namaBerkas = kepala.Judul;
                    break;
                default:
                    berkas = keExcel
                        ? _penulisExcel.TulisSemester(kepala, PratinjauSemester, _subjekTerpilih)
                        : _penulisPdf.TulisSemester(kepala, PratinjauSemester, _subjekTerpilih);
                    namaBerkas = kepala.Judul;
                    break;
            }

            SimpanKeBerkas(berkas, namaBerkas, keExcel);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            PesanGalat = PesanAman(ex);
        }
    }

    // Dipisah dari Ekspor supaya bisa diuji tanpa SaveFileDialog sungguhan
    // (pola sama seperti ImporSiswaWizardViewModel.AnalisisBerkas, Tugas 7).
    public void SimpanKeBerkas(byte[] isi, string namaSaran, bool keExcel)
    {
        var dialog = new SaveFileDialog
        {
            FileName = namaSaran,
            Filter = keExcel ? "Berkas Excel (*.xlsx)|*.xlsx" : "Berkas PDF (*.pdf)|*.pdf",
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        // Berkas terkunci (mis. masih terbuka di Excel) / izin ditolak: pesan jelas,
        // bukan galat global; "tersimpan" hanya setelah berkas benar-benar tertulis.
        if (!PenangananGalat.Coba(
                () => File.WriteAllBytes(dialog.FileName, isi),
                pesan => PesanGalat = pesan,
                "LaporanViewModel.SimpanKeBerkas"))
        {
            PesanStatus = null;
            return;
        }

        PesanGalat = null;
        PesanStatus = $"Laporan tersimpan di {dialog.FileName}.";
    }

    // Cetak (terpisah dari Ekspor PDF, instruksi eksplisit pemilik proyek
    // 2026-09-25): TIDAK memakai byte[] Tulis*/berkas PDF sama sekali --
    // memanggil Gambar* (satu PNG per halaman) supaya Ekspor PDF di atas
    // benar-benar tidak tersentuh sedikit pun. Beda dari Ekspor, di sini
    // laporan KOSONG diperiksa dulu dan diberi pesan jelas (bukan tetap
    // mencetak halaman kosong) -- Ekspor sengaja dibiarkan apa adanya,
    // bukan bug yang diperbaiki di sini, karena "berkas tersimpan kosong"
    // dan "mencetak kertas kosong" beda dampaknya bagi pengguna.
    private void Cetak()
    {
        PesanGalat = null;
        PesanStatus = null;
        try
        {
            var kepala = BangunKepala();
            IReadOnlyList<byte[]> halaman;

            switch (_jenisTerpilih)
            {
                case JenisLaporan.Harian or JenisLaporan.Mingguan:
                    if (PratinjauKejadian.Count == 0)
                    {
                        PesanGalat = "Tidak ada data untuk dicetak. Ubah tanggal atau kelas, lalu coba lagi.";
                        return;
                    }

                    halaman = _penulisPdf.GambarKejadian(kepala, PratinjauKejadian, _subjekTerpilih == JenisSubjekLaporan.Siswa);
                    break;
                case JenisLaporan.Bulanan:
                    var bulanan = _layananLaporan.Bulanan(_connection, _akun, _tahunAjaranId, _tanggalAwal.Year, _tanggalAwal.Month, _kelasTerpilihId, _subjekTerpilih);
                    if (bulanan.Baris.Count == 0)
                    {
                        PesanGalat = "Tidak ada data untuk dicetak. Ubah bulan atau kelas, lalu coba lagi.";
                        return;
                    }

                    halaman = _penulisPdf.GambarBulanan(kepala, bulanan, _subjekTerpilih);
                    break;
                case JenisLaporan.PerOrang when _cetakSatuOrang:
                    if (KartuTerpilih is not { } satu)
                    {
                        PesanGalat = "Pilih orang yang akan dicetak terlebih dahulu.";
                        return;
                    }

                    halaman = _penulisPdf.GambarKartuOrang(kepala, LengkapiFoto([satu]), _subjekTerpilih);
                    break;
                case JenisLaporan.PerOrang:
                    var batch = _layananLaporan.PerKelasAtauSemua(
                        _connection, _akun, _subjekTerpilih, _kelasTerpilihId, _tahunAjaranId, DateOnly.FromDateTime(_tanggalAwal), DateOnly.FromDateTime(_tanggalAkhir));
                    if (batch.Count == 0)
                    {
                        PesanGalat = "Tidak ada data untuk dicetak. Ubah rentang tanggal atau kelas, lalu coba lagi.";
                        return;
                    }

                    halaman = _penulisPdf.GambarKartuOrang(kepala, LengkapiFoto(batch), _subjekTerpilih);
                    break;
                default:
                    if (PratinjauSemester.Count == 0)
                    {
                        PesanGalat = "Tidak ada data untuk dicetak. Ubah kelas, lalu coba lagi.";
                        return;
                    }

                    halaman = _penulisPdf.GambarSemester(kepala, PratinjauSemester, _subjekTerpilih);
                    break;
            }

            _layananCetak.Cetak(halaman, kepala.Judul, pesan => PesanGalat = pesan);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            PesanGalat = PesanAman(ex);
        }
    }

    // FotoPath/LogoPath tersimpan sebagai nama berkas polos (mis. "0012345678.png"),
    // relatif terhadap data/foto atau data/. PenulisPdfLaporan memeriksa File.Exists
    // pada path apa adanya, jadi tanpa ini foto/logo tidak pernah tercetak.
    private static string? AbsolutDariFolderData(string? nama, string? subfolder)
    {
        if (string.IsNullOrWhiteSpace(nama) || Path.IsPathRooted(nama))
        {
            return nama;
        }

        return subfolder is null ? Path.Combine(LokasiData.FolderData, nama) : Path.Combine(LokasiData.FolderData, subfolder, nama);
    }

    private static IReadOnlyList<KartuOrang> LengkapiFoto(IReadOnlyList<KartuOrang> kartu) =>
        kartu.Select(k => k with { FotoPath = AbsolutDariFolderData(k.FotoPath, "foto") }).ToList();

    private KepalaLaporan BangunKepala()
    {
        var sekolah = _repositoriSekolah.CariSatuSatunya(_connection);
        var judul = BangunJudul();
        var rentang = BangunRentangTeks();
        var dicetakOleh = _akun.GuruId is { } guruId ? _repositoriGuru.CariNama(_connection, guruId) : _akun.Username;

        return new KepalaLaporan(
            sekolah?.Nama ?? "Nama sekolah belum diatur",
            sekolah?.Npsn,
            AbsolutDariFolderData(sekolah?.LogoPath, null),
            judul,
            rentang,
            dicetakOleh,
            _penyediaWaktu.Sekarang);
    }

    private string BangunJudul()
    {
        var subjekTeks = _subjekTerpilih == JenisSubjekLaporan.Siswa ? "Siswa" : "Guru";
        return _jenisTerpilih switch
        {
            JenisLaporan.Harian => $"Laporan Harian {subjekTeks}",
            JenisLaporan.Mingguan => $"Laporan Mingguan {subjekTeks}",
            JenisLaporan.Bulanan => $"Laporan Bulanan {subjekTeks}",
            JenisLaporan.PerOrang => $"Laporan Per {(_subjekTerpilih == JenisSubjekLaporan.Siswa ? "Siswa" : "Guru")}",
            JenisLaporan.Semester => $"Laporan Semester {subjekTeks}",
            _ => "Laporan",
        };
    }

    private string BangunRentangTeks() => _jenisTerpilih switch
    {
        JenisLaporan.Harian => DateOnly.FromDateTime(_tanggalAwal).ToString("d MMMM yyyy", BudayaIndonesia),
        JenisLaporan.Mingguan => RentangMinggu(),
        JenisLaporan.Bulanan => DateOnly.FromDateTime(_tanggalAwal).ToString("MMMM yyyy", BudayaIndonesia),
        JenisLaporan.PerOrang =>
            $"{DateOnly.FromDateTime(_tanggalAwal):d MMMM yyyy} - {DateOnly.FromDateTime(_tanggalAkhir):d MMMM yyyy}",
        JenisLaporan.Semester => RentangSemester(),
        _ => "",
    };

    private string RentangMinggu()
    {
        var senin = AwalMinggu(DateOnly.FromDateTime(_tanggalAwal));
        var sabtu = senin.AddDays(5);
        return $"{senin:d MMMM} - {sabtu.ToString("d MMMM yyyy", BudayaIndonesia)}";
    }

    private string RentangSemester()
    {
        var nama = _repositoriTahunAjaran.CariNamaAktif(_connection) ?? "";
        var rentang = _repositoriTahunAjaran.CariRentang(_connection, _tahunAjaranId);
        return rentang is { } r ? $"{nama} ({r.TglMulai:d MMMM yyyy} - {r.TglSelesai.ToString("d MMMM yyyy", BudayaIndonesia)})" : nama;
    }
}
