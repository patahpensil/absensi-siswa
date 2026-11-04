using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Pengaturan;

// RENCANA-TUGAS.md Tugas 10 — PRD §16.6 langkah 2: "Admin memasukkan token
// bot dan username bot di menu Pengaturan". Setiap perubahan lewat
// JalurPerubahanData (CLAUDE.md §4.1 aturan 4).
public sealed class LayananPengaturan
{
    private readonly RepositoriPengaturan _repositoriPengaturan = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    public void Simpan(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, string kunci, string nilai)
    {
        AuthorizationGuard.EnsureFullAuthority(akun, "mengubah pengaturan");

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "ubah_pengaturan",
            entitas: "pengaturan",
            perubahan: transaction =>
            {
                _repositoriPengaturan.Tulis(connection, transaction, kunci, nilai);
                return null;
            },
            nilaiBaru: $"{kunci}={NilaiUntukAudit(kunci, nilai)}");
    }

    // Audit tidak bisa diubah/dihapus (trigger A-38) dan ikut arsip serta
    // backup, jadi rahasia tidak boleh pernah masuk ke sana. Cukup tercatat
    // BAHWA token diganti, bukan nilainya.
    private static string NilaiUntukAudit(string kunci, string nilai) =>
        kunci.Contains("token", StringComparison.OrdinalIgnoreCase) || kunci.Contains("sandi", StringComparison.OrdinalIgnoreCase)
            ? "(disamarkan)"
            : nilai;
}
