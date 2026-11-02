"""Layanan Notifikasi (PRD §13, menu §18 NOTIFIKASI): kelola empat
template pesan, telusuri antrean pengiriman, dan atur mode uji. Lingkup
kepala sekolah/admin SAJA (app.core.notifikasi.PERAN_NOTIFIKASI), sama
seperti Koreksi Massal dan Koreksi Guru.

KONEKSI WHATSAPP NYATA TETAP MATI (Fase 2, PRD §22) - layanan ini hanya
menyediakan struktur (template, antrean, mode uji). TIDAK mengantrekan
notifikasi (NotificationRepository.antrekan()) dari titik pemicu absensi
mana pun - LANGKAH 0 slice yang menambahkan berkas ini menemukan tidak
ada satu pun pemanggil produksi untuk antrekan(), hanya test. Menyambungkan
pemicu nyata (Terlambat setelah absensi tercatat, ALPA pagi 09:15, ALPA
pulang 60 menit setelah cutoff, Rekap sesuai pengaturan) DISENGAJA di
luar cakupan - itu menuntut menyentuh app.core.engine (Aturan Global #1),
dikonfirmasi pemilik proyek tetap sebagai utang terpisah (lihat README).
"""

from __future__ import annotations

from dataclasses import dataclass

from app.config.pengaturan import Pengaturan
from app.core.notifikasi import pastikan_berwenang
from app.core.status import Peran
from app.data.models.notification import JenisNotifikasi, MessageTemplate, NotificationQueue, StatusNotifikasi
from app.data.models.system import User
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.message_template import MessageTemplateRepository
from app.data.repositories.notification import NotificationRepository
from app.notify.template import VariabelTemplateTidakLengkap, rakit_pesan

#: Nilai contoh untuk pratinjau/validasi template sebelum disimpan -
#: HANYA dipakai memvalidasi bentuk placeholder, tidak pernah disimpan.
#: Selaras dengan variabel yang didokumentasikan app.notify.template.rakit_pesan
#: dan PRD §13.1 ("nama siswa, kelas, tanggal, dan jam").
VARIABEL_CONTOH: dict[str, str] = {
    "nama_siswa": "Budi Santoso",
    "kelas": "VIII. PASCAL",
    "tanggal": "9 September 2026",
    "jam": "07:22",
    "status": "Terlambat",
}


class TemplateTidakValid(Exception):
    """Konten template menyebut placeholder yang tidak dikenal (lihat
    VARIABEL_CONTOH) - ditolak sebelum tersimpan, bukan dibiarkan gagal
    diam-diam saat benar-benar dirakit untuk siswa sungguhan."""


@dataclass(frozen=True)
class TemplateSaatIni:
    """Satu baris untuk layar editor template - salah satu dari EMPAT
    JenisNotifikasi, konten/versi None berarti belum pernah diisi admin."""

    jenis: JenisNotifikasi
    konten: str | None
    versi: int | None
    aktif: bool


@dataclass(frozen=True)
class ModeUjiInfo:
    aktif: bool
    nomor_admin: str | None


class NotifikasiService:
    def __init__(
        self,
        *,
        template_repo: MessageTemplateRepository,
        notification_repo: NotificationRepository,
        audit_repo: AuditLogRepository,
        pengaturan: Pengaturan,
    ) -> None:
        self._template_repo = template_repo
        self._notification_repo = notification_repo
        self._audit_repo = audit_repo
        self._pengaturan = pengaturan

    # ------------------------------------------------------------------
    # Template (PRD §13.1)
    # ------------------------------------------------------------------

    def daftar_template(self, *, pelaku: User) -> list[TemplateSaatIni]:
        pastikan_berwenang(Peran(pelaku.peran.value))
        hasil: list[TemplateSaatIni] = []
        for jenis in JenisNotifikasi:
            aktif = self._template_repo.cari_aktif(jenis)
            hasil.append(
                TemplateSaatIni(
                    jenis=jenis,
                    konten=aktif.konten if aktif is not None else None,
                    versi=aktif.versi if aktif is not None else None,
                    aktif=aktif is not None,
                )
            )
        return hasil

    def ubah_template(self, *, pelaku: User, jenis: JenisNotifikasi, konten: str, alasan: str) -> MessageTemplate:
        """Selalu menambah versi baru (aktif=True), tidak pernah menimpa
        versi lama - MessageTemplateRepository.tambah() yang menonaktifkan
        versi lama secara otomatis, versi lama tetap ada untuk pesan yang
        sudah terlanjur memakainya."""
        pastikan_berwenang(Peran(pelaku.peran.value))
        self._pastikan_placeholder_dikenal(jenis, konten)

        versi_lama = self._template_repo.cari_aktif(jenis)
        versi_baru = (versi_lama.versi if versi_lama is not None else 0) + 1

        template = self._template_repo.tambah(jenis=jenis, versi=versi_baru, konten=konten, aktif=True)
        self._audit_repo.catat(
            id_pengguna=pelaku.id,
            aksi="ubah_template_notifikasi",
            nilai_sebelum={"jenis": jenis.value, "versi": versi_lama.versi, "konten": versi_lama.konten}
            if versi_lama is not None
            else None,
            nilai_sesudah={"jenis": jenis.value, "versi": template.versi, "konten": template.konten},
            alasan=alasan,
        )
        return template

    def _pastikan_placeholder_dikenal(self, jenis: JenisNotifikasi, konten: str) -> None:
        pratinjau = MessageTemplate(jenis=jenis, versi=0, konten=konten, aktif=False)
        try:
            rakit_pesan(pratinjau, VARIABEL_CONTOH)
        except VariabelTemplateTidakLengkap as exc:
            raise TemplateTidakValid(
                f"Template menyebut variabel yang tidak dikenal - variabel yang tersedia: "
                f"{', '.join(sorted(VARIABEL_CONTOH))}"
            ) from exc

    # ------------------------------------------------------------------
    # Antrean (PRD §13.2) - baca-saja
    # ------------------------------------------------------------------

    def daftar_antrean(self, *, pelaku: User, status: StatusNotifikasi | None = None) -> list[NotificationQueue]:
        pastikan_berwenang(Peran(pelaku.peran.value))
        return self._notification_repo.daftar(status=status)

    # ------------------------------------------------------------------
    # Mode uji (PRD §13.3)
    # ------------------------------------------------------------------

    def ambil_mode_uji(self, *, pelaku: User) -> ModeUjiInfo:
        pastikan_berwenang(Peran(pelaku.peran.value))
        return ModeUjiInfo(
            aktif=self._pengaturan.mode_uji_whatsapp_aktif(), nomor_admin=self._pengaturan.nomor_admin_mode_uji()
        )

    def ubah_mode_uji(
        self, *, pelaku: User, aktif: bool | None, nomor_admin: str | None, alasan: str
    ) -> ModeUjiInfo:
        """Nomor admin sudah dirapikan ke format baku oleh pemanggil
        (skema Pydantic, lihat app.server.schemas) sebelum sampai di sini
        - lapisan ini hanya menegakkan ATURANNYA: mode uji tidak boleh
        aktif tanpa nomor admin tersimpan (nomor baru ATAU yang sudah ada
        sebelumnya), supaya §13.3 tidak bisa diam-diam dilanggar dari
        endpoint ini."""
        pastikan_berwenang(Peran(pelaku.peran.value))

        if nomor_admin is not None:
            self._pengaturan.atur_nomor_admin_mode_uji(nomor_admin, id_pengguna=pelaku.id, alasan=alasan)

        aktif_final = aktif if aktif is not None else self._pengaturan.mode_uji_whatsapp_aktif()
        if aktif_final and not self._pengaturan.nomor_admin_mode_uji():
            raise ModeUjiTanpaNomorAdminTersimpan()

        if aktif is not None:
            self._pengaturan.atur_mode_uji_whatsapp_aktif(aktif, id_pengguna=pelaku.id, alasan=alasan)

        return self.ambil_mode_uji(pelaku=pelaku)


class ModeUjiTanpaNomorAdminTersimpan(Exception):
    """Percobaan mengaktifkan mode uji tanpa nomor admin yang tersimpan -
    ditolak di lapisan layanan (bukan hanya app.notify.processor saat
    kirim) supaya keadaan tidak konsisten ini tidak pernah tersimpan sama
    sekali (PRD §13.3: mode uji WAJIB dipakai sebelum diaktifkan ke orang
    tua - percuma kalau nomornya sendiri belum ada)."""
