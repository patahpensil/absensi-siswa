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
        PenjagaKewenangan.PastikanKewenanganPenuh(akun, "menambah siswa");

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

    // PRD §12: perubahan master dicatat "nilai lama -> nilai baru". Nilai lama
    // dibaca di dalam transaksi yang sama sebelum UPDATE; hanya kolom yang
    // benar-benar berubah yang ditulis (lihat RingkasanPerubahan).
    public void Perbarui(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        int id,
        DataSiswa data,
        (int TahunAjaranId, int KelasId)? kelas = null)
    {
        PenjagaKewenangan.PastikanKewenanganPenuh(akun, "mengubah data siswa");

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "perbarui_siswa",
            entitas: "siswa",
            perubahan: transaction =>
            {
                var lama = _repositoriSiswa.CariUntukAudit(connection, transaction, id);
                var kelasLama = kelas is { } kl ? _repositoriSiswaKelas.CariNamaKelas(connection, transaction, id, kl.TahunAjaranId) : null;

                _repositoriSiswa.Perbarui(connection, transaction, id, data);
                if (kelas is { } k)
                {
                    _repositoriSiswaKelas.Tetapkan(connection, transaction, id, k.KelasId, k.TahunAjaranId);
                }

                var kelasBaru = kelas is { } kb ? _repositoriSiswaKelas.CariNamaKelas(connection, transaction, id, kb.TahunAjaranId) : null;

                var kolom = new List<RingkasanPerubahan.Kolom>
                {
                    new("nama", lama?.Data.Nama, data.Nama),
                    new("jenis_kelamin", lama?.Data.JenisKelamin, data.JenisKelamin),
                    new("tempat_lahir", lama?.Data.TempatLahir, data.TempatLahir),
                    new("tanggal_lahir", lama?.Data.TanggalLahir, data.TanggalLahir),
                    new("alamat", lama?.Data.Alamat, data.Alamat),
                    new("nama_ortu", lama?.Data.NamaOrtu, data.NamaOrtu),
                    new("no_hp_ortu", lama?.Data.NoHpOrtu, data.NoHpOrtu),
                };
                if (kelas is not null)
                {
                    kolom.Add(new("kelas", kelasLama, kelasBaru));
                }

                return lama is null
                    ? RingkasanPerubahan.HanyaBaru(id, kolom.ToArray())
                    : RingkasanPerubahan.Bandingkan(id, kolom.ToArray());
            });
    }

    // PRD §5.3: siswa keluar/pindah ditandai tidak aktif, tidak dihapus.
    public void TandaiTidakAktif(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int id, string alasan)
    {
        PenjagaKewenangan.PastikanKewenanganPenuh(akun, "menonaktifkan siswa");

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "nonaktifkan_siswa",
            entitas: "siswa",
            perubahan: transaction =>
            {
                var lama = _repositoriSiswa.CariUntukAudit(connection, transaction, id);
                _repositoriSiswa.TandaiTidakAktif(connection, transaction, id, alasan);

                var kolom = new RingkasanPerubahan.Kolom("status", lama is null ? null : StatusAktif.Teks(lama.Aktif, lama.AlasanNonaktif), StatusAktif.Teks(false, alasan));
                return lama is null ? RingkasanPerubahan.HanyaBaru(id, kolom) : RingkasanPerubahan.Bandingkan(id, kolom);
            });
    }

    // PRD §5.5: unggah per siswa untuk foto susulan/siswa pindahan — foto
    // sudah dikecilkan ke 200x200 oleh pemanggil (PengolahFoto) sebelum
    // sampai di sini.
    public void SimpanFoto(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int id, string fotoPath)
    {
        PenjagaKewenangan.PastikanKewenanganPenuh(akun, "mengubah foto siswa");

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "unggah_foto_siswa",
            entitas: "siswa",
            perubahan: transaction =>
            {
                var lama = _repositoriSiswa.CariUntukAudit(connection, transaction, id);
                _repositoriSiswa.PerbaruiFotoPath(connection, transaction, id, fotoPath);

                var kolom = new RingkasanPerubahan.Kolom("foto_path", lama?.FotoPath, fotoPath);
                return lama is null ? RingkasanPerubahan.HanyaBaru(id, kolom) : RingkasanPerubahan.Bandingkan(id, kolom);
            });
    }
}

// Teks status aktif/nonaktif untuk nilai lama/baru di jejak audit.
internal static class StatusAktif
{
    public static string Teks(bool aktif, string? alasan) =>
        aktif ? "aktif" : string.IsNullOrWhiteSpace(alasan) ? "nonaktif" : $"nonaktif ({alasan.Trim()})";
}
