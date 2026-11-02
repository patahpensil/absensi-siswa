from datetime import datetime

from app.config.pengaturan import Pengaturan
from app.core.engine import ke_baris_absensi
from app.core.mesin_absensi import masih_perlu_dikirim
from app.core.notifikasi import ModeUjiTanpaNomorAdmin, tujuan_efektif
from app.core.timeutil import now_wita
from app.data.models.notification import JenisNotifikasi, NotificationQueue
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.notification import NotificationRepository
from app.notify.provider import StatusKirim, WhatsAppProvider

#: Jenis yang berarti "siswa ini ALPA" - dicek ulang lewat masih_perlu_dikirim()
#: sebelum benar-benar dikirim (PRD §13.1: "koreksi dalam masa tunggu
#: otomatis membatalkan pengiriman"). ALPA_PAGI dan ALPA_PULANG sama-sama
#: berarti status_final == ALPA saat ini, jadi satu pemeriksaan yang sama
#: berlaku untuk keduanya - tidak perlu membedakan pagi/pulang di sini.
_JENIS_PERLU_CEK_ULANG_ALPA = (JenisNotifikasi.ALPA_PAGI, JenisNotifikasi.ALPA_PULANG)


class QueueProcessor:
    """Memproses notification_queue lewat repository saja - tidak pernah
    menyentuh Session atau tabel lain secara langsung.

    Tidak menyambung ke mesin absensi maupun scheduler; hanya menyediakan
    proses_satu_putaran(), yang sudah dipanggil app/scheduler/. Perubahan
    di sini tidak menambah pemicu waktu baru.
    """

    def __init__(
        self,
        *,
        notification_repo: NotificationRepository,
        attendance_repo: AttendanceRepository,
        provider: WhatsAppProvider,
        pengaturan: Pengaturan,
    ) -> None:
        self._notification_repo = notification_repo
        self._attendance_repo = attendance_repo
        self._provider = provider
        self._pengaturan = pengaturan

    def proses_satu_putaran(self, *, sekarang: datetime | None = None) -> list[NotificationQueue]:
        """Proses PENDING yang sudah waktunya, plus FAILED yang masih di
        bawah batas percobaan (retry otomatis).

        Untuk notifikasi ALPA, cek ulang dulu status absensi terkait
        sebelum mengirim lewat app.core.mesin_absensi.masih_perlu_dikirim -
        kalau siswa akhirnya scan atau statusnya sudah dikoreksi (status_final
        bukan lagi ALPA), notifikasi dibatalkan (CANCELLED), tidak pernah
        dikirim. Sengaja memeriksa status_final, bukan status_masuk - koreksi
        manual bisa mengubah status_final saja sementara status_masuk tetap
        apa adanya (lihat SKILL.md, "Absensi pulang dan ALPA pulang").

        Mengembalikan baris yang benar-benar diproses (SENT, FAILED, atau
        CANCELLED). Baris yang dilewati provider (mis. NullProvider saat
        modul nonaktif, status TERLEWAT) sengaja dibiarkan apa adanya -
        itu bukan pengiriman yang gagal maupun berhasil, cuma belum
        dicoba sungguhan.

        waktu_kejadian pada baris tidak pernah disentuh di sini - hanya
        waktu_kirim yang diisi saat berhasil, supaya keduanya tetap
        terpisah.

        Mode uji (PRD §13.3) dicek di sini lewat app.core.notifikasi.
        tujuan_efektif() - kalau aktif, SELURUH pesan dialihkan ke nomor
        admin saat benar-benar dikirim. nomor_tujuan yang tersimpan di
        baris tidak pernah ditimpa, jadi riwayat tetap menunjukkan
        penerima sesungguhnya begitu mode uji dimatikan. Mode uji aktif
        tanpa nomor admin diatur ditandai FAILED (bukan diam-diam
        dikirim ke nomor asli, dan bukan pula dibiarkan PENDING selamanya).
        """
        sekarang = sekarang or now_wita()
        batas_percobaan = self._pengaturan.batas_percobaan_notifikasi()
        diproses: list[NotificationQueue] = []

        kandidat = self._notification_repo.daftar_untuk_diproses(sebelum=sekarang, batas_percobaan=batas_percobaan)
        for item in kandidat:
            if item.jenis in _JENIS_PERLU_CEK_ULANG_ALPA and self._alpa_sudah_tidak_berlaku(item):
                baris = self._notification_repo.batalkan(item.id)
                if baris is not None:
                    diproses.append(baris)
                continue

            try:
                tujuan = tujuan_efektif(
                    item.nomor_tujuan,
                    mode_uji_aktif=self._pengaturan.mode_uji_whatsapp_aktif(),
                    nomor_admin_uji=self._pengaturan.nomor_admin_mode_uji(),
                )
            except ModeUjiTanpaNomorAdmin as exc:
                baris = self._notification_repo.tandai_gagal(item.id, pesan_galat=str(exc))
                if baris is not None:
                    diproses.append(baris)
                continue

            hasil = self._provider.send(
                to=tujuan,
                message=item.isi_pesan,
                idempotency_key=item.kunci_idempotensi,
            )

            if hasil.status == StatusKirim.TERKIRIM:
                baris = self._notification_repo.tandai_terkirim(item.id, waktu_kirim=now_wita())
            elif hasil.status == StatusKirim.GAGAL:
                baris = self._notification_repo.tandai_gagal(
                    item.id, pesan_galat=hasil.pesan_galat or "Provider melaporkan gagal tanpa keterangan"
                )
            else:  # StatusKirim.TERLEWAT
                continue

            if baris is not None:
                diproses.append(baris)

        return diproses

    def _alpa_sudah_tidak_berlaku(self, item: NotificationQueue) -> bool:
        absensi = self._attendance_repo.cari_by_id(item.id_absensi)
        if absensi is None:
            return True
        return not masih_perlu_dikirim(ke_baris_absensi(absensi))
