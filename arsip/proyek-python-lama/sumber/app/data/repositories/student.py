from datetime import date

from sqlalchemy import select

from app.data.models.core import JenisKelamin, Student, StatusSiswa
from app.data.repositories.base import BaseRepository


class StudentRepository(BaseRepository):
    def tambah(
        self,
        *,
        nisn: str,
        nama_lengkap: str,
        jenis_kelamin: JenisKelamin,
        tempat_lahir: str,
        tanggal_lahir: date,
        alamat: str,
        id_kelas: int | None = None,
        nipd: str | None = None,
        nik: str | None = None,
        status_siswa: StatusSiswa = StatusSiswa.AKTIF,
        aktif: bool = True,
    ) -> Student:
        siswa = Student(
            nisn=nisn,
            nipd=nipd,
            nama_lengkap=nama_lengkap,
            id_kelas=id_kelas,
            jenis_kelamin=jenis_kelamin,
            tempat_lahir=tempat_lahir,
            tanggal_lahir=tanggal_lahir,
            nik=nik,
            alamat=alamat,
            status_siswa=status_siswa,
            aktif=aktif,
        )
        self._session.add(siswa)
        self._commit()
        return siswa

    def ubah(
        self,
        id_siswa: int,
        *,
        nama_lengkap: str,
        jenis_kelamin: JenisKelamin,
        tempat_lahir: str,
        tanggal_lahir: date,
        alamat: str,
        id_kelas: int | None,
        nipd: str | None,
        nik: str | None,
    ) -> Student | None:
        """Tidak mengubah NISN (kunci utama, PRD §11.1) maupun status_siswa/aktif -
        keduanya punya alur perubahan tersendiri (koreksi/pergantian tahun ajaran)."""
        siswa = self._session.get(Student, id_siswa)
        if siswa is None:
            return None
        siswa.nama_lengkap = nama_lengkap
        siswa.jenis_kelamin = jenis_kelamin
        siswa.tempat_lahir = tempat_lahir
        siswa.tanggal_lahir = tanggal_lahir
        siswa.alamat = alamat
        siswa.id_kelas = id_kelas
        siswa.nipd = nipd
        siswa.nik = nik
        self._commit()
        return siswa

    def cari_by_id(self, id_siswa: int) -> Student | None:
        return self._session.get(Student, id_siswa)

    def cari_by_nisn(self, nisn: str) -> Student | None:
        stmt = select(Student).where(Student.nisn == nisn)
        return self._session.execute(stmt).scalar_one_or_none()

    def cari_by_nik(self, nik: str) -> Student | None:
        stmt = select(Student).where(Student.nik == nik)
        return self._session.execute(stmt).scalar_one_or_none()

    def daftar_by_kelas(self, id_kelas: int, *, hanya_aktif: bool = True) -> list[Student]:
        stmt = select(Student).where(Student.id_kelas == id_kelas)
        if hanya_aktif:
            stmt = stmt.where(Student.aktif.is_(True))
        return list(self._session.execute(stmt).scalars())

    def daftar_aktif(self) -> list[Student]:
        stmt = select(Student).where(Student.aktif.is_(True))
        return list(self._session.execute(stmt).scalars())
