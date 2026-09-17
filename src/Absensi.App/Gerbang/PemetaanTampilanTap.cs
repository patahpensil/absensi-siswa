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

            JenisTindakanTap.TidakBerubah => (KategoriWarnaTap.Info, TeksStatusGuru(keputusan.StatusGuruBaru), true),

            JenisTindakanTap.IsiJamPulang => (KategoriWarnaTap.Hadir, "Pulang", true),

            JenisTindakanTap.IsiJamMasuk when keputusan.StatusSiswaBaru == StatusAbsensiSiswa.Hadir =>
                (KategoriWarnaTap.Hadir, "Hadir", true),

            JenisTindakanTap.IsiJamMasuk when keputusan.StatusSiswaBaru == StatusAbsensiSiswa.Terlambat =>
                (KategoriWarnaTap.Terlambat, "Terlambat", true),

            // Guru tap pertama: status Masuk, belum masuk kategori Hadir/Terlambat siswa.
            JenisTindakanTap.IsiJamMasuk => (KategoriWarnaTap.Hadir, "Masuk", true),

            JenisTindakanTap.Ditolak => (KategoriWarnaTap.Ditolak, "Ditolak", false),

            _ => (KategoriWarnaTap.Netral, "-", true),
        };

    private static string TeksStatusGuru(StatusAbsensiGuru? status) => status switch
    {
        StatusAbsensiGuru.Izin => "Izin",
        StatusAbsensiGuru.Sakit => "Sakit",
        StatusAbsensiGuru.DinasLuar => "Dinas luar",
        StatusAbsensiGuru.Cuti => "Cuti",
        _ => "-",
    };
}
