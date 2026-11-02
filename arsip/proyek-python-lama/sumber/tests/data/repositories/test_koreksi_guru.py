from datetime import date

from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.koreksi_guru import KoreksiGuruRepository
from app.data.repositories.teacher import TeacherRepository
from app.data.repositories.user import UserRepository


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def _buat_absensi_guru(session) -> int:
    guru = TeacherRepository(session).tambah(nama_lengkap="Pak Budi")
    absensi = AttendanceRepository(session).tambah(
        id_pemilik=guru.id, jenis_pemilik=JenisPemilik.GURU, tanggal=date(2026, 9, 8), status_masuk="HADIR"
    )
    return absensi.id


def test_catat_dan_baca_tanpa_lampiran(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi_guru(session)
    repo = KoreksiGuruRepository(session)

    koreksi = repo.catat(
        id_absensi=id_absensi, id_pengguna=id_admin,
        nilai_sebelum={"status_final": "HADIR"}, nilai_sesudah={"status_final": "SAKIT"},
        alasan="Sakit demam",
    )
    repo.commit()

    dibaca = repo.cari_by_id(koreksi.id)
    assert dibaca is not None
    assert dibaca.lampiran is None
    assert dibaca.dibatalkan is False


def test_catat_dengan_lampiran(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi_guru(session)
    repo = KoreksiGuruRepository(session)

    koreksi = repo.catat(
        id_absensi=id_absensi, id_pengguna=id_admin,
        nilai_sebelum={"status_final": "HADIR"}, nilai_sesudah={"status_final": "DINAS_LUAR"},
        alasan="Diklat kurikulum", lampiran="koreksi_guru/abc123.jpg",
    )
    repo.commit()

    dibaca = repo.cari_by_id(koreksi.id)
    assert dibaca.lampiran == "koreksi_guru/abc123.jpg"


def test_daftar_by_absensi_terbaru_dulu(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi_guru(session)
    repo = KoreksiGuruRepository(session)

    repo.catat(id_absensi=id_absensi, id_pengguna=id_admin, nilai_sebelum={"a": 1}, nilai_sesudah={"a": 2}, alasan="Uji 1")
    repo.commit()
    kedua = repo.catat(id_absensi=id_absensi, id_pengguna=id_admin, nilai_sebelum={"a": 2}, nilai_sesudah={"a": 3}, alasan="Uji 2")
    repo.commit()

    daftar = repo.daftar_by_absensi(id_absensi)
    assert len(daftar) == 2
    assert daftar[0].id == kedua.id  # terbaru dulu


def test_tandai_dibatalkan(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi_guru(session)
    repo = KoreksiGuruRepository(session)
    koreksi = repo.catat(id_absensi=id_absensi, id_pengguna=id_admin, nilai_sebelum={"a": 1}, nilai_sesudah={"a": 2}, alasan="Uji")
    repo.commit()

    repo.tandai_dibatalkan(koreksi, id_pengguna=id_admin)
    repo.commit()

    dibaca = repo.cari_by_id(koreksi.id)
    assert dibaca.dibatalkan is True
    assert dibaca.dibatalkan_oleh == id_admin
    assert dibaca.waktu_dibatalkan is not None
