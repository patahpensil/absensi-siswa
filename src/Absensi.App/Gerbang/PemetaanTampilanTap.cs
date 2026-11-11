using Absensi.Domain.Kehadiran;

namespace Absensi.App.Gerbang;

// Memetakan KeputusanTap (Absensi.Domain) ke tiga hal yang layar gerbang
// butuh: kategori warna kartu, teks status sentence-case (PANDUAN-UI-UX.md
// §4.3), dan bunyi mana yang dimainkan (§8.2: bunyi berbeda berhasil/gagal).
public static class PemetaanTampilanTap
{
    public static (KategoriWarnaTap Kategori, string Teks, bool Berhasil) Petakan(KeputusanTap keputusan) =>
        keputusan.Tindakan switch
        {
            JenisTindakanTap.Registrasi => (KategoriWarnaTap.Netral, "Registrasi", true),

            JenisTindakanTap.Diabaikan => (KategoriWarnaTap.Netral, "Sudah tercatat", true),

            JenisTindakanTap.TidakBerubah when keputusan.StatusSiswaBaru is { } status =>
                (KategoriWarnaTap.Info, TeksStatusSiswa(status), true),

            JenisTindakanTap.TidakBerubah => (KategoriWarnaTap.Info, TeksStatusGuru(keputusan.StatusGuruBaru), true),

            JenisTindakanTap.IsiJamPulang => (KategoriWarnaTap.Hadir, "Pulang", true),

            JenisTindakanTap.IsiJamMasuk when keputusan.StatusSiswaBaru == StatusAbsensiSiswa.Hadir =>
                (KategoriWarnaTap.Hadir, "Hadir", true),

            JenisTindakanTap.IsiJamMasuk when keputusan.StatusSiswaBaru == StatusAbsensiSiswa.Terlambat =>
                (KategoriWarnaTap.Terlambat, "Terlambat", true),

            // Guru tap pertama: status Masuk, belum masuk kategori Hadir/Terlambat siswa.
            JenisTindakanTap.IsiJamMasuk => (KategoriWarnaTap.Hadir, "Masuk", true),

            JenisTindakanTap.Ditolak => (KategoriWarnaTap.Ditolak, TeksAlasanDitolak(keputusan.Alasan), false),

            _ => (KategoriWarnaTap.Netral, "-", true),
        };

    public static string? PetunjukUntuk(KeputusanTap keputusan) =>
        keputusan.Tindakan == JenisTindakanTap.TidakBerubah
        && keputusan.StatusSiswaBaru is StatusAbsensiSiswa.Izin or StatusAbsensiSiswa.Sakit
            ? $"Status hari ini: {TeksStatusSiswa(keputusan.StatusSiswaBaru.Value)}. Hubungi wali kelas atau admin untuk koreksi."
            : null;

    private static string TeksStatusSiswa(StatusAbsensiSiswa status) => status switch
    {
        StatusAbsensiSiswa.Izin => "Izin",
        StatusAbsensiSiswa.Sakit => "Sakit",
        _ => "-",
    };

    private static string TeksStatusGuru(StatusAbsensiGuru? status) => status switch
    {
        StatusAbsensiGuru.Izin => "Izin",
        StatusAbsensiGuru.Sakit => "Sakit",
        StatusAbsensiGuru.DinasLuar => "Dinas luar",
        StatusAbsensiGuru.Cuti => "Cuti",
        _ => "-",
    };

    // Sebelumnya seluruh JenisTindakanTap.Ditolak dipetakan ke satu kata
    // "Ditolak" tanpa alasan — operator gerbang tidak bisa membedakan UID
    // tidak dikenal, kartu/pemilik nonaktif, tap terlalu cepat, di luar
    // jam, atau tap pulang tanpa catatan masuk. keputusan.Alasan sudah
    // dihitung benar oleh PenentuStatusKehadiran dan sudah tersimpan ke
    // tap_log.catatan sejak awal (PemrosesTap.cs) — di sini hanya
    // ditampilkan, logika penentuan status TIDAK disentuh.
    private static string TeksAlasanDitolak(AlasanTap? alasan) => alasan switch
    {
        AlasanTap.TanggalLibur => "Hari libur",
        AlasanTap.KartuTidakTerdaftar => "Tidak terdaftar",
        AlasanTap.PemilikTidakAktif => "Nonaktif",
        AlasanTap.JedaTapTerlaluCepat => "Tap terlalu cepat",
        AlasanTap.BelumPunyaCatatanMasuk => "Belum tap masuk",
        AlasanTap.LewatBatasTapMasuk => "Lewat batas tap",
        // Bug jadwal kosong (docs/MASTER-BUG-UIUX-REGISTER.md, perbaikan
        // Bug #11 2026-09-23): PemrosesTap.cs sekarang menolak eksplisit
        // saat jadwal_hari hari sekolah ada tapi jam intinya kosong,
        // alih-alih diam-diam dijadikan 00:00. Teks ini yang membedakannya
        // di layar dari "Belum tap masuk" (alasan lain, siswa/guru memang
        // tap di luar urutan) — di sini keduanya kini tampil berbeda.
        AlasanTap.JadwalBelumDiatur => "Jadwal belum diatur",
        AlasanTap.BulanTerkunci => "Bulan absensi sudah dikunci.",
        AlasanTap.HariDitutup => "Hari absensi sudah ditutup.",
        _ => "Ditolak",
    };
}
