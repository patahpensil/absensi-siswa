"""Layanan manajemen akun & peran (PRD §10): pembuatan akun, reset
sandi, dan penetapan wali kelas ke kelas. Lapisan integrasi DB (sama
seperti app.server.auth_service dan app.server.data_master_service) -
orkestrasi repository + audit log. TIDAK menyentuh app.core.engine/
mesin_absensi sama sekali.

Hierarki wewenang pembuatan/reset akun (PRD §10, tabel "Peran dan Hak
Akses" - dikonfirmasi lewat PRD, BUKAN pembagian eksklusif seperti
paraphrase "kepsek buat admin&kepsek, admin buat wali kelas&operator"):
kepala sekolah berwenang atas KEEMPAT peran (termasuk admin dan kepala
sekolah lain - tabel PRD: "Membuat akun admin dan kepala sekolah: Kepsek
Ya, Admin —"); admin HANYA berwenang atas wali kelas dan operator ("Buat
akun wali kelas dan operator: Kepsek Ya, Admin Ya"). Dipakai sama untuk
reset_password() - wewenang mereset selalu mengikuti wewenang membuat.
"""

from __future__ import annotations

from app.data.models.system import PeranPengguna, User
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.session import SesiKlienRepository
from app.data.repositories.user import UserRepository
from app.server.security import hash_password

#: Peran pembuat/pelaku -> himpunan peran target yang boleh dibuat/direset.
HIRARKI_WEWENANG_AKUN: dict[PeranPengguna, frozenset[PeranPengguna]] = {
    PeranPengguna.KEPALA_SEKOLAH: frozenset(PeranPengguna),
    PeranPengguna.ADMIN: frozenset({PeranPengguna.WALI_KELAS, PeranPengguna.OPERATOR}),
}


class PeranTidakBerwenang(Exception):
    def __init__(self, pelaku: PeranPengguna, target: PeranPengguna) -> None:
        self.pelaku = pelaku
        self.target = target
        super().__init__(f"Peran {pelaku.value} tidak berwenang atas akun berperan {target.value}")


class UsernameSudahDipakai(Exception):
    pass


class KelasTidakDitemukan(LookupError):
    pass


class IdKelasDiampuHanyaUntukWaliKelas(Exception):
    pass


class PenggunaTidakDitemukan(LookupError):
    pass


def _wewenang(peran: PeranPengguna) -> frozenset[PeranPengguna]:
    return HIRARKI_WEWENANG_AKUN.get(peran, frozenset())


class PenggunaService:
    def __init__(
        self,
        *,
        user_repo: UserRepository,
        kelas_repo: KelasRepository,
        sesi_repo: SesiKlienRepository,
        audit_repo: AuditLogRepository,
    ) -> None:
        self._user_repo = user_repo
        self._kelas_repo = kelas_repo
        self._sesi_repo = sesi_repo
        self._audit_repo = audit_repo

    def buat_akun(
        self,
        *,
        pembuat: User,
        username: str,
        password_awal: str,
        peran: PeranPengguna,
        id_kelas_diampu: int | None = None,
    ) -> User:
        peran_pembuat = PeranPengguna(pembuat.peran.value)
        if peran not in _wewenang(peran_pembuat):
            raise PeranTidakBerwenang(peran_pembuat, peran)

        if id_kelas_diampu is not None:
            if peran != PeranPengguna.WALI_KELAS:
                raise IdKelasDiampuHanyaUntukWaliKelas()
            if self._kelas_repo.cari_by_id(id_kelas_diampu) is None:
                raise KelasTidakDitemukan(id_kelas_diampu)

        if self._user_repo.cari_by_username(username) is not None:
            raise UsernameSudahDipakai(username)

        pengguna = self._user_repo.tambah(
            username=username,
            password_hash=hash_password(password_awal),
            peran=peran,
            id_kelas_diampu=id_kelas_diampu,
        )
        self._audit_repo.catat(
            id_pengguna=pembuat.id,
            aksi="buat_akun",
            nilai_sebelum=None,
            nilai_sesudah={"id": pengguna.id, "username": username, "peran": peran.value},
            alasan=f"Akun {username} ({peran.value}) dibuat oleh {pembuat.username}",
        )
        return pengguna

    def reset_password(self, *, pelaku: User, id_target: int, password_baru: str) -> User:
        """Reset sandi TANPA mengetahui sandi lama - beda dari
        AuthService.ganti_password (self-service, wajib sandi lama).
        Sandi baru wajib diganti pada login berikutnya, dan seluruh sesi
        target dicabut - sama seperti perilaku ganti_password sendiri."""
        target = self._user_repo.cari_by_id(id_target)
        if target is None:
            raise PenggunaTidakDitemukan(id_target)

        peran_pelaku = PeranPengguna(pelaku.peran.value)
        peran_target = PeranPengguna(target.peran.value)
        if peran_target not in _wewenang(peran_pelaku):
            raise PeranTidakBerwenang(peran_pelaku, peran_target)

        self._user_repo.atur_password(id_target, password_hash=hash_password(password_baru), wajib_ganti_password=True)
        self._sesi_repo.cabut_semua_milik_pengguna(id_target)
        self._audit_repo.catat(
            id_pengguna=pelaku.id,
            aksi="reset_password",
            nilai_sebelum=None,
            nilai_sesudah={"id_target": id_target, "username_target": target.username},
            alasan=f"Sandi akun {target.username} direset oleh {pelaku.username}",
        )
        return self._user_repo.cari_by_id(id_target)

    def atur_kelas_diampu(self, *, pelaku: User, id_target: int, id_kelas_diampu: int | None) -> User:
        """Penetapan/pemindahan wali kelas ke kelas (PRD §10) - menyambung
        ke data kelas (app.data.repositories.kelas, slice Data Master)."""
        target = self._user_repo.cari_by_id(id_target)
        if target is None:
            raise PenggunaTidakDitemukan(id_target)
        if PeranPengguna(target.peran.value) != PeranPengguna.WALI_KELAS:
            raise IdKelasDiampuHanyaUntukWaliKelas()
        if id_kelas_diampu is not None and self._kelas_repo.cari_by_id(id_kelas_diampu) is None:
            raise KelasTidakDitemukan(id_kelas_diampu)

        nilai_sebelum = target.id_kelas_diampu
        diperbarui = self._user_repo.atur_kelas_diampu(id_target, id_kelas_diampu=id_kelas_diampu)
        self._audit_repo.catat(
            id_pengguna=pelaku.id,
            aksi="atur_kelas_diampu",
            nilai_sebelum={"id_kelas_diampu": nilai_sebelum},
            nilai_sesudah={"id_kelas_diampu": id_kelas_diampu},
            alasan=f"Kelas diampu {target.username} diubah oleh {pelaku.username}",
        )
        return diperbarui

    def cari_by_id(self, id_pengguna: int) -> User | None:
        return self._user_repo.cari_by_id(id_pengguna)

    def daftar(self, *, hanya_aktif: bool = True) -> list[User]:
        return self._user_repo.daftar(hanya_aktif=hanya_aktif)
