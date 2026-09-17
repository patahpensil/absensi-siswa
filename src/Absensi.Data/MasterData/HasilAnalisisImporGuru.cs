namespace Absensi.Data.MasterData;

public sealed record HasilAnalisisImporGuru(IReadOnlyList<BarisImporGuru> Baris)
{
    public bool AdaKesalahan => Baris.Any(b => !b.Sah);

    public int JumlahBaru => Baris.Count(b => b.Sah && !b.SudahAdaDiDatabase);

    public int JumlahDiperbarui => Baris.Count(b => b.Sah && b.SudahAdaDiDatabase);

    public int JumlahBermasalah => Baris.Count(b => !b.Sah);
}
