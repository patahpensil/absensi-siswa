from datetime import date, datetime

from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.laporan import LaporanRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.user import UserRepository


def _buat_kelas(session, *, nama_kelas: str = "VIII. PASCAL") -> int:
    return KelasRepository(session).tambah(tingkat=8, nama_kelas=nama_kelas, tahun_ajaran="2026/2027").id


def _buat_siswa(session, *, id_kelas: int, nisn: str, nama: str) -> int:
    return StudentRepository(session).tambah(
        nisn=nisn, nama_lengkap=nama, jenis_kelamin=JenisKelamin.L, tempat_lahir="Sidrap",
        tanggal_lahir=date(2012, 1, 1), alamat="Jl. Contoh", id_kelas=id_kelas,
    ).id


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def test_cari_absensi_untuk_laporan_menyaring_rentang_dan_kelas(session):
    id_kelas_a = _buat_kelas(session, nama_kelas="VIII. PASCAL")
    id_kelas_b = _buat_kelas(session, nama_kelas="VIII. COBOL")
    id_siswa_a = _buat_siswa(session, id_kelas=id_kelas_a, nisn="1000000001", nama="Budi")
    id_siswa_b = _buat_siswa(session, id_kelas=id_kelas_b, nisn="1000000002", nama="Siti")

    attendance_repo = AttendanceRepository(session)
    attendance_repo.tambah(id_pemilik=id_siswa_a, jenis_pemilik=JenisPemilik.SISWA, tanggal=date(2026, 9, 1), status_masuk="HADIR")
    attendance_repo.tambah(id_pemilik=id_siswa_a, jenis_pemilik=JenisPemilik.SISWA, tanggal=date(2026, 9, 10), status_masuk="HADIR")  # di luar rentang
    attendance_repo.tambah(id_pemilik=id_siswa_b, jenis_pemilik=JenisPemilik.SISWA, tanggal=date(2026, 9, 1), status_masuk="HADIR")

    repo = LaporanRepository(session)
    hasil = repo.cari_absensi_untuk_laporan(tanggal_mulai=date(2026, 9, 1), tanggal_selesai=date(2026, 9, 5), id_kelas=id_kelas_a)

    assert len(hasil) == 1
    assert hasil[0].id_pemilik == id_siswa_a
    assert hasil[0].tanggal == date(2026, 9, 1)


def test_cari_absensi_untuk_laporan_saring_siswa_mengalahkan_kelas(session):
    id_kelas = _buat_kelas(session)
    id_siswa = _buat_siswa(session, id_kelas=id_kelas, nisn="1000000003", nama="Budi")
    AttendanceRepository(session).tambah(id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=date(2026, 9, 1), status_masuk="HADIR")

    repo = LaporanRepository(session)
    hasil = repo.cari_absensi_untuk_laporan(tanggal_mulai=date(2026, 9, 1), tanggal_selesai=date(2026, 9, 1), id_siswa=id_siswa)
    assert len(hasil) == 1


def test_catat_dan_daftar_arsip(session):
    id_admin = _buat_admin(session)
    id_kelas = _buat_kelas(session)
    repo = LaporanRepository(session)

    arsip = repo.catat_arsip(
        id_pengguna=id_admin, label="Laporan Uji", tanggal_mulai=date(2026, 9, 1), tanggal_selesai=date(2026, 9, 5),
        id_kelas=id_kelas, id_siswa=None,
        penyaring={"tanggal_mulai": "2026-09-01", "tanggal_selesai": "2026-09-05", "id_kelas": id_kelas, "id_siswa": None},
        path_pdf="laporan/abc.pdf", waktu_cetak=datetime(2026, 9, 5, 10, 0),
    )
    assert arsip.id is not None
    assert repo.cari_arsip_by_id(arsip.id).label == "Laporan Uji"

    daftar_semua = repo.daftar_arsip()
    assert len(daftar_semua) == 1

    daftar_kelas_lain = repo.daftar_arsip(id_kelas=id_kelas + 999)
    assert daftar_kelas_lain == []

    daftar_kelas_sesuai = repo.daftar_arsip(id_kelas=id_kelas)
    assert len(daftar_kelas_sesuai) == 1


def test_cari_nama_siswa_untuk_absensi(session):
    id_kelas = _buat_kelas(session)
    id_siswa = _buat_siswa(session, id_kelas=id_kelas, nisn="1000000004", nama="Ani Wijaya")
    id_absensi = AttendanceRepository(session).tambah(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=date(2026, 9, 1), status_masuk="HADIR"
    ).id

    repo = LaporanRepository(session)
    assert repo.cari_nama_siswa_untuk_absensi(id_absensi) == "Ani Wijaya"
    assert repo.cari_nama_siswa_untuk_absensi(id_absensi + 999) is None
