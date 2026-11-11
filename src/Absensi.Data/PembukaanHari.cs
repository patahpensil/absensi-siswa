using Absensi.Data.Repositori;
using Absensi.Data.Koreksi;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data;

// PRD §16.7: dijalankan saat aplikasi dibuka dan saat melewati pukul 00.01.
// Tidak melakukan apa pun bila tanggal ini libur — tidak ada baris, tidak
// ada notifikasi (docs/SKEMA-DATABASE.md §11).
public sealed class PembukaanHari
{
    private readonly RepositoriTahunAjaran _repositoriTahunAjaran = new();
    private readonly RepositoriKalender _repositoriKalender = new();
    private readonly RepositoriKelas _repositoriKelas = new();
    private readonly RepositoriGuru _repositoriGuru = new();
    private readonly RepositoriAbsensi _repositoriAbsensi = new();

    public void Jalankan(SqliteConnection connection, DateOnly tanggal)
    {
        var tahunAjaranId = _repositoriTahunAjaran.CariIdAktif(connection)
            ?? throw new InvalidOperationException("Tidak ada tahun ajaran aktif; pembukaan hari tidak bisa dijalankan.");

        using var transaction = connection.BeginTransaction(deferred: false);
        var jenisHari = _repositoriKalender.TentukanJenisHari(connection, tanggal, tahunAjaranId, transaction);
        if (_repositoriTahunAjaran.FindForDate(connection, tanggal, transaction) != tahunAjaranId ||
            new RepositoriKuncimBulan().SudahDikunci(connection, tanggal.Year, tanggal.Month, transaction) ||
            jenisHari == JenisHariKalender.Libur || new LocalAttendanceClosure().IsClosed(connection, tanggal, transaction))
        {
            return;
        }

        var anggotaAktif = _repositoriKelas.DaftarSiswaAktifPerKelas(connection, tahunAjaranId);
        var guruIdAktif = _repositoriGuru.DaftarIdAktif(connection);

        _repositoriAbsensi.BukaHariSiswa(connection, transaction, tanggal, anggotaAktif);
        _repositoriAbsensi.BukaHariGuru(connection, transaction, tanggal, guruIdAktif);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO opened_school_day(date, academic_period_id) VALUES ($date, $period);
            """;
        command.Parameters.AddWithValue("$date", tanggal.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$period", tahunAjaranId);
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
