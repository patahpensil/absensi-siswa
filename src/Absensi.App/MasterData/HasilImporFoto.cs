namespace Absensi.App.MasterData;

// PRD §16.4 langkah 5: aplikasi menampilkan daftar file yang tidak cocok.
public sealed record HasilImporFoto(int Cocok, IReadOnlyList<string> TidakCocok);
