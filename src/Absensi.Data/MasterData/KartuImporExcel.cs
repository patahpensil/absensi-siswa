using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.MasterData;

internal sealed class KartuImporExcel(JenisPemilikKartu jenisPemilik)
{
    internal const string NamaKolom = "NO_KARTU";
    private readonly RepositoriKartu _kartu = new();

    public static int? CariKolom(IXLWorksheet worksheet, int jumlahKolomWajib)
    {
        var kolom = worksheet.Row(1).CellsUsed()
            .Where(cell => string.Equals(cell.GetString().Trim(), NamaKolom, StringComparison.OrdinalIgnoreCase))
            .Select(cell => cell.Address.ColumnNumber)
            .ToArray();
        if (kolom.Length > 1 || (kolom.Length == 1 && kolom[0] <= jumlahKolomWajib))
        {
            throw new FormatException("NO_KARTU hanya boleh muncul sekali setelah kolom wajib.");
        }

        return kolom.Length == 0 ? null : kolom[0];
    }

    public string? Baca(
        SqliteConnection connection, IXLWorksheet worksheet, int baris, int? kolom, int? pemilikId,
        HashSet<string> uidDalamBerkas, List<string> kesalahan)
    {
        if (kolom is not { } nomorKolom) return null;

        var cell = worksheet.Cell(baris, nomorKolom);
        if (cell.HasFormula)
        {
            kesalahan.Add("NO_KARTU harus diisi sebagai teks tanpa rumus.");
            return null;
        }

        var uid = cell.GetString().Trim();
        if (uid.Length == 0) return null;

        if (cell.DataType != XLDataType.Text || uid.Length > 64 || uid.Any(char.IsControl))
        {
            kesalahan.Add("NO_KARTU harus berupa teks tanpa rumus, maksimal 64 karakter. Masukkan kembali nomor kartu sebagai teks agar nol di depan utuh.");
        }
        else if (!uidDalamBerkas.Add(uid))
        {
            kesalahan.Add("NO_KARTU duplikat di dalam berkas");
        }
        else if (Konflik(connection, null, uid, pemilikId) is { } konflik)
        {
            kesalahan.Add(konflik);
        }

        return uid;
    }

    // Periksa ulang setelah transaksi dibuka agar pratinjau yang sudah lama
    // tidak bisa menimpa perubahan dari operator lain.
    public void Tautkan(
        SqliteConnection connection, SqliteTransaction transaction, string? uid, int pemilikId,
        DateTime waktu, int baris, ref int ditautkan, ref int tetap)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            if (_kartu.CariUidAktifUntukPemilik(connection, jenisPemilik, pemilikId, transaction) is not null) tetap++;
            return;
        }
        if (uid.Length > 64 || uid.Any(char.IsControl))
        {
            throw new InvalidOperationException($"Baris {baris}: NO_KARTU tidak valid.");
        }

        if (Konflik(connection, transaction, uid, pemilikId) is { } konflik)
        {
            throw new InvalidOperationException($"Baris {baris}: {konflik}");
        }

        if (_kartu.CariUidAktifUntukPemilik(connection, jenisPemilik, pemilikId, transaction) == uid)
        {
            tetap++;
            return;
        }

        _kartu.Daftarkan(connection, transaction, uid, jenisPemilik, pemilikId, waktu);
        ditautkan++;
    }

    private string? Konflik(SqliteConnection connection, SqliteTransaction? transaction, string uid, int? pemilikId)
    {
        var pemilikKartu = _kartu.CariAktifBerdasarkanUid(connection, uid, transaction);
        if (pemilikKartu is not null && (pemilikKartu.PemilikTipe != jenisPemilik || pemilikKartu.PemilikId != pemilikId))
        {
            return "Kartu sudah aktif untuk siswa atau guru lain";
        }

        if (pemilikId is { } id && _kartu.CariUidAktifUntukPemilik(connection, jenisPemilik, id, transaction) is { } aktif && aktif != uid)
        {
            return "Pemilik sudah memiliki kartu aktif. Gunakan alur penggantian kartu untuk menggantinya";
        }

        return null;
    }
}
