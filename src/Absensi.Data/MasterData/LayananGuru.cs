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

    // PRD §12: nilai lama dibaca di dalam transaksi yang sama sebelum UPDATE.
    // NIP tidak diubah oleh Perbarui, jadi tidak ikut dibandingkan.
    public void Perbarui(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int id, DataGuru data)
    {
        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "perbarui_guru",
            entitas: "guru",
            perubahan: transaction =>
            {
                var lama = _repositoriGuru.CariUntukAudit(connection, transaction, id);
                _repositoriGuru.Perbarui(connection, transaction, id, data);

                var kolom = new[]
                {
                    new RingkasanPerubahan.Kolom("nama", lama?.Nama, data.Nama),
                    new RingkasanPerubahan.Kolom("pangkat", lama?.Pangkat, data.Pangkat),
                    new RingkasanPerubahan.Kolom("jabatan", lama?.Jabatan, data.Jabatan),
                };
                return lama is null ? RingkasanPerubahan.HanyaBaru(id, kolom) : RingkasanPerubahan.Bandingkan(id, kolom);
            });
    }

    // PRD §5.3-setara untuk guru: pensiun/pindah ditandai tidak aktif, tidak dihapus.
    public void TandaiTidakAktif(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int id, string alasan)
    {
        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "nonaktifkan_guru",
            entitas: "guru",
            perubahan: transaction =>
            {
                var lama = _repositoriGuru.CariUntukAudit(connection, transaction, id);
                _repositoriGuru.TandaiTidakAktif(connection, transaction, id, alasan);

                var kolom = new RingkasanPerubahan.Kolom("status", lama is null ? null : StatusAktif.Teks(lama.Aktif, lama.AlasanNonaktif), StatusAktif.Teks(false, alasan));
                return lama is null ? RingkasanPerubahan.HanyaBaru(id, kolom) : RingkasanPerubahan.Bandingkan(id, kolom);
            });
    }

    public void SimpanFoto(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int id, string fotoPath)
    {
        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "unggah_foto_guru",
            entitas: "guru",
            perubahan: transaction =>
            {
                var lama = _repositoriGuru.CariUntukAudit(connection, transaction, id);
                _repositoriGuru.PerbaruiFotoPath(connection, transaction, id, fotoPath);

                var kolom = new RingkasanPerubahan.Kolom("foto_path", lama?.FotoPath, fotoPath);
                return lama is null ? RingkasanPerubahan.HanyaBaru(id, kolom) : RingkasanPerubahan.Bandingkan(id, kolom);
            });
    }
}
