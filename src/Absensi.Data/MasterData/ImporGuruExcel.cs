using Absensi.Data.Akun;
using Absensi.Data.Repositori;
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
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    public HasilAnalisisImporGuru Analisis(SqliteConnection connection, string pathBerkas)
    {
        using var workbook = new XLWorkbook(pathBerkas);
        // Master Bug & UI/UX Register A-8: .First() bawaan Linq melempar
        // InvalidOperationException generik -- tipe yang sama juga bisa
        // muncul dari kegagalan internal tak terkait (mis. state koneksi
        // SQLite), sehingga tidak aman ditangkap berdasarkan tipe semata di
        // pemanggil. FormatException di sini konsisten dengan validasi
        // header di bawah -- keduanya sama-sama "berkas ini tidak valid".
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

        var barisTerakhir = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var hasil = new List<BarisImporGuru>();
        var nipDalamBerkas = new HashSet<string>();

        for (var nomorBaris = 2; nomorBaris <= barisTerakhir; nomorBaris++)
        {
            if (worksheet.Row(nomorBaris).IsEmpty())
            {
                continue;
            }

            var nama = Teks(worksheet, nomorBaris, 1);
            var nip = Teks(worksheet, nomorBaris, 2);
            var pangkat = Teks(worksheet, nomorBaris, 3);
            var jabatan = Teks(worksheet, nomorBaris, 4);

            var kesalahan = new List<string>();

            // §16.15: NIP wajib dan unik
            if (string.IsNullOrWhiteSpace(nip))
            {
                kesalahan.Add("NIP wajib diisi");
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

            hasil.Add(new BarisImporGuru(
                nomorBaris,
                nip,
                nama,
                string.IsNullOrWhiteSpace(pangkat) ? null : pangkat,
                string.IsNullOrWhiteSpace(jabatan) ? null : jabatan,
                kesalahan,
                SudahAdaDiDatabase: kesalahan.Count == 0 &&
                    _repositoriGuru.CariIdBerdasarkanNip(connection, nip) is not null));
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

        var rencana = barisTervalidasi
            .Select(baris => (Baris: baris, IdGuruAda: _repositoriGuru.CariIdBerdasarkanNip(connection, baris.Nip!)))
            .ToList();

        var ditambah = 0;
        var diperbarui = 0;

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "impor_guru",
            entitas: "guru",
            perubahan: transaction =>
            {
                foreach (var (baris, idGuruAda) in rencana)
                {
                    var data = new DataGuru(baris.Nip!, baris.Nama!, baris.Pangkat, baris.Jabatan);

                    if (idGuruAda is { } id)
                    {
                        _repositoriGuru.Perbarui(connection, transaction, id, data);
                        diperbarui++;
                    }
                    else
                    {
                        _repositoriGuru.Tambah(connection, transaction, data);
                        ditambah++;
                    }
                }

                return null;
            },
            nilaiBaru: $"{ditambah} ditambah, {diperbarui} diperbarui");

        return new HasilImporGuru(ditambah, diperbarui);
    }

    // A-12: template siap-isi, header persis KolomBaku -- satu sumber
    // kebenaran yang sama dipakai Analisis di atas untuk validasi.
    public byte[] BuatTemplate() => PembuatTemplateExcel.Bangun("Guru", KolomBaku);

    private static string Teks(IXLWorksheet worksheet, int baris, int kolom) =>
        worksheet.Cell(baris, kolom).GetString().Trim();
}
