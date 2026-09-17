using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Repositori;

public sealed record AbsensiGuruUntukNotifikasi(int GuruId, string NamaGuru, StatusAbsensiGuru Status);
