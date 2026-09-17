using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Repositori;

// Satu baris absensi_siswa untuk satu tanggal, dilengkapi nama siswa dan
// kelas — dipakai Absensi.Data.Notifikasi.LayananNotifikasi (PRD §9.2/§9.3)
// untuk menyusun isi pesan tanpa kueri terpisah per siswa.
public sealed record AbsensiSiswaUntukNotifikasi(
    int SiswaId,
    string NamaSiswa,
    int KelasId,
    string NamaKelas,
    StatusAbsensiSiswa Status,
    TimeOnly? JamMasuk,
    TimeOnly? JamPulang);
