using Absensi.Data.Repositori;

namespace Absensi.Data.Wizard;

// Kumpulan masukan keenam langkah wizard instalasi pertama (PRD §16.1):
// lisensi, identitas sekolah, tahun ajaran, jadwal harian (7 baris), dan
// akun Kepala Sekolah. Dikumpulkan dulu oleh WizardInstalasiViewModel di
// Absensi.App, baru ditulis sekaligus lewat LayananWizard.Selesaikan.
public sealed record DataWizardInstalasi(
    string KunciLisensi,
    string NamaSekolah,
    string? Npsn,
    string? Alamat,
    string? LogoPath,
    string NamaTahunAjaran,
    string Semester,
    DateOnly TglMulaiTahunAjaran,
    DateOnly TglSelesaiTahunAjaran,
    IReadOnlyList<JadwalHariBaris> JadwalHarian,
    string UsernameKepalaSekolah,
    string SandiKepalaSekolah);
