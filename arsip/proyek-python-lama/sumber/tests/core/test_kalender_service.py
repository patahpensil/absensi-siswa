"""Tes integrasi KalenderService: jenis_hari_siswa/guru dua jalur,
pratinjau dampak, tambah (dengan audit log), dan batalkan (undo)."""

from datetime import date

import pytest

from app.core.kalender import EntriKalender as EntriKalenderMurni
from app.core.kalender_service import (
    EntriKalenderSudahDibatalkan,
    EntriKalenderTidakDitemukan,
    KalenderService,
)
from app.core.koreksi_massal import AksesKoreksiMassalDitolak
from app.core.status import JenisHari, Peran
from app.data.models.system import PeranPengguna
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kalender import KalenderRepository
from app.data.repositories.user import UserRepository


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def _buat_service(session) -> KalenderService:
    return KalenderService(kalender_repo=KalenderRepository(session), audit_repo=AuditLogRepository(session))


def test_wali_kelas_tidak_bisa_menambah(session):
    id_admin = _buat_admin(session)
    service = _buat_service(session)

    with pytest.raises(AksesKoreksiMassalDitolak):
        service.tambah(
            mulai=date(2026, 12, 14),
            selesai=date(2026, 12, 19),
            keterangan="Uji",
            jenis_siswa=JenisHari.KEGIATAN_TANPA_KBM,
            jenis_guru=JenisHari.EFEKTIF,
            tahun_ajaran="2026/2027",
            id_pengguna=id_admin,
            peran=Peran.WALI_KELAS,
            alasan="Uji",
        )


def test_tambah_lalu_jenis_hari_dua_jalur_berbeda(session):
    """Contoh nyata SKILL.md: 14-19 Desember libur siswa (LHBPD) tapi
    efektif guru."""
    id_admin = _buat_admin(session)
    service = _buat_service(session)

    service.tambah(
        mulai=date(2026, 12, 14),
        selesai=date(2026, 12, 19),
        keterangan="Pengolahan nilai LHBPD",
        jenis_siswa=JenisHari.KEGIATAN_TANPA_KBM,
        jenis_guru=JenisHari.EFEKTIF,
        tahun_ajaran="2026/2027",
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Sesuai kalender dinas",
    )

    assert service.jenis_hari_siswa(date(2026, 12, 16)) == JenisHari.KEGIATAN_TANPA_KBM
    assert service.jenis_hari_guru(date(2026, 12, 16)) == JenisHari.EFEKTIF
    # Di luar rentang (Senin, bukan Minggu) - kembali ke bawaan EFEKTIF.
    assert service.jenis_hari_siswa(date(2026, 12, 21)) == JenisHari.EFEKTIF


def test_tambah_mencatat_audit_log(session):
    id_admin = _buat_admin(session)
    service = _buat_service(session)

    service.tambah(
        mulai=date(2026, 8, 17),
        selesai=date(2026, 8, 17),
        keterangan="HUT RI",
        jenis_siswa=JenisHari.LIBUR_NASIONAL,
        jenis_guru=JenisHari.LIBUR_NASIONAL,
        tahun_ajaran="2026/2027",
        id_pengguna=id_admin,
        peran=Peran.KEPALA_SEKOLAH,
        alasan="Libur nasional",
    )

    entri_audit = AuditLogRepository(session).daftar_terbaru(limit=1)[0]
    assert entri_audit.aksi == "tambah_kalender"
    assert entri_audit.id_pengguna == id_admin


def test_pratinjau_dampak_menghitung_hari_dan_baris(session):
    service = _buat_service(session)

    dampak = service.pratinjau_dampak(
        EntriKalenderMurni(
            mulai=date(2026, 12, 14),
            selesai=date(2026, 12, 19),
            keterangan="Uji",
            jenis_siswa=JenisHari.KEGIATAN_TANPA_KBM,
            jenis_guru=JenisHari.EFEKTIF,
        ),
        jumlah_orang=1000,
    )

    assert dampak["jumlah_hari"] == 6
    assert dampak["perkiraan_baris"] == 6000


def test_batalkan_mengembalikan_ke_bawaan_efektif(session):
    id_admin = _buat_admin(session)
    service = _buat_service(session)
    entri = service.tambah(
        mulai=date(2026, 9, 9),
        selesai=date(2026, 9, 9),
        keterangan="Salah input",
        jenis_siswa=JenisHari.LIBUR_MENDADAK,
        jenis_guru=JenisHari.LIBUR_MENDADAK,
        tahun_ajaran="2026/2027",
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Uji",
    )
    assert service.jenis_hari_siswa(date(2026, 9, 9)) == JenisHari.LIBUR_MENDADAK

    service.batalkan(entri.id, id_pengguna=id_admin, peran=Peran.ADMIN, alasan="Salah tanggal")

    assert service.jenis_hari_siswa(date(2026, 9, 9)) == JenisHari.EFEKTIF


def test_batalkan_tidak_ditemukan(session):
    id_admin = _buat_admin(session)
    service = _buat_service(session)

    with pytest.raises(EntriKalenderTidakDitemukan):
        service.batalkan(999, id_pengguna=id_admin, peran=Peran.ADMIN, alasan="Uji")


def test_daftar_termasuk_yang_sudah_dibatalkan(session):
    id_admin = _buat_admin(session)
    service = _buat_service(session)
    entri = service.tambah(
        mulai=date(2026, 9, 9),
        selesai=date(2026, 9, 9),
        keterangan="Uji",
        jenis_siswa=JenisHari.LIBUR_MENDADAK,
        jenis_guru=JenisHari.LIBUR_MENDADAK,
        tahun_ajaran="2026/2027",
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Uji",
    )
    service.batalkan(entri.id, id_pengguna=id_admin, peran=Peran.ADMIN, alasan="Salah tanggal")

    daftar = service.daftar(tahun_ajaran="2026/2027")
    assert len(daftar) == 1
    assert daftar[0].dibatalkan is True


def test_salin_dari_tahun_sebelumnya_menduplikasi_entri_ke_tahun_baru(session):
    id_admin = _buat_admin(session)
    service = _buat_service(session)
    service.tambah(
        mulai=date(2026, 8, 17),
        selesai=date(2026, 8, 17),
        keterangan="HUT RI",
        jenis_siswa=JenisHari.LIBUR_NASIONAL,
        jenis_guru=JenisHari.LIBUR_NASIONAL,
        tahun_ajaran="2026/2027",
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Libur nasional",
    )

    disalin = service.salin_dari_tahun_sebelumnya(
        tahun_asal="2026/2027",
        tahun_tujuan="2027/2028",
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Persiapan tahun ajaran baru",
    )

    assert len(disalin) == 1
    assert disalin[0].tahun_ajaran == "2027/2028"
    assert disalin[0].keterangan == "HUT RI"
    # Tahun asal tidak berubah/terganggu.
    assert len(service.daftar(tahun_ajaran="2026/2027")) == 1
    assert len(service.daftar(tahun_ajaran="2027/2028")) == 1


def test_salin_dari_tahun_sebelumnya_mencatat_satu_audit_induk(session):
    id_admin = _buat_admin(session)
    service = _buat_service(session)
    service.tambah(
        mulai=date(2026, 8, 17), selesai=date(2026, 8, 17), keterangan="A",
        jenis_siswa=JenisHari.LIBUR_NASIONAL, jenis_guru=JenisHari.LIBUR_NASIONAL,
        tahun_ajaran="2026/2027", id_pengguna=id_admin, peran=Peran.ADMIN, alasan="Uji",
    )
    service.tambah(
        mulai=date(2026, 12, 26), selesai=date(2027, 1, 2), keterangan="B",
        jenis_siswa=JenisHari.LIBUR_SEMESTER, jenis_guru=JenisHari.LIBUR_SEMESTER,
        tahun_ajaran="2026/2027", id_pengguna=id_admin, peran=Peran.ADMIN, alasan="Uji",
    )

    jumlah_audit_sebelum = len(AuditLogRepository(session).daftar_terbaru(limit=100))
    service.salin_dari_tahun_sebelumnya(
        tahun_asal="2026/2027", tahun_tujuan="2027/2028",
        id_pengguna=id_admin, peran=Peran.ADMIN, alasan="Salin tahun",
    )
    jumlah_audit_sesudah = len(AuditLogRepository(session).daftar_terbaru(limit=100))

    # Dua baris disalin, tapi hanya SATU entri audit induk yang bertambah.
    assert jumlah_audit_sesudah - jumlah_audit_sebelum == 1


def test_salin_dari_tahun_sebelumnya_ditolak_untuk_wali_kelas(session):
    id_admin = _buat_admin(session)
    service = _buat_service(session)

    with pytest.raises(AksesKoreksiMassalDitolak):
        service.salin_dari_tahun_sebelumnya(
            tahun_asal="2026/2027", tahun_tujuan="2027/2028",
            id_pengguna=id_admin, peran=Peran.WALI_KELAS, alasan="Uji",
        )


def test_batalkan_dua_kali_ditolak(session):
    id_admin = _buat_admin(session)
    service = _buat_service(session)
    entri = service.tambah(
        mulai=date(2026, 9, 9),
        selesai=date(2026, 9, 9),
        keterangan="Uji",
        jenis_siswa=JenisHari.LIBUR_MENDADAK,
        jenis_guru=JenisHari.LIBUR_MENDADAK,
        tahun_ajaran="2026/2027",
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Uji",
    )
    service.batalkan(entri.id, id_pengguna=id_admin, peran=Peran.ADMIN, alasan="Batal pertama")

    with pytest.raises(EntriKalenderSudahDibatalkan):
        service.batalkan(entri.id, id_pengguna=id_admin, peran=Peran.ADMIN, alasan="Batal kedua")
