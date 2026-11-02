from datetime import date, timedelta

from app.core.timeutil import now_wita
from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.koreksi_satuan import KoreksiSatuanRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.user import UserRepository


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def _buat_absensi(session) -> int:
    siswa = StudentRepository(session).tambah(
        nisn="1000000001", nama_lengkap="Budi", jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang", tanggal_lahir=date(2012, 1, 1), alamat="Jl. Contoh No. 1",
    )
    absensi = AttendanceRepository(session).tambah(
        id_pemilik=siswa.id, jenis_pemilik=JenisPemilik.SISWA, tanggal=date(2026, 9, 8), status_masuk="ALPA"
    )
    return absensi.id


def test_catat_dan_baca(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi(session)
    repo = KoreksiSatuanRepository(session)

    koreksi = repo.catat(
        id_absensi=id_absensi, id_pengguna=id_admin,
        nilai_sebelum={"status_final": "ALPA"}, nilai_sesudah={"status_final": "IZIN"},
        alasan="Surat dokter",
    )
    repo.commit()

    dibaca = repo.cari_by_id(koreksi.id)
    assert dibaca is not None
    assert dibaca.nilai_sebelum == {"status_final": "ALPA"}
    assert dibaca.dibatalkan is False


def test_daftar_by_absensi_terbaru_dulu(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi(session)
    repo = KoreksiSatuanRepository(session)

    repo.catat(id_absensi=id_absensi, id_pengguna=id_admin, nilai_sebelum={"a": 1}, nilai_sesudah={"a": 2}, alasan="Uji 1")
    repo.commit()
    kedua = repo.catat(id_absensi=id_absensi, id_pengguna=id_admin, nilai_sebelum={"a": 2}, nilai_sesudah={"a": 3}, alasan="Uji 2")
    repo.commit()

    daftar = repo.daftar_by_absensi(id_absensi)
    assert len(daftar) == 2
    assert daftar[0].id == kedua.id  # terbaru dulu


def test_tandai_dibatalkan(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi(session)
    repo = KoreksiSatuanRepository(session)
    koreksi = repo.catat(id_absensi=id_absensi, id_pengguna=id_admin, nilai_sebelum={"a": 1}, nilai_sesudah={"a": 2}, alasan="Uji")
    repo.commit()

    repo.tandai_dibatalkan(koreksi, id_pengguna=id_admin)
    repo.commit()

    dibaca = repo.cari_by_id(koreksi.id)
    assert dibaca.dibatalkan is True
    assert dibaca.dibatalkan_oleh == id_admin
    assert dibaca.waktu_dibatalkan is not None


def test_daftar_setelah_menyaring_waktu_dan_ids_absensi(session):
    """Dipakai layar Koreksi Setelah Laporan Terbit (app.server.laporan_service)."""
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi(session)
    repo = KoreksiSatuanRepository(session)

    # Selisih detik yang jelas, bukan now_wita() telanjang tepat sebelum -
    # dua panggilan now_wita() yang berdekatan bisa jatuh di resolusi jam
    # yang sama (lihat README, flakiness AuditLogRepository.daftar_terbaru()
    # soal ORDER BY waktu tanpa tiebreaker - akar masalah yang sama).
    sebelum = now_wita() - timedelta(seconds=1)
    koreksi = repo.catat(
        id_absensi=id_absensi, id_pengguna=id_admin, nilai_sebelum={"a": 1}, nilai_sesudah={"a": 2}, alasan="Uji"
    )
    repo.commit()

    assert [k.id for k in repo.daftar_setelah(sejak=sebelum, ids_absensi=[id_absensi])] == [koreksi.id]
    # ids_absensi yang tidak cocok atau kosong - tidak ikut.
    assert repo.daftar_setelah(sejak=sebelum, ids_absensi=[]) == []
    assert repo.daftar_setelah(sejak=sebelum, ids_absensi=[id_absensi + 999]) == []
    # sejak SETELAH koreksi ditulis - tidak ikut (koreksi lama, bukan baru).
    assert repo.daftar_setelah(sejak=now_wita() + timedelta(seconds=1), ids_absensi=[id_absensi]) == []


def test_daftar_setelah_mengecualikan_yang_dibatalkan(session):
    id_admin = _buat_admin(session)
    id_absensi = _buat_absensi(session)
    repo = KoreksiSatuanRepository(session)

    sebelum = now_wita() - timedelta(seconds=1)
    koreksi = repo.catat(
        id_absensi=id_absensi, id_pengguna=id_admin, nilai_sebelum={"a": 1}, nilai_sesudah={"a": 2}, alasan="Uji"
    )
    repo.commit()
    repo.tandai_dibatalkan(koreksi, id_pengguna=id_admin)
    repo.commit()

    assert repo.daftar_setelah(sejak=sebelum, ids_absensi=[id_absensi]) == []
