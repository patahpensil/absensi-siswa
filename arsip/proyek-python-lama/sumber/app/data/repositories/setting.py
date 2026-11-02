from app.core.timeutil import now_wita
from app.data.models.system import AuditLog, Setting
from app.data.repositories.audit_log import pastikan_alasan_terisi
from app.data.repositories.base import BaseRepository


class SettingRepository(BaseRepository):
    """Akses data untuk settings (pasangan kunci-nilai).

    atur() wajib menyertakan alasan - divalidasi lewat jalur yang sama
    dengan AttendanceRepository.koreksi() - dan selalu tercatat di
    audit_logs dalam transaksi yang sama dengan perubahan nilainya.
    """

    def cari_by_kunci(self, kunci: str) -> str | None:
        baris = self._session.get(Setting, kunci)
        return baris.nilai if baris is not None else None

    def atur(self, *, kunci: str, nilai: str, id_pengguna: int, alasan: str) -> Setting:
        alasan = pastikan_alasan_terisi(alasan)

        baris = self._session.get(Setting, kunci)
        nilai_sebelum = baris.nilai if baris is not None else None

        if baris is None:
            baris = Setting(kunci=kunci, nilai=nilai)
            self._session.add(baris)
        else:
            baris.nilai = nilai

        self._session.add(
            AuditLog(
                id_pengguna=id_pengguna,
                aksi="ubah_pengaturan",
                nilai_sebelum={"kunci": kunci, "nilai": nilai_sebelum},
                nilai_sesudah={"kunci": kunci, "nilai": nilai},
                alasan=alasan,
                waktu=now_wita(),
            )
        )
        self._commit()
        return baris
