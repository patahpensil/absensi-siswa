namespace Absensi.Domain.Kehadiran;

// Keluaran PenentuStatusKehadiran: keputusan berupa jenis tindakan dan
// alasannya, bukan penulisan ke database (RENCANA-TUGAS.md Tugas 2).
public sealed record KeputusanTap(
    JenisTindakanTap Tindakan,
    AlasanTap? Alasan = null,
    StatusAbsensiSiswa? StatusSiswaBaru = null,
    StatusAbsensiGuru? StatusGuruBaru = null)
{
    public static KeputusanTap Registrasi() => new(JenisTindakanTap.Registrasi);

    public static KeputusanTap Ditolak(AlasanTap alasan) => new(JenisTindakanTap.Ditolak, alasan);

    public static KeputusanTap Diabaikan() => new(JenisTindakanTap.Diabaikan);

    public static KeputusanTap IsiJamMasukSiswa(StatusAbsensiSiswa status) =>
        new(JenisTindakanTap.IsiJamMasuk, StatusSiswaBaru: status);

    public static KeputusanTap IsiJamPulangSiswa() => new(JenisTindakanTap.IsiJamPulang);

    public static KeputusanTap IsiJamMasukGuru() =>
        new(JenisTindakanTap.IsiJamMasuk, StatusGuruBaru: StatusAbsensiGuru.Masuk);

    public static KeputusanTap IsiJamPulangGuru() =>
        new(JenisTindakanTap.IsiJamPulang, StatusGuruBaru: StatusAbsensiGuru.Hadir);

    public static KeputusanTap TidakBerubahGuru(StatusAbsensiGuru status) =>
        new(JenisTindakanTap.TidakBerubah, AlasanTap.StatusSudahDitetapkanDiMuka, StatusGuruBaru: status);
}
