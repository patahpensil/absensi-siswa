using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Data.Wizard;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Web.Layanan;

public sealed record AkunContoh(string Username, string Sandi, string Peran);

// Data sintetis (PRD D-12): semua nama ditandai "(contoh)". Diisi lewat layanan
// asli (LayananWizard, LayananGuru, dst.), bukan SQL langsung, supaya aturan
// dan jejak audit yang diuji sama dengan aplikasi sebenarnya.
public static class DataContoh
{
    public const string SandiContoh = "contoh123";

    public static IReadOnlyList<AkunContoh> Akun { get; } = new[]
    {
        new AkunContoh("kepsek", SandiContoh, "Kepala Sekolah"),
        new AkunContoh("admin", SandiContoh, "Admin"),
        new AkunContoh("wali7a", SandiContoh, "Wali Kelas 7A"),
    };

    public static void Isi(SqliteConnection connection, DateTime sekarang)
    {
        var jadwal = Enumerable.Range(1, 7).Select(hari => hari switch
        {
            7 => new JadwalHariBaris(7, false, null, null, null, null),
            5 => Baris(hari, "10:00", "11:30"),
            _ => Baris(hari, "12:00", "13:10"),
        }).ToList();

        var kepsek = new LayananWizard().Selesaikan(connection, sekarang, new DataWizardInstalasi(
            KunciLisensi: "LISENSI-CONTOH",
            NamaSekolah: "SMP Negeri Contoh (contoh)",
            Npsn: null,
            Alamat: null,
            LogoPath: null,
            NamaTahunAjaran: "2026/2027",
            Semester: "ganjil",
            TglMulaiTahunAjaran: new DateOnly(2026, 7, 13),
            TglSelesaiTahunAjaran: new DateOnly(2026, 12, 19),
            JadwalHarian: jadwal,
            UsernameKepalaSekolah: "kepsek",
            SandiKepalaSekolah: SandiContoh));

        var tahunAjaranId = new RepositoriTahunAjaran().CariIdAktif(connection)!.Value;
        var layananGuru = new LayananGuru();
        var guruId = new List<int>();
        var namaGuru = new[] { "Ibu Sari", "Pak Budi", "Ibu Lina", "Pak Andi" };
        for (var i = 0; i < namaGuru.Length; i++)
        {
            guruId.Add(layananGuru.Tambah(connection, kepsek, sekarang,
                new DataGuru($"19800101{i + 1:00}0001", $"{namaGuru[i]} (contoh)", null, "Guru")));
        }

        var layananKelas = new LayananKelas();
        var kelas = new (string Nama, int Tingkat, int Wali)[] { ("7A", 7, guruId[0]), ("7B", 7, guruId[1]), ("8A", 8, guruId[2]) };
        var layananSiswa = new LayananSiswa();
        var nomor = 1;
        foreach (var (nama, tingkat, wali) in kelas)
        {
            var kelasId = layananKelas.Tambah(connection, kepsek, sekarang, tahunAjaranId, nama, tingkat, wali);
            for (var i = 1; i <= 8; i++, nomor++)
            {
                layananSiswa.Tambah(connection, kepsek, sekarang,
                    new DataSiswa($"{9000000000L + nomor}", $"Siswa {nama}-{i} (contoh)", nomor % 2 == 0 ? "P" : "L",
                        null, null, null, $"Orang tua {nama}-{i} (contoh)", null),
                    (tahunAjaranId, kelasId));
            }
        }

        var layananAkun = new LayananAkun();
        layananAkun.BuatAkun(connection, kepsek, sekarang, "admin", SandiContoh, PeranAkun.Admin, null);
        layananAkun.BuatAkun(connection, kepsek, sekarang, "wali7a", SandiContoh, PeranAkun.WaliKelas, guruId[0]);
    }

    private static JadwalHariBaris Baris(int hari, string batasTapMasuk, string mulaiPulang) =>
        new(hari, true, TimeOnly.Parse("07:30"), TimeOnly.Parse("08:10"), TimeOnly.Parse(batasTapMasuk), TimeOnly.Parse(mulaiPulang), TimeOnly.Parse("08:00"));
}
