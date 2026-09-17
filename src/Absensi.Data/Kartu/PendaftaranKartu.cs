using Absensi.Domain.Kehadiran;

namespace Absensi.Data.Kartu;

// Satu pendaftaran yang belum tersimpan — dikumpulkan di memori oleh
// ViewModel selama sesi Registrasi Kartu berjalan (PRD §16.5 langkah 5:
// "Seluruh sesi tersimpan" saat Simpan ditekan, bukan per tap).
public sealed record PendaftaranKartu(string Uid, JenisPemilikKartu PemilikTipe, int PemilikId, string NamaTampil);
