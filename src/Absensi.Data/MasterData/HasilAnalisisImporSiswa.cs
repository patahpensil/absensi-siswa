namespace Absensi.Data.MasterData;

// docs/PRD.md §14.5: pratinjau "Baru · Diperbarui · Bermasalah" sebelum
// operator menekan Impor — dihitung dari hasil analisis, sebelum transaksi
// impor benar-benar berjalan.
public sealed record HasilAnalisisImporSiswa(IReadOnlyList<BarisImporSiswa> Baris)
{
    public bool AdaKesalahan => Baris.Any(b => !b.Sah);

    public int JumlahBaru => Baris.Count(b => b.Sah && !b.SudahAdaDiDatabase);

    public int JumlahDiperbarui => Baris.Count(b => b.Sah && b.SudahAdaDiDatabase);

    public int JumlahBermasalah => Baris.Count(b => !b.Sah);
}
