using ClosedXML.Excel;

namespace Absensi.Data.MasterData;

// Master Bug & UI/UX Register A-12 (docs/PRD.md §16.2 langkah 3): membangun
// template Excel siap-isi dari kolom baku yang SAMA dipakai validasi impor
// (satu sumber kebenaran -- lihat ImporSiswaExcel.KolomBaku dan
// ImporGuruExcel.KolomBaku, keduanya cuma meneruskan array privat mereka
// sendiri ke sini). Dipakai bersama oleh kedua kelas supaya format (tebal,
// warna, freeze, autofilter, lebar kolom) tidak ditulis dua kali.
internal static class PembuatTemplateExcel
{
    // PANDUAN-UI-UX.md §3.1 "Biru muda" -- token warna yang sudah ada di
    // GayaAplikasi.xaml, dipakai ulang di sini (bukan warna baru) supaya
    // template ikut identitas visual aplikasi.
    private static readonly XLColor WarnaHeader = XLColor.FromHtml("#E7EDF6");

    public static byte[] Bangun(string namaWorksheet, IReadOnlyList<string> kolomBaku)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(namaWorksheet);

        for (var kolom = 0; kolom < kolomBaku.Count; kolom++)
        {
            var cell = worksheet.Cell(1, kolom + 1);
            cell.SetValue(kolomBaku[kolom]);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = WarnaHeader;
        }

        // Sengaja TIDAK menambah baris data contoh -- berisiko ikut
        // terimpor sebagai data nyata (diminta eksplisit saat A-12
        // dikerjakan).
        worksheet.Range(1, 1, 1, kolomBaku.Count).SetAutoFilter();
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, kolomBaku.Count).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
