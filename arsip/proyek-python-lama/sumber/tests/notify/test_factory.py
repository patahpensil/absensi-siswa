from app.config.pengaturan import Pengaturan
from app.data.models.system import PeranPengguna
from app.data.repositories.setting import SettingRepository
from app.data.repositories.user import UserRepository
from app.notify.factory import buat_provider
from app.notify.log_provider import LogProvider
from app.notify.meta_cloud_provider import MetaCloudProvider
from app.notify.null_provider import NullProvider


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def test_modul_nonaktif_selalu_null_provider_apa_pun_pilihan_provider(session):
    id_admin = _buat_admin(session)
    pengaturan = Pengaturan(SettingRepository(session))
    pengaturan.atur_provider_whatsapp("LOG", id_pengguna=id_admin, alasan="Uji pilihan provider")

    # modul_whatsapp_aktif masih default False - belum diaktifkan admin.
    assert isinstance(buat_provider(pengaturan), NullProvider)


def test_modul_aktif_memakai_provider_log_dari_pengaturan(session):
    id_admin = _buat_admin(session)
    pengaturan = Pengaturan(SettingRepository(session))
    pengaturan.atur_modul_whatsapp_aktif(True, id_pengguna=id_admin, alasan="Anggaran cair")
    pengaturan.atur_provider_whatsapp("LOG", id_pengguna=id_admin, alasan="Pakai log dulu untuk uji coba")

    assert isinstance(buat_provider(pengaturan), LogProvider)


def test_modul_aktif_memakai_meta_cloud_dari_pengaturan(session):
    id_admin = _buat_admin(session)
    pengaturan = Pengaturan(SettingRepository(session))
    pengaturan.atur_modul_whatsapp_aktif(True, id_pengguna=id_admin, alasan="Anggaran cair")
    pengaturan.atur_provider_whatsapp("META_CLOUD", id_pengguna=id_admin, alasan="Pindah ke layanan resmi")

    assert isinstance(buat_provider(pengaturan), MetaCloudProvider)


def test_modul_aktif_tanpa_pilihan_provider_jatuh_ke_null(session):
    id_admin = _buat_admin(session)
    pengaturan = Pengaturan(SettingRepository(session))
    pengaturan.atur_modul_whatsapp_aktif(True, id_pengguna=id_admin, alasan="Anggaran cair")

    assert isinstance(buat_provider(pengaturan), NullProvider)
