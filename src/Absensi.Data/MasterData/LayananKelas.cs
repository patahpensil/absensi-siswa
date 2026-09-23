using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.MasterData;

// Layar Kelas (PRD §14.4 daftar layar, §5.2). Setiap perubahan lewat
// JalurPerubahanData (CLAUDE.md §4.1 aturan 4).
public sealed class LayananKelas
{
    private readonly RepositoriKelas _repositoriKelas = new();
    private readonly RepositoriGuru _repositoriGuru = new();
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

    // PRD §12: nilai lama dibaca di dalam transaksi yang sama sebelum UPDATE.
    // Wali kelas dicatat dengan NAMA guru (bukan id) supaya bisa dibaca.
    public void Perbarui(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        int id,
        string nama,
        int tingkat,
        int? waliGuruId)
    {
        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "perbarui_kelas",
            entitas: "kelas",
            perubahan: transaction =>
            {
                var lama = _repositoriKelas.CariUntukAudit(connection, transaction, id);
                _repositoriKelas.Perbarui(connection, transaction, id, nama, tingkat, waliGuruId);

                string? NamaWali(int? guruId) =>
                    guruId is { } g ? _repositoriGuru.CariNamaUntukAudit(connection, g, transaction) ?? $"id {g}" : null;

                var kolom = new[]
                {
                    new RingkasanPerubahan.Kolom("nama", lama?.Nama, nama),
                    new RingkasanPerubahan.Kolom("tingkat", lama?.Tingkat.ToString(), tingkat.ToString()),
                    new RingkasanPerubahan.Kolom("wali_kelas", lama is null ? null : NamaWali(lama.WaliGuruId), NamaWali(waliGuruId)),
                };
                return lama is null ? RingkasanPerubahan.HanyaBaru(id, kolom) : RingkasanPerubahan.Bandingkan(id, kolom);
            });
    }
}
