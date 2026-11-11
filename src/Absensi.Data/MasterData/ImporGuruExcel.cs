using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.MasterData;

// Impor Excel guru mengikuti docs/PRD.md §16.15 langkah 2, dengan pola
// yang sama seperti ImporSiswaExcel (analisis lalu impor satu transaksi,
// "utuh").
public sealed class ImporGuruExcel
{
    // A-12: internal (bukan public) + IReadOnlyList (bukan string[]) --
    // pembuat template (PembuatTemplateExcel) ada di assembly yang sama,
    // dan test (Absensi.Data.Tests) diberi akses lewat
    // [assembly: InternalsVisibleTo] di AssemblyInfo.cs, bukan dengan
    // melebarkan visibilitas jadi public. Array.AsReadOnly (bukan sekadar
    // deklarasi tipe IReadOnlyList di atas array mentah) supaya objek
    // RUNTIME-nya sendiri (ReadOnlyCollection<T>) menolak mutasi lewat
    // NotSupportedException walau di-cast ke IList<string> -- deklarasi
    // tipe saja hanya membatasi di compile time, tidak di runtime.
    internal static readonly IReadOnlyList<string> KolomBaku = Array.AsReadOnly(new[] { "NAMA", "NIP", "PANGKAT", "JABATAN" });

    private readonly RepositoriGuru _repositoriGuru = new();
    private readonly KartuImporExcel _kartu = new(JenisPemilikKartu.Guru);
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    public HasilAnalisisImporGuru Analisis(SqliteConnection connection, string pathBerkas)
    {
        using var stream = File.OpenRead(pathBerkas);
        return Analisis(connection, stream);
    }

    public HasilAnalisisImporGuru Analisis(SqliteConnection connection, Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new FormatException("Berkas Excel ini tidak punya lembar kerja (sheet) sama sekali.");

        for (var kolom = 0; kolom < KolomBaku.Count; kolom++)
        {
            var judul = worksheet.Cell(1, kolom + 1).GetString().Trim();
            if (!string.Equals(judul, KolomBaku[kolom], StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException(
                    $"Kolom {kolom + 1} seharusnya '{KolomBaku[kolom]}', berkas ini punya '{judul}'.");
            }
        }

        var kolomKartu = KartuImporExcel.CariKolom(worksheet, KolomBaku.Count);
        var uidDalamBerkas = new HashSet<string>(StringComparer.Ordinal);
        var hasil = new List<BarisImporGuru>();
        var nipDalamBerkas = new HashSet<string>();

        foreach (var row in worksheet.RowsUsed().Where(row => row.RowNumber() > 1).OrderBy(row => row.RowNumber()))
        {
            var nomorBaris = row.RowNumber();

            var nama = Teks(worksheet, nomorBaris, 1);
            var selNip = worksheet.Cell(nomorBaris, 2);
            var nip = selNip.GetString().Trim();
            var pangkat = Teks(worksheet, nomorBaris, 3);
            var jabatan = Teks(worksheet, nomorBaris, 4);

            var kesalahan = new List<string>();

            // §16.15: NIP wajib dan unik
            if (string.IsNullOrWhiteSpace(nip))
            {
                kesalahan.Add("NIP wajib diisi");
            }
            else if (selNip.DataType == XLDataType.Number && Math.Abs(selNip.GetDouble()) >= 1e15)
            {
                kesalahan.Add("NIP numerik melebihi presisi Excel. Masukkan kembali NIP asli sebagai teks");
            }
            else if (!nipDalamBerkas.Add(nip))
            {
                kesalahan.Add("NIP duplikat di dalam berkas");
            }

            // §16.15: NAMA wajib
            if (string.IsNullOrWhiteSpace(nama))
            {
                kesalahan.Add("Nama wajib diisi");
            }

            var id = _repositoriGuru.CariIdBerdasarkanNip(connection, nip);
            var uid = _kartu.Baca(connection, worksheet, nomorBaris, kolomKartu, id, uidDalamBerkas, kesalahan);

            hasil.Add(new BarisImporGuru(
                nomorBaris,
                nip,
                nama,
                string.IsNullOrWhiteSpace(pangkat) ? null : pangkat,
                string.IsNullOrWhiteSpace(jabatan) ? null : jabatan,
                kesalahan,
                SudahAdaDiDatabase: kesalahan.Count == 0 && id is not null,
                NoKartu: uid));
        }

        return new HasilAnalisisImporGuru(hasil);
    }

    public HasilImporGuru Impor(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        IReadOnlyList<BarisImporGuru> barisTervalidasi)
    {
        AuthorizationGuard.EnsureFullAuthority(akun, "mengimpor data guru");

        if (barisTervalidasi.Any(b => !b.Sah))
        {
            throw new InvalidOperationException("Tidak bisa mengimpor selagi masih ada baris bermasalah.");
        }

        var ditambah = 0;
        var diperbarui = 0;

        var kartuDitautkan = 0;
        var kartuTetap = 0;

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "impor_guru",
            entitas: "guru",
            perubahan: transaction =>
            {
                foreach (var baris in barisTervalidasi)
                {
                    var idGuruAda = _repositoriGuru.CariIdBerdasarkanNip(connection, baris.Nip!, transaction);
                    int idGuru;
                    var data = new DataGuru(baris.Nip!, baris.Nama!, baris.Pangkat, baris.Jabatan);

                    if (idGuruAda is { } id)
                    {
                        _repositoriGuru.Perbarui(connection, transaction, id, data);
                        idGuru = id;
                        diperbarui++;
                    }
                    else
                    {
                        idGuru = _repositoriGuru.Tambah(connection, transaction, data);
                        ditambah++;
                    }

                    _kartu.Tautkan(connection, transaction, baris.NoKartu, idGuru, waktu, baris.NomorBaris, ref kartuDitautkan, ref kartuTetap);
                }

                return new CatatanPerubahan(null, null,
                    $"{ditambah} ditambah, {diperbarui} diperbarui, {kartuDitautkan} kartu ditautkan, {kartuTetap} kartu tetap");
            });

        return new HasilImporGuru(ditambah, diperbarui) { KartuDitautkan = kartuDitautkan, KartuTetap = kartuTetap };
    }

    public byte[] BuatTemplate() => PembuatTemplateExcel.Bangun("Guru", [.. KolomBaku, KartuImporExcel.NamaKolom]);

    private static string Teks(IXLWorksheet worksheet, int baris, int kolom) =>
        worksheet.Cell(baris, kolom).GetString().Trim();
}
