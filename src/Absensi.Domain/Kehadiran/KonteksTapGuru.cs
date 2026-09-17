namespace Absensi.Domain.Kehadiran;

// Bagian dari keadaan absensi hari itu dan konfigurasi jadwal hari itu
// (docs/PRD.md §16.8.b) yang khusus berlaku untuk kartu guru.
public sealed record KonteksTapGuru(
    bool GuruAktif,
    DateTime? TapTerakhirKartuIni,
    TimeSpan JedaTapMinimal,
    StatusAbsensiGuru? StatusDitetapkanDiMuka,
    bool SudahTapMasukHariIni,
    TimeOnly MulaiTapPulang);
