namespace Absensi.Data.Migrations;

// Master Bug & UI/UX Register A-38: PRD §12 "tidak dapat dihapus atau
// diedit siapa pun" tidak ditegakkan database -- hanya RepositoriAudit yang
// (kebetulan) cuma pernah INSERT. Dua trigger ini menegakkannya langsung di
// SQLite: UPDATE/DELETE apa pun terhadap tabel audit ditolak, termasuk
// baris yang sudah ada sebelum migrasi ini (trigger berlaku ke seluruh
// tabel, bukan cuma baris baru). INSERT (satu-satunya yang dipakai
// RepositoriAudit.Catat) tidak disentuh sama sekali.
//
// Batas jujur: ini pencegah lewat jalur SQL biasa (UPDATE/DELETE), BUKAN
// kebal-rusak -- DROP TRIGGER atau menimpa berkas .db langsung di luar
// aplikasi (mis. lewat DB Browser) tetap mungkin. Deteksi tamper yang lebih
// kuat (rantai hash per baris) sengaja tidak dikerjakan di sini, dicatat
// sebagai opsi lanjutan terpisah di register.
internal sealed class Migration002CegahUbahHapusAudit : IMigration
{
    public int Number => 2;

    public string Sql => """
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
