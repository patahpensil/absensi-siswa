using Absensi.Domain.Kehadiran;

namespace Absensi.Domain.Laporan;

// PANDUAN-UI-UX.md §11: "status dibedakan huruf (H, T, S, I, A)" supaya
// cetakan hitam-putih tetap terbaca. Bolos ("B") dan TidakLengkap ("L")
// bukan bagian dari lima huruf itu — panduan ditulis sebelum kedua
// status ini masuk lingkup laporan — tapi tetap diberi huruf sendiri
// (dikonfirmasi pemilik proyek 2026-09-18) supaya wali kelas/kepala
// sekolah melihat polanya sekali pandang di grid bulanan, BUKAN dilebur
// ke "H" seperti draf awal: persentase kehadiran tetap menghitung
// keduanya sebagai hadir (lihat DihitungHadir di bawah), pembedaan ini
// murni visual/grid. DL/C untuk guru (Dinas Luar/Cuti, PRD §17.8) adalah
// perluasan penulis yang sama.
public static class KodeStatus
{
    public static string Huruf(StatusAbsensiSiswa status) => status switch
    {
        StatusAbsensiSiswa.Alpa => "A",
        StatusAbsensiSiswa.Hadir => "H",
        StatusAbsensiSiswa.Terlambat => "T",
        StatusAbsensiSiswa.Bolos => "B",
        StatusAbsensiSiswa.Izin => "I",
        StatusAbsensiSiswa.Sakit => "S",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static string Huruf(StatusAbsensiGuru status) => status switch
    {
        StatusAbsensiGuru.Alpa => "A",
        StatusAbsensiGuru.Masuk => "H",
        StatusAbsensiGuru.Hadir => "H",
        StatusAbsensiGuru.TidakLengkap => "L",
        StatusAbsensiGuru.Izin => "I",
        StatusAbsensiGuru.Sakit => "S",
        StatusAbsensiGuru.DinasLuar => "DL",
        StatusAbsensiGuru.Cuti => "C",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    // PRD §11.5: Terlambat dan Bolos tetap dihitung hadir untuk
    // persentase, tapi tercatat lewat kolom keterangan supaya polanya
    // tetap terlihat wali kelas/orang tua. TidakLengkap guru (§16.16 —
    // guru tap masuk, tidak tap pulang) diberi perlakuan sama, simetris
    // dengan Bolos siswa (dikonfirmasi pemilik proyek 2026-09-18 sebagai
    // bagian cakupan laporan guru).
    public static bool DihitungHadir(StatusAbsensiSiswa status) =>
        status is StatusAbsensiSiswa.Hadir or StatusAbsensiSiswa.Terlambat or StatusAbsensiSiswa.Bolos;

    public static bool DihitungHadir(StatusAbsensiGuru status) =>
        status is StatusAbsensiGuru.Hadir or StatusAbsensiGuru.Masuk or StatusAbsensiGuru.TidakLengkap;

    public static string? Keterangan(StatusAbsensiSiswa status, TimeOnly? jamMasuk, string? keteranganTersimpan) => status switch
    {
        StatusAbsensiSiswa.Terlambat => jamMasuk is { } jam ? $"Terlambat {jam:HH:mm}" : "Terlambat",
        StatusAbsensiSiswa.Bolos => "Bolos",
        StatusAbsensiSiswa.Izin => DenganAlasan("Izin", keteranganTersimpan),
        StatusAbsensiSiswa.Sakit => DenganAlasan("Sakit", keteranganTersimpan),
        _ => null,
    };

    public static string? Keterangan(StatusAbsensiGuru status, TimeOnly? jamMasuk, string? keteranganTersimpan) => status switch
    {
        StatusAbsensiGuru.TidakLengkap => "Tidak lengkap (belum tap pulang)",
        StatusAbsensiGuru.Izin => DenganAlasan("Izin", keteranganTersimpan),
        StatusAbsensiGuru.Sakit => DenganAlasan("Sakit", keteranganTersimpan),
        StatusAbsensiGuru.DinasLuar => DenganAlasan("Dinas luar", keteranganTersimpan),
        StatusAbsensiGuru.Cuti => DenganAlasan("Cuti", keteranganTersimpan),
        _ => null,
    };

    private static string DenganAlasan(string label, string? keteranganTersimpan) =>
        string.IsNullOrWhiteSpace(keteranganTersimpan) ? label : $"{label}: {keteranganTersimpan}";
}
