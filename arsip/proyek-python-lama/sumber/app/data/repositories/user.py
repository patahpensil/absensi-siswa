from datetime import timedelta

from sqlalchemy import select

from app.core.timeutil import now_wita
from app.data.models.system import PeranPengguna, User
from app.data.repositories.base import BaseRepository

#: Salah 5 kali memberi jeda 30 detik (SKILL.md "Peran dan hak akses").
BATAS_PERCOBAAN_GAGAL = 5
JEDA_KUNCI_DETIK = 30


class UserTidakDitemukan(LookupError):
    pass


class UserRepository(BaseRepository):
    def tambah(
        self,
        *,
        username: str,
        password_hash: str,
        peran: PeranPengguna,
        id_kelas_diampu: int | None = None,
        wajib_ganti_password: bool = True,
    ) -> User:
        pengguna = User(
            username=username,
            password_hash=password_hash,
            peran=peran,
            id_kelas_diampu=id_kelas_diampu,
            aktif=True,
            wajib_ganti_password=wajib_ganti_password,
        )
        self._session.add(pengguna)
        self._commit()
        return pengguna

    def cari_by_id(self, id_pengguna: int) -> User | None:
        return self._session.get(User, id_pengguna)

    def cari_by_username(self, username: str) -> User | None:
        stmt = select(User).where(User.username == username)
        return self._session.execute(stmt).scalar_one_or_none()

    def catat_login(self, id_pengguna: int) -> User | None:
        """Login berhasil - catat waktunya dan reset penghitung percobaan
        gagal (kalau sebelumnya sempat salah beberapa kali tapi belum
        sampai terkunci)."""
        pengguna = self._session.get(User, id_pengguna)
        if pengguna is None:
            return None
        pengguna.waktu_login_terakhir = now_wita()
        pengguna.percobaan_gagal_berturut = 0
        pengguna.terkunci_sampai = None
        self._commit()
        return pengguna

    def catat_login_gagal(self, id_pengguna: int) -> User | None:
        """Tambah penghitung percobaan gagal; pada percobaan ke-5 (dan
        setiap kelipatannya kalau tetap dicoba selagi terkunci) kunci
        30 detik dari sekarang."""
        pengguna = self._session.get(User, id_pengguna)
        if pengguna is None:
            return None
        pengguna.percobaan_gagal_berturut += 1
        if pengguna.percobaan_gagal_berturut >= BATAS_PERCOBAAN_GAGAL:
            pengguna.terkunci_sampai = now_wita() + timedelta(seconds=JEDA_KUNCI_DETIK)
        self._commit()
        return pengguna

    def atur_password(self, id_pengguna: int, *, password_hash: str, wajib_ganti_password: bool = False) -> User:
        pengguna = self._session.get(User, id_pengguna)
        if pengguna is None:
            raise UserTidakDitemukan(id_pengguna)
        pengguna.password_hash = password_hash
        pengguna.wajib_ganti_password = wajib_ganti_password
        self._commit()
        return pengguna

    def atur_kelas_diampu(self, id_pengguna: int, *, id_kelas_diampu: int | None) -> User:
        """Penetapan/pemindahan wali kelas ke kelas (PRD §10). Hanya
        mengubah kolom ini - tidak memeriksa peran pengguna, itu tanggung
        jawab pemanggil (app.server.pengguna_service.PenggunaService)."""
        pengguna = self._session.get(User, id_pengguna)
        if pengguna is None:
            raise UserTidakDitemukan(id_pengguna)
        pengguna.id_kelas_diampu = id_kelas_diampu
        self._commit()
        return pengguna

    def daftar(self, *, hanya_aktif: bool = True) -> list[User]:
        stmt = select(User)
        if hanya_aktif:
            stmt = stmt.where(User.aktif.is_(True))
        return list(self._session.execute(stmt).scalars())
