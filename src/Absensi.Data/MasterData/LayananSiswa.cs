using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.MasterData;

// Layar Siswa (PRD §14.4): tambah/edit satu siswa, mutasi (tidak aktif),
// dan unggah foto per orang. Impor massal ada di ImporSiswaExcel/
// ImporFotoSiswa. Setiap perubahan lewat JalurPerubahanData
// (CLAUDE.md §4.1 aturan 4).
public sealed class LayananSiswa
{
    private readonly RepositoriSiswa _repositoriSiswa = new();
    private readonly RepositoriSiswaKelas _repositoriSiswaKelas = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    // kelas: (TahunAjaranId, KelasId) opsional — bila diisi, keanggotaan
    // kelas siswa ikut ditetapkan dalam transaksi yang sama (satu baris
    // audit untuk keduanya).
    public int Tambah(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        DataSiswa data,
        (int TahunAjaranId, int KelasId)? kelas = null)
    {
        var idBaru = 0;

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "tambah_siswa",
            entitas: "siswa",
            perubahan: transaction =>
            {
                idBaru = _repositoriSiswa.Tambah(connection, transaction, data);
                if (kelas is { } k)
                {
                    _repositoriSiswaKelas.Tetapkan(connection, transaction, idBaru, k.KelasId, k.TahunAjaranId);
                }

                return idBaru;
            },
            nilaiBaru: data.Nama);

        return idBaru;
    }

    public void Perbarui(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        int id,
        DataSiswa data,
        (int TahunAjaranId, int KelasId)? kelas = null)
    {
        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "perbarui_siswa",
            entitas: "siswa",
            perubahan: transaction =>
            {
                _repositoriSiswa.Perbarui(connection, transaction, id, data);
                if (kelas is { } k)
                {
                    _repositoriSiswaKelas.Tetapkan(connection, transaction, id, k.KelasId, k.TahunAjaranId);
                }

                return id;
            },
            nilaiBaru: data.Nama);
    }

    // PRD §5.3: siswa keluar/pindah ditandai tidak aktif, tidak dihapus.
    public void TandaiTidakAktif(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int id, string alasan)
    {
        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "nonaktifkan_siswa",
            entitas: "siswa",
            perubahan: transaction =>
            {
                _repositoriSiswa.TandaiTidakAktif(connection, transaction, id, alasan);
                return id;
            },
            nilaiBaru: alasan);
    }

    // PRD §5.5: unggah per siswa untuk foto susulan/siswa pindahan — foto
    // sudah dikecilkan ke 200x200 oleh pemanggil (PengolahFoto) sebelum
    // sampai di sini.
    public void SimpanFoto(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int id, string fotoPath)
    {
        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "unggah_foto_siswa",
            entitas: "siswa",
            perubahan: transaction =>
            {
                _repositoriSiswa.PerbaruiFotoPath(connection, transaction, id, fotoPath);
                return id;
            },
            nilaiBaru: fotoPath);
    }
}
