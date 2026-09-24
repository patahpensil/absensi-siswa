using Absensi.Domain.Laporan;
using ClosedXML.Excel;

namespace Absensi.Laporan.Tests;

// RENCANA-TUGAS.md Tugas 12: PenulisExcelLaporan/PenulisPdfLaporan tidak
// menyentuh database — pengujian di sini murni memberi DTO
// Absensi.Domain.Laporan dan memeriksa berkas yang dihasilkan. Untuk PDF
// hanya diperiksa berkas itu valid dan berukuran wajar (tanda tangan
// "%PDF" + ukuran bertambah seiring isi) — tata letak visualnya sendiri
// tidak realistis diuji otomatis (lihat rencana Tugas 12: "smoke tests
// ... bukan pixel-level PDF assertions").
public sealed class PenulisLaporanTests
{
    private static readonly KepalaLaporan Kepala = new(
        "SMP 1 Uji", "12345678", null, "Laporan Uji", "1-5 September 2026", "admin1", new DateTime(2026, 9, 18, 8, 0, 0));

    private readonly PenulisExcelLaporan _excel = new();
    private readonly PenulisPdfLaporan _pdf = new();

    [Fact]
    public void TulisKejadian_Excel_BarisSesuaiJumlahData()
    {
        var baris = new List<BarisKejadian>
        {
            new(new DateOnly(2026, 9, 18), "Siswa Satu", "0001", "7A", "T", new TimeOnly(7, 52), null, "Terlambat 07:52"),
            new(new DateOnly(2026, 9, 18), "Siswa Dua", "0002", "7A", "H", new TimeOnly(7, 20), new TimeOnly(13, 10), null),
        };

        var berkas = _excel.TulisKejadian(Kepala, baris, tampilkanKelas: true);

        using var workbook = new XLWorkbook(new MemoryStream(berkas));
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Tanggal", sheet.Cell(7, 1).GetString());
        Assert.Equal("Siswa Satu", sheet.Cell(8, 2).GetString());
        Assert.Equal("Siswa Dua", sheet.Cell(9, 2).GetString());
    }

    [Fact]
    public void TulisKejadian_Pdf_MenghasilkanBerkasValid()
    {
        var baris = new List<BarisKejadian>
        {
            new(new DateOnly(2026, 9, 18), "Siswa Satu", "0001", "7A", "T", new TimeOnly(7, 52), null, "Terlambat 07:52"),
        };

        var berkas = _pdf.TulisKejadian(Kepala, baris, tampilkanKelas: true);

        AssertPdfValid(berkas);
    }

    [Fact]
    public void TulisBulanan_Excel_KolomLiburDiarsir()
    {
        var kolom = new List<KolomTanggalBulanan>
        {
            new(new DateOnly(2026, 9, 5), false, null),
            new(new DateOnly(2026, 9, 6), true, null), // Minggu
        };
        var baris = new List<BarisBulanan>
        {
            new("Siswa Satu", "0001", "7A",
                new Dictionary<DateOnly, string> { [new DateOnly(2026, 9, 5)] = "H" },
                new RekapKehadiran(1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 100.0)),
        };
        var laporan = new LaporanBulanan(kolom, baris);

        var berkas = _excel.TulisBulanan(Kepala, laporan, JenisSubjekLaporan.Siswa);

        using var workbook = new XLWorkbook(new MemoryStream(berkas));
        var sheet = workbook.Worksheet(1);
        // Kolom tanggal dimulai kolom ke-4 untuk siswa (Nama, Identitas, Kelas, lalu tanggal).
        var warnaLibur = sheet.Column(5).Style.Fill.BackgroundColor;
        Assert.NotEqual(XLColor.NoColor, warnaLibur);
    }

    // Koreksi 2026-09-18: kolom rekap guru punya urutan sendiri —
    // Hadir · Tidak Lengkap · Sakit · Izin · Dinas Luar · Cuti · Alpa —
    // bukan siswa (H/S/I/A) plus tambahan DL/C seperti draf awal.
    [Fact]
    public void TulisBulanan_Excel_HeaderRekapGuruUrutanHadirTidakLengkapSakitIzinDinasLuarCutiAlpa()
    {
        var kolom = new List<KolomTanggalBulanan> { new(new DateOnly(2026, 9, 5), false, null) };
        var baris = new List<BarisBulanan>
        {
            new("Guru Satu", "111", null,
                new Dictionary<DateOnly, string> { [new DateOnly(2026, 9, 5)] = "H" },
                new RekapKehadiran(2, 0, 0, 0, 0, 0, 1, 1, 1, 5, 60.0)),
        };
        var laporan = new LaporanBulanan(kolom, baris);

        var berkas = _excel.TulisBulanan(Kepala, laporan, JenisSubjekLaporan.Guru);

        using var workbook = new XLWorkbook(new MemoryStream(berkas));
        var sheet = workbook.Worksheet(1);
        // Baris header ada di baris 7 (TulisKepalaSheet); Nama, Identitas, lalu 1 kolom tanggal (5 Sept), baru rekap.
        var kolomRekap = Enumerable.Range(4, 7).Select(c => sheet.Cell(7, c).GetString()).ToList();
        Assert.Equal(["H", "L", "S", "I", "DL", "C", "A"], kolomRekap);
    }

    [Fact]
    public void TulisBulanan_Pdf_MenghasilkanBerkasValid()
    {
        var kolom = new List<KolomTanggalBulanan> { new(new DateOnly(2026, 9, 5), false, null) };
        var baris = new List<BarisBulanan>
        {
            new("Siswa Satu", "0001", "7A",
                new Dictionary<DateOnly, string> { [new DateOnly(2026, 9, 5)] = "H" },
                new RekapKehadiran(1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 100.0)),
        };
        var laporan = new LaporanBulanan(kolom, baris);

        var berkas = _pdf.TulisBulanan(Kepala, laporan, JenisSubjekLaporan.Siswa);

        AssertPdfValid(berkas);
    }

    // Regresi bug 2026-09-25 (ditemukan tidak sengaja saat mengerjakan
    // fitur Cetak terpisah, sama sekali bukan akibat fitur itu): tes di
    // atas selalu memakai data uji SATU kolom tanggal, jadi tidak pernah
    // membuktikan tabel Bulanan benar-benar muat untuk bulan NYATA (28-31
    // kolom). margin 24 + ConstantColumn(16) lama HANYA muat sampai 28
    // kolom (Februari non-kabisat) -- bulan APA PUN 29-31 hari (hampir
    // semua bulan) melempar DocumentLayoutException yang TIDAK tertangkap
    // LaporanViewModel.Ekspor (bukan InvalidOperationException/
    // ArgumentException), menutup aplikasi. Keempat kasus di bawah
    // (bulan terpendek dan terpanjang yang benar-benar ada di kalender)
    // terbukti GAGAL pada kode lama, sekarang lulus setelah margin
    // diperkecil ke 20 dan kolom tanggal ke 14 (TulisBulanan/
    // TulisTabelBulanan) -- tanpa mengurangi kolom atau data apa pun.
    [Theory]
    [InlineData(2026, 2)] // Februari 2026 (bukan tahun kabisat) = 28 hari
    [InlineData(2028, 2)] // Februari 2028 (tahun kabisat) = 29 hari
    [InlineData(2026, 4)] // April = 30 hari
    [InlineData(2026, 1)] // Januari = 31 hari (bulan terpanjang, kasus paling ketat)
    public void TulisBulanan_Pdf_Siswa_MuatUntukBulanApaPunTanpaKehilanganKolom(int tahun, int bulan)
    {
        var laporan = BangunLaporanBulananUjiLebar(tahun, bulan, JenisSubjekLaporan.Siswa);

        var berkas = _pdf.TulisBulanan(Kepala, laporan, JenisSubjekLaporan.Siswa);

        AssertPdfValid(berkas);
    }

    [Theory]
    [InlineData(2026, 2)]
    [InlineData(2028, 2)]
    [InlineData(2026, 4)]
    [InlineData(2026, 1)]
    public void TulisBulanan_Pdf_Guru_MuatUntukBulanApaPunTanpaKehilanganKolom(int tahun, int bulan)
    {
        var laporan = BangunLaporanBulananUjiLebar(tahun, bulan, JenisSubjekLaporan.Guru);

        var berkas = _pdf.TulisBulanan(Kepala, laporan, JenisSubjekLaporan.Guru);

        AssertPdfValid(berkas);
    }

    private static LaporanBulanan BangunLaporanBulananUjiLebar(int tahun, int bulan, JenisSubjekLaporan subjek)
    {
        var jumlahHari = DateTime.DaysInMonth(tahun, bulan);
        var kolom = Enumerable.Range(1, jumlahHari)
            .Select(hari => new KolomTanggalBulanan(new DateOnly(tahun, bulan, hari), false, null))
            .ToList();
        var kodePerTanggal = kolom.ToDictionary(k => k.Tanggal, _ => "H");
        var rekap = new RekapKehadiran(jumlahHari, 0, 0, 0, 0, 0, 0, 0, 0, jumlahHari, 100.0);
        var baris = new List<BarisBulanan>
        {
            new("Orang Satu", "0001", subjek == JenisSubjekLaporan.Siswa ? "7A" : null, kodePerTanggal, rekap),
        };
        return new LaporanBulanan(kolom, baris);
    }

    // Ini juga pengujian regresi: TulisRekap (kartu per orang) sempat
    // salah hitung jumlah kolom untuk guru (8 kolom didefinisikan, 9 sel
    // header ditulis) — ditemukan justru saat memperbaiki poin 1-2 ini,
    // bukan sebelumnya. QuestPDF akan melempar galat bila jumlah kolom
    // dan sel header tidak cocok, jadi tes ini gagal lebih dulu daripada
    // diam-diam merender salah.
    [Fact]
    public void TulisBulanan_Pdf_Guru_MenghasilkanBerkasValid()
    {
        var kolom = new List<KolomTanggalBulanan> { new(new DateOnly(2026, 9, 5), false, null) };
        var baris = new List<BarisBulanan>
        {
            new("Guru Satu", "111", null,
                new Dictionary<DateOnly, string> { [new DateOnly(2026, 9, 5)] = "H" },
                new RekapKehadiran(2, 0, 0, 0, 0, 0, 1, 1, 1, 5, 60.0)),
        };
        var laporan = new LaporanBulanan(kolom, baris);

        var berkas = _pdf.TulisBulanan(Kepala, laporan, JenisSubjekLaporan.Guru);

        AssertPdfValid(berkas);
    }

    [Fact]
    public void TulisKartuOrang_Pdf_UkuranBertambahSeiringJumlahOrang()
    {
        var satuOrang = new List<KartuOrang>
        {
            new("Siswa Satu", "0001", "7A", null, new RekapKehadiran(10, 0, 0, 0, 0, 0, 0, 0, 0, 10, 100.0), []),
        };
        var duaOrang = new List<KartuOrang>
        {
            satuOrang[0],
            new("Siswa Dua", "0002", "7A", null, new RekapKehadiran(9, 1, 0, 0, 0, 0, 0, 0, 0, 10, 90.0),
                [new RincianKejadian(new DateOnly(2026, 9, 5), "S", "Sakit: demam")]),
        };

        var berkasSatu = _pdf.TulisKartuOrang(Kepala, satuOrang, JenisSubjekLaporan.Siswa);
        var berkasDua = _pdf.TulisKartuOrang(Kepala, duaOrang, JenisSubjekLaporan.Siswa);

        AssertPdfValid(berkasSatu);
        AssertPdfValid(berkasDua);
        Assert.True(berkasDua.Length > berkasSatu.Length, "PDF dua kartu semestinya lebih besar dari satu kartu.");
    }

    // Regresi langsung untuk bug kolom yang ditemukan saat memperbaiki
    // poin 1-2: TulisRekap (kartu per orang) guru sempat mendefinisikan
    // 8 kolom tapi menulis 9 sel header (Hadir/Sakit/Izin/Alpa/Terlambat/
    // Bolos/DinasLuar/Cuti + Kehadiran) — QuestPDF akan melempar galat
    // saat GeneratePdf() bila ini terjadi lagi, jadi cukup memastikan
    // guru tidak melempar galat di sini.
    [Fact]
    public void TulisKartuOrang_Pdf_Guru_MenghasilkanBerkasValidDenganTidakLengkap()
    {
        var kartu = new List<KartuOrang>
        {
            new("Guru Satu", "111", null, null, new RekapKehadiran(2, 0, 0, 0, 0, 0, 1, 1, 1, 5, 60.0),
                [new RincianKejadian(new DateOnly(2026, 9, 18), "L", "Tidak lengkap (belum tap pulang)")]),
        };

        var berkas = _pdf.TulisKartuOrang(Kepala, kartu, JenisSubjekLaporan.Guru);

        AssertPdfValid(berkas);
    }

    [Fact]
    public void TulisKartuOrang_Excel_SetiapOrangPunyaBarisNama()
    {
        var kartu = new List<KartuOrang>
        {
            new("Siswa Satu", "0001", "7A", null, new RekapKehadiran(10, 0, 0, 0, 0, 0, 0, 0, 0, 10, 100.0), []),
            new("Siswa Dua", "0002", "7A", null, new RekapKehadiran(9, 1, 0, 0, 0, 0, 0, 0, 0, 10, 90.0),
                [new RincianKejadian(new DateOnly(2026, 9, 5), "S", "Sakit: demam")]),
        };

        var berkas = _excel.TulisKartuOrang(Kepala, kartu, JenisSubjekLaporan.Siswa);

        using var workbook = new XLWorkbook(new MemoryStream(berkas));
        var teksSheet = string.Join(" ", workbook.Worksheet(1).CellsUsed().Select(c => c.GetString()));
        Assert.Contains("Siswa Satu", teksSheet);
        Assert.Contains("Siswa Dua", teksSheet);
        Assert.Contains("Sakit: demam", teksSheet);
    }

    [Fact]
    public void TulisKartuOrang_Excel_Guru_RingkasanMemuatTidakLengkap()
    {
        var kartu = new List<KartuOrang>
        {
            new("Guru Satu", "111", null, null, new RekapKehadiran(2, 0, 0, 0, 0, 0, 1, 1, 1, 5, 60.0), []),
        };

        var berkas = _excel.TulisKartuOrang(Kepala, kartu, JenisSubjekLaporan.Guru);

        using var workbook = new XLWorkbook(new MemoryStream(berkas));
        var teksSheet = string.Join(" ", workbook.Worksheet(1).CellsUsed().Select(c => c.GetString()));
        Assert.Contains("Tidak lengkap 1", teksSheet);
    }

    [Fact]
    public void TulisSemester_Excel_BarisSesuaiJumlahOrang()
    {
        var baris = new List<BarisSemester>
        {
            new("Siswa Satu", "0001", "7A", new RekapKehadiran(80, 2, 1, 0, 3, 1, 0, 0, 0, 84, 100.0)),
            new("Siswa Dua", "0002", "7A", new RekapKehadiran(70, 0, 0, 5, 0, 0, 0, 0, 0, 75, 93.3)),
        };

        var berkas = _excel.TulisSemester(Kepala, baris, JenisSubjekLaporan.Siswa);

        using var workbook = new XLWorkbook(new MemoryStream(berkas));
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Siswa Satu", sheet.Cell(8, 1).GetString());
        Assert.Equal("Siswa Dua", sheet.Cell(9, 1).GetString());
    }

    [Fact]
    public void TulisSemester_Pdf_MenghasilkanBerkasValid()
    {
        var baris = new List<BarisSemester>
        {
            new("Guru Satu", "111", null, new RekapKehadiran(80, 2, 1, 0, 0, 0, 3, 1, 5, 84, 100.0)),
        };

        var berkas = _pdf.TulisSemester(Kepala, baris, JenisSubjekLaporan.Guru);

        AssertPdfValid(berkas);
    }

    [Fact]
    public void TulisSemester_Excel_HeaderRekapGuruUrutanHadirTidakLengkapSakitIzinDinasLuarCutiAlpa()
    {
        var baris = new List<BarisSemester>
        {
            new("Guru Satu", "111", null, new RekapKehadiran(80, 2, 1, 0, 0, 0, 3, 1, 5, 84, 100.0)),
        };

        var berkas = _excel.TulisSemester(Kepala, baris, JenisSubjekLaporan.Guru);

        using var workbook = new XLWorkbook(new MemoryStream(berkas));
        var sheet = workbook.Worksheet(1);
        // Baris header di baris 7; Nama(1), Identitas(2), tanpa Kelas untuk guru, lalu 7 label rekap.
        var kolomRekap = Enumerable.Range(3, 7).Select(c => sheet.Cell(7, c).GetString()).ToList();
        Assert.Equal(["Hadir", "Tidak lengkap", "Sakit", "Izin", "Dinas luar", "Cuti", "Alpa"], kolomRekap);
    }

    private static void AssertPdfValid(byte[] berkas)
    {
        Assert.True(berkas.Length > 500, "Berkas PDF terlalu kecil untuk realistis.");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(berkas, 0, 4));
    }
}
