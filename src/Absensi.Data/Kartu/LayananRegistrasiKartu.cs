using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Kartu;

// RENCANA-TUGAS.md Tugas 8 — Registrasi Kartu (PRD §16.5, §16.11, §17.9).
// Setiap perubahan lewat JalurPerubahanData (CLAUDE.md §4.1 aturan 4).
public sealed class LayananRegistrasiKartu
{
    private readonly RepositoriKartu _repositoriKartu = new();
    private readonly RepositoriSiswa _repositoriSiswa = new();
    private readonly RepositoriGuru _repositoriGuru = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    // PRD §17.9: keunikan UID diperiksa lintas kedua master, bukan hanya
    // master yang sedang dibuka. Null berarti UID bebas dipakai.
    public PemilikKartuAktif? CariPemilikAktif(SqliteConnection connection, string uid, int tahunAjaranId)
    {
        var kartu = _repositoriKartu.CariAktifBerdasarkanUid(connection, uid);
        if (kartu is null)
        {
            return null;
        }

        if (kartu.PemilikTipe == JenisPemilikKartu.Siswa)
        {
            var (nama, kelasNama) = _repositoriSiswa.CariNamaDanKelas(connection, kartu.PemilikId, tahunAjaranId);
            return new PemilikKartuAktif(JenisPemilikKartu.Siswa, kartu.PemilikId, nama, kelasNama);
        }

        return new PemilikKartuAktif(JenisPemilikKartu.Guru, kartu.PemilikId, _repositoriGuru.CariNama(connection, kartu.PemilikId), KelasNama: null);
    }

    // UAT Bug #11 (2026-09-23): "satu siswa satu kartu aktif" — dipakai
    // RegistrasiKartuViewModel.TapUid (pra-cek terhadap database, bukan
    // cuma properti KartuUid yang mungkin basi kalau kartu siswa itu baru
    // saja diubah lewat layar lain selagi dialog ini terbuka) dan Simpan
    // (verifikasi ulang setelah transaksi commit).
    public string? CariUidAktifSiswa(SqliteConnection connection, int siswaId) =>
        _repositoriKartu.CariUidAktifUntukPemilik(connection, JenisPemilikKartu.Siswa, siswaId);

    // UAT Bug #11 (2026-09-23, redesain fundamental Registrasi Kartu Siswa):
    // menggantikan alur antrean otomatis (DaftarkanSesi di bawah, TETAP
    // DIPERTAHANKAN — masih dipakai TapKartuTunggalViewModel untuk Guru dan
    // Ganti Kartu Siswa, TIDAK DISENTUH sesuai batas cakupan). Satu siswa,
    // satu kartu, satu klik Simpan. Re-verifikasi KEDUA aturan integritas
    // DI DALAM transaksi (bukan cuma sebelum transaksi dibuka di TapUid) —
    // menutup celah race condition kalau kartu yang sama atau siswa yang
    // sama sempat terdaftar oleh aksi lain persis di antara tap dan Simpan.
    // Melempar InvalidOperationException dengan pesan siap-tampil kalau
    // salah satu aturan integritas gagal saat re-verifikasi — pemanggil
    // (ViewModel) menampilkannya sebagai PesanGalat, TIDAK menutup dialog.
    public void DaftarkanSatu(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, string uid, int siswaId, string namaSiswa)
    {
        PenjagaKewenangan.PastikanKewenanganPenuh(akun, "mendaftarkan kartu");

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "daftar_kartu",
            entitas: "kartu",
            perubahan: transaction =>
            {
                var pemilikSaatIni = _repositoriKartu.CariAktifBerdasarkanUid(connection, uid, transaction);
                if (pemilikSaatIni is not null)
                {
                    throw new InvalidOperationException(
                        "Kartu ini baru saja terdaftar untuk pemilik lain. Tap ulang dengan kartu yang berbeda.");
                }

                var kartuSiswaSaatIni = _repositoriKartu.CariUidAktifUntukPemilik(connection, JenisPemilikKartu.Siswa, siswaId, transaction);
                if (kartuSiswaSaatIni is not null)
                {
                    throw new InvalidOperationException(
                        $"{namaSiswa} baru saja mendapat kartu lain ({kartuSiswaSaatIni}). Muat ulang daftar siswa dan coba lagi.");
                }

                _repositoriKartu.Daftarkan(connection, transaction, uid, JenisPemilikKartu.Siswa, siswaId, waktu);
                return siswaId;
            },
            nilaiBaru: $"{uid}->{namaSiswa}");
    }

    // PRD §16.5 langkah 5: "Seluruh sesi tersimpan" — semua-atau-tidak
    // sama sekali dalam satu transaksi (CLAUDE.md §4.2 aturan 10, sama
    // seperti impor Excel; dikonfirmasi pemilik proyek 2026-09-18: kalau
    // aplikasi gagal di tengah, operator tidak boleh diminta mengulang
    // kartu yang sudah sempat ditap). Pemanggil (ViewModel) wajib sudah
    // memvalidasi tiap UID lewat CariPemilikAktif sebelum sampai sini.
    //
    // UAT 2026-09-23: method ini TIDAK LAGI dipakai RegistrasiKartuViewModel
    // (siswa per-kelas) — lihat DaftarkanSatu di atas untuk alur baru.
    // TETAP DIPERTAHANKAN karena TapKartuTunggalViewModel (Guru + Ganti
    // Kartu Siswa) masih memakainya — di luar cakupan redesain ini.
    public void DaftarkanSesi(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, IReadOnlyList<PendaftaranKartu> pendaftaran)
    {
        PenjagaKewenangan.PastikanKewenanganPenuh(akun, "mendaftarkan kartu");

        if (pendaftaran.Count == 0)
        {
            return;
        }


        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "daftar_kartu",
            entitas: "kartu",
            perubahan: transaction =>
            {
                foreach (var p in pendaftaran)
                {
                    _repositoriKartu.Daftarkan(connection, transaction, p.Uid, p.PemilikTipe, p.PemilikId, waktu);
                }

                return null;
            },
            nilaiBaru: string.Join(", ", pendaftaran.Select(p => $"{p.Uid}->{p.NamaTampil}")));
    }

    // PRD §16.11: kartu lama dinonaktifkan (bukan dihapus — CLAUDE.md §4.2
    // aturan 8), kartu baru dikaitkan ke pemilik yang sama, satu transaksi.
    // Riwayat absensi tetap utuh karena menunjuk ke pemilik, bukan ke kartu
    // (aturan 7).
    public void GantiKartuHilang(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        JenisPemilikKartu pemilikTipe,
        int pemilikId,
        string uidBaru)
    {
        PenjagaKewenangan.PastikanKewenanganPenuh(akun, "mengganti kartu");

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "ganti_kartu",
            entitas: "kartu",
            perubahan: transaction =>
            {
                // PRD §12: penggantian kartu dicatat UID lama -> UID baru.
                // UID lama dibaca di dalam transaksi SEBELUM dinonaktifkan;
                // null (tidak ada kartu aktif sebelumnya) memang berarti tidak
                // ada nilai lama — bukan tebakan. Nama pemilik ikut dicatat
                // (format "uid->nama", sama dengan daftar_kartu) karena
                // entitas_id hanya memuat id pemilik tanpa jenisnya.
                var uidLama = _repositoriKartu.CariUidAktifUntukPemilik(connection, pemilikTipe, pemilikId, transaction);
                var nama = pemilikTipe == JenisPemilikKartu.Siswa
                    ? _repositoriSiswa.CariNamaUntukAudit(connection, pemilikId, transaction)
                    : _repositoriGuru.CariNamaUntukAudit(connection, pemilikId, transaction);

                _repositoriKartu.Nonaktifkan(connection, transaction, pemilikTipe, pemilikId, waktu, alasan: "hilang");
                _repositoriKartu.Daftarkan(connection, transaction, uidBaru, pemilikTipe, pemilikId, waktu);

                string Teks(string uid) => string.IsNullOrWhiteSpace(nama) ? uid : $"{uid}->{nama}";
                return new CatatanPerubahan(pemilikId, uidLama is null ? null : Teks(uidLama), Teks(uidBaru));
            });
    }
}
