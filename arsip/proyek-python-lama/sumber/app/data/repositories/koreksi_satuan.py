from datetime import datetime

from sqlalchemy import select

from app.core.timeutil import now_wita
from app.data.models.koreksi_satuan import KoreksiSatuan
from app.data.repositories.base import BaseRepository


class KoreksiSatuanTidakDitemukan(LookupError):
    pass


class KoreksiSatuanSudahDibatalkan(Exception):
    pass


class KoreksiSatuanRepository(BaseRepository):
    """Akses data untuk koreksi_satuan.

    Method yang menulis di sini SENGAJA tidak commit sendiri (kecuali
    commit() eksplisit) - satu koreksi menyentuh baris attendance DAN
    baris koreksi_satuan sekaligus, keduanya harus tersimpan dalam satu
    transaksi lewat app.server.koreksi_satuan_service (pola yang sama
    dengan MassCorrectionRepository).
    """

    def catat(
        self,
        *,
        id_absensi: int,
        id_pengguna: int,
        nilai_sebelum: dict,
        nilai_sesudah: dict,
        alasan: str,
    ) -> KoreksiSatuan:
        koreksi = KoreksiSatuan(
            id_absensi=id_absensi,
            id_pengguna=id_pengguna,
            nilai_sebelum=nilai_sebelum,
            nilai_sesudah=nilai_sesudah,
            alasan=alasan,
        )
        self._session.add(koreksi)
        self._session.flush()  # supaya koreksi.id terisi untuk dikembalikan sebelum commit
        return koreksi

    def cari_by_id(self, id_koreksi: int) -> KoreksiSatuan | None:
        return self._session.get(KoreksiSatuan, id_koreksi)

    def daftar_setelah(self, *, sejak: datetime, ids_absensi: list[int]) -> list[KoreksiSatuan]:
        """Koreksi satuan (tidak dibatalkan) dengan waktu SETELAH ``sejak``,
        untuk baris attendance dalam ``ids_absensi`` - dipakai layar
        Koreksi Setelah Laporan Terbit (app.server.laporan_service) untuk
        mengetahui laporan mana yang sudah basi karena ada koreksi
        belakangan. Koreksi yang sudah dibatalkan TIDAK dihitung - itu
        berarti nilainya sudah kembali seperti semula, laporan lama tetap
        akurat.
        """
        if not ids_absensi:
            return []
        stmt = (
            select(KoreksiSatuan)
            .where(
                KoreksiSatuan.waktu > sejak,
                KoreksiSatuan.id_absensi.in_(ids_absensi),
                KoreksiSatuan.dibatalkan.is_(False),
            )
            .order_by(KoreksiSatuan.waktu.desc())
        )
        return list(self._session.execute(stmt).scalars())

    def daftar_by_absensi(self, id_absensi: int, *, limit: int = 50) -> list[KoreksiSatuan]:
        """Riwayat koreksi UNTUK SATU baris attendance, terbaru dulu -
        dipakai layar Koreksi Satuan menampilkan histori + tombol batalkan
        untuk satu (siswa, tanggal)."""
        stmt = (
            select(KoreksiSatuan)
            .where(KoreksiSatuan.id_absensi == id_absensi)
            # id sebagai penentu kedua - dua koreksi bisa punya `waktu` yang
            # sama persis kalau tercatat dalam mikrodetik yang sama.
            .order_by(KoreksiSatuan.waktu.desc(), KoreksiSatuan.id.desc())
            .limit(limit)
        )
        return list(self._session.execute(stmt).scalars())

    def tandai_dibatalkan(self, koreksi: KoreksiSatuan, *, id_pengguna: int) -> KoreksiSatuan:
        koreksi.dibatalkan = True
        koreksi.waktu_dibatalkan = now_wita()
        koreksi.dibatalkan_oleh = id_pengguna
        return koreksi

    def commit(self) -> None:
        self._commit()
