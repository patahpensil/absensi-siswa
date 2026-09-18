namespace Absensi.Domain.Laporan;

// PRD §11.4/§11.5: rekap yang dipakai laporan bulanan, per-orang, dan
// semester. Terlambat/Bolos selalu 0 untuk guru (statusnya tidak ada di
// absensi_guru) dan DinasLuar/Cuti/TidakLengkap selalu 0 untuk siswa
// (sebaliknya) — dibiarkan tetap satu record, bukan dipecah jadi dua,
// supaya satu penulis Excel/PDF bisa melayani siswa dan guru tanpa
// percabangan tipe.
//
// TidakLengkap (guru tap masuk tanpa tap pulang, PRD §17.3 — butuh
// keputusan Kepala Sekolah) sengaja PUNYA kolom rekap sendiri, tidak
// dilipat ke Hadir (dikonfirmasi pemilik proyek 2026-09-18) — beda dari
// Masuk (transien, hari masih berjalan) yang tetap masuk hitungan Hadir.
//
// HariEfektif dihitung dari kalender pendidikan (jadwal_hari dikurangi
// kalender.libur, ditambah kalender.sekolah) — BUKAN dari jumlah baris
// absensi_* yang tersimpan, supaya tetap benar walau ada bolong
// pembukaan hari (RENCANA-TUGAS.md Tugas 12, "Selesai bila").
//
// Persentase = (Hadir + Terlambat + Bolos + TidakLengkap) / HariEfektif —
// PRD §11.5: "Terlambat dihitung sebagai hadir ... Bolos juga dihitung
// sebagai hadir"; TidakLengkap diberi perlakuan sama (dikonfirmasi
// pemilik proyek 2026-09-18, simetris dengan Bolos siswa) meskipun kini
// juga punya kolom rekap sendiri — dua hal berbeda: kolom rekap vs
// pembilang persentase.
public sealed record RekapKehadiran(
    int Hadir,
    int Sakit,
    int Izin,
    int Alpa,
    int Terlambat,
    int Bolos,
    int DinasLuar,
    int Cuti,
    int TidakLengkap,
    int HariEfektif,
    double Persentase);
