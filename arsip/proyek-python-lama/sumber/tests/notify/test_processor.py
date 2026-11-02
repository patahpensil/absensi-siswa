from datetime import date, datetime

from app.config.pengaturan import Pengaturan
from app.data.models.enums import JenisPemilik
from app.data.models.notification import JenisNotifikasi, StatusNotifikasi
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.notification import NotificationRepository
from app.data.repositories.setting import SettingRepository
from app.data.repositories.user import UserRepository
from app.notify.factory import buat_provider
from app.notify.processor import QueueProcessor
from app.notify.provider import HasilKirim, StatusKirim


class _ProviderTerkirim:
    def __init__(self) -> None:
        self.dipanggil: list[tuple[str, str, str]] = []

    def send(self, to, message, idempotency_key):
        self.dipanggil.append((to, message, idempotency_key))
        return HasilKirim(status=StatusKirim.TERKIRIM)


class _ProviderGagal:
    def __init__(self, pesan: str = "simulasi gagal") -> None:
        self.dipanggil: list[tuple[str, str, str]] = []
        self._pesan = pesan

    def send(self, to, message, idempotency_key):
        self.dipanggil.append((to, message, idempotency_key))
        return HasilKirim(status=StatusKirim.GAGAL, pesan_galat=self._pesan)


class _ProviderTerlewat:
    def __init__(self) -> None:
        self.dipanggil: list[tuple[str, str, str]] = []

    def send(self, to, message, idempotency_key):
        self.dipanggil.append((to, message, idempotency_key))
        return HasilKirim(status=StatusKirim.TERLEWAT)


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def _buat_absensi(session, *, status_masuk: str = "HADIR") -> int:
    return AttendanceRepository(session).tambah(
        id_pemilik=1,
        jenis_pemilik=JenisPemilik.SISWA,
        tanggal=date(2026, 9, 8),
        status_masuk=status_masuk,
    ).id


def _buat_processor(session, provider, *, pengaturan: Pengaturan | None = None) -> QueueProcessor:
    return QueueProcessor(
        notification_repo=NotificationRepository(session),
        attendance_repo=AttendanceRepository(session),
        provider=provider,
        pengaturan=pengaturan or Pengaturan(SettingRepository(session)),
    )


def test_processor_menandai_terkirim_dan_memisahkan_waktu_kejadian_dan_kirim(session):
    id_absensi = _buat_absensi(session)
    repo = NotificationRepository(session)
    waktu_kejadian = datetime(2026, 9, 8, 7, 16)
    repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+6281234567890",
        isi_pesan="Anak Anda hadir",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-1",
        waktu_kejadian=waktu_kejadian,
    )

    provider = _ProviderTerkirim()
    processor = _buat_processor(session, provider)
    hasil = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 7, 17))

    assert len(hasil) == 1
    diproses = hasil[0]
    assert diproses.status == StatusNotifikasi.SENT
    assert diproses.waktu_kejadian == waktu_kejadian
    assert diproses.waktu_kirim is not None
    assert diproses.waktu_kirim != waktu_kejadian
    assert provider.dipanggil == [("+6281234567890", "Anak Anda hadir", "evt-1")]


def test_idempotensi_mencegah_kirim_ganda(session):
    id_absensi = _buat_absensi(session)
    repo = NotificationRepository(session)

    pertama = repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="A",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-sama",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
    )
    kedua = repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="A (duplikat)",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-sama",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
    )
    assert pertama.id == kedua.id

    provider = _ProviderTerkirim()
    processor = _buat_processor(session, provider)
    hasil = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 7, 17))

    # Hanya satu baris yang pernah ada untuk kunci ini, jadi hanya sekali terkirim.
    assert len(hasil) == 1
    assert len(provider.dipanggil) == 1


def test_failed_di_retry_otomatis_sampai_batas_lalu_berhenti(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi(session)
    repo = NotificationRepository(session)
    item = repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="A",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-retry",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
    )

    pengaturan = Pengaturan(SettingRepository(session))
    pengaturan.atur_batas_percobaan_notifikasi(2, id_pengguna=id_admin, alasan="Batasi retry untuk uji")

    provider = _ProviderGagal("jaringan putus")
    processor = _buat_processor(session, provider, pengaturan=pengaturan)

    # Percobaan pertama: gagal, jumlah_percobaan jadi 1, masih di bawah batas (2).
    hasil_1 = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 7, 17))
    assert len(hasil_1) == 1
    assert hasil_1[0].status == StatusNotifikasi.FAILED
    assert hasil_1[0].jumlah_percobaan == 1

    # Percobaan kedua: diambil otomatis lagi (belum mencapai batas), gagal lagi -> jumlah_percobaan 2.
    hasil_2 = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 7, 18))
    assert len(hasil_2) == 1
    assert hasil_2[0].jumlah_percobaan == 2

    # Sudah mencapai batas (2) - putaran berikutnya tidak mengambilnya lagi sama sekali.
    hasil_3 = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 7, 19))
    assert hasil_3 == []
    assert len(provider.dipanggil) == 2

    tetap_failed = repo.cari_by_kunci_idempotensi("evt-retry")
    assert tetap_failed.status == StatusNotifikasi.FAILED
    assert tetap_failed.jumlah_percobaan == 2
    assert item.id == tetap_failed.id


def test_cancelled_manual_tidak_pernah_diproses_ulang(session):
    id_absensi = _buat_absensi(session, status_masuk="ALPA")
    repo = NotificationRepository(session)
    item = repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="ALPA",
        jenis=JenisNotifikasi.ALPA_PAGI,
        kunci_idempotensi="evt-alpa-manual",
        waktu_kejadian=datetime(2026, 9, 8, 7, 30),
        waktu_dijadwalkan=datetime(2026, 9, 8, 9, 15),
    )
    dibatalkan = repo.batalkan(item.id)
    assert dibatalkan.status == StatusNotifikasi.CANCELLED

    provider = _ProviderTerkirim()
    processor = _buat_processor(session, provider)
    hasil = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 9, 16))

    assert hasil == []
    assert provider.dipanggil == []
    assert repo.cari_by_kunci_idempotensi("evt-alpa-manual").status == StatusNotifikasi.CANCELLED


def test_alpa_dibatalkan_otomatis_jika_status_sudah_dikoreksi(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi(session, status_masuk="ALPA")
    repo = NotificationRepository(session)
    repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="Ananda belum tercatat hadir",
        jenis=JenisNotifikasi.ALPA_PAGI,
        kunci_idempotensi="evt-alpa-koreksi",
        waktu_kejadian=datetime(2026, 9, 8, 7, 30),
        waktu_dijadwalkan=datetime(2026, 9, 8, 9, 15),
    )

    # Siswa ternyata datang - wali kelas mengoreksi status sebelum 09:15.
    # Koreksi menyentuh status_final (status resmi laporan) - status_masuk
    # (hasil scan pagi apa adanya) sengaja dibiarkan ALPA, lihat SKILL.md
    # "Perilaku setelah ALPA ditetapkan".
    AttendanceRepository(session).koreksi(
        id_absensi,
        perubahan={"status_final": "TERLAMBAT"},
        id_pengguna=id_admin,
        alasan="Siswa datang terlambat, ada scan pukul 07:45",
    )

    provider = _ProviderTerkirim()
    processor = _buat_processor(session, provider)
    hasil = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 9, 15))

    assert len(hasil) == 1
    assert hasil[0].status == StatusNotifikasi.CANCELLED
    assert provider.dipanggil == []


def test_alpa_masih_berlaku_tetap_dikirim(session):
    id_absensi = _buat_absensi(session, status_masuk="ALPA")
    repo = NotificationRepository(session)
    repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="Ananda belum tercatat hadir",
        jenis=JenisNotifikasi.ALPA_PAGI,
        kunci_idempotensi="evt-alpa-masih",
        waktu_kejadian=datetime(2026, 9, 8, 7, 30),
        waktu_dijadwalkan=datetime(2026, 9, 8, 9, 15),
    )

    provider = _ProviderTerkirim()
    processor = _buat_processor(session, provider)
    hasil = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 9, 15))

    assert len(hasil) == 1
    assert hasil[0].status == StatusNotifikasi.SENT
    assert len(provider.dipanggil) == 1


def test_provider_terlewat_dibiarkan_pending(session):
    id_absensi = _buat_absensi(session)
    repo = NotificationRepository(session)
    repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="A",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-null",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
    )

    provider = _ProviderTerlewat()
    processor = _buat_processor(session, provider)
    hasil = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 7, 17))

    assert hasil == []
    assert repo.cari_by_kunci_idempotensi("evt-null").status == StatusNotifikasi.PENDING


def test_notifikasi_tanpa_jadwal_tetap_diambil_segera(session):
    """waktu_dijadwalkan kosong (MASUK/PULANG) tidak boleh membuat baris
    tidak pernah terambil - regresi untuk NULL <= sebelum yang selalu
    salah di SQL.
    """
    id_absensi = _buat_absensi(session)
    repo = NotificationRepository(session)
    repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="A",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-segera",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
        waktu_dijadwalkan=None,
    )

    provider = _ProviderTerkirim()
    processor = _buat_processor(session, provider)
    hasil = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 7, 16, 1))

    assert len(hasil) == 1
    assert hasil[0].status == StatusNotifikasi.SENT


def test_modul_nonaktif_tidak_memproses_apa_pun(session):
    """modul_whatsapp_aktif default False -> buat_provider() mengembalikan
    NullProvider -> proses_satu_putaran() tidak mengirim atau mengubah
    status apa pun, walau ada baris PENDING yang jatuh tempo.
    """
    id_absensi = _buat_absensi(session)
    repo = NotificationRepository(session)
    repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="A",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-nonaktif",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
    )

    pengaturan = Pengaturan(SettingRepository(session))
    assert pengaturan.modul_whatsapp_aktif() is False

    processor = _buat_processor(session, buat_provider(pengaturan), pengaturan=pengaturan)
    hasil = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 7, 17))

    assert hasil == []
    assert repo.cari_by_kunci_idempotensi("evt-nonaktif").status == StatusNotifikasi.PENDING


def test_alpa_pulang_dibatalkan_otomatis_jika_status_sudah_dikoreksi(session):
    """Generalisasi dari test_alpa_dibatalkan_otomatis_jika_status_sudah_dikoreksi:
    cek-ulang status sebelum kirim (PRD §13.1) berlaku untuk ALPA_PULANG
    juga, bukan cuma ALPA_PAGI - keduanya sama-sama berarti "status_final
    ALPA saat ini"."""
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi(session, status_masuk="ALPA")
    repo = NotificationRepository(session)
    repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="Ananda hadir namun tidak melakukan absen pulang",
        jenis=JenisNotifikasi.ALPA_PULANG,
        kunci_idempotensi="evt-alpa-pulang-koreksi",
        waktu_kejadian=datetime(2026, 9, 8, 15, 0),
        waktu_dijadwalkan=datetime(2026, 9, 8, 16, 0),
    )

    AttendanceRepository(session).koreksi(
        id_absensi,
        perubahan={"status_final": "TERLAMBAT"},
        id_pengguna=id_admin,
        alasan="Ternyata ada scan pulang yang terlambat masuk sistem",
    )

    provider = _ProviderTerkirim()
    processor = _buat_processor(session, provider)
    hasil = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 16, 0))

    assert len(hasil) == 1
    assert hasil[0].status == StatusNotifikasi.CANCELLED
    assert provider.dipanggil == []


def test_mode_uji_aktif_mengalihkan_tujuan_tanpa_mengubah_nomor_tersimpan(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi(session)
    repo = NotificationRepository(session)
    repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+6281111111111",
        isi_pesan="Ananda terlambat",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-mode-uji",
        waktu_kejadian=datetime(2026, 9, 8, 7, 25),
    )

    pengaturan = Pengaturan(SettingRepository(session))
    pengaturan.atur_nomor_admin_mode_uji("081298765432", id_pengguna=id_admin, alasan="Uji notifikasi")
    pengaturan.atur_mode_uji_whatsapp_aktif(True, id_pengguna=id_admin, alasan="Uji notifikasi")

    provider = _ProviderTerkirim()
    processor = _buat_processor(session, provider, pengaturan=pengaturan)
    hasil = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 7, 26))

    assert len(hasil) == 1
    assert hasil[0].status == StatusNotifikasi.SENT
    # Provider dipanggil dengan nomor admin (dirapikan ke format baku),
    # BUKAN nomor tujuan asli.
    assert provider.dipanggil == [("+6281298765432", "Ananda terlambat", "evt-mode-uji")]
    # Baris tersimpan tetap mencatat nomor tujuan ASLI - pengalihan murni
    # terjadi saat kirim, bukan menimpa riwayat.
    assert repo.cari_by_kunci_idempotensi("evt-mode-uji").nomor_tujuan == "+6281111111111"


def test_mode_uji_aktif_tanpa_nomor_admin_ditandai_gagal_bukan_terkirim_ke_nomor_asli(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi(session)
    repo = NotificationRepository(session)
    repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+6281111111111",
        isi_pesan="Ananda terlambat",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-mode-uji-tanpa-nomor",
        waktu_kejadian=datetime(2026, 9, 8, 7, 25),
    )

    pengaturan = Pengaturan(SettingRepository(session))
    pengaturan.atur_mode_uji_whatsapp_aktif(True, id_pengguna=id_admin, alasan="Uji notifikasi")
    assert pengaturan.nomor_admin_mode_uji() is None

    provider = _ProviderTerkirim()
    processor = _buat_processor(session, provider, pengaturan=pengaturan)
    hasil = processor.proses_satu_putaran(sekarang=datetime(2026, 9, 8, 7, 26))

    assert len(hasil) == 1
    assert hasil[0].status == StatusNotifikasi.FAILED
    # TIDAK pernah mencoba mengirim ke nomor asli - itu justru pelanggaran
    # PRD §13.3 (mode uji mengalihkan SELURUH pesan).
    assert provider.dipanggil == []
