namespace Absensi.Domain.Kehadiran;

// Bagian dari keadaan absensi hari itu dan konfigurasi jadwal hari itu
// (docs/PRD.md §16.8.a) yang khusus berlaku untuk kartu siswa.
public sealed record KonteksTapSiswa(
    bool SiswaAktif,
    DateTime? TapTerakhirKartuIni,
    TimeSpan JedaTapMinimal,
    bool SudahTapMasukHariIni,
    TimeOnly JamMasuk,
    TimeOnly BatasAkhirTapMasuk,
    TimeOnly MulaiAbsenPulang);
