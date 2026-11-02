from app.config.pengaturan import Pengaturan
from app.notify.log_provider import LogProvider
from app.notify.meta_cloud_provider import MetaCloudProvider
from app.notify.null_provider import NullProvider
from app.notify.provider import WhatsAppProvider


def buat_provider(pengaturan: Pengaturan) -> WhatsAppProvider:
    """Pilih provider dari app/config, bukan hardcode.

    modul_whatsapp_aktif() adalah saklar utama: kalau nonaktif, selalu
    NullProvider apa pun nilai provider_whatsapp() - modul benar-benar
    mati sampai anggaran tersedia, bukan cuma default yang bisa kebobolan
    lewat pengaturan lain.
    """
    if not pengaturan.modul_whatsapp_aktif():
        return NullProvider()

    nama_provider = pengaturan.provider_whatsapp()
    if nama_provider == "LOG":
        return LogProvider()
    if nama_provider == "META_CLOUD":
        return MetaCloudProvider()
    return NullProvider()
