from datetime import date, datetime

from app.data.models.enums import JenisPemilik
from app.data.models.notification import JenisNotifikasi, StatusNotifikasi
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.message_template import MessageTemplateRepository
from app.data.repositories.notification import NotificationRepository


def _buat_absensi(session) -> int:
    return AttendanceRepository(session).tambah(
        id_pemilik=1, jenis_pemilik=JenisPemilik.SISWA, tanggal=date(2026, 9, 8), status_masuk="HADIR"
    ).id


def test_antrekan_mencatat_id_template(session):
    id_absensi = _buat_absensi(session)
    template = MessageTemplateRepository(session).tambah(jenis=JenisNotifikasi.TERLAMBAT, versi=1, konten="v1")

    notif_repo = NotificationRepository(session)
    notifikasi = notif_repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="Sudah dirakit",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-template",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
        id_template=template.id,
    )

    assert notifikasi.id_template == template.id


def test_daftar_untuk_diproses_menyertakan_pending_dan_failed_di_bawah_batas(session):
    id_absensi = _buat_absensi(session)
    repo = NotificationRepository(session)

    pending = repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="pending",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-pending",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
    )
    gagal_ringan = repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="gagal ringan",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-gagal-ringan",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
    )
    repo.tandai_gagal(gagal_ringan.id, pesan_galat="coba lagi nanti")  # jumlah_percobaan = 1

    gagal_habis = repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="gagal habis batas",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-gagal-habis",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
    )
    repo.tandai_gagal(gagal_habis.id, pesan_galat="gagal 1")
    repo.tandai_gagal(gagal_habis.id, pesan_galat="gagal 2")  # jumlah_percobaan = 2

    sekarang = datetime(2026, 9, 8, 7, 20)
    hasil = repo.daftar_untuk_diproses(sebelum=sekarang, batas_percobaan=2)
    id_hasil = {n.id for n in hasil}

    assert pending.id in id_hasil
    assert gagal_ringan.id in id_hasil
    assert gagal_habis.id not in id_hasil


def test_daftar_untuk_diproses_tidak_menyertakan_sent_atau_cancelled(session):
    id_absensi = _buat_absensi(session)
    repo = NotificationRepository(session)

    terkirim = repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="terkirim",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-terkirim",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
    )
    repo.tandai_terkirim(terkirim.id, waktu_kirim=datetime(2026, 9, 8, 7, 17))

    dibatalkan = repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="dibatalkan",
        jenis=JenisNotifikasi.ALPA_PAGI,
        kunci_idempotensi="evt-dibatalkan",
        waktu_kejadian=datetime(2026, 9, 8, 7, 30),
    )
    repo.batalkan(dibatalkan.id)

    hasil = repo.daftar_untuk_diproses(sebelum=datetime(2026, 9, 8, 9, 0), batas_percobaan=3)
    id_hasil = {n.id for n in hasil}

    assert terkirim.id not in id_hasil
    assert dibatalkan.id not in id_hasil


def test_daftar_menyaring_status_dan_urut_terbaru_dulu(session):
    id_absensi = _buat_absensi(session)
    repo = NotificationRepository(session)

    lama = repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="lama",
        jenis=JenisNotifikasi.TERLAMBAT,
        kunci_idempotensi="evt-lama",
        waktu_kejadian=datetime(2026, 9, 8, 7, 16),
    )
    baru = repo.antrekan(
        id_absensi=id_absensi,
        id_penerima=None,
        nomor_tujuan="+628123",
        isi_pesan="baru",
        jenis=JenisNotifikasi.ALPA_PAGI,
        kunci_idempotensi="evt-baru",
        waktu_kejadian=datetime(2026, 9, 8, 9, 15),
    )
    repo.tandai_terkirim(baru.id, waktu_kirim=datetime(2026, 9, 8, 9, 16))

    semua = repo.daftar()
    assert [n.id for n in semua] == [baru.id, lama.id]

    hanya_pending = repo.daftar(status=StatusNotifikasi.PENDING)
    assert [n.id for n in hanya_pending] == [lama.id]

    hanya_alpa_pagi = repo.daftar(jenis=JenisNotifikasi.ALPA_PAGI)
    assert [n.id for n in hanya_alpa_pagi] == [baru.id]
