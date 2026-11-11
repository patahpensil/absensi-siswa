using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.MasterData;

// Layar Kelas (PRD §14.4 daftar layar, §5.2). Setiap perubahan lewat
// JalurPerubahanData (PRD §12).
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
        AuthorizationGuard.EnsureFullAuthority(akun, "menambah kelas");

        nama = Validate(nama, tingkat);
        int idBaru = 0;

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "tambah_kelas",
            entitas: "kelas",
            perubahan: transaction =>
            {
                EnsureActiveTeacher(connection, transaction, waliGuruId);
                idBaru = _repositoriKelas.Tambah(connection, transaction, tahunAjaranId, nama, tingkat, waliGuruId);
                _repositoriKelas.RecordHomeroomChange(connection, transaction, idBaru, null, waliGuruId, waktu);
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
        AuthorizationGuard.EnsureFullAuthority(akun, "mengubah kelas");
        nama = Validate(nama, tingkat);

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "perbarui_kelas",
            entitas: "kelas",
            perubahan: transaction =>
            {
                var lama = _repositoriKelas.CariUntukAudit(connection, transaction, id)
                    ?? throw new InvalidOperationException("Kelas tidak ditemukan.");
                if (lama.WaliGuruId != waliGuruId)
                    EnsureActiveTeacher(connection, transaction, waliGuruId);

                _repositoriKelas.Perbarui(connection, transaction, id, nama, tingkat, waliGuruId);
                _repositoriKelas.RecordHomeroomChange(connection, transaction, id, lama.WaliGuruId, waliGuruId, waktu);

                string? NamaWali(int? guruId) =>
                    guruId is { } g ? _repositoriGuru.CariNamaUntukAudit(connection, g, transaction) ?? $"id {g}" : null;

                var kolom = new[]
                {
                    new RingkasanPerubahan.Kolom("nama", lama.Nama, nama),
                    new RingkasanPerubahan.Kolom("tingkat", lama.Tingkat.ToString(), tingkat.ToString()),
                    new RingkasanPerubahan.Kolom("wali_kelas", NamaWali(lama.WaliGuruId), NamaWali(waliGuruId)),
                };
                return RingkasanPerubahan.Bandingkan(id, kolom);
            });
    }

    private static string Validate(string name, int level)
    {
        name = name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Nama kelas wajib diisi.");
        if (level is < 1 or > 9) throw new InvalidOperationException("Tingkat harus antara 1 dan 9.");
        return name;
    }

    private void EnsureActiveTeacher(SqliteConnection connection, SqliteTransaction transaction, int? teacherId)
    {
        if (teacherId is { } id && _repositoriGuru.CariUntukAudit(connection, transaction, id) is not { Aktif: true })
            throw new InvalidOperationException("Pilih guru aktif sebagai wali kelas.");
    }
}
