using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Absensi.Domain.Notifikasi;

namespace Absensi.Data.Repositori;

// Konversi antara enum Absensi.Domain dan teks yang ditegakkan CHECK
// database (docs/SKEMA-DATABASE.md), agar kata Indonesia yang dipakai kode
// dan yang tersimpan di database tidak diketik ulang di banyak tempat.
internal static class PemetaanEnum
{
    public static JenisPemilikKartu PemilikTipeDariTeks(string teks) => teks switch
    {
        "siswa" => JenisPemilikKartu.Siswa,
        "guru" => JenisPemilikKartu.Guru,
        _ => throw new ArgumentOutOfRangeException(nameof(teks), teks, "pemilik_tipe tidak dikenal"),
    };

    public static string TeksDariPemilikTipe(JenisPemilikKartu jenis) => jenis switch
    {
        JenisPemilikKartu.Siswa => "siswa",
        JenisPemilikKartu.Guru => "guru",
        _ => throw new ArgumentOutOfRangeException(nameof(jenis), jenis, null),
    };

    public static StatusAbsensiSiswa StatusSiswaDariTeks(string teks) => teks switch
    {
        "alpa" => StatusAbsensiSiswa.Alpa,
        "hadir" => StatusAbsensiSiswa.Hadir,
        "terlambat" => StatusAbsensiSiswa.Terlambat,
        "bolos" => StatusAbsensiSiswa.Bolos,
        "izin" => StatusAbsensiSiswa.Izin,
        "sakit" => StatusAbsensiSiswa.Sakit,
        _ => throw new ArgumentOutOfRangeException(nameof(teks), teks, "status absensi_siswa tidak dikenal"),
    };

    public static string TeksDariStatusSiswa(StatusAbsensiSiswa status) => status switch
    {
        StatusAbsensiSiswa.Alpa => "alpa",
        StatusAbsensiSiswa.Hadir => "hadir",
        StatusAbsensiSiswa.Terlambat => "terlambat",
        StatusAbsensiSiswa.Bolos => "bolos",
        StatusAbsensiSiswa.Izin => "izin",
        StatusAbsensiSiswa.Sakit => "sakit",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static StatusAbsensiGuru StatusGuruDariTeks(string teks) => teks switch
    {
        "alpa" => StatusAbsensiGuru.Alpa,
        "masuk" => StatusAbsensiGuru.Masuk,
        "hadir" => StatusAbsensiGuru.Hadir,
        "tidak_lengkap" => StatusAbsensiGuru.TidakLengkap,
        "izin" => StatusAbsensiGuru.Izin,
        "sakit" => StatusAbsensiGuru.Sakit,
        "dinas_luar" => StatusAbsensiGuru.DinasLuar,
        "cuti" => StatusAbsensiGuru.Cuti,
        _ => throw new ArgumentOutOfRangeException(nameof(teks), teks, "status absensi_guru tidak dikenal"),
    };

    public static string TeksDariStatusGuru(StatusAbsensiGuru status) => status switch
    {
        StatusAbsensiGuru.Alpa => "alpa",
        StatusAbsensiGuru.Masuk => "masuk",
        StatusAbsensiGuru.Hadir => "hadir",
        StatusAbsensiGuru.TidakLengkap => "tidak_lengkap",
        StatusAbsensiGuru.Izin => "izin",
        StatusAbsensiGuru.Sakit => "sakit",
        StatusAbsensiGuru.DinasLuar => "dinas_luar",
        StatusAbsensiGuru.Cuti => "cuti",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static PeranAkun PeranDariTeks(string teks) => teks switch
    {
        "kepala_sekolah" => PeranAkun.KepalaSekolah,
        "admin" => PeranAkun.Admin,
        "wali_kelas" => PeranAkun.WaliKelas,
        _ => throw new ArgumentOutOfRangeException(nameof(teks), teks, "peran akun tidak dikenal"),
    };

    public static string TeksDariPeran(PeranAkun peran) => peran switch
    {
        PeranAkun.KepalaSekolah => "kepala_sekolah",
        PeranAkun.Admin => "admin",
        PeranAkun.WaliKelas => "wali_kelas",
        _ => throw new ArgumentOutOfRangeException(nameof(peran), peran, null),
    };

    // Guru: hanya izin/sakit/dinas_luar/cuti berarti "ditetapkan di muka"
    // (PRD 16.8.b langkah 3). alpa/masuk/hadir/tidak_lengkap bukan status
    // yang ditetapkan di muka.
    public static bool AdalahStatusDitetapkanDiMuka(StatusAbsensiGuru status) => status is
        StatusAbsensiGuru.Izin or StatusAbsensiGuru.Sakit or StatusAbsensiGuru.DinasLuar or StatusAbsensiGuru.Cuti;

    public static JenisHariKalender JenisHariDariTeks(string teks) => teks switch
    {
        "sekolah" => JenisHariKalender.Sekolah,
        "libur" => JenisHariKalender.Libur,
        _ => throw new ArgumentOutOfRangeException(nameof(teks), teks, "jenis kalender tidak dikenal"),
    };

    public static string TeksDariJenisHari(JenisHariKalender jenis) => jenis switch
    {
        JenisHariKalender.Sekolah => "sekolah",
        JenisHariKalender.Libur => "libur",
        _ => throw new ArgumentOutOfRangeException(nameof(jenis), jenis, null),
    };

    public static StatusPairing StatusPairingDariTeks(string teks) => teks switch
    {
        "menunggu" => StatusPairing.Menunggu,
        "tertaut" => StatusPairing.Tertaut,
        "dibatalkan" => StatusPairing.Dibatalkan,
        _ => throw new ArgumentOutOfRangeException(nameof(teks), teks, "status pairing tidak dikenal"),
    };

    public static string TeksDariStatusPairing(StatusPairing status) => status switch
    {
        StatusPairing.Menunggu => "menunggu",
        StatusPairing.Tertaut => "tertaut",
        StatusPairing.Dibatalkan => "dibatalkan",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static JenisSubjekOutbox SubjekOutboxDariTeks(string teks) => teks switch
    {
        "siswa" => JenisSubjekOutbox.Siswa,
        "guru" => JenisSubjekOutbox.Guru,
        "kelas" => JenisSubjekOutbox.Kelas,
        _ => throw new ArgumentOutOfRangeException(nameof(teks), teks, "subjek_tipe outbox tidak dikenal"),
    };

    public static string TeksDariSubjekOutbox(JenisSubjekOutbox subjek) => subjek switch
    {
        JenisSubjekOutbox.Siswa => "siswa",
        JenisSubjekOutbox.Guru => "guru",
        JenisSubjekOutbox.Kelas => "kelas",
        _ => throw new ArgumentOutOfRangeException(nameof(subjek), subjek, null),
    };

    public static JenisNotifikasi JenisNotifikasiDariTeks(string teks) => teks switch
    {
        "hadir" => JenisNotifikasi.Hadir,
        "terlambat" => JenisNotifikasi.Terlambat,
        "alpa" => JenisNotifikasi.Alpa,
        "ralat" => JenisNotifikasi.Ralat,
        "rekap_wali_belum_tap" => JenisNotifikasi.RekapWaliBelumTap,
        "bolos_wali" => JenisNotifikasi.BolosWali,
        "guru_tanpa_kabar" => JenisNotifikasi.GuruTanpaKabar,
        _ => throw new ArgumentOutOfRangeException(nameof(teks), teks, "jenis outbox tidak dikenal"),
    };

    public static string TeksDariJenisNotifikasi(JenisNotifikasi jenis) => jenis switch
    {
        JenisNotifikasi.Hadir => "hadir",
        JenisNotifikasi.Terlambat => "terlambat",
        JenisNotifikasi.Alpa => "alpa",
        JenisNotifikasi.Ralat => "ralat",
        JenisNotifikasi.RekapWaliBelumTap => "rekap_wali_belum_tap",
        JenisNotifikasi.BolosWali => "bolos_wali",
        JenisNotifikasi.GuruTanpaKabar => "guru_tanpa_kabar",
        _ => throw new ArgumentOutOfRangeException(nameof(jenis), jenis, null),
    };
}
