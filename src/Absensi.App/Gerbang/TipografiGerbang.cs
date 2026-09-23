using System.Windows;

namespace Absensi.App.Gerbang;

// Ukuran tipografi dan jarak layar Gerbang per kelas tinggi layar.
//
// PANDUAN-UI-UX.md §8.1 menetapkan ukuran pada 1920x1080 (nama 64, status
// 40, NISN/NIP 28, label 24). UAT 2026-09-23 (1366x768): ukuran itu tidak
// muat, dan menyusutkan SELURUH kartu secara proporsional (skala ±0,56)
// membuat NISN/NIP ±16 px dan status ±22 px terlalu kecil dari jarak
// gerbang. Solusinya bertingkat: layar tinggi memakai ukuran panduan
// apa adanya; layar pendek (area kerja < 900 px) memakai ukuran teks yang
// LEBIH KECIL TAPI TETAP BESAR (nama 44, NISN/NIP 24, status 32) dengan
// jarak/tepi lebih rapat, dan foto (3:4) mengambil sisa tinggi. Header dan
// ringkasan harian digabung satu baris di tingkat kompak untuk
// menghemat ±78 px tinggi.
//
// TinggiLabel/TinggiNisn/TinggiStatus: tinggi baris label Siswa/Guru,
// NISN/NIP, dan status DICADANGKAN walau teksnya kosong (kartu tak dikenal
// tanpa label/NISN) atau menyusut (status panjang) — supaya foto semua
// kartu sejajar dan berukuran sama.
//
// FaktorJarakBarisNama: jarak baris nama = ukuran font x faktor (0 = bawaan
// font). Tingkat kompak merapatkan jarak baris (1,05) supaya dua baris nama
// tetap muat di kotak yang lebih pendek TANPA mengecilkan hurufnya — ruang
// vertikal itu diserahkan ke foto (target >= 320 px, PANDUAN §8.1).
//
// Riwayat tap (panel bawah) SELALU setinggi TinggiRiwayat (tetap) —
// kosong atau berisi — supaya munculnya riwayat tidak mengubah ukuran
// kartu di atasnya.
public sealed record TipografiGerbang(
    bool Kompak,
    double FontLabel,
    double TinggiLabel,
    double FontNama,
    double FontNisn,
    double TinggiNisn,
    double FontStatus,
    double TinggiStatus,
    double TinggiNama,
    double FaktorJarakBarisNama,
    Thickness MarginFoto,
    Thickness MarginHalaman,
    Thickness PaddingKartu,
    Thickness MarginKartu,
    Thickness MarginHeader,
    Thickness MarginRiwayat,
    double TinggiRiwayat,
    Thickness PaddingRiwayat)
{
    // Area kerja (dikurangi taskbar) di bawah ini memakai tingkat kompak.
    // 1366x768 -> 728 (atau 768 tanpa taskbar) dan 1440x900 -> 860:
    // kompak; 1920x1080 -> 1040/1080: besar.
    public const double BatasTinggiKompak = 900;

    // Ukuran PANDUAN-UI-UX.md §8.1 (rancangan 1920x1080).
    public static TipografiGerbang PresetBesar { get; } = new(
        Kompak: false,
        FontLabel: 24,
        TinggiLabel: 32,
        FontNama: 64,
        FontNisn: 28,
        TinggiNisn: 38,
        FontStatus: 40,
        TinggiStatus: 54,
        TinggiNama: 172,
        FaktorJarakBarisNama: 0,
        MarginFoto: new Thickness(0, 0, 0, 10),
        MarginHalaman: new Thickness(24),
        PaddingKartu: new Thickness(24),
        MarginKartu: new Thickness(8),
        MarginHeader: new Thickness(0, 0, 0, 16),
        MarginRiwayat: new Thickness(0, 20, 0, 0),
        TinggiRiwayat: 56,
        PaddingRiwayat: new Thickness(16, 8, 16, 8));

    public static TipografiGerbang PresetKompak { get; } = new(
        Kompak: true,
        FontLabel: 20,
        TinggiLabel: 27,
        FontNama: 44,
        FontNisn: 24,
        TinggiNisn: 32,
        FontStatus: 32,
        TinggiStatus: 43,
        TinggiNama: 94,
        FaktorJarakBarisNama: 1.05,
        MarginFoto: new Thickness(0, 0, 0, 12),
        MarginHalaman: new Thickness(12),
        PaddingKartu: new Thickness(12),
        MarginKartu: new Thickness(5),
        MarginHeader: new Thickness(0, 0, 0, 8),
        MarginRiwayat: new Thickness(0, 8, 0, 0),
        TinggiRiwayat: 46,
        PaddingRiwayat: new Thickness(12, 4, 12, 4));

    public static TipografiGerbang Untuk(double tinggiArea) =>
        tinggiArea < BatasTinggiKompak ? PresetKompak : PresetBesar;
}
