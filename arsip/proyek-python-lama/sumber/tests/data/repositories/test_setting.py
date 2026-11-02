import pytest

from app.data.models.system import PeranPengguna
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.setting import SettingRepository
from app.data.repositories.user import UserRepository


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def test_cari_by_kunci_mengembalikan_none_kalau_belum_ada(session):
    repo = SettingRepository(session)
    assert repo.cari_by_kunci("TIDAK_ADA") is None


def test_atur_menyimpan_dan_bisa_dibaca_kembali(session):
    id_admin = _buat_admin(session)
    repo = SettingRepository(session)

    repo.atur(kunci="JAM_MASUK", nilai="07:15:00", id_pengguna=id_admin, alasan="Seed awal")

    assert repo.cari_by_kunci("JAM_MASUK") == "07:15:00"


def test_atur_menimpa_nilai_lama(session):
    id_admin = _buat_admin(session)
    repo = SettingRepository(session)

    repo.atur(kunci="JAM_MASUK", nilai="07:15:00", id_pengguna=id_admin, alasan="Seed awal")
    repo.atur(kunci="JAM_MASUK", nilai="07:00:00", id_pengguna=id_admin, alasan="Koreksi jam masuk")

    assert repo.cari_by_kunci("JAM_MASUK") == "07:00:00"


def test_atur_tanpa_alasan_ditolak_dan_tidak_mengubah_nilai(session):
    id_admin = _buat_admin(session)
    repo = SettingRepository(session)
    repo.atur(kunci="JAM_MASUK", nilai="07:15:00", id_pengguna=id_admin, alasan="Seed awal")

    with pytest.raises(ValueError):
        repo.atur(kunci="JAM_MASUK", nilai="99:99:99", id_pengguna=id_admin, alasan="")

    assert repo.cari_by_kunci("JAM_MASUK") == "07:15:00"


def test_atur_tercatat_di_audit_log(session):
    id_admin = _buat_admin(session)
    repo = SettingRepository(session)
    audit_repo = AuditLogRepository(session)

    repo.atur(kunci="JAM_PULANG", nilai="13:30:00", id_pengguna=id_admin, alasan="Seed jam pulang")

    entri = audit_repo.daftar_terbaru(limit=1)[0]
    assert entri.aksi == "ubah_pengaturan"
    assert entri.nilai_sesudah == {"kunci": "JAM_PULANG", "nilai": "13:30:00"}
    assert entri.alasan == "Seed jam pulang"
