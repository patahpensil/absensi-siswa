"""
Seluruh nilai waktu, toleransi, ambang, dan saklar modul.

PRINSIP: tidak ada satu pun angka di sini yang boleh ditulis ulang di modul
lain. Modul lain menerima objek Pengaturan, tidak pernah mengimpor angkanya.

Nilai di berkas ini adalah nilai AWAL saat aplikasi pertama dipasang. Setelah
itu semuanya tersimpan di basis data dan diubah lewat menu admin.
"""

from dataclasses import dataclass, field
from datetime import time

# Hari dalam seminggu mengikuti datetime.weekday(): Senin=0 ... Minggu=6
SENIN, SELASA, RABU, KAMIS, JUMAT, SABTU, MINGGU = range(7)

HARI_SEKOLAH = (SENIN, SELASA, RABU, KAMIS, JUMAT, SABTU)


@dataclass(frozen=True)
class JadwalHarian:
    """Jadwal untuk satu hari dalam seminggu.

    ``batas_terlambat`` bersifat inklusif ke bawah: scan <= nilai ini HADIR,
    lebih dari itu TERLAMBAT.
    """

    jam_masuk: time
    batas_terlambat: time
    cutoff_alpa: time
    pulang_mulai: time
    pulang_selesai: time  # sekaligus cutoff ALPA pulang

    def dalam_jendela_masuk(self, jam: time) -> bool:
        return jam < self.cutoff_alpa

    def dalam_jendela_pulang(self, jam: time) -> bool:
        return self.pulang_mulai <= jam <= self.pulang_selesai


def _jadwal_biasa() -> JadwalHarian:
    """Senin-Kamis dan Sabtu."""
    return JadwalHarian(
        jam_masuk=time(7, 15),
        batas_terlambat=time(7, 20, 59),
        cutoff_alpa=time(7, 30),
        pulang_mulai=time(13, 20),
        pulang_selesai=time(15, 0),
    )


def _jadwal_jumat() -> JadwalHarian:
    return JadwalHarian(
        jam_masuk=time(7, 15),
        batas_terlambat=time(7, 20, 59),
        cutoff_alpa=time(7, 30),
        pulang_mulai=time(11, 0),
        pulang_selesai=time(14, 0),
    )


@dataclass
class Pengaturan:
    # --- Jadwal siswa, per hari dalam seminggu ---
    jadwal_siswa: dict = field(
        default_factory=lambda: {
            SENIN: _jadwal_biasa(),
            SELASA: _jadwal_biasa(),
            RABU: _jadwal_biasa(),
            KAMIS: _jadwal_biasa(),
            JUMAT: _jadwal_jumat(),
            SABTU: _jadwal_biasa(),
        }
    )

    # --- Guru ---
    # Toleransi keterlambatan guru 09:30 (dikonfirmasi sekolah, bukan salah ketik).
    # Guru tidak mengenal ALPA, karena itu tidak ada cutoff_alpa untuk guru.
    guru_jam_masuk: time = time(7, 15)
    guru_batas_terlambat: time = time(9, 30)

    # --- Perilaku scan ---
    #: Scan UID yang sama dalam rentang ini hanya dihitung sekali.
    #: Berlaku GLOBAL per UID, bukan per jalur, supaya tap di jalur 1 lalu
    #: jalur 3 tidak tercatat dua kali.
    jeda_antiduplikat_detik: int = 10

    # --- Cutoff pulang ---
    #: Tidak scan pulang sampai cutoff dihitung ALPA (keputusan sekolah).
    tidak_scan_pulang_jadi_alpa: bool = True
    #: Bila proporsi yang tidak scan pulang melebihi ambang ini, konversi
    #: ditahan dan admin diminta konfirmasi — melindungi dari alat yang mati.
    ambang_tahan_konversi_pulang: float = 0.30

    # --- Notifikasi ---
    jam_kirim_alpa_pagi: time = time(9, 15)
    #: Jeda setelah cutoff pulang sebelum notifikasi dikirim, memberi waktu
    #: admin mengoreksi lebih dulu. Status diperiksa ulang sebelum kirim.
    jeda_kirim_alpa_pulang_menit: int = 60

    # --- Saklar modul ---
    modul_absensi_guru: bool = True
    modul_absen_pulang: bool = True
    modul_notifikasi_whatsapp: bool = False  # dibangun penuh, koneksi dimatikan
    modul_dashboard: bool = True
    modul_sinkronisasi_online: bool = False

    # --- Alat RFID ---
    #: "PCSC" atau "SERIAL". Dikunci saat pemasangan; alat jalur lain ditolak.
    jalur_alat_aktif: str = "PCSC"
    jumlah_jalur_lobi: int = 4

    # --- Backup ---
    backup_jam: time = time(22, 0)
    backup_tujuan: tuple = ("HARDISK_EKSTERNAL", "FOLDER_PC_LAIN")
    backup_simpan_harian: int = 30
    backup_simpan_bulanan: int = 12
    #: Snapshot otomatis sebelum tindakan massal yang menyentuh baris sebanyak ini.
    ambang_snapshot_sebelum_massal: int = 5000
    #: Tindakan massal di atas ambang ini menuntut konfirmasi ketik ulang.
    ambang_konfirmasi_ketik: int = 5000

    def jadwal_untuk(self, weekday: int) -> JadwalHarian | None:
        """Jadwal siswa untuk satu hari; None bila bukan hari sekolah."""
        return self.jadwal_siswa.get(weekday)
