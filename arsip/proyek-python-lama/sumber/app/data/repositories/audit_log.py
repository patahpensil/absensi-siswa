from typing import Any

from sqlalchemy import select

from app.core.timeutil import now_wita
from app.data.models.system import AuditLog
from app.data.repositories.base import BaseRepository


def pastikan_alasan_terisi(alasan: str | None) -> str:
    """Satu-satunya jalur validasi alasan audit log.

    Dipakai baik oleh AuditLogRepository.catat() maupun repository lain
    yang menulis koreksi (mis. AttendanceRepository.koreksi()), supaya
    aturan "tanpa alasan, koreksi ditolak" tidak bisa diam-diam dilewati
    dari jalur lain. Gagal di sini, sebelum baris apa pun disentuh.
    """
    if alasan is None or not alasan.strip():
        raise ValueError("Audit log wajib menyertakan alasan, tidak boleh kosong")
    return alasan


class AuditLogRepository(BaseRepository):
    """Jalur terpusat untuk menulis audit_logs.

    Siapa, apa, kapan, nilai sebelum, nilai sesudah, dan alasan - lihat
    data-model.md. Konfigurasi, data siswa, jadwal, dan penggantian kartu
    juga memakai jalur ini saat perubahannya perlu diaudit.
    """

    def catat(
        self,
        *,
        id_pengguna: int | None,
        aksi: str,
        nilai_sebelum: dict[str, Any] | None,
        nilai_sesudah: dict[str, Any] | None,
        alasan: str,
    ) -> AuditLog:
        alasan = pastikan_alasan_terisi(alasan)
        entri = AuditLog(
            id_pengguna=id_pengguna,
            aksi=aksi,
            nilai_sebelum=nilai_sebelum,
            nilai_sesudah=nilai_sesudah,
            alasan=alasan,
            waktu=now_wita(),
        )
        self._session.add(entri)
        self._commit()
        return entri

    def cari_by_id(self, id_entri: int) -> AuditLog | None:
        return self._session.get(AuditLog, id_entri)

    def daftar_terbaru(self, *, limit: int = 50) -> list[AuditLog]:
        stmt = select(AuditLog).order_by(AuditLog.waktu.desc()).limit(limit)
        return list(self._session.execute(stmt).scalars())
