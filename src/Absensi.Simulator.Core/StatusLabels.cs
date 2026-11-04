using Absensi.Domain.Kehadiran;

namespace Absensi.Simulator.Core;

// Teks berbahasa Indonesia untuk status dan keputusan yang ditampilkan.
public static class StatusLabels
{
    public static string Student(StatusAbsensiSiswa status) => status switch
    {
        StatusAbsensiSiswa.Alpa => "Alpa",
        StatusAbsensiSiswa.Hadir => "Hadir",
        StatusAbsensiSiswa.Terlambat => "Terlambat",
        StatusAbsensiSiswa.Bolos => "Bolos",
        StatusAbsensiSiswa.Izin => "Izin",
        StatusAbsensiSiswa.Sakit => "Sakit",
        _ => status.ToString(),
    };

    public static string Teacher(StatusAbsensiGuru status) => status switch
    {
        StatusAbsensiGuru.Alpa => "Alpa",
        StatusAbsensiGuru.Masuk => "Masuk",
        StatusAbsensiGuru.Hadir => "Hadir",
        StatusAbsensiGuru.TidakLengkap => "Tidak lengkap",
        StatusAbsensiGuru.Izin => "Izin",
        StatusAbsensiGuru.Sakit => "Sakit",
        StatusAbsensiGuru.DinasLuar => "Dinas luar",
        StatusAbsensiGuru.Cuti => "Cuti",
        _ => status.ToString(),
    };

    public static string Reason(AlasanTap reason) => reason switch
    {
        AlasanTap.TanggalLibur => "tanggal libur",
        AlasanTap.KartuTidakTerdaftar => "kartu tidak terdaftar",
        AlasanTap.PemilikTidakAktif => "pemilik kartu tidak aktif",
        AlasanTap.JedaTapTerlaluCepat => "tap terlalu cepat setelah tap sebelumnya",
        AlasanTap.BelumPunyaCatatanMasuk => "belum ada catatan masuk",
        AlasanTap.LewatBatasTapMasuk => "lewat batas akhir tap masuk",
        AlasanTap.StatusSudahDitetapkanDiMuka => "status sudah ditetapkan sebelumnya",
        AlasanTap.JadwalBelumDiatur => "jadwal belum diatur",
        _ => reason.ToString(),
    };

    public static string Decision(KeputusanTap decision) => decision.Tindakan switch
    {
        JenisTindakanTap.Registrasi => "Mode registrasi: tidak dicatat sebagai absensi",
        JenisTindakanTap.Ditolak => $"Ditolak: {Reason(decision.Alasan!.Value)}",
        JenisTindakanTap.Diabaikan => "Diabaikan: sudah tap masuk, belum jam pulang",
        JenisTindakanTap.IsiJamMasuk when decision.StatusSiswaBaru is { } student => $"Jam masuk tercatat: {Student(student)}",
        JenisTindakanTap.IsiJamMasuk => "Jam masuk tercatat: Masuk",
        JenisTindakanTap.IsiJamPulang => "Jam pulang tercatat",
        JenisTindakanTap.TidakBerubah when decision.StatusSiswaBaru is { } student => $"Tidak berubah: tetap {Student(student)}",
        JenisTindakanTap.TidakBerubah when decision.StatusGuruBaru is { } teacher => $"Tidak berubah: tetap {Teacher(teacher)}",
        _ => decision.Tindakan.ToString(),
    };
}
