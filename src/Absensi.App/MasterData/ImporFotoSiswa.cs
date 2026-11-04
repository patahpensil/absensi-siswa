using System.IO;
using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.App.MasterData;

// PRD §16.4: nama file = NISN, dicocokkan ke Master Siswa, dikecilkan lalu
// disimpan ke data/foto/. Harus dijalankan setelah impor siswa.
public sealed class ImporFotoSiswa
{
    private readonly RepositoriSiswa _repositoriSiswa = new();
    private readonly LayananSiswa _layananSiswa = new();

    public HasilImporFoto Impor(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, string folderSumber)
    {
        // T6: tolak sebelum berkas apa pun disalin ke data/foto (PRD §3).
        PenjagaKewenangan.PastikanKewenanganPenuh(akun, "mengimpor foto siswa");

        var siswaBerdasarkanNisn = _repositoriSiswa.DaftarAktifRingkas(connection)
            .ToDictionary(s => s.Nisn, s => s.Id);

        var tidakCocok = new List<string>();
        var cocok = 0;

        foreach (var pathBerkas in Directory.EnumerateFiles(folderSumber))
        {
            var nisn = Path.GetFileNameWithoutExtension(pathBerkas);
            if (!siswaBerdasarkanNisn.TryGetValue(nisn, out var idSiswa))
            {
                tidakCocok.Add(Path.GetFileName(pathBerkas));
                continue;
            }

            var namaBerkasTujuan = $"{nisn}.png";
            var pathTujuan = Path.Combine(LokasiData.FolderData, "foto", namaBerkasTujuan);
            PengolahFoto.SimpanDiubahUkuran(pathBerkas, pathTujuan);
            _layananSiswa.SimpanFoto(connection, akun, waktu, idSiswa, namaBerkasTujuan);
            cocok++;
        }

        return new HasilImporFoto(cocok, tidakCocok);
    }
}
