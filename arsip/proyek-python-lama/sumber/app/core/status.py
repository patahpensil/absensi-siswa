"""
Status kehadiran dan jenis hari.

Semua nilai di sini adalah kosakata baku aplikasi. Jangan menambah status baru
tanpa menyesuaikan mesin absensi dan template notifikasi.
"""

from enum import Enum


class StatusSiswa(str, Enum):
    HADIR = "HADIR"
    TERLAMBAT = "TERLAMBAT"
    ALPA = "ALPA"
    IZIN = "IZIN"
    SAKIT = "SAKIT"
    DISPENSASI = "DISPENSASI"


class StatusGuru(str, Enum):
    HADIR = "HADIR"
    TERLAMBAT = "TERLAMBAT"
    DINAS_LUAR = "DINAS_LUAR"
    IZIN = "IZIN"
    SAKIT = "SAKIT"
    CUTI = "CUTI"
    # Catatan: guru tidak mengenal ALPA. Ini keputusan sekolah, bukan kelalaian.


#: Status yang diinput manusia, tidak pernah dihasilkan dari scan maupun cutoff.
STATUS_MANUAL_SISWA = frozenset(
    {StatusSiswa.IZIN, StatusSiswa.SAKIT, StatusSiswa.DISPENSASI}
)

STATUS_MANUAL_GURU = frozenset(
    {StatusGuru.DINAS_LUAR, StatusGuru.IZIN, StatusGuru.SAKIT, StatusGuru.CUTI}
)


class AlasanFinal(str, Enum):
    """Kenapa ``status_final`` berbeda dari ``status_masuk``."""

    TIDAK_SCAN_PULANG = "TIDAK_SCAN_PULANG"
    KOREKSI_MANUAL = "KOREKSI_MANUAL"
    KOREKSI_MASSAL = "KOREKSI_MASSAL"
    PERUBAHAN_KALENDER = "PERUBAHAN_KALENDER"


class HasilScan(str, Enum):
    """Apa yang terjadi pada satu peristiwa scan.

    Seluruh nilai ini disimpan permanen di tabel ``peristiwa_scan``, termasuk
    yang tidak membentuk baris absensi. Peristiwa mentah tidak pernah diubah.
    """

    ABSEN_MASUK = "ABSEN_MASUK"
    ABSEN_PULANG = "ABSEN_PULANG"
    DUPLIKAT = "DUPLIKAT"
    DI_LUAR_JENDELA = "DI_LUAR_JENDELA"
    KARTU_TIDAK_DIKENAL = "KARTU_TIDAK_DIKENAL"
    KARTU_NONAKTIF = "KARTU_NONAKTIF"
    HARI_LIBUR = "HARI_LIBUR"
    MANUAL_ENTRY = "MANUAL_ENTRY"


class JenisHari(str, Enum):
    """Jenis hari dalam kalender akademik.

    Kalender siswa dan kalender guru disimpan terpisah — satu tanggal bisa
    EFEKTIF bagi guru sekaligus LIBUR bagi siswa (contoh nyata: pengolahan
    nilai LHBPD, 14-19 Desember).
    """

    EFEKTIF = "EFEKTIF"
    LIBUR_NASIONAL = "LIBUR_NASIONAL"
    LIBUR_SEMESTER = "LIBUR_SEMESTER"
    KEGIATAN_TANPA_KBM = "KEGIATAN_TANPA_KBM"
    LIBUR_MENDADAK = "LIBUR_MENDADAK"


#: Jenis hari yang tidak menghasilkan absensi sama sekali.
HARI_TANPA_ABSENSI = frozenset(
    {
        JenisHari.LIBUR_NASIONAL,
        JenisHari.LIBUR_SEMESTER,
        JenisHari.KEGIATAN_TANPA_KBM,
        JenisHari.LIBUR_MENDADAK,
    }
)


class Peran(str, Enum):
    KEPALA_SEKOLAH = "KEPALA_SEKOLAH"
    ADMIN = "ADMIN"
    WALI_KELAS = "WALI_KELAS"
    OPERATOR = "OPERATOR"
    # Satu akun memegang tepat satu peran. Tidak ada akun bersama.


#: Peran yang boleh menjalankan koreksi massal dan mengelola kalender akademik.
PERAN_KOREKSI_MASSAL = frozenset({Peran.KEPALA_SEKOLAH, Peran.ADMIN})

#: Peran yang boleh mengoreksi absensi satuan.
PERAN_KOREKSI_SATUAN = frozenset(
    {Peran.KEPALA_SEKOLAH, Peran.ADMIN, Peran.WALI_KELAS}
)

#: Peran yang boleh mengaktifkan izin darurat alat RFID.
PERAN_IZIN_DARURAT_ALAT = frozenset(
    {Peran.KEPALA_SEKOLAH, Peran.ADMIN, Peran.WALI_KELAS}
)

#: Peran yang boleh memperpanjang atau mencabut izin darurat alat yang
#: sedang berjalan - LEBIH SEMPIT dari PERAN_IZIN_DARURAT_ALAT di atas.
#: PRD §12.4 sendiri hanya menyebut siapa yang boleh MEMBERI izin; batasan
#: "perpanjangan hanya kepsek/admin" adalah keputusan tambahan SKILL.md
#: (bagian "Kunci jalur alat") yang lebih spesifik - wali kelas boleh
#: memberi izin darurat awal, tidak boleh memperpanjangnya.
PERAN_PERPANJANG_IZIN_DARURAT_ALAT = frozenset({Peran.KEPALA_SEKOLAH, Peran.ADMIN})

#: Peran yang boleh menginput status manual guru (DINAS_LUAR/IZIN/SAKIT/
#: CUTI, PRD §6). TIDAK termasuk wali kelas - wali kelas mengampu kelas
#: siswa, bukan guru (beda dari PERAN_KOREKSI_SATUAN di atas).
PERAN_KOREKSI_GURU = frozenset({Peran.KEPALA_SEKOLAH, Peran.ADMIN})

#: Peran yang boleh mengelola template pesan, antrean, dan mode uji
#: notifikasi WhatsApp (PRD §18 menu NOTIFIKASI, tabel "Template dan
#: antrean WhatsApp": Kepsek Ya, Admin Ya, Wali Kelas -, Operator -).
PERAN_NOTIFIKASI = frozenset({Peran.KEPALA_SEKOLAH, Peran.ADMIN})

#: Peran yang boleh melihat/mengekspor laporan (PRD §10, baris "Dashboard
#: dan laporan": Kepsek Ya, Admin Ya, Wali Kelas Ya-kelasnya, Operator
#: TIDAK - sel operator di baris itu berisi "riwayat scan" saja, bukan
#: laporan). Wali kelas dibatasi lewat app.core.akses_kelas.pastikan_akses_kelas()
#: yang sama dipakai Riwayat Scan/Koreksi Satuan, bukan penjaga baru.
PERAN_LAPORAN = frozenset({Peran.KEPALA_SEKOLAH, Peran.ADMIN, Peran.WALI_KELAS})
