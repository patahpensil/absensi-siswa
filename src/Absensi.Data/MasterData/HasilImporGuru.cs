namespace Absensi.Data.MasterData;

public sealed record HasilImporGuru(int Ditambah, int Diperbarui)
{
    public int KartuDitautkan { get; init; }
    public int KartuTetap { get; init; }
}
