from datetime import datetime

from sqlalchemy import select

from app.data.models.notification import JenisNotifikasi, NotificationQueue, StatusNotifikasi
from app.data.repositories.base import BaseRepository


class NotificationRepository(BaseRepository):
    def antrekan(
        self,
        *,
        id_absensi: int,
        id_penerima: int | None,
        nomor_tujuan: str,
        isi_pesan: str,
        jenis: JenisNotifikasi,
        kunci_idempotensi: str,
        waktu_kejadian: datetime,
        waktu_dijadwalkan: datetime | None = None,
        id_template: int | None = None,
    ) -> NotificationQueue:
        """Masukkan pesan ke antrean.

        Idempoten lewat kunci_idempotensi: kalau kunci sudah ada, baris
        yang sudah ada dikembalikan, bukan membuat baris baru - satu
        peristiwa absensi tidak pernah menghasilkan dua pesan.

        id_template mencatat template mana yang dipakai merakit isi_pesan
        (lihat app.notify.template.rakit_pesan) - boleh kosong untuk pesan
        yang tidak lewat template.
        """
        yang_sudah_ada = self.cari_by_kunci_idempotensi(kunci_idempotensi)
        if yang_sudah_ada is not None:
            return yang_sudah_ada

        notifikasi = NotificationQueue(
            id_absensi=id_absensi,
            id_penerima=id_penerima,
            nomor_tujuan=nomor_tujuan,
            isi_pesan=isi_pesan,
            id_template=id_template,
            jenis=jenis,
            status=StatusNotifikasi.PENDING,
            kunci_idempotensi=kunci_idempotensi,
            waktu_kejadian=waktu_kejadian,
            waktu_dijadwalkan=waktu_dijadwalkan,
            jumlah_percobaan=0,
        )
        self._session.add(notifikasi)
        self._commit()
        return notifikasi

    def cari_by_kunci_idempotensi(self, kunci_idempotensi: str) -> NotificationQueue | None:
        stmt = select(NotificationQueue).where(NotificationQueue.kunci_idempotensi == kunci_idempotensi)
        return self._session.execute(stmt).scalar_one_or_none()

    def daftar_pending_terjadwal(self, *, sebelum: datetime) -> list[NotificationQueue]:
        """PENDING yang sudah waktunya dikirim.

        waktu_dijadwalkan kosong berarti dikirim segera (mis. TERLAMBAT)
        - bukan berarti "tidak pernah". NULL diperlakukan sebagai sudah
        waktunya, bukan disaring keluar begitu saja seperti yang
        dilakukan SQL untuk NULL <= x.
        """
        stmt = select(NotificationQueue).where(
            NotificationQueue.status == StatusNotifikasi.PENDING,
            (NotificationQueue.waktu_dijadwalkan.is_(None)) | (NotificationQueue.waktu_dijadwalkan <= sebelum),
        )
        return list(self._session.execute(stmt).scalars())

    def daftar_untuk_diproses(self, *, sebelum: datetime, batas_percobaan: int) -> list[NotificationQueue]:
        """PENDING yang sudah waktunya, DITAMBAH FAILED yang jumlah
        percobaannya masih di bawah batas - baris pengambilan tunggal
        untuk satu putaran QueueProcessor, termasuk retry otomatis.

        FAILED yang sudah mencapai batas_percobaan tidak pernah muncul di
        sini lagi - berhenti dicoba secara permanen sampai ada tindakan
        manual (lihat tandai_untuk_dicoba_ulang).
        """
        stmt = select(NotificationQueue).where(
            (
                (NotificationQueue.status == StatusNotifikasi.PENDING)
                & (
                    (NotificationQueue.waktu_dijadwalkan.is_(None))
                    | (NotificationQueue.waktu_dijadwalkan <= sebelum)
                )
            )
            | (
                (NotificationQueue.status == StatusNotifikasi.FAILED)
                & (NotificationQueue.jumlah_percobaan < batas_percobaan)
            )
        )
        return list(self._session.execute(stmt).scalars())

    def daftar(
        self, *, status: StatusNotifikasi | None = None, jenis: JenisNotifikasi | None = None, limit: int = 200
    ) -> list[NotificationQueue]:
        """Telusuri antrean untuk layar Notifikasi (antrean/terkirim/gagal,
        PRD §18) - baca-saja, tidak dipakai app.notify.processor (yang
        punya jalur pengambilan sendiri lewat daftar_untuk_diproses()).

        Terbaru dulu (waktu_kejadian desc) supaya kejadian yang baru
        selalu terlihat tanpa perlu menggulir; ``limit`` mencegah layar
        memuat seluruh riwayat sekaligus di sekolah yang sudah lama
        berjalan.
        """
        stmt = select(NotificationQueue).order_by(NotificationQueue.waktu_kejadian.desc()).limit(limit)
        if status is not None:
            stmt = stmt.where(NotificationQueue.status == status)
        if jenis is not None:
            stmt = stmt.where(NotificationQueue.jenis == jenis)
        return list(self._session.execute(stmt).scalars())

    def tandai_terkirim(self, id_notifikasi: int, *, waktu_kirim: datetime) -> NotificationQueue | None:
        notifikasi = self._session.get(NotificationQueue, id_notifikasi)
        if notifikasi is None:
            return None
        notifikasi.status = StatusNotifikasi.SENT
        notifikasi.waktu_kirim = waktu_kirim
        self._commit()
        return notifikasi

    def tandai_gagal(self, id_notifikasi: int, *, pesan_galat: str) -> NotificationQueue | None:
        notifikasi = self._session.get(NotificationQueue, id_notifikasi)
        if notifikasi is None:
            return None
        notifikasi.status = StatusNotifikasi.FAILED
        notifikasi.pesan_galat = pesan_galat
        notifikasi.jumlah_percobaan += 1
        self._commit()
        return notifikasi

    def tandai_untuk_dicoba_ulang(self, id_notifikasi: int) -> NotificationQueue | None:
        """Kembalikan notifikasi FAILED ke PENDING supaya diambil lagi di
        putaran proses berikutnya. jumlah_percobaan dan pesan_galat
        sebelumnya dibiarkan sebagai riwayat, tidak direset.
        """
        notifikasi = self._session.get(NotificationQueue, id_notifikasi)
        if notifikasi is None:
            return None
        notifikasi.status = StatusNotifikasi.PENDING
        self._commit()
        return notifikasi

    def batalkan(self, id_notifikasi: int) -> NotificationQueue | None:
        notifikasi = self._session.get(NotificationQueue, id_notifikasi)
        if notifikasi is None:
            return None
        notifikasi.status = StatusNotifikasi.CANCELLED
        self._commit()
        return notifikasi
