from datetime import date, time

from app.config.pengaturan import Pengaturan
from app.data.models.enums import JenisPemilik
from app.data.repositories.schedule import ScheduleRepository

HARI_SEKOLAH = range(1, 7)  # Senin(1) sampai Sabtu(6) - Minggu (7) libur
HARI_JUMAT = 5

JAM_MASUK_SISWA = time(7, 15)
# Inklusif ke bawah - lihat app.core.mesin_absensi.tentukan_status_masuk dan
# app.config.pengaturan.JadwalHarian: scan <= nilai ini HADIR, lebih dari
# itu TERLAMBAT. 07:21:00 ke atas dihitung TERLAMBAT (SKILL.md), jadi
# batasnya 07:20:59, bukan 07:21:00.
BATAS_TERLAMBAT_SISWA = time(7, 20, 59)
CUTOFF_ALPA = time(7, 30)

PULANG_MULAI_JUMAT = time(11, 0)
JAM_PULANG_SISWA_JUMAT = time(14, 0)
PULANG_MULAI_LAIN = time(13, 20)
JAM_PULANG_SISWA_LAIN = time(15, 0)

WAKTU_KIRIM_NOTIF_ALPA = time(9, 15)
BATAS_TERLAMBAT_GURU = time(9, 30)

AMBANG_TAHAN_KONVERSI_PULANG = 0.30
JEDA_KIRIM_ALPA_PULANG_MENIT = 60


def seed_jadwal_siswa(schedule_repo: ScheduleRepository, *, berlaku_mulai: date) -> None:
    """Isi school_schedules untuk Senin-Sabtu sesuai keputusan final di
    SKILL.md - jendela pulang 13:20-15:00, Jumat 11:00-14:00.

    Aman dipanggil untuk tanggal mulai berlaku yang baru (mis. jadwal
    berubah), tapi jangan dipanggil dua kali dengan berlaku_mulai yang
    sama - itu akan membuat baris duplikat, bukan menimpa yang lama.
    """
    for hari in HARI_SEKOLAH:
        jumat = hari == HARI_JUMAT
        schedule_repo.tambah(
            jenis_pemilik=JenisPemilik.SISWA,
            hari=hari,
            jam_masuk=JAM_MASUK_SISWA,
            batas_terlambat=BATAS_TERLAMBAT_SISWA,
            cutoff_alpa=CUTOFF_ALPA,
            pulang_mulai=PULANG_MULAI_JUMAT if jumat else PULANG_MULAI_LAIN,
            jam_pulang=JAM_PULANG_SISWA_JUMAT if jumat else JAM_PULANG_SISWA_LAIN,
            berlaku_mulai=berlaku_mulai,
        )


def seed_pengaturan(pengaturan: Pengaturan, *, id_pengguna: int, alasan: str) -> None:
    """Isi parameter waktu global yang sudah final di SKILL.md.

    Aman dipanggil ulang - menimpa nilai lama lewat SettingRepository.atur(),
    yang selalu mencatat audit log untuk setiap perubahan.
    """
    pengaturan.atur_cutoff_alpa(CUTOFF_ALPA, id_pengguna=id_pengguna, alasan=alasan)
    pengaturan.atur_waktu_kirim_notif_alpa(WAKTU_KIRIM_NOTIF_ALPA, id_pengguna=id_pengguna, alasan=alasan)
    pengaturan.atur_batas_terlambat_guru(BATAS_TERLAMBAT_GURU, id_pengguna=id_pengguna, alasan=alasan)
    pengaturan.atur_ambang_tahan_konversi_pulang(
        AMBANG_TAHAN_KONVERSI_PULANG, id_pengguna=id_pengguna, alasan=alasan
    )
    pengaturan.atur_jeda_kirim_alpa_pulang_menit(
        JEDA_KIRIM_ALPA_PULANG_MENIT, id_pengguna=id_pengguna, alasan=alasan
    )
    # Nilai tampilan/default siswa - bukan sumber otoritatif mesin absensi
    # (itu school_schedules), tapi diisi juga supaya konsisten dan tersedia
    # untuk kebutuhan lain, mis. mengisi awal form jadwal baru di antarmuka.
    pengaturan.atur_jam_masuk(JAM_MASUK_SISWA, id_pengguna=id_pengguna, alasan=alasan)
    pengaturan.atur_batas_terlambat(BATAS_TERLAMBAT_SISWA, id_pengguna=id_pengguna, alasan=alasan)
    pengaturan.atur_jam_pulang(JAM_PULANG_SISWA_LAIN, id_pengguna=id_pengguna, alasan=alasan)
