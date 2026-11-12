using System.Text.RegularExpressions;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Pengaturan;

// PANDUAN-UI-UX §9 (Pengaturan: identitas sekolah). Name, NPSN and address
// appear on reports (PANDUAN §11), so changes go through the audit trail.
public sealed class SchoolIdentityService
{
    private readonly RepositoriSekolah _schools = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    public void Update(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, SchoolIdentity identity)
    {
        AuthorizationGuard.EnsureFullAuthority(akun, "mengubah identitas sekolah");

        var clean = new SchoolIdentity(
            identity.Name.Trim(),
            string.IsNullOrWhiteSpace(identity.Npsn) ? null : identity.Npsn.Trim(),
            string.IsNullOrWhiteSpace(identity.Address) ? null : identity.Address.Trim());

        if (clean.Name.Length is 0 or > 120)
        {
            throw new InvalidOperationException("Nama sekolah wajib diisi, paling banyak 120 karakter.");
        }

        if (clean.Npsn is not null && !Regex.IsMatch(clean.Npsn, "^[0-9]{8}$"))
        {
            throw new InvalidOperationException("NPSN terdiri dari 8 angka.");
        }

        if (clean.Address is { Length: > 250 })
        {
            throw new InvalidOperationException("Alamat paling banyak 250 karakter.");
        }

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "ubah_identitas_sekolah",
            entitas: "sekolah",
            perubahan: transaction =>
            {
                var before = _schools.FindIdentity(connection, transaction);
                _schools.UpdateIdentity(connection, transaction, clean);
                return RingkasanPerubahan.Bandingkan(1,
                    new RingkasanPerubahan.Kolom("nama", before?.Name, clean.Name),
                    new RingkasanPerubahan.Kolom("npsn", before?.Npsn, clean.Npsn),
                    new RingkasanPerubahan.Kolom("alamat", before?.Address, clean.Address));
            });
    }

    // PANDUAN-UI-UX §11: logo tampil di kepala laporan. Yang disimpan hanya
    // nama berkasnya (seperti foto siswa/guru); berkas gambarnya dikelola
    // pemanggil. Nama berkas dibatasi agar tidak bisa menunjuk folder lain.
    public void SetLogo(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, string fileName)
    {
        AuthorizationGuard.EnsureFullAuthority(akun, "mengubah logo sekolah");

        if (!Regex.IsMatch(fileName, "^[A-Za-z0-9._-]{1,60}\\.png$") || fileName.Contains(".."))
        {
            throw new InvalidOperationException("Nama berkas logo tidak sah.");
        }

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "ubah_logo_sekolah",
            entitas: "sekolah",
            perubahan: transaction =>
            {
                var before = _schools.CariSatuSatunya(connection)?.LogoPath;
                _schools.UpdateLogo(connection, transaction, fileName);
                return RingkasanPerubahan.Bandingkan(1, new RingkasanPerubahan.Kolom("logo", before, fileName));
            });
    }
}
