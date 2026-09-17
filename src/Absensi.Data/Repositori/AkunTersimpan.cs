using Absensi.Domain.Akun;

namespace Absensi.Data.Repositori;

public sealed record AkunTersimpan(int Id, string Username, string SandiHash, PeranAkun Peran, int? GuruId, bool Aktif);
