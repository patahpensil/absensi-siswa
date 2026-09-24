namespace Absensi.Data.Migrations;

// Master Bug & UI/UX Register A-39: identitas permanen untuk setiap baris
// audit, supaya baris yang sama tetap DIKENALI sebagai peristiwa yang sama
// walau `id` SQLite-nya (INTEGER PRIMARY KEY tanpa AUTOINCREMENT) dipakai
// ulang oleh peristiwa LAIN setelah restore -- dibuktikan lewat prototipe
// terpisah (scratchpad, 2026-09-24) sebelum migrasi ini ditulis, bukan
// diasumsikan.
//
// Baris LAMA (sudah ada sebelum migrasi ini dijalankan): audit_uid diisi
// FINGERPRINT DETERMINISTIK dari isi baris itu sendiri (murni SQL, lihat
// UPDATE di bawah) -- SENGAJA BUKAN GUID acak, supaya baris yang SAMA
// tetap dikenali sama walau di-backfill ulang secara independen di
// lini masa/berkas berbeda (mis. backup lama yang suatu saat di-restore
// lalu database hasilnya dimigrasi ulang dari versi 2). Fingerprint
// SELALU mengandung '|' -- ciri ini dipakai kode/query lain untuk
// membedakan baris lama (fingerprint) dari baris baru (GUID, tidak
// pernah mengandung '|').
//
// Baris BARU (dibuat setelah migrasi ini ada): audit_uid diisi GUID acak
// oleh RepositoriAudit.Catat saat INSERT -- aman karena peristiwa nyata
// hanya pernah di-INSERT SEKALI; salinan berikutnya (backup/arsip) selalu
// berupa salinan berkas, bukan INSERT ulang, jadi GUID itu ikut tersalin
// apa adanya.
//
// JANGAN UBAH rumus fingerprint di bawah setelah dirilis -- mengubahnya
// akan membuat baris lama yang SAMA dihitung ulang jadi audit_uid BERBEDA
// di lini masa yang berbeda, merusak jaminan identitas yang justru jadi
// tujuan migrasi ini (lihat docs/SKEMA-DATABASE.md §9).
//
// Trigger migrasi 002 (A-38, "audit tidak dapat diubah") memblokir SEMUA
// UPDATE ke tabel audit -- termasuk UPDATE backfill migrasi ini sendiri.
// Ditemukan lewat tes gagal, bukan diasumsikan (lihat
// Migration002CegahUbahHapusAuditTests, dijalankan sebelum perbaikan ini).
// Diselesaikan dengan cara yang SAMA persis seperti migrasi lain yang
// legitimately perlu menulis ulang data lama secara terkendali: lepas
// KEDUA trigger, backfill, pasang kembali PERSIS sama -- semuanya di
// DALAM satu transaksi migrasi (MigrationRunner.ApplyMigration), jadi
// kalau ada langkah manapun gagal, seluruhnya batal termasuk pelepasan
// trigger -- tidak pernah ada jendela waktu tabel audit tanpa trigger di
// database yang sudah selesai dimigrasi.
internal sealed class Migration003TambahAuditUid : IMigration
{
    public int Number => 3;

    public string Sql => """
        DROP TRIGGER trg_audit_cegah_update;
        DROP TRIGGER trg_audit_cegah_delete;

        ALTER TABLE audit ADD COLUMN audit_uid TEXT NOT NULL DEFAULT '';

        UPDATE audit SET audit_uid =
            waktu || '|' || akun_id || '|' || aksi || '|' || entitas || '|' ||
            COALESCE(entitas_id, '') || '|' || COALESCE(nilai_lama, '') || '|' ||
            COALESCE(nilai_baru, '') || '|' || COALESCE(atas_permintaan, '')
        WHERE audit_uid = '';

        CREATE UNIQUE INDEX ix_audit_uid ON audit(audit_uid);

        CREATE TRIGGER trg_audit_cegah_update
        BEFORE UPDATE ON audit
        BEGIN
            SELECT RAISE(ABORT, 'Baris audit tidak dapat diubah (PRD §12).');
        END;

        CREATE TRIGGER trg_audit_cegah_delete
        BEFORE DELETE ON audit
        BEGIN
            SELECT RAISE(ABORT, 'Baris audit tidak dapat dihapus (PRD §12).');
        END;
        """;
}
