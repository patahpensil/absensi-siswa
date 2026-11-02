from sqlalchemy import select

from app.core.timeutil import now_wita
from app.data.models.koreksi_guru import KoreksiGuru
from app.data.repositories.base import BaseRepository


class KoreksiGuruTidakDitemukan(LookupError):
    pass


class KoreksiGuruSudahDibatalkan(Exception):
    pass


class KoreksiGuruRepository(BaseRepository):
    """Akses data untuk koreksi_guru.

    Method yang menulis di sini SENGAJA tidak commit sendiri (kecuali
    commit() eksplisit) - satu koreksi menyentuh baris attendance DAN
    baris koreksi_guru sekaligus, keduanya harus tersimpan dalam satu
    transaksi lewat app.server.koreksi_guru_service (pola yang sama
    dengan KoreksiSatuanRepository).
    """

    def catat(
        self,
        *,
        id_absensi: int,
        id_pengguna: int,
        nilai_sebelum: dict,
        nilai_sesudah: dict,
        alasan: str,
        lampiran: str | None = None,
    ) -> KoreksiGuru:
        koreksi = KoreksiGuru(
            id_absensi=id_absensi,
            id_pengguna=id_pengguna,
            nilai_sebelum=nilai_sebelum,
            nilai_sesudah=nilai_sesudah,
            alasan=alasan,
            lampiran=lampiran,
        )
        self._session.add(koreksi)
        self._session.flush()  # supaya koreksi.id terisi untuk dikembalikan sebelum commit
        return koreksi

    def cari_by_id(self, id_koreksi: int) -> KoreksiGuru | None:
        return self._session.get(KoreksiGuru, id_koreksi)

    def daftar_by_absensi(self, id_absensi: int, *, limit: int = 50) -> list[KoreksiGuru]:
        """Riwayat koreksi UNTUK SATU baris attendance, terbaru dulu -
        dipakai layar Koreksi Guru menampilkan histori + tombol batalkan
        untuk satu (guru, tanggal)."""
        stmt = (
            select(KoreksiGuru)
            .where(KoreksiGuru.id_absensi == id_absensi)
            # id sebagai penentu kedua - dua koreksi bisa punya `waktu` yang
            # sama persis kalau tercatat dalam mikrodetik yang sama.
            .order_by(KoreksiGuru.waktu.desc(), KoreksiGuru.id.desc())
            .limit(limit)
        )
        return list(self._session.execute(stmt).scalars())

    def tandai_dibatalkan(self, koreksi: KoreksiGuru, *, id_pengguna: int) -> KoreksiGuru:
        koreksi.dibatalkan = True
        koreksi.waktu_dibatalkan = now_wita()
        koreksi.dibatalkan_oleh = id_pengguna
        return koreksi

    def commit(self) -> None:
        self._commit()
