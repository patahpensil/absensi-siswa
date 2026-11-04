using System.IO;
using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.App.MasterData;

// PRD §16.15 langkah 3: nama file = NIP, sama seperti foto siswa.
public sealed class ImporFotoGuru
{
    private readonly RepositoriGuru _repositoriGuru = new();
    private readonly LayananGuru _layananGuru = new();

    public HasilImporFoto Impor(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, string folderSumber)
    {
        // T6: tolak sebelum berkas apa pun disalin ke data/foto (PRD §3).
        AuthorizationGuard.EnsureFullAuthority(akun, "mengimpor foto guru");

        var guruBerdasarkanNip = _repositoriGuru.DaftarAktifRingkas(connection)
            .ToDictionary(g => g.Nip, g => g.Id);

        var tidakCocok = new List<string>();
        var cocok = 0;

        foreach (var pathBerkas in Directory.EnumerateFiles(folderSumber))
        {
            var nip = Path.GetFileNameWithoutExtension(pathBerkas);
            if (!guruBerdasarkanNip.TryGetValue(nip, out var idGuru))
            {
                tidakCocok.Add(Path.GetFileName(pathBerkas));
                continue;
            }

            var namaBerkasTujuan = $"{nip}.png";
            var pathTujuan = Path.Combine(LokasiData.FolderData, "foto", namaBerkasTujuan);
            PengolahFoto.SimpanDiubahUkuran(pathBerkas, pathTujuan);
            _layananGuru.SimpanFoto(connection, akun, waktu, idGuru, namaBerkasTujuan);
            cocok++;
        }

        return new HasilImporFoto(cocok, tidakCocok);
    }
}
