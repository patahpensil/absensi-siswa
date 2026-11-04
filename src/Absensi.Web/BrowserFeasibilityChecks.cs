using Absensi.Data;
using Absensi.Data.Migrations;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;

namespace Absensi.Web;

public sealed record CheckResult(string Name, bool Passed, string Detail);

// Pemeriksaan kelayakan teknis (PRD §21.5, D-08): setiap pemeriksaan memakai
// kode asli Absensi.Data. Kegagalan dilaporkan apa adanya ke layar, bukan
// ditelan, supaya penyebabnya terlihat saat diuji di browser.
public static class BrowserFeasibilityChecks
{
    private const string DatabasePath = "/tmp/absensi-uji.db";

    public static IReadOnlyList<CheckResult> RunAll()
    {
        var results = new List<CheckResult>();
        SqliteConnection? connection = null;

        results.Add(Run("SQLite terbuka lewat SqliteConnectionFactory (foreign_keys + WAL)", () =>
        {
            connection = SqliteConnectionFactory.Open(DatabasePath);
            return $"journal_mode = {Scalar(connection, "PRAGMA journal_mode;")}";
        }));

        if (connection is null)
        {
            results.Add(Run("Pembanding: SQLite terbuka tanpa WAL", () =>
            {
                using var plain = new SqliteConnection($"Data Source={DatabasePath}");
                plain.Open();
                return $"versi SQLite = {Scalar(plain, "SELECT sqlite_version();")}";
            }));
            return results;
        }

        using (connection)
        {
            results.Add(Run("Migrasi bernomor berjalan sampai versi terbaru", () =>
            {
                new MigrationRunner().Run(connection);
                return $"skema_versi maksimum = {Scalar(connection, "SELECT MAX(versi) FROM skema_versi;")}";
            }));

            results.Add(Run("Tabel skema terbentuk", () =>
                $"{Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table';")} tabel"));

            results.Add(Run("foreign_keys aktif", () =>
            {
                var value = Scalar(connection, "PRAGMA foreign_keys;");
                return value == "1" ? "aktif" : throw new InvalidOperationException($"nilai = {value}");
            }));

            results.Add(Run("Tulis lalu baca kembali satu baris", () =>
            {
                using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT OR REPLACE INTO pengaturan (kunci, nilai) VALUES ('uji_browser', 'data contoh');";
                insert.ExecuteNonQuery();
                var value = Scalar(connection, "SELECT nilai FROM pengaturan WHERE kunci = 'uji_browser';");
                return value == "data contoh" ? "terbaca" : throw new InvalidOperationException($"nilai = {value}");
            }));
        }

        results.Add(Run("Excel dibuat di memori (ClosedXML)", () =>
        {
            using var workbook = new XLWorkbook();
            workbook.AddWorksheet("Uji").Cell(1, 1).Value = "NISN";
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.Length > 0 ? $"{stream.Length} byte" : throw new InvalidOperationException("berkas kosong");
        }));

        return results;
    }

    private static CheckResult Run(string name, Func<string> check)
    {
        try
        {
            return new CheckResult(name, true, check());
        }
        catch (Exception exception)
        {
            return new CheckResult(name, false, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
}
