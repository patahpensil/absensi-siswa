using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Lisensi;

// Disimpan di tabel pengaturan (kunci/nilai generik, docs/SKEMA-DATABASE.md
// §3) supaya tidak perlu migrasi/tabel baru untuk tiga nilai ini.
public sealed class RepositoriLisensi
{
    private const string KunciLisensi = "lisensi_kunci";
    private const string KunciStatus = "lisensi_status";
    private const string KunciTanggalAktivasi = "lisensi_tanggal_aktivasi";

    private readonly RepositoriPengaturan _repositoriPengaturan = new();

    public StatusLisensi? BacaStatus(SqliteConnection connection)
    {
        var kunci = _repositoriPengaturan.BacaString(connection, KunciLisensi);
        if (kunci is null)
        {
            return null;
        }

        var status = _repositoriPengaturan.BacaString(connection, KunciStatus);
        var tanggalTeks = _repositoriPengaturan.BacaString(connection, KunciTanggalAktivasi);

        return new StatusLisensi(
            kunci,
            status == "aktif",
            tanggalTeks is null ? DateTime.MinValue : DateTime.Parse(tanggalTeks));
    }

    // Hanya dipanggil dari Absensi.Data.Wizard.LayananWizard, dalam
    // transaksi yang sama dengan seluruh langkah wizard lainnya — belum ada
    // akun untuk lewat JalurPerubahanData/LayananPengaturan pada titik ini
    // (lihat catatan di LayananWizard).
    public void Tulis(SqliteConnection connection, SqliteTransaction transaction, string kunciLisensi, DateTime waktuAktivasi)
    {
        _repositoriPengaturan.Tulis(connection, transaction, KunciLisensi, kunciLisensi);
        _repositoriPengaturan.Tulis(connection, transaction, KunciStatus, "aktif");
        _repositoriPengaturan.Tulis(connection, transaction, KunciTanggalAktivasi, waktuAktivasi.ToString("o"));
    }
}
