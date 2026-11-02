"""Tes DashboardRepository (PRD §14.1) - agregasi SQL per-tanggal/per-
kelas/alpa-terbanyak, siswa saja."""

from datetime import date

from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.dashboard import DashboardRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.student import StudentRepository


def _buat_kelas(session, *, nama_kelas: str) -> int:
    return KelasRepository(session).tambah(tingkat=8, nama_kelas=nama_kelas, tahun_ajaran="2026/2027").id


def _buat_siswa(session, *, id_kelas: int | None, nisn: str, nama: str) -> int:
    return StudentRepository(session).tambah(
        nisn=nisn, nama_lengkap=nama, jenis_kelamin=JenisKelamin.L, tempat_lahir="Sidrap",
        tanggal_lahir=date(2012, 1, 1), alamat="Jl. Contoh", id_kelas=id_kelas,
    ).id


def _tambah_absensi(session, *, id_siswa: int, tanggal: date, status: str) -> None:
    AttendanceRepository(session).tambah(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=tanggal, status_masuk=status
    )


# ---------------------------------------------------------------------------
# hitung_status_per_hari
# ---------------------------------------------------------------------------


def test_hitung_status_per_hari_mengelompokkan_tanggal_dan_status(session):
    id_kelas = _buat_kelas(session, nama_kelas="8A")
    siswa1 = _buat_siswa(session, id_kelas=id_kelas, nisn="1000000001", nama="Budi")
    siswa2 = _buat_siswa(session, id_kelas=id_kelas, nisn="1000000002", nama="Siti")
    _tambah_absensi(session, id_siswa=siswa1, tanggal=date(2026, 9, 11), status="HADIR")
    _tambah_absensi(session, id_siswa=siswa2, tanggal=date(2026, 9, 11), status="TERLAMBAT")
    _tambah_absensi(session, id_siswa=siswa1, tanggal=date(2026, 9, 12), status="HADIR")

    baris = DashboardRepository(session).hitung_status_per_hari(
        tanggal_mulai=date(2026, 9, 11), tanggal_selesai=date(2026, 9, 12)
    )

    hasil = {(t, s): j for t, s, j in baris}
    assert hasil[(date(2026, 9, 11), "HADIR")] == 1
    assert hasil[(date(2026, 9, 11), "TERLAMBAT")] == 1
    assert hasil[(date(2026, 9, 12), "HADIR")] == 1


def test_hitung_status_per_hari_menyaring_kelas(session):
    id_kelas_a = _buat_kelas(session, nama_kelas="8A")
    id_kelas_b = _buat_kelas(session, nama_kelas="8B")
    siswa_a = _buat_siswa(session, id_kelas=id_kelas_a, nisn="1000000001", nama="Budi")
    siswa_b = _buat_siswa(session, id_kelas=id_kelas_b, nisn="1000000002", nama="Siti")
    _tambah_absensi(session, id_siswa=siswa_a, tanggal=date(2026, 9, 11), status="HADIR")
    _tambah_absensi(session, id_siswa=siswa_b, tanggal=date(2026, 9, 11), status="HADIR")

    baris = DashboardRepository(session).hitung_status_per_hari(
        tanggal_mulai=date(2026, 9, 11), tanggal_selesai=date(2026, 9, 11), id_kelas=id_kelas_a
    )

    assert baris == [(date(2026, 9, 11), "HADIR", 1)]


# ---------------------------------------------------------------------------
# hitung_status_per_kelas
# ---------------------------------------------------------------------------


def test_hitung_status_per_kelas_termasuk_siswa_tanpa_kelas(session):
    id_kelas = _buat_kelas(session, nama_kelas="8A")
    siswa_berkelas = _buat_siswa(session, id_kelas=id_kelas, nisn="1000000001", nama="Budi")
    siswa_tanpa_kelas = _buat_siswa(session, id_kelas=None, nisn="1000000002", nama="Siti")
    _tambah_absensi(session, id_siswa=siswa_berkelas, tanggal=date(2026, 9, 11), status="HADIR")
    _tambah_absensi(session, id_siswa=siswa_tanpa_kelas, tanggal=date(2026, 9, 11), status="ALPA")

    baris = DashboardRepository(session).hitung_status_per_kelas(
        tanggal_mulai=date(2026, 9, 11), tanggal_selesai=date(2026, 9, 11)
    )

    per_kelas = {(id_kelas, nama): (status, jumlah) for id_kelas, nama, status, jumlah in baris}
    assert per_kelas[(id_kelas, "8A")] == ("HADIR", 1)
    assert per_kelas[(None, None)] == ("ALPA", 1)


# ---------------------------------------------------------------------------
# daftar_alpa_terbanyak
# ---------------------------------------------------------------------------


def test_daftar_alpa_terbanyak_urut_desc_dan_hanya_alpa(session):
    id_kelas = _buat_kelas(session, nama_kelas="8A")
    siswa_sering_alpa = _buat_siswa(session, id_kelas=id_kelas, nisn="1000000001", nama="Budi")
    siswa_jarang_alpa = _buat_siswa(session, id_kelas=id_kelas, nisn="1000000002", nama="Siti")
    siswa_rajin = _buat_siswa(session, id_kelas=id_kelas, nisn="1000000003", nama="Andi")

    _tambah_absensi(session, id_siswa=siswa_sering_alpa, tanggal=date(2026, 9, 9), status="ALPA")
    _tambah_absensi(session, id_siswa=siswa_sering_alpa, tanggal=date(2026, 9, 10), status="ALPA")
    _tambah_absensi(session, id_siswa=siswa_sering_alpa, tanggal=date(2026, 9, 11), status="ALPA")
    _tambah_absensi(session, id_siswa=siswa_jarang_alpa, tanggal=date(2026, 9, 11), status="ALPA")
    _tambah_absensi(session, id_siswa=siswa_rajin, tanggal=date(2026, 9, 11), status="HADIR")

    hasil = DashboardRepository(session).daftar_alpa_terbanyak(
        tanggal_mulai=date(2026, 9, 9), tanggal_selesai=date(2026, 9, 11), limit=10
    )

    assert hasil[0][:2] == (siswa_sering_alpa, "Budi")
    assert hasil[0][3] == 3
    assert [h[0] for h in hasil] == [siswa_sering_alpa, siswa_jarang_alpa]  # siswa_rajin tidak muncul


def test_daftar_alpa_terbanyak_menghormati_limit(session):
    id_kelas = _buat_kelas(session, nama_kelas="8A")
    for i in range(5):
        siswa = _buat_siswa(session, id_kelas=id_kelas, nisn=f"100000000{i}", nama=f"Siswa{i}")
        _tambah_absensi(session, id_siswa=siswa, tanggal=date(2026, 9, 11), status="ALPA")

    hasil = DashboardRepository(session).daftar_alpa_terbanyak(
        tanggal_mulai=date(2026, 9, 11), tanggal_selesai=date(2026, 9, 11), limit=2
    )

    assert len(hasil) == 2
