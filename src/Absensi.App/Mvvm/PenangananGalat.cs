using System.IO;
using Absensi.Data.Diagnostik;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Mvvm;

// Satu tempat untuk mengubah galat yang BISA DIPULIHKAN pengguna menjadi
// kalimat Indonesia yang bisa ditindaklanjuti — supaya aksi seperti Simpan,
// Ekspor, dan Unggah tidak lagi membiarkan pelanggaran aturan/unik/berkas
// lolos ke penanganan galat global (yang menutup aplikasi) dan tidak pernah
// menampilkan pesan mentah dari SQLite/.NET (bisa memuat perintah SQL, path,
// atau data — audit UI 2026-09-24).
//
//  - InvalidOperationException: aturan bisnis yang dilempar layanan
//    aplikasi ini sendiri (bulan dikunci, wali kelas tidak berwenang, ...),
//    pesannya sudah ditulis untuk pengguna.
//  - SqliteException: pesan mentah TIDAK pernah ditampilkan; dipetakan ke
//    kalimat umum dan dicatat ke log.
//  - IOException/UnauthorizedAccessException: masalah berkas.
//  - Lainnya: null = bukan galat yang boleh ditelan, biarkan sampai ke
//    penanganan galat global (dicatat dan dilaporkan).
public static class PenangananGalat
{
    public static string? PesanUntuk(Exception ex) => ex switch
    {
        InvalidOperationException => ex.Message,
        SqliteException { SqliteErrorCode: 19 } =>
            "Data ini bertabrakan dengan data yang sudah ada (mis. nama atau nomor yang sama). Periksa isian, lalu coba lagi.",
        SqliteException =>
            "Perubahan tidak bisa disimpan ke database saat ini. Coba lagi; bila terus terjadi, laporkan ke pengelola aplikasi.",
        UnauthorizedAccessException =>
            "Berkas tidak bisa diakses karena izin ditolak. Pilih lokasi lain atau periksa izin folder, lalu coba lagi.",
        IOException =>
            "Berkas sedang dipakai aplikasi lain atau tidak bisa dibaca/ditulis. Tutup berkasnya, lalu coba lagi.",
        _ => null,
    };

    // Menjalankan aksi; bila galat yang bisa dipulihkan, memanggil `galat` dengan
    // pesan aman dan mengembalikan false. Galat lain dilempar ulang.
    public static bool Coba(Action aksi, Action<string> galat, string sumber)
    {
        try
        {
            aksi();
            return true;
        }
        catch (Exception ex) when (PesanUntuk(ex) is not null)
        {
            if (ex is not InvalidOperationException)
            {
                PencatatGalat.Catat(ex, sumber, "ERROR");
            }

            galat(PesanUntuk(ex)!);
            return false;
        }
    }
}
