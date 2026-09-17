using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Akun;

// CLAUDE.md §4.1 aturan 4: seluruh perubahan data melewati satu jalur yang
// sekaligus menulis jejak audit. Setiap operasi yang mengubah data atas
// tindakan seorang pengguna (bukan proses sistem seperti pembukaan hari di
// Absensi.Data.PembukaanHari, yang tidak punya "pengguna" untuk diaudit)
// wajib lewat sini, tidak menulis langsung lewat repositori.
public sealed class JalurPerubahanData
{
    private readonly RepositoriAudit _repositoriAudit = new();

    public void Jalankan(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        string aksi,
        string entitas,
        Func<SqliteTransaction, int?> perubahan,
        string? nilaiLama = null,
        string? nilaiBaru = null,
        string? atasPermintaan = null)
    {
        using var transaction = connection.BeginTransaction();
        var entitasId = perubahan(transaction);
        _repositoriAudit.Catat(connection, transaction, waktu, akun.Id, aksi, entitas, entitasId, nilaiLama, nilaiBaru, atasPermintaan);
        transaction.Commit();
    }
}
