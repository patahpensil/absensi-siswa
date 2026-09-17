using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Repositori;

public sealed record AbsensiSiswaHariIni(StatusAbsensiSiswa Status, TimeOnly? JamMasuk, TimeOnly? JamPulang)
{
    public bool SudahTapMasuk => JamMasuk is not null;
}
