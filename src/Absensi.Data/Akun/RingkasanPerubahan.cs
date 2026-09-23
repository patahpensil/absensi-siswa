namespace Absensi.Data.Akun;

// Menyusun isi kolom audit.nilai_lama / nilai_baru untuk perubahan beberapa
// kolom sekaligus (PRD §12: "nilai lama -> nilai baru"). Hanya kolom yang
// BENAR-BENAR berubah ditulis, dalam bentuk "kolom=nilai" dipisah "; ",
// mis. lama "alamat=Jl. A; kelas=7A" -> baru "alamat=Jl. B; kelas=7B".
// Nilai kosong/spasi dianggap sama dengan null dan ditulis "(kosong)".
// Baris audit LAMA (sebelum format ini) hanya berisi satu nilai polos
// (mis. nama) tanpa tanda "=" — tidak pernah diubah oleh kode ini.
public static class RingkasanPerubahan
{
    public const string Kosong = "(kosong)";
    public const string TidakAdaPerubahan = "(tidak ada perubahan nilai)";

    public readonly record struct Kolom(string Nama, string? Lama, string? Baru);

    public static CatatanPerubahan Bandingkan(int? entitasId, params Kolom[] kolom)
    {
        var berubah = kolom.Where(k => Normalisasi(k.Lama) != Normalisasi(k.Baru)).ToList();
        if (berubah.Count == 0)
        {
            return new CatatanPerubahan(entitasId, null, TidakAdaPerubahan);
        }

        return new CatatanPerubahan(
            entitasId,
            string.Join("; ", berubah.Select(k => $"{k.Nama}={Tampil(k.Lama)}")),
            string.Join("; ", berubah.Select(k => $"{k.Nama}={Tampil(k.Baru)}")));
    }

    // Dipakai bila baris yang diubah tidak ditemukan: nilai lama TIDAK
    // diketahui (null), hanya nilai baru yang ditulis — bukan menebak lama.
    public static CatatanPerubahan HanyaBaru(int? entitasId, params Kolom[] kolom) =>
        new(entitasId, null, string.Join("; ", kolom.Select(k => $"{k.Nama}={Tampil(k.Baru)}")));

    private static string? Normalisasi(string? nilai) =>
        string.IsNullOrWhiteSpace(nilai) ? null : nilai.Trim();

    private static string Tampil(string? nilai) => Normalisasi(nilai) ?? Kosong;
}
