namespace Absensi.Data.MasterData;

public sealed record HasilImporSiswa(int Ditambah, int Diperbarui)
{
    public int KartuDitautkan { get; init; }
    public int KartuTetap { get; init; }
}
