"""Tes KalenderRepository: catat, baca, dan batalkan entri kalender."""

from datetime import date

from app.data.models.system import PeranPengguna
from app.data.repositories.kalender import KalenderRepository
from app.data.repositories.user import UserRepository


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def test_tambah_dan_baca_entri(session):
    id_admin = _buat_admin(session)
    repo = KalenderRepository(session)

    entri = repo.tambah(
        mulai=date(2026, 12, 14),
        selesai=date(2026, 12, 19),
        keterangan="Pengolahan nilai LHBPD",
        jenis_siswa="KEGIATAN_TANPA_KBM",
        jenis_guru="EFEKTIF",
        tahun_ajaran="2026/2027",
        id_pengguna=id_admin,
        alasan="Sesuai kalender dinas",
    )

    dibaca = repo.cari_by_id(entri.id)
    assert dibaca is not None
    assert dibaca.jenis_siswa == "KEGIATAN_TANPA_KBM"
    assert dibaca.jenis_guru == "EFEKTIF"
    assert dibaca.dibatalkan is False


def test_daftar_aktif_mengecualikan_yang_dibatalkan(session):
    id_admin = _buat_admin(session)
    repo = KalenderRepository(session)
    entri1 = repo.tambah(
        mulai=date(2026, 8, 17), selesai=date(2026, 8, 17), keterangan="Libur nasional",
        jenis_siswa="LIBUR_NASIONAL", jenis_guru="LIBUR_NASIONAL", tahun_ajaran="2026/2027",
        id_pengguna=id_admin, alasan="HUT RI",
    )
    repo.tambah(
        mulai=date(2026, 8, 25), selesai=date(2026, 8, 25), keterangan="Maulid Nabi",
        jenis_siswa="LIBUR_NASIONAL", jenis_guru="LIBUR_NASIONAL", tahun_ajaran="2026/2027",
        id_pengguna=id_admin, alasan="Libur nasional",
    )
    repo.batalkan(entri1, id_pengguna=id_admin)

    aktif = repo.daftar_aktif(tahun_ajaran="2026/2027")

    assert len(aktif) == 1
    assert aktif[0].keterangan == "Maulid Nabi"


def test_daftar_aktif_menyaring_tahun_ajaran(session):
    id_admin = _buat_admin(session)
    repo = KalenderRepository(session)
    repo.tambah(
        mulai=date(2026, 12, 26), selesai=date(2027, 1, 2), keterangan="Libur semester ganjil",
        jenis_siswa="LIBUR_SEMESTER", jenis_guru="LIBUR_SEMESTER", tahun_ajaran="2026/2027",
        id_pengguna=id_admin, alasan="Sesuai kalender",
    )
    repo.tambah(
        mulai=date(2027, 6, 20), selesai=date(2027, 7, 10), keterangan="Libur semester genap",
        jenis_siswa="LIBUR_SEMESTER", jenis_guru="LIBUR_SEMESTER", tahun_ajaran="2026/2027",
        id_pengguna=id_admin, alasan="Sesuai kalender",
    )
    repo.tambah(
        mulai=date(2027, 7, 12), selesai=date(2027, 7, 16), keterangan="MPLS tahun ajaran baru",
        jenis_siswa="EFEKTIF", jenis_guru="EFEKTIF", tahun_ajaran="2027/2028",
        id_pengguna=id_admin, alasan="Sesuai kalender",
    )

    aktif_2027_2028 = repo.daftar_aktif(tahun_ajaran="2027/2028")

    assert len(aktif_2027_2028) == 1
    assert aktif_2027_2028[0].keterangan == "MPLS tahun ajaran baru"


def test_batalkan_menandai_dan_mencatat_pelaku(session):
    id_admin = _buat_admin(session)
    repo = KalenderRepository(session)
    entri = repo.tambah(
        mulai=date(2026, 9, 9), selesai=date(2026, 9, 9), keterangan="Salah input",
        jenis_siswa="LIBUR_MENDADAK", jenis_guru="LIBUR_MENDADAK", tahun_ajaran="2026/2027",
        id_pengguna=id_admin, alasan="Uji",
    )

    repo.batalkan(entri, id_pengguna=id_admin)

    dibaca = repo.cari_by_id(entri.id)
    assert dibaca.dibatalkan is True
    assert dibaca.dibatalkan_oleh == id_admin
    assert dibaca.waktu_dibatalkan is not None


def test_daftar_terbaru(session):
    id_admin = _buat_admin(session)
    repo = KalenderRepository(session)
    entri = repo.tambah(
        mulai=date(2026, 9, 9), selesai=date(2026, 9, 9), keterangan="Uji",
        jenis_siswa="EFEKTIF", jenis_guru="EFEKTIF", tahun_ajaran="2026/2027",
        id_pengguna=id_admin, alasan="Uji",
    )

    terbaru = repo.daftar_terbaru(limit=5)

    assert terbaru[0].id == entri.id
