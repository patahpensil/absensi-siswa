using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Repositori;

// Hasil pencocokan token pairing yang masih berlaku (status 'menunggu' —
// belum pernah dipakai). docs/PRD.md §16.6 langkah 6.2.
public sealed record PairingAktif(int Id, JenisPemilikKartu SubjekTipe, int SubjekId);
