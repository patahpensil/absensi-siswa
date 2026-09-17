using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.MasterData;

// Layar Kelas (PRD §14.4 daftar layar, §5.2). Setiap perubahan lewat
// JalurPerubahanData (CLAUDE.md §4.1 aturan 4).
public sealed class LayananKelas
{
    private readonly RepositoriKelas _repositoriKelas = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    public int Tambah(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        int tahunAjaranId,
        string nama,
        int tingkat,
        int? waliGuruId)
    {
        int idBaru = 0;

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "tambah_kelas",
            entitas: "kelas",
            perubahan: transaction =>
            {
                idBaru = _repositoriKelas.Tambah(connection, transaction, tahunAjaranId, nama, tingkat, waliGuruId);
                return idBaru;
            },
            nilaiBaru: nama);

        return idBaru;
    }

    public void Perbarui(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        int id,
        string nama,
        int tingkat,
        int? waliGuruId)
    {
        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "perbarui_kelas",
            entitas: "kelas",
            perubahan: transaction =>
            {
                _repositoriKelas.Perbarui(connection, transaction, id, nama, tingkat, waliGuruId);
                return id;
            },
            nilaiBaru: nama);
    }
}
