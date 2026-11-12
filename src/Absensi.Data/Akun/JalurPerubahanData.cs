using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Akun;

// Isi catatan audit yang dihitung DI DALAM transaksi perubahan: nilai lama
// dibaca dari database sesaat sebelum diubah (bukan ditebak, bukan dibaca di
// luar transaksi), nilai baru sesudahnya. Null berarti memang tidak ada
// (mis. baris baru tidak punya nilai lama).
public sealed record CatatanPerubahan(int? EntitasId, string? NilaiLama, string? NilaiBaru);

// PRD §12: seluruh perubahan data melewati satu jalur yang
// sekaligus menulis jejak audit. Setiap operasi yang mengubah data atas
// tindakan seorang pengguna (bukan proses sistem seperti pembukaan hari di
// Absensi.Data.PembukaanHari, yang tidak punya "pengguna" untuk diaudit)
// wajib lewat sini, tidak menulis langsung lewat repositori.
public sealed class JalurPerubahanData
{
    private readonly RepositoriAudit _repositoriAudit = new();

    // PRD §12 mewajibkan "nilai lama -> nilai baru" untuk perubahan master
    // dan penggantian kartu. Nilai lama hanya diketahui saat perubahan
    // berjalan, jadi delegasi ini mengembalikannya bersama entitas_id —
    // ditulis ke audit dalam transaksi yang SAMA (semua-atau-tidak).
    // Nama berbeda dari Jalankan (bukan overload): lambda `_ => null` di call site
    // lama akan ambigu antara int? dan CatatanPerubahan.
    public void JalankanDenganCatatan(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        string aksi,
        string entitas,
        Func<SqliteTransaction, CatatanPerubahan> perubahan,
        string? atasPermintaan = null)
    {
        using var transaction = connection.BeginTransaction();
        var catatan = perubahan(transaction);
        _repositoriAudit.Catat(connection, transaction, waktu, akun.Id, aksi, entitas, catatan.EntitasId, catatan.NilaiLama, catatan.NilaiBaru, atasPermintaan);
        transaction.Commit();
    }

    public void Jalankan(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        string aksi,
        string entitas,
        Func<SqliteTransaction, int?> perubahan,
        string? nilaiLama = null,
        string? nilaiBaru = null,
        string? atasPermintaan = null,
        Action<SqliteTransaction>? setelahAudit = null)
    {
        using var transaction = connection.BeginTransaction();
        var entitasId = perubahan(transaction);
        _repositoriAudit.Catat(connection, transaction, waktu, akun.Id, aksi, entitas, entitasId, nilaiLama, nilaiBaru, atasPermintaan);
        setelahAudit?.Invoke(transaction);
        transaction.Commit();
    }
}
