"""
Mesin absensi: seluruh aturan penentuan status.

Modul ini TIDAK BOLEH mengimpor apa pun dari ``app.ui`` maupun ``app.data``.
Isinya fungsi murni supaya bisa diuji tanpa membuka jendela dan tanpa basis
data. Semua perbandingan waktu memakai waktu lokal WITA, tidak pernah UTC.

Aturan yang dikodekan di sini:

    scan <= batas_terlambat        -> HADIR
    scan >  batas_terlambat        -> TERLAMBAT
    tidak ada scan sampai cutoff   -> ALPA
    sudah ALPA lalu datang         -> scan tetap dicatat, status TIDAK berubah
    tidak scan pulang sampai cutoff-> status_final ALPA (alasan TIDAK_SCAN_PULANG)

Status IZIN, SAKIT, dan DISPENSASI tidak pernah dihasilkan mesin ini.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date, datetime, timedelta

from app.config.pengaturan import JadwalHarian, Pengaturan
from app.core.status import (
    HARI_TANPA_ABSENSI,
    STATUS_MANUAL_SISWA,
    AlasanFinal,
    HasilScan,
    JenisHari,
    StatusGuru,
    StatusSiswa,
)


# ---------------------------------------------------------------- struktur data


@dataclass(frozen=True)
class ScanMasuk:
    """Satu peristiwa scan yang sudah dikenali pemiliknya."""

    orang_id: int
    waktu: datetime
    station_id: str


@dataclass
class BarisAbsensi:
    """Hasil olahan untuk satu orang pada satu tanggal.

    ``status_masuk`` adalah hasil scan pagi. ``status_final`` adalah status
    resmi untuk laporan. Keduanya disimpan terpisah supaya wali kelas bisa
    membedakan "tidak pernah datang" dari "datang tapi tidak absen pulang" —
    informasi yang hilang permanen kalau ditumpuk jadi satu kolom.
    """

    orang_id: int
    tanggal: date
    status_masuk: StatusSiswa | StatusGuru | None = None
    status_final: StatusSiswa | StatusGuru | None = None
    jam_masuk: datetime | None = None
    jam_pulang: datetime | None = None
    alasan_final: AlasanFinal | None = None
    izin_pulang_cepat: bool = False


# ---------------------------------------------------------------- klasifikasi scan


def scan_duplikat(
    waktu_scan: datetime,
    waktu_scan_sebelumnya: datetime | None,
    pengaturan: Pengaturan,
) -> bool:
    """Scan UID yang sama dalam jeda anti-duplikat hanya dihitung sekali.

    Perbandingan sengaja memakai waktu scan terakhir untuk UID tersebut di
    SELURUH jalur, bukan per jalur. Tanpa itu, siswa bisa tap di jalur 1 lalu
    jalur 3 dan tercatat dua kali.
    """
    if waktu_scan_sebelumnya is None:
        return False
    selisih = abs((waktu_scan - waktu_scan_sebelumnya).total_seconds())
    return selisih < pengaturan.jeda_antiduplikat_detik


def klasifikasi_scan(
    scan: ScanMasuk,
    jadwal: JadwalHarian | None,
    jenis_hari: JenisHari,
    sudah_punya_absen_masuk: bool,
    waktu_scan_sebelumnya: datetime | None,
    pengaturan: Pengaturan,
) -> HasilScan:
    """Tentukan arti satu scan.

    Scan yang tidak membentuk absensi TETAP disimpan sebagai peristiwa mentah.
    Fungsi ini hanya menentukan labelnya.
    """
    if scan_duplikat(scan.waktu, waktu_scan_sebelumnya, pengaturan):
        return HasilScan.DUPLIKAT

    if jenis_hari in HARI_TANPA_ABSENSI or jadwal is None:
        return HasilScan.HARI_LIBUR

    jam = scan.waktu.time()

    if jadwal.dalam_jendela_masuk(jam) and not sudah_punya_absen_masuk:
        return HasilScan.ABSEN_MASUK

    if pengaturan.modul_absen_pulang and jadwal.dalam_jendela_pulang(jam):
        # Absen pulang hanya sah bila sudah ada catatan absen masuk hari itu.
        if sudah_punya_absen_masuk:
            return HasilScan.ABSEN_PULANG
        return HasilScan.DI_LUAR_JENDELA

    return HasilScan.DI_LUAR_JENDELA


# ---------------------------------------------------------------- status masuk


def tentukan_status_masuk(waktu_scan: datetime, jadwal: JadwalHarian) -> StatusSiswa:
    """HADIR bila scan sampai batas terlambat, TERLAMBAT bila lewat."""
    if waktu_scan.time() <= jadwal.batas_terlambat:
        return StatusSiswa.HADIR
    return StatusSiswa.TERLAMBAT


def tentukan_status_masuk_guru(
    waktu_scan: datetime, pengaturan: Pengaturan
) -> StatusGuru:
    """Guru memakai toleransi tersendiri (09:30) dan tidak mengenal ALPA."""
    if waktu_scan.time() <= pengaturan.guru_batas_terlambat:
        return StatusGuru.HADIR
    return StatusGuru.TERLAMBAT


# ---------------------------------------------------------------- cutoff


def terapkan_cutoff_alpa(
    baris: BarisAbsensi, jenis_hari: JenisHari
) -> BarisAbsensi:
    """Tetapkan ALPA bagi siswa yang belum scan sampai cutoff pagi.

    Tidak menyentuh status manual (IZIN, SAKIT, DISPENSASI) dan tidak berlaku
    pada hari tanpa absensi.
    """
    if jenis_hari in HARI_TANPA_ABSENSI:
        return baris
    if baris.status_masuk in STATUS_MANUAL_SISWA:
        return baris
    if baris.status_masuk is None:
        baris.status_masuk = StatusSiswa.ALPA
        baris.status_final = StatusSiswa.ALPA
    return baris


def boleh_dikonversi_alpa_pulang(baris: BarisAbsensi) -> bool:
    """Apakah baris ini termasuk sasaran konversi ALPA karena tidak scan pulang.

    Tiga pengecualian: status manual, izin pulang cepat, dan baris yang memang
    sudah ALPA sejak pagi.
    """
    if baris.status_masuk in STATUS_MANUAL_SISWA:
        return False
    if baris.izin_pulang_cepat:
        return False
    if baris.status_masuk == StatusSiswa.ALPA:
        return False
    return baris.jam_pulang is None


def konversi_alpa_pulang(baris: BarisAbsensi) -> BarisAbsensi:
    """Ubah status_final menjadi ALPA, status_masuk dibiarkan apa adanya."""
    baris.status_final = StatusSiswa.ALPA
    baris.alasan_final = AlasanFinal.TIDAK_SCAN_PULANG
    return baris


@dataclass(frozen=True)
class HasilCutoffPulang:
    """Hasil pemeriksaan sebelum konversi massal dijalankan."""

    jumlah_sasaran: int
    jumlah_hadir: int
    proporsi: float
    ditahan: bool


def periksa_cutoff_pulang(
    daftar: list[BarisAbsensi], pengaturan: Pengaturan
) -> HasilCutoffPulang:
    """Periksa apakah konversi ALPA pulang aman dijalankan.

    Kalau alat mati sejak siang, hampir semua siswa akan tampak "tidak scan
    pulang". Menjalankan konversi begitu saja berarti menghukum seluruh
    sekolah atas kegagalan perangkat, karena itu konversi ditahan di atas
    ambang dan admin diminta memutuskan.
    """
    hadir = [b for b in daftar if b.status_masuk not in (None, StatusSiswa.ALPA)]
    sasaran = [b for b in hadir if boleh_dikonversi_alpa_pulang(b)]
    proporsi = len(sasaran) / len(hadir) if hadir else 0.0
    return HasilCutoffPulang(
        jumlah_sasaran=len(sasaran),
        jumlah_hadir=len(hadir),
        proporsi=proporsi,
        ditahan=proporsi > pengaturan.ambang_tahan_konversi_pulang,
    )


def jalankan_cutoff_pulang(
    daftar: list[BarisAbsensi],
    pengaturan: Pengaturan,
    paksa: bool = False,
) -> tuple[list[BarisAbsensi], HasilCutoffPulang]:
    """Jalankan konversi ALPA pulang.

    ``paksa=True`` dipakai ketika admin sudah mengonfirmasi lewat peringatan
    ambang. Tanpa itu, konversi tidak dijalankan bila hasil pemeriksaan
    menyatakan ditahan.
    """
    hasil = periksa_cutoff_pulang(daftar, pengaturan)
    if not pengaturan.tidak_scan_pulang_jadi_alpa:
        return daftar, hasil
    if hasil.ditahan and not paksa:
        return daftar, hasil
    for baris in daftar:
        if boleh_dikonversi_alpa_pulang(baris):
            konversi_alpa_pulang(baris)
    return daftar, hasil


# ---------------------------------------------------------------- notifikasi


def waktu_kirim_alpa_pulang(
    tanggal: date, jadwal: JadwalHarian, pengaturan: Pengaturan
) -> datetime:
    """Notifikasi ALPA pulang dikirim setelah jeda, bukan tepat saat cutoff.

    Jeda ini memberi admin waktu mengoreksi lebih dulu; status diperiksa ulang
    sebelum pesan benar-benar dikirim, sehingga koreksi dalam jeda otomatis
    membatalkan pesannya.
    """
    cutoff = datetime.combine(tanggal, jadwal.pulang_selesai)
    return cutoff + timedelta(minutes=pengaturan.jeda_kirim_alpa_pulang_menit)


def masih_perlu_dikirim(baris: BarisAbsensi) -> bool:
    """Pemeriksaan terakhir tepat sebelum pesan dikirim."""
    return baris.status_final == StatusSiswa.ALPA
