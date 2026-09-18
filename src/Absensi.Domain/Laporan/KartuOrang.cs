namespace Absensi.Domain.Laporan;

// PRD §11.3/§17.8: "Laporan Per Siswa"/"Laporan Guru" — satu kartu per
// orang, dicetak sendiri-sendiri atau sekaligus satu kelas (siswa) /
// seluruh guru. Rincian hanya memuat tanggal tidak-hadir dan terlambat,
// bukan seluruh hari efektif (PRD §11.3: "Rincian hari tidak hadir dan
// terlambat").
public sealed record RincianKejadian(DateOnly Tanggal, string StatusHuruf, string? Keterangan);

public sealed record KartuOrang(
    string Nama,
    string Identitas,
    string? Kelas,
    string? FotoPath,
    RekapKehadiran Rekap,
    IReadOnlyList<RincianKejadian> Rincian);
