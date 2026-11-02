"""Tes MassCorrectionRepository: penyaring tiga lapis (waktu, orang,
status) dan siklus catat/baca/batalkan tindakan_massal.
"""

from datetime import date, timedelta

from app.core.timeutil import now_wita
from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.mass_correction import MassCorrectionRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.user import UserRepository

TANGGAL = date(2026, 9, 8)


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def _buat_siswa(session, *, tingkat: int, nis: str) -> tuple[int, int]:
    kelas = KelasRepository(session).tambah(
        tingkat=tingkat, nama_kelas=f"{tingkat}A-{nis}", tahun_ajaran="2026/2027"
    )
    siswa = StudentRepository(session).tambah(
        nisn=nis.zfill(10),
        nama_lengkap=f"Siswa {nis}",
        id_kelas=kelas.id,
        jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang",
        tanggal_lahir=date(2012, 1, 1),
        alamat="Jl. Contoh No. 1",
    )
    return siswa.id, kelas.id


def _tambah_absensi(session, *, id_siswa: int, status_final: str) -> int:
    absensi = AttendanceRepository(session).tambah(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL, status_masuk=status_final
    )
    return absensi.id


def test_cari_baris_terdampak_lingkup_sekolah(session):
    id_siswa1, _ = _buat_siswa(session, tingkat=7, nis="1")
    id_siswa2, _ = _buat_siswa(session, tingkat=8, nis="2")
    _tambah_absensi(session, id_siswa=id_siswa1, status_final="ALPA")
    _tambah_absensi(session, id_siswa=id_siswa2, status_final="ALPA")

    repo = MassCorrectionRepository(session)
    terdampak = repo.cari_baris_terdampak(
        tanggal_mulai=TANGGAL,
        tanggal_selesai=TANGGAL,
        lingkup="SEKOLAH",
        tingkat=None,
        id_kelas=None,
        id_siswa_terpilih=frozenset(),
        status_termasuk=frozenset({"ALPA"}),
    )
    assert len(terdampak) == 2


def test_cari_baris_terdampak_lingkup_tingkat(session):
    id_siswa7, _ = _buat_siswa(session, tingkat=7, nis="1")
    id_siswa8, _ = _buat_siswa(session, tingkat=8, nis="2")
    _tambah_absensi(session, id_siswa=id_siswa7, status_final="ALPA")
    _tambah_absensi(session, id_siswa=id_siswa8, status_final="ALPA")

    repo = MassCorrectionRepository(session)
    terdampak = repo.cari_baris_terdampak(
        tanggal_mulai=TANGGAL,
        tanggal_selesai=TANGGAL,
        lingkup="TINGKAT",
        tingkat=7,
        id_kelas=None,
        id_siswa_terpilih=frozenset(),
        status_termasuk=frozenset({"ALPA"}),
    )
    assert len(terdampak) == 1
    assert terdampak[0].id_pemilik == id_siswa7


def test_cari_baris_terdampak_lingkup_kelas(session):
    id_siswa1, id_kelas1 = _buat_siswa(session, tingkat=7, nis="1")
    id_siswa2, _ = _buat_siswa(session, tingkat=7, nis="2")
    _tambah_absensi(session, id_siswa=id_siswa1, status_final="ALPA")
    _tambah_absensi(session, id_siswa=id_siswa2, status_final="ALPA")

    repo = MassCorrectionRepository(session)
    terdampak = repo.cari_baris_terdampak(
        tanggal_mulai=TANGGAL,
        tanggal_selesai=TANGGAL,
        lingkup="KELAS",
        tingkat=None,
        id_kelas=id_kelas1,
        id_siswa_terpilih=frozenset(),
        status_termasuk=frozenset({"ALPA"}),
    )
    assert len(terdampak) == 1
    assert terdampak[0].id_pemilik == id_siswa1


def test_cari_baris_terdampak_siswa_terpilih(session):
    id_siswa1, _ = _buat_siswa(session, tingkat=7, nis="1")
    id_siswa2, _ = _buat_siswa(session, tingkat=7, nis="2")
    _tambah_absensi(session, id_siswa=id_siswa1, status_final="ALPA")
    _tambah_absensi(session, id_siswa=id_siswa2, status_final="ALPA")

    repo = MassCorrectionRepository(session)
    terdampak = repo.cari_baris_terdampak(
        tanggal_mulai=TANGGAL,
        tanggal_selesai=TANGGAL,
        lingkup="SISWA_TERPILIH",
        tingkat=None,
        id_kelas=None,
        id_siswa_terpilih=frozenset({id_siswa2}),
        status_termasuk=frozenset({"ALPA"}),
    )
    assert len(terdampak) == 1
    assert terdampak[0].id_pemilik == id_siswa2


def test_cari_baris_terdampak_menyaring_status(session):
    id_siswa1, _ = _buat_siswa(session, tingkat=7, nis="1")
    id_siswa2, _ = _buat_siswa(session, tingkat=7, nis="2")
    _tambah_absensi(session, id_siswa=id_siswa1, status_final="ALPA")
    _tambah_absensi(session, id_siswa=id_siswa2, status_final="SAKIT")

    repo = MassCorrectionRepository(session)
    terdampak = repo.cari_baris_terdampak(
        tanggal_mulai=TANGGAL,
        tanggal_selesai=TANGGAL,
        lingkup="SEKOLAH",
        tingkat=None,
        id_kelas=None,
        id_siswa_terpilih=frozenset(),
        status_termasuk=frozenset({"ALPA"}),
    )
    assert len(terdampak) == 1
    assert terdampak[0].id_pemilik == id_siswa1


def test_catat_dan_baca_tindakan_massal(session):
    id_admin = _buat_admin(session)
    repo = MassCorrectionRepository(session)

    tindakan = repo.catat(
        id_pengguna=id_admin,
        jenis_tindakan="UBAH_STATUS",
        status_baru="SAKIT",
        penyaring={"lingkup": "SEKOLAH"},
        jumlah_baris=3,
        alasan="Uji",
    )
    repo.commit()

    dibaca = repo.cari_by_id(tindakan.id)
    assert dibaca is not None
    assert dibaca.jumlah_baris == 3
    assert dibaca.dibatalkan is False

    terbaru = repo.daftar_terbaru(limit=5)
    assert terbaru[0].id == tindakan.id


def test_catat_baris_dan_daftar_baris(session):
    id_admin = _buat_admin(session)
    id_siswa, _ = _buat_siswa(session, tingkat=7, nis="1")
    id_absensi = _tambah_absensi(session, id_siswa=id_siswa, status_final="ALPA")

    repo = MassCorrectionRepository(session)
    tindakan = repo.catat(
        id_pengguna=id_admin,
        jenis_tindakan="UBAH_STATUS",
        status_baru="SAKIT",
        penyaring={},
        jumlah_baris=1,
        alasan="Uji",
    )
    repo.catat_baris(
        id_tindakan_massal=tindakan.id,
        id_absensi=id_absensi,
        nilai_sebelum={"status_final": "ALPA"},
        nilai_sesudah={"status_final": "SAKIT"},
    )
    repo.commit()

    baris = repo.daftar_baris(tindakan.id)
    assert len(baris) == 1
    assert baris[0].id_absensi == id_absensi
    assert baris[0].nilai_sebelum == {"status_final": "ALPA"}


def test_tandai_dibatalkan(session):
    id_admin = _buat_admin(session)
    repo = MassCorrectionRepository(session)
    tindakan = repo.catat(
        id_pengguna=id_admin,
        jenis_tindakan="UBAH_STATUS",
        status_baru="SAKIT",
        penyaring={},
        jumlah_baris=1,
        alasan="Uji",
    )
    repo.commit()

    repo.tandai_dibatalkan(tindakan, id_pengguna=id_admin)
    repo.commit()

    dibaca = repo.cari_by_id(tindakan.id)
    assert dibaca.dibatalkan is True
    assert dibaca.dibatalkan_oleh == id_admin
    assert dibaca.waktu_dibatalkan is not None


def test_daftar_baris_setelah_menyaring_waktu_dan_ids_absensi(session):
    """Dipakai layar Koreksi Setelah Laporan Terbit (app.server.laporan_service)."""
    id_admin = _buat_admin(session)
    id_siswa, _ = _buat_siswa(session, tingkat=7, nis="1")
    id_absensi = _tambah_absensi(session, id_siswa=id_siswa, status_final="ALPA")
    repo = MassCorrectionRepository(session)

    sebelum = now_wita() - timedelta(seconds=1)
    tindakan = repo.catat(
        id_pengguna=id_admin, jenis_tindakan="UBAH_STATUS", status_baru="SAKIT", penyaring={}, jumlah_baris=1,
        alasan="Uji",
    )
    repo.catat_baris(
        id_tindakan_massal=tindakan.id, id_absensi=id_absensi, nilai_sebelum={"status_final": "ALPA"},
        nilai_sesudah={"status_final": "SAKIT"},
    )
    repo.commit()

    hasil = repo.daftar_baris_setelah(sejak=sebelum, ids_absensi=[id_absensi])
    assert len(hasil) == 1
    baris, induk = hasil[0]
    assert baris.id_absensi == id_absensi
    assert induk.id == tindakan.id

    assert repo.daftar_baris_setelah(sejak=sebelum, ids_absensi=[]) == []
    assert repo.daftar_baris_setelah(sejak=now_wita() + timedelta(seconds=1), ids_absensi=[id_absensi]) == []


def test_daftar_baris_setelah_mengecualikan_tindakan_dibatalkan(session):
    id_admin = _buat_admin(session)
    id_siswa, _ = _buat_siswa(session, tingkat=7, nis="1")
    id_absensi = _tambah_absensi(session, id_siswa=id_siswa, status_final="ALPA")
    repo = MassCorrectionRepository(session)

    sebelum = now_wita() - timedelta(seconds=1)
    tindakan = repo.catat(
        id_pengguna=id_admin, jenis_tindakan="UBAH_STATUS", status_baru="SAKIT", penyaring={}, jumlah_baris=1,
        alasan="Uji",
    )
    repo.catat_baris(
        id_tindakan_massal=tindakan.id, id_absensi=id_absensi, nilai_sebelum={"status_final": "ALPA"},
        nilai_sesudah={"status_final": "SAKIT"},
    )
    repo.commit()
    repo.tandai_dibatalkan(tindakan, id_pengguna=id_admin)
    repo.commit()

    assert repo.daftar_baris_setelah(sejak=sebelum, ids_absensi=[id_absensi]) == []
