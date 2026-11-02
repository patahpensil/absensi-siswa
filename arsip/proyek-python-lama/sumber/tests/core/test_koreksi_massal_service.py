"""Tes integrasi KoreksiMassalService: jalankan (dengan ambang snapshot/
konfirmasi ketik), batalkan (undo), dan penolakan wewenang - lewat basis
data sungguhan (SQLite in-memory), bukan tiruan.
"""

from datetime import date

import pytest

from app.config.pengaturan import Pengaturan
from app.core.koreksi_massal import (
    AksesKoreksiMassalDitolak,
    JenisTindakanMassal,
    LingkupOrang,
    PenyaringKoreksiMassal,
)
from app.core.koreksi_massal_service import (
    KonfirmasiKetikDiperlukan,
    KonfirmasiKetikTidakCocok,
    KoreksiMassalService,
    SnapshotDiperlukan,
)
from app.core.status import Peran, StatusSiswa
from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.mass_correction import MassCorrectionRepository, TindakanMassalSudahDibatalkan
from app.data.repositories.setting import SettingRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.user import UserRepository

TANGGAL = date(2026, 9, 8)


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def _buat_siswa(session, *, tingkat: int = 7, nis: str = "1") -> int:
    kelas = KelasRepository(session).tambah(
        tingkat=tingkat, nama_kelas=f"{tingkat}A-{nis}", tahun_ajaran="2026/2027"
    )
    return StudentRepository(session).tambah(
        nisn=nis.zfill(10),
        nama_lengkap=f"Siswa {nis}",
        id_kelas=kelas.id,
        jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang",
        tanggal_lahir=date(2012, 1, 1),
        alamat="Jl. Contoh No. 1",
    ).id


def _tambah_absensi(session, *, id_siswa: int, status: str = "ALPA") -> int:
    return AttendanceRepository(session).tambah(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL, status_masuk=status
    ).id


def _buat_service(session, *, pengaturan: Pengaturan | None = None) -> KoreksiMassalService:
    return KoreksiMassalService(
        attendance_repo=AttendanceRepository(session),
        mass_correction_repo=MassCorrectionRepository(session),
        audit_repo=AuditLogRepository(session),
        pengaturan=pengaturan or Pengaturan(SettingRepository(session)),
    )


def _penyaring_sekolah(**kwargs) -> PenyaringKoreksiMassal:
    dasar = dict(tanggal_mulai=TANGGAL, tanggal_selesai=TANGGAL, lingkup=LingkupOrang.SEKOLAH)
    dasar.update(kwargs)
    return PenyaringKoreksiMassal(**dasar)


# ---------------------------------------------------------------------------
# Wewenang
# ---------------------------------------------------------------------------


def test_wali_kelas_tidak_bisa_menjalankan(session):
    id_admin = _buat_admin(session)
    service = _buat_service(session)

    with pytest.raises(AksesKoreksiMassalDitolak):
        service.jalankan(
            penyaring=_penyaring_sekolah(),
            jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
            status_baru=StatusSiswa.SAKIT,
            id_pengguna=id_admin,
            peran=Peran.WALI_KELAS,
            alasan="Uji",
        )


# ---------------------------------------------------------------------------
# Jalankan - ubah status
# ---------------------------------------------------------------------------


def test_jalankan_ubah_status_menerapkan_dan_mencatat(session):
    id_admin = _buat_admin(session)
    id_siswa1 = _buat_siswa(session, nis="1")
    id_siswa2 = _buat_siswa(session, nis="2")
    _tambah_absensi(session, id_siswa=id_siswa1, status="ALPA")
    _tambah_absensi(session, id_siswa=id_siswa2, status="ALPA")
    service = _buat_service(session)

    tindakan = service.jalankan(
        penyaring=_penyaring_sekolah(),
        jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
        status_baru=StatusSiswa.SAKIT,
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Wabah demam berdarah, surat dari puskesmas menyusul",
    )

    assert tindakan.jumlah_baris == 2
    assert tindakan.dibatalkan is False

    absensi_repo = AttendanceRepository(session)
    a1 = absensi_repo.cari_by_pemilik_tanggal(id_pemilik=id_siswa1, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL)
    assert a1.status_final == "SAKIT"
    assert a1.status_masuk == "ALPA"  # status_masuk tidak pernah disentuh koreksi
    assert a1.alasan_final == "KOREKSI_MASSAL"


def test_jalankan_satu_entri_audit_log_bukan_per_baris(session):
    id_admin = _buat_admin(session)
    for i in range(5):
        id_siswa = _buat_siswa(session, nis=str(i))
        _tambah_absensi(session, id_siswa=id_siswa, status="ALPA")
    service = _buat_service(session)

    service.jalankan(
        penyaring=_penyaring_sekolah(),
        jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
        status_baru=StatusSiswa.SAKIT,
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Uji satu entri audit",
    )

    entri = AuditLogRepository(session).daftar_terbaru(limit=50)
    entri_koreksi_massal = [e for e in entri if e.aksi == "koreksi_massal"]
    assert len(entri_koreksi_massal) == 1
    assert entri_koreksi_massal[0].nilai_sesudah["jumlah_baris"] == 5


def test_jalankan_default_mengecualikan_status_manual(session):
    id_admin = _buat_admin(session)
    id_alpa = _buat_siswa(session, nis="1")
    id_sakit = _buat_siswa(session, nis="2")
    _tambah_absensi(session, id_siswa=id_alpa, status="ALPA")
    _tambah_absensi(session, id_siswa=id_sakit, status="SAKIT")
    service = _buat_service(session)

    tindakan = service.jalankan(
        penyaring=_penyaring_sekolah(),
        jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
        status_baru=StatusSiswa.HADIR,
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Uji default mengecualikan SAKIT",
    )

    assert tindakan.jumlah_baris == 1
    absensi_sakit = AttendanceRepository(session).cari_by_pemilik_tanggal(
        id_pemilik=id_sakit, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL
    )
    assert absensi_sakit.status_final == "SAKIT"  # tidak tersentuh


# ---------------------------------------------------------------------------
# Ambang snapshot dan konfirmasi ketik
# ---------------------------------------------------------------------------


def test_di_atas_ambang_snapshot_ditolak_tanpa_konfirmasi(session):
    id_admin = _buat_admin(session)
    id_siswa = _buat_siswa(session)
    _tambah_absensi(session, id_siswa=id_siswa, status="ALPA")
    pengaturan = Pengaturan()
    pengaturan.ambang_snapshot_sebelum_massal = 0
    pengaturan.ambang_konfirmasi_ketik = 1_000_000  # jangan ikut terpicu di tes ini
    service = _buat_service(session, pengaturan=pengaturan)

    with pytest.raises(SnapshotDiperlukan):
        service.jalankan(
            penyaring=_penyaring_sekolah(),
            jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
            status_baru=StatusSiswa.SAKIT,
            id_pengguna=id_admin,
            peran=Peran.ADMIN,
            alasan="Uji ambang snapshot",
        )


def test_di_atas_ambang_snapshot_berhasil_setelah_dikonfirmasi(session):
    id_admin = _buat_admin(session)
    id_siswa = _buat_siswa(session)
    _tambah_absensi(session, id_siswa=id_siswa, status="ALPA")
    pengaturan = Pengaturan()
    pengaturan.ambang_snapshot_sebelum_massal = 0
    pengaturan.ambang_konfirmasi_ketik = 1_000_000
    service = _buat_service(session, pengaturan=pengaturan)

    tindakan = service.jalankan(
        penyaring=_penyaring_sekolah(),
        jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
        status_baru=StatusSiswa.SAKIT,
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Uji ambang snapshot",
        snapshot_sudah_diambil=True,
    )
    assert tindakan.jumlah_baris == 1


def test_di_atas_ambang_konfirmasi_ketik_ditolak_tanpa_teks(session):
    id_admin = _buat_admin(session)
    id_siswa = _buat_siswa(session)
    _tambah_absensi(session, id_siswa=id_siswa, status="ALPA")
    pengaturan = Pengaturan()
    pengaturan.ambang_snapshot_sebelum_massal = 1_000_000
    pengaturan.ambang_konfirmasi_ketik = 0
    service = _buat_service(session, pengaturan=pengaturan)

    with pytest.raises(KonfirmasiKetikDiperlukan):
        service.jalankan(
            penyaring=_penyaring_sekolah(),
            jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
            status_baru=StatusSiswa.SAKIT,
            id_pengguna=id_admin,
            peran=Peran.ADMIN,
            alasan="Uji konfirmasi ketik",
        )


def test_konfirmasi_ketik_yang_salah_ditolak(session):
    id_admin = _buat_admin(session)
    id_siswa = _buat_siswa(session)
    _tambah_absensi(session, id_siswa=id_siswa, status="ALPA")
    pengaturan = Pengaturan()
    pengaturan.ambang_snapshot_sebelum_massal = 1_000_000
    pengaturan.ambang_konfirmasi_ketik = 0
    service = _buat_service(session, pengaturan=pengaturan)

    with pytest.raises(KonfirmasiKetikTidakCocok):
        service.jalankan(
            penyaring=_penyaring_sekolah(),
            jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
            status_baru=StatusSiswa.SAKIT,
            id_pengguna=id_admin,
            peran=Peran.ADMIN,
            alasan="Uji konfirmasi ketik salah",
            konfirmasi_ketik="999",
        )


# ---------------------------------------------------------------------------
# Batalkan (undo)
# ---------------------------------------------------------------------------


def test_batalkan_mengembalikan_nilai_sebelum(session):
    id_admin = _buat_admin(session)
    id_siswa = _buat_siswa(session)
    _tambah_absensi(session, id_siswa=id_siswa, status="ALPA")
    service = _buat_service(session)

    tindakan = service.jalankan(
        penyaring=_penyaring_sekolah(),
        jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
        status_baru=StatusSiswa.SAKIT,
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Akan dibatalkan",
    )
    service.batalkan(tindakan.id, id_pengguna=id_admin, peran=Peran.ADMIN, alasan="Salah input")

    absensi = AttendanceRepository(session).cari_by_pemilik_tanggal(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=TANGGAL
    )
    assert absensi.status_final == "ALPA"
    assert absensi.alasan_final is None

    dibaca = MassCorrectionRepository(session).cari_by_id(tindakan.id)
    assert dibaca.dibatalkan is True


def test_batalkan_dua_kali_ditolak(session):
    id_admin = _buat_admin(session)
    id_siswa = _buat_siswa(session)
    _tambah_absensi(session, id_siswa=id_siswa, status="ALPA")
    service = _buat_service(session)

    tindakan = service.jalankan(
        penyaring=_penyaring_sekolah(),
        jenis_tindakan=JenisTindakanMassal.UBAH_STATUS,
        status_baru=StatusSiswa.SAKIT,
        id_pengguna=id_admin,
        peran=Peran.ADMIN,
        alasan="Uji",
    )
    service.batalkan(tindakan.id, id_pengguna=id_admin, peran=Peran.ADMIN, alasan="Batal pertama")

    with pytest.raises(TindakanMassalSudahDibatalkan):
        service.batalkan(tindakan.id, id_pengguna=id_admin, peran=Peran.ADMIN, alasan="Batal kedua")
