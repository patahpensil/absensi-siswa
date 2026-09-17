using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.MasterData;

public sealed class LayananGuru
{
    private readonly RepositoriGuru _repositoriGuru = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    public int Tambah(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, DataGuru data)
    {
        var idBaru = 0;

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "tambah_guru",
            entitas: "guru",
            perubahan: transaction =>
            {
                idBaru = _repositoriGuru.Tambah(connection, transaction, data);
                return idBaru;
            },
            nilaiBaru: data.Nama);

        return idBaru;
    }

    public void Perbarui(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int id, DataGuru data)
    {
        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "perbarui_guru",
            entitas: "guru",
            perubahan: transaction =>
            {
                _repositoriGuru.Perbarui(connection, transaction, id, data);
                return id;
            },
            nilaiBaru: data.Nama);
    }

    // PRD §5.3-setara untuk guru: pensiun/pindah ditandai tidak aktif, tidak dihapus.
    public void TandaiTidakAktif(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int id, string alasan)
    {
        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "nonaktifkan_guru",
            entitas: "guru",
            perubahan: transaction =>
            {
                _repositoriGuru.TandaiTidakAktif(connection, transaction, id, alasan);
                return id;
            },
            nilaiBaru: alasan);
    }

    public void SimpanFoto(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int id, string fotoPath)
    {
        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "unggah_foto_guru",
            entitas: "guru",
            perubahan: transaction =>
            {
                _repositoriGuru.PerbaruiFotoPath(connection, transaction, id, fotoPath);
                return id;
            },
            nilaiBaru: fotoPath);
    }
}
