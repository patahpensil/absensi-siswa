"""Membuktikan alasan wajib diisi untuk audit log dan koreksi absensi.

"Setiap koreksi wajib menyimpan ... alasan. Tanpa alasan, koreksi
ditolak." - skill proyek. Tes ini membuktikan penolakannya terjadi di
lapisan repository (ValueError), bukan cuma validasi di UI, dan baris
attendance tidak berubah sama sekali kalau alasan kosong.
"""

from datetime import date

import pytest

from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.user import UserRepository


def _buat_admin(session) -> int:
    pengguna = UserRepository(session).tambah(
        username="admin_uji",
        password_hash="hash-tidak-relevan-di-tes-ini",
        peran=PeranPengguna.ADMIN,
    )
    return pengguna.id


@pytest.mark.parametrize("alasan_kosong", ["", "   ", None])
def test_catat_menolak_alasan_kosong(session, alasan_kosong):
    repo = AuditLogRepository(session)
    with pytest.raises(ValueError):
        repo.catat(
            id_pengguna=_buat_admin(session),
            aksi="uji",
            nilai_sebelum=None,
            nilai_sesudah=None,
            alasan=alasan_kosong,
        )


def test_catat_berhasil_dengan_alasan_terisi(session):
    repo = AuditLogRepository(session)
    entri = repo.catat(
        id_pengguna=_buat_admin(session),
        aksi="uji",
        nilai_sebelum={"a": 1},
        nilai_sesudah={"a": 2},
        alasan="koreksi manual",
    )
    assert entri.id is not None
    assert repo.cari_by_id(entri.id).alasan == "koreksi manual"


def test_koreksi_absensi_menolak_tanpa_alasan(session):
    absensi_repo = AttendanceRepository(session)
    absensi = absensi_repo.tambah(
        id_pemilik=1,
        jenis_pemilik=JenisPemilik.SISWA,
        tanggal=date(2026, 9, 8),
        status_masuk="ALPA",
    )

    with pytest.raises(ValueError):
        absensi_repo.koreksi(
            absensi.id,
            perubahan={"status_masuk": "HADIR"},
            id_pengguna=1,
            alasan="",
        )

    # Gagal di lapisan repository - baris attendance sama sekali tidak
    # boleh berubah kalau validasi alasan gagal.
    tidak_berubah = absensi_repo.cari_by_id(absensi.id)
    assert tidak_berubah.status_masuk == "ALPA"
    assert tidak_berubah.dikoreksi is False


def test_koreksi_absensi_berhasil_dengan_alasan_dan_tercatat_di_audit_log(session):
    absensi_repo = AttendanceRepository(session)
    audit_repo = AuditLogRepository(session)

    absensi = absensi_repo.tambah(
        id_pemilik=1,
        jenis_pemilik=JenisPemilik.SISWA,
        tanggal=date(2026, 9, 8),
        status_masuk="ALPA",
    )

    hasil = absensi_repo.koreksi(
        absensi.id,
        perubahan={"status_masuk": "HADIR"},
        id_pengguna=_buat_admin(session),
        alasan="Siswa datang terlambat karena banjir, ada scan pukul 07:45",
    )

    assert hasil.status_masuk == "HADIR"
    assert hasil.dikoreksi is True

    entri_terbaru = audit_repo.daftar_terbaru(limit=1)[0]
    assert entri_terbaru.aksi == "koreksi_absensi"
    assert entri_terbaru.alasan.startswith("Siswa datang terlambat")
    assert entri_terbaru.nilai_sebelum["status_masuk"] == "ALPA"
    assert entri_terbaru.nilai_sesudah["status_masuk"] == "HADIR"
