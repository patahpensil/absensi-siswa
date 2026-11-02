"""Aturan murni modul Notifikasi WhatsApp (PRD §13): kewenangan
mengelola template/antrean/mode uji, dan pemilihan nomor tujuan efektif
saat mode uji aktif.

Fungsi murni - tidak menyentuh basis data maupun app.core.engine/
mesin_absensi. Modul ini TIDAK menghitung status kehadiran maupun
menentukan KAPAN notifikasi harus diantrekan (lihat README, "Utang
aktif" - pemicu nyata dari TERLAMBAT/ALPA pagi/ALPA pulang/Rekap
disengaja belum disambungkan pada slice yang menambahkan modul ini);
ia hanya menjawab "siapa boleh mengelola template/antrean/mode uji" dan
"pesan sebenarnya dikirim ke nomor mana saat mode uji aktif".
"""

from __future__ import annotations

from app.core.status import PERAN_NOTIFIKASI, Peran


class AksesNotifikasiDitolak(Exception):
    """Peran pemanggil bukan kepala sekolah atau admin (PRD §18, tabel
    "Template dan antrean WhatsApp")."""

    def __init__(self, peran: Peran) -> None:
        self.peran = peran
        super().__init__(f"Peran {peran.value} tidak berwenang mengelola notifikasi")


class ModeUjiTanpaNomorAdmin(Exception):
    """Mode uji aktif tapi nomor admin belum diatur - jangan diam-diam
    mengirim ke nomor tujuan asli, itu justru pelanggaran PRD §13.3."""


def pastikan_berwenang(peran: Peran) -> None:
    if peran not in PERAN_NOTIFIKASI:
        raise AksesNotifikasiDitolak(peran)


def tujuan_efektif(nomor_asli: str, *, mode_uji_aktif: bool, nomor_admin_uji: str | None) -> str:
    """PRD §13.3: mode uji mengalihkan SELURUH pesan ke satu nomor admin.

    ``nomor_asli`` (nomor_tujuan yang sudah tersimpan di antrean) tidak
    pernah ditimpa di basis data - pengalihan ini murni terjadi pada saat
    kirim, supaya riwayat tetap menunjukkan penerima sesungguhnya begitu
    mode uji dimatikan kembali.
    """
    if not mode_uji_aktif:
        return nomor_asli
    if not nomor_admin_uji:
        raise ModeUjiTanpaNomorAdmin("Mode uji aktif tapi nomor admin belum diatur di Pengaturan Notifikasi")
    return nomor_admin_uji
