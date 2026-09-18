using Absensi.Domain.Laporan;
using ClosedXML.Excel;

namespace Absensi.Laporan;

// RENCANA-TUGAS.md Tugas 12. Satu lembar kerja per laporan, blok kepala
// (sekolah/judul/rentang) di baris teratas sebelum tabel — bukan satu
// baris per orang seperti PDF Per Orang, karena Excel dipakai untuk
// diolah lagi (filter/pivot), bukan dicetak per kartu.
public sealed class PenulisExcelLaporan
{
    public byte[] TulisKejadian(KepalaLaporan kepala, IReadOnlyList<BarisKejadian> baris, bool tampilkanKelas)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Laporan");
        var baris0 = TulisKepalaSheet(sheet, kepala);

        var kolom = new List<string> { "Tanggal", "Nama", "Identitas" };
        if (tampilkanKelas)
        {
            kolom.Add("Kelas");
        }

        kolom.AddRange(["Status", "Jam masuk", "Jam pulang", "Keterangan"]);
        TulisHeaderTabel(sheet, baris0, kolom);

        var b = baris0 + 1;
        foreach (var item in baris)
        {
            var k = 1;
            sheet.Cell(b, k++).Value = item.Tanggal.ToString("dd/MM/yyyy");
            sheet.Cell(b, k++).Value = item.Nama;
            sheet.Cell(b, k++).Value = item.Identitas;
            if (tampilkanKelas)
            {
                sheet.Cell(b, k++).Value = item.Kelas ?? "-";
            }

            sheet.Cell(b, k++).Value = item.StatusHuruf;
            sheet.Cell(b, k++).Value = item.JamMasuk?.ToString("HH:mm") ?? "-";
            sheet.Cell(b, k++).Value = item.JamPulang?.ToString("HH:mm") ?? "-";
            sheet.Cell(b, k).Value = item.Keterangan ?? "-";
            b++;
        }

        sheet.Columns().AdjustToContents();
        return SimpanKeByte(workbook);
    }

    public byte[] TulisBulanan(KepalaLaporan kepala, LaporanBulanan laporan, JenisSubjekLaporan subjek)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Laporan Bulanan");
        var baris0 = TulisKepalaSheet(sheet, kepala);

        var kolom = new List<string> { "Nama", "Identitas" };
        if (subjek == JenisSubjekLaporan.Siswa)
        {
            kolom.Add("Kelas");
        }

        kolom.AddRange(laporan.Kolom.Select(k => k.Tanggal.Day.ToString()));
        kolom.AddRange(KolomRekap(subjek));
        kolom.Add("%");
        TulisHeaderTabel(sheet, baris0, kolom);

        var kolomTanggalAwal = subjek == JenisSubjekLaporan.Siswa ? 4 : 3;
        for (var i = 0; i < laporan.Kolom.Count; i++)
        {
            if (laporan.Kolom[i].Libur)
            {
                sheet.Column(kolomTanggalAwal + i).Style.Fill.BackgroundColor = XLColor.FromArgb(0xE8, 0xE8, 0xE8);
            }
        }

        var b = baris0 + 1;
        foreach (var item in laporan.Baris)
        {
            var k = 1;
            sheet.Cell(b, k++).Value = item.Nama;
            sheet.Cell(b, k++).Value = item.Identitas;
            if (subjek == JenisSubjekLaporan.Siswa)
            {
                sheet.Cell(b, k++).Value = item.Kelas ?? "-";
            }

            foreach (var tgl in laporan.Kolom)
            {
                sheet.Cell(b, k++).Value = item.KodePerTanggal.TryGetValue(tgl.Tanggal, out var kode) ? kode : "";
            }

            foreach (var nilai in NilaiRekap(item.Rekap, subjek))
            {
                sheet.Cell(b, k++).Value = nilai;
            }

            sheet.Cell(b, k).Value = item.Rekap.Persentase;
            b++;
        }

        var alasanLibur = laporan.Kolom.Where(k => k.Libur && !string.IsNullOrWhiteSpace(k.Alasan)).ToList();
        if (alasanLibur.Count > 0)
        {
            sheet.Cell(b + 1, 1).Value = "Catatan libur mendadak: " +
                string.Join("; ", alasanLibur.Select(k => $"{k.Tanggal:d/M} — {k.Alasan}"));
        }

        sheet.Columns().AdjustToContents();
        return SimpanKeByte(workbook);
    }

    public byte[] TulisKartuOrang(KepalaLaporan kepala, IReadOnlyList<KartuOrang> kartu, JenisSubjekLaporan subjek)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Laporan Per Orang");
        var b = TulisKepalaSheet(sheet, kepala) + 1;
        var labelIdentitas = subjek == JenisSubjekLaporan.Siswa ? "NISN" : "NIP";

        foreach (var orang in kartu)
        {
            sheet.Cell(b, 1).Value = orang.Nama;
            sheet.Cell(b, 1).Style.Font.SetBold();
            b++;
            sheet.Cell(b, 1).Value = $"{labelIdentitas}: {orang.Identitas}" + (orang.Kelas is null ? "" : $" · Kelas: {orang.Kelas}");
            b++;
            var pasangan = LabelPenuhRekap(subjek).Zip(NilaiRekap(orang.Rekap, subjek), (label, nilai) => $"{label} {nilai}");
            sheet.Cell(b, 1).Value = string.Join(" · ", pasangan) +
                $" · Kehadiran {orang.Rekap.Persentase:0.0}% dari {orang.Rekap.HariEfektif} hari efektif";
            b++;

            if (orang.Rincian.Count > 0)
            {
                TulisHeaderTabel(sheet, b, ["Tanggal", "Status", "Keterangan"]);
                b++;
                foreach (var r in orang.Rincian)
                {
                    sheet.Cell(b, 1).Value = r.Tanggal.ToString("dd/MM/yyyy");
                    sheet.Cell(b, 2).Value = r.StatusHuruf;
                    sheet.Cell(b, 3).Value = r.Keterangan ?? "-";
                    b++;
                }
            }

            b += 2;
        }

        sheet.Columns().AdjustToContents();
        return SimpanKeByte(workbook);
    }

    public byte[] TulisSemester(KepalaLaporan kepala, IReadOnlyList<BarisSemester> baris, JenisSubjekLaporan subjek)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Laporan Semester");
        var baris0 = TulisKepalaSheet(sheet, kepala);

        var kolom = new List<string> { "Nama", "Identitas" };
        if (subjek == JenisSubjekLaporan.Siswa)
        {
            kolom.Add("Kelas");
        }

        kolom.AddRange(LabelPenuhRekap(subjek));
        kolom.Add("% Kehadiran");
        TulisHeaderTabel(sheet, baris0, kolom);

        var b = baris0 + 1;
        foreach (var item in baris)
        {
            var k = 1;
            sheet.Cell(b, k++).Value = item.Nama;
            sheet.Cell(b, k++).Value = item.Identitas;
            if (subjek == JenisSubjekLaporan.Siswa)
            {
                sheet.Cell(b, k++).Value = item.Kelas ?? "-";
            }

            foreach (var nilai in NilaiRekap(item.Rekap, subjek))
            {
                sheet.Cell(b, k++).Value = nilai;
            }

            sheet.Cell(b, k).Value = item.Rekap.Persentase;
            b++;
        }

        sheet.Columns().AdjustToContents();
        return SimpanKeByte(workbook);
    }

    // Format kolom rekap guru — Hadir · Tidak Lengkap · Sakit · Izin ·
    // Dinas Luar · Cuti · Alpa (dikonfirmasi pemilik proyek 2026-09-18,
    // PRD §17.3) — sengaja BEDA urutan dari siswa, bukan siswa+tambahan,
    // karena guru tidak punya Terlambat/Bolos. Dipakai bersama oleh grid
    // bulanan, kartu per orang, dan tabel semester supaya ketiganya
    // konsisten satu sama lain — sama seperti PenulisPdfLaporan.
    private static string[] KolomRekap(JenisSubjekLaporan subjek) => subjek == JenisSubjekLaporan.Guru
        ? ["H", "L", "S", "I", "DL", "C", "A"]
        : ["H", "S", "I", "A", "T", "Bolos"];

    private static string[] LabelPenuhRekap(JenisSubjekLaporan subjek) => subjek == JenisSubjekLaporan.Guru
        ? ["Hadir", "Tidak lengkap", "Sakit", "Izin", "Dinas luar", "Cuti", "Alpa"]
        : ["Hadir", "Sakit", "Izin", "Alpa", "Terlambat", "Bolos"];

    private static int[] NilaiRekap(RekapKehadiran rekap, JenisSubjekLaporan subjek) => subjek == JenisSubjekLaporan.Guru
        ? [rekap.Hadir, rekap.TidakLengkap, rekap.Sakit, rekap.Izin, rekap.DinasLuar, rekap.Cuti, rekap.Alpa]
        : [rekap.Hadir, rekap.Sakit, rekap.Izin, rekap.Alpa, rekap.Terlambat, rekap.Bolos];

    // PANDUAN-UI-UX.md §11: kepala halaman menyebut sekolah/NPSN/judul/
    // rentang walau di Excel tidak ada konsep "halaman" — dipakai supaya
    // berkas Excel yang diunduh terpisah dari layar tetap bisa diketahui
    // asal dan cakupannya. Mengembalikan nomor baris tabel dimulai.
    private static int TulisKepalaSheet(IXLWorksheet sheet, KepalaLaporan kepala)
    {
        sheet.Cell(1, 1).Value = kepala.NamaSekolah;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13);
        if (!string.IsNullOrWhiteSpace(kepala.Npsn))
        {
            sheet.Cell(2, 1).Value = $"NPSN: {kepala.Npsn}";
        }

        sheet.Cell(3, 1).Value = kepala.Judul;
        sheet.Cell(3, 1).Style.Font.SetBold();
        sheet.Cell(4, 1).Value = kepala.RentangTeks;
        sheet.Cell(5, 1).Value = $"Dicetak {kepala.DicetakPada:d MMMM yyyy HH:mm} oleh {kepala.DicetakOleh}";
        return 7;
    }

    private static void TulisHeaderTabel(IXLWorksheet sheet, int baris, IReadOnlyList<string> kolom)
    {
        for (var i = 0; i < kolom.Count; i++)
        {
            var sel = sheet.Cell(baris, i + 1);
            sel.Value = kolom[i];
            sel.Style.Font.SetBold();
            sel.Style.Border.SetBottomBorder(XLBorderStyleValues.Thin);
        }
    }

    private static byte[] SimpanKeByte(XLWorkbook workbook)
    {
        using var aliran = new MemoryStream();
        workbook.SaveAs(aliran);
        return aliran.ToArray();
    }
}
