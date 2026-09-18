using System.IO;
using System.Windows.Media.Imaging;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Gerbang;

// RENCANA-TUGAS.md Tugas 6: "Muat seluruh foto ke memori saat layar
// dibuka." Nama/NISN/NIP dan identitas sekolah (nama+logo, PRD §14.1)
// dimuat sekalian supaya layar gerbang tidak menunggu kueri database lagi
// begitu terbuka.
public sealed class CacheTampilanGerbang
{
    // PRD §15 struktur folder: foto siswa/guru di data/foto/, logo sekolah
    // langsung di data/ (Absensi.App.LokasiData — sumber jalur yang sama
    // dipakai App.xaml.cs untuk database).
    private static readonly string FolderFoto = Path.Combine(LokasiData.FolderData, "foto");

    private readonly Dictionary<int, InfoTampilanPemilik> _siswa;
    private readonly Dictionary<int, InfoTampilanPemilik> _guru;

    private CacheTampilanGerbang(
        Dictionary<int, InfoTampilanPemilik> siswa,
        Dictionary<int, InfoTampilanPemilik> guru,
        string namaSekolah,
        BitmapImage? logo)
    {
        _siswa = siswa;
        _guru = guru;
        NamaSekolah = namaSekolah;
        Logo = logo;
    }

    public string NamaSekolah { get; }

    public BitmapImage? Logo { get; }

    public static CacheTampilanGerbang Muat(SqliteConnection connection)
    {
        var siswa = new RepositoriSiswa().DaftarAktifRingkas(connection)
            .ToDictionary(s => s.Id, s => new InfoTampilanPemilik(s.Nama, s.Nisn, PemuatGambar.MuatDariFolder(FolderFoto, s.FotoPath)));

        var guru = new RepositoriGuru().DaftarAktifRingkas(connection)
            .ToDictionary(g => g.Id, g => new InfoTampilanPemilik(g.Nama, g.Nip, PemuatGambar.MuatDariFolder(FolderFoto, g.FotoPath)));

        var sekolah = new RepositoriSekolah().CariSatuSatunya(connection);
        var namaSekolah = string.IsNullOrWhiteSpace(sekolah?.Nama) ? "Nama sekolah belum diatur" : sekolah.Nama;
        var logo = PemuatGambar.MuatDariFolder(LokasiData.FolderData, sekolah?.LogoPath);

        return new CacheTampilanGerbang(siswa, guru, namaSekolah, logo);
    }

    public InfoTampilanPemilik? Cari(JenisPemilikKartu? tipe, int? id)
    {
        if (tipe is null || id is null)
        {
            return null;
        }

        var kamus = tipe == JenisPemilikKartu.Siswa ? _siswa : _guru;
        return kamus.GetValueOrDefault(id.Value);
    }
}
