using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Repositori;

public sealed record AbsensiGuruHariIni(StatusAbsensiGuru Status, TimeOnly? JamMasuk, TimeOnly? JamPulang)
{
    public bool SudahTapMasuk => JamMasuk is not null;
}
