import secrets
from datetime import datetime, timedelta

from sqlalchemy import select

from app.core.timeutil import now_wita
from app.data.models.system import SesiKlien
from app.data.repositories.base import BaseRepository

#: Panjang token buram, cukup untuk secrets.token_urlsafe menghasilkan
#: string unik yang praktis tidak bisa ditebak.
_PANJANG_TOKEN_BYTES = 32


class SesiKlienRepository(BaseRepository):
    """Akses data untuk sesi_klien - token yang dibawa satu klien setelah
    login (SKILL.md "Tiap klien membawa token sendiri")."""

    def buat(self, *, id_pengguna: int, umur: timedelta) -> SesiKlien:
        sekarang = now_wita()
        sesi = SesiKlien(
            token=secrets.token_urlsafe(_PANJANG_TOKEN_BYTES),
            id_pengguna=id_pengguna,
            dibuat=sekarang,
            kadaluarsa=sekarang + umur,
            terakhir_dipakai=sekarang,
            dicabut=False,
        )
        self._session.add(sesi)
        self._commit()
        return sesi

    def cari_by_token(self, token: str) -> SesiKlien | None:
        stmt = select(SesiKlien).where(SesiKlien.token == token)
        return self._session.execute(stmt).scalar_one_or_none()

    def sesi_aktif(self, token: str, *, sekarang: datetime | None = None) -> SesiKlien | None:
        """Sesi yang valid untuk dipakai sekarang: ada, belum dicabut, dan
        belum kadaluarsa. Tidak membedakan "tidak ada" dari "kadaluarsa/
        dicabut" - keduanya sama-sama berarti klien harus login ulang."""
        sesi = self.cari_by_token(token)
        if sesi is None or sesi.dicabut:
            return None
        if sesi.kadaluarsa <= (sekarang or now_wita()):
            return None
        return sesi

    def daftar_aktif(self, *, sekarang: datetime | None = None) -> list[SesiKlien]:
        """Sesi yang belum dicabut/kedaluwarsa, terbaru dipakai dulu -
        untuk status operasional Dashboard (PRD §14.1: "status koneksi
        tiap klien"). Ini proksi AKTIVITAS TERAKHIR (waktu request
        terautentikasi terakhir dari sesi itu, lihat perbarui_terakhir_dipakai
        di app.server.auth_service.verifikasi_token), BUKAN status
        koneksi jaringan real-time - tidak ada heartbeat terpisah, dan
        ini nilai paling jujur yang tersedia dari data yang ada."""
        stmt = (
            select(SesiKlien)
            .where(SesiKlien.dicabut.is_(False), SesiKlien.kadaluarsa > (sekarang or now_wita()))
            .order_by(SesiKlien.terakhir_dipakai.desc())
        )
        return list(self._session.execute(stmt).scalars())

    def perbarui_terakhir_dipakai(self, sesi: SesiKlien) -> None:
        sesi.terakhir_dipakai = now_wita()
        self._commit()

    def cabut(self, token: str) -> SesiKlien | None:
        sesi = self.cari_by_token(token)
        if sesi is None:
            return None
        sesi.dicabut = True
        self._commit()
        return sesi

    def cabut_semua_milik_pengguna(self, id_pengguna: int) -> int:
        """Dipanggil saat ganti password - sesi lama (di perangkat lain)
        tidak boleh terus berlaku memakai kata sandi yang sudah diganti."""
        stmt = select(SesiKlien).where(SesiKlien.id_pengguna == id_pengguna, SesiKlien.dicabut.is_(False))
        sesi_aktif = list(self._session.execute(stmt).scalars())
        for sesi in sesi_aktif:
            sesi.dicabut = True
        self._commit()
        return len(sesi_aktif)
