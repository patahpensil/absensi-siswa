"""Layanan data master: siswa, guru, kelas, dan siklus hidup kartu RFID
(PRD §11, §12). Lapisan integrasi DB (sama seperti app.server.auth_service
dan app.core.koreksi_massal_service) - orkestrasi repository + audit log,
BUKAN aturan absensi. Tidak mengimpor app.core.engine/mesin_absensi sama
sekali; pendaftaran kartu di sini murni menyimpan identitas UID<->pemilik,
tidak pernah memproses klasifikasi kehadiran.

Setiap tulis di sini melewati AuditLogRepository.catat(), yang MEWAJIBKAN
alasan tidak kosong (app.data.repositories.audit_log.pastikan_alasan_terisi)
- aturan itu dibangun untuk koreksi absensi, bukan untuk sekadar mengisi
data master. Supaya layar input tetap CEPAT sesuai PRD §11.4 (tabel isian,
Tab/Enter, tanpa dialog alasan di setiap baris), _alasan_efektif() memberi
teks bawaan yang jujur menggambarkan aksinya kalau pengguna tidak mengisi
alasan sendiri - bukan mengarang justifikasi, hanya mencatat APA yang
terjadi supaya log audit tetap terisi.
"""

from __future__ import annotations

from dataclasses import dataclass

from app.core.timeutil import now_wita
from app.data.models.core import JenisKelamin, RfidCard, StatusKartu, Student, Teacher
from app.data.models.enums import JenisPemilik
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.guardian import GuardianRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.rfid_card import RfidCardRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.teacher import TeacherRepository

HUBUNGAN_BAWAAN = "Orang Tua/Wali"


def _alasan_efektif(alasan: str | None, bawaan: str) -> str:
    return alasan.strip() if alasan and alasan.strip() else bawaan


class NisnSudahDipakai(Exception):
    pass


class NikSudahDipakai(Exception):
    pass


class NipSudahDipakai(Exception):
    pass


class SiswaTidakDitemukan(LookupError):
    pass


class GuruTidakDitemukan(LookupError):
    pass


class KelasTidakDitemukan(LookupError):
    pass


class KartuTidakDitemukan(LookupError):
    pass


class UidKartuSudahDipakai(Exception):
    pass


class PemilikTidakDitemukan(LookupError):
    pass


class SiswaService:
    def __init__(
        self,
        *,
        student_repo: StudentRepository,
        guardian_repo: GuardianRepository,
        kelas_repo: KelasRepository,
        audit_repo: AuditLogRepository,
    ) -> None:
        self._student_repo = student_repo
        self._guardian_repo = guardian_repo
        self._kelas_repo = kelas_repo
        self._audit_repo = audit_repo

    def _pastikan_kelas_ada(self, id_kelas: int | None) -> None:
        if id_kelas is not None and self._kelas_repo.cari_by_id(id_kelas) is None:
            raise KelasTidakDitemukan(id_kelas)

    def buat(self, *, data, id_pengguna: int) -> Student:
        if self._student_repo.cari_by_nisn(data.nisn) is not None:
            raise NisnSudahDipakai(data.nisn)
        if data.nik is not None and self._student_repo.cari_by_nik(data.nik) is not None:
            raise NikSudahDipakai(data.nik)
        self._pastikan_kelas_ada(data.id_kelas)

        siswa = self._student_repo.tambah(
            nisn=data.nisn,
            nipd=data.nipd,
            nama_lengkap=data.nama_lengkap,
            id_kelas=data.id_kelas,
            jenis_kelamin=JenisKelamin(data.jenis_kelamin),
            tempat_lahir=data.tempat_lahir,
            tanggal_lahir=data.tanggal_lahir,
            nik=data.nik,
            alamat=data.alamat,
        )
        self._guardian_repo.tambah(
            id_siswa=siswa.id,
            nama=data.nama_ortu,
            hubungan=HUBUNGAN_BAWAAN,
            nomor_whatsapp=data.nomor_whatsapp_ortu,
            menerima_notifikasi=data.nomor_whatsapp_ortu is not None,
        )
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="buat_siswa",
            nilai_sebelum=None,
            nilai_sesudah={"id": siswa.id, "nisn": siswa.nisn, "nama_lengkap": siswa.nama_lengkap},
            alasan=_alasan_efektif(None, f"Siswa {data.nama_lengkap} ({data.nisn}) dibuat lewat data master"),
        )
        return siswa

    def ubah(self, id_siswa: int, *, data, id_pengguna: int) -> Student:
        siswa = self._student_repo.cari_by_id(id_siswa)
        if siswa is None:
            raise SiswaTidakDitemukan(id_siswa)
        if data.nik is not None and data.nik != siswa.nik:
            existing = self._student_repo.cari_by_nik(data.nik)
            if existing is not None and existing.id != id_siswa:
                raise NikSudahDipakai(data.nik)
        self._pastikan_kelas_ada(data.id_kelas)

        nilai_sebelum = {"nama_lengkap": siswa.nama_lengkap, "id_kelas": siswa.id_kelas}
        siswa = self._student_repo.ubah(
            id_siswa,
            nama_lengkap=data.nama_lengkap,
            jenis_kelamin=JenisKelamin(data.jenis_kelamin),
            tempat_lahir=data.tempat_lahir,
            tanggal_lahir=data.tanggal_lahir,
            alamat=data.alamat,
            id_kelas=data.id_kelas,
            nipd=data.nipd,
            nik=data.nik,
        )

        wali = self._guardian_repo.daftar_by_siswa(id_siswa)
        if wali:
            self._guardian_repo.ubah(
                wali[0].id,
                nama=data.nama_ortu,
                hubungan=wali[0].hubungan,
                nomor_whatsapp=data.nomor_whatsapp_ortu,
            )
        else:
            self._guardian_repo.tambah(
                id_siswa=id_siswa,
                nama=data.nama_ortu,
                hubungan=HUBUNGAN_BAWAAN,
                nomor_whatsapp=data.nomor_whatsapp_ortu,
                menerima_notifikasi=data.nomor_whatsapp_ortu is not None,
            )

        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="ubah_siswa",
            nilai_sebelum=nilai_sebelum,
            nilai_sesudah={"nama_lengkap": siswa.nama_lengkap, "id_kelas": siswa.id_kelas},
            alasan=_alasan_efektif(None, f"Data siswa {siswa.nama_lengkap} diubah lewat data master"),
        )
        return siswa

    def cari_by_id(self, id_siswa: int) -> Student | None:
        return self._student_repo.cari_by_id(id_siswa)

    def daftar_aktif(self) -> list[Student]:
        return self._student_repo.daftar_aktif()

    def nama_ortu_dan_kontak(self, id_siswa: int) -> tuple[str | None, str | None]:
        wali = self._guardian_repo.daftar_by_siswa(id_siswa)
        if not wali:
            return None, None
        return wali[0].nama, wali[0].nomor_whatsapp


class GuruService:
    def __init__(self, *, teacher_repo: TeacherRepository, audit_repo: AuditLogRepository) -> None:
        self._teacher_repo = teacher_repo
        self._audit_repo = audit_repo

    def buat(self, *, data, id_pengguna: int) -> Teacher:
        if data.nip is not None and self._teacher_repo.cari_by_nip(data.nip) is not None:
            raise NipSudahDipakai(data.nip)
        guru = self._teacher_repo.tambah(
            nama_lengkap=data.nama_lengkap, nip=data.nip, pangkat_jabatan=data.pangkat_jabatan
        )
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="buat_guru",
            nilai_sebelum=None,
            nilai_sesudah={"id": guru.id, "nama_lengkap": guru.nama_lengkap},
            alasan=_alasan_efektif(None, f"Guru {data.nama_lengkap} dibuat lewat data master"),
        )
        return guru

    def ubah(self, id_guru: int, *, data, id_pengguna: int) -> Teacher:
        guru = self._teacher_repo.cari_by_id(id_guru)
        if guru is None:
            raise GuruTidakDitemukan(id_guru)
        if data.nip is not None and data.nip != guru.nip:
            existing = self._teacher_repo.cari_by_nip(data.nip)
            if existing is not None and existing.id != id_guru:
                raise NipSudahDipakai(data.nip)

        nilai_sebelum = {"nama_lengkap": guru.nama_lengkap, "nip": guru.nip}
        guru = self._teacher_repo.ubah(
            id_guru, nama_lengkap=data.nama_lengkap, nip=data.nip, pangkat_jabatan=data.pangkat_jabatan
        )
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="ubah_guru",
            nilai_sebelum=nilai_sebelum,
            nilai_sesudah={"nama_lengkap": guru.nama_lengkap, "nip": guru.nip},
            alasan=_alasan_efektif(None, f"Data guru {guru.nama_lengkap} diubah lewat data master"),
        )
        return guru

    def cari_by_id(self, id_guru: int) -> Teacher | None:
        return self._teacher_repo.cari_by_id(id_guru)

    def daftar(self) -> list[Teacher]:
        return self._teacher_repo.daftar()


class KelasService:
    def __init__(
        self, *, kelas_repo: KelasRepository, teacher_repo: TeacherRepository, audit_repo: AuditLogRepository
    ) -> None:
        self._kelas_repo = kelas_repo
        self._teacher_repo = teacher_repo
        self._audit_repo = audit_repo

    def _pastikan_wali_ada(self, wali_kelas_id: int | None) -> None:
        if wali_kelas_id is not None and self._teacher_repo.cari_by_id(wali_kelas_id) is None:
            raise GuruTidakDitemukan(wali_kelas_id)

    def buat(self, *, data, id_pengguna: int):
        self._pastikan_wali_ada(data.wali_kelas_id)
        kelas = self._kelas_repo.tambah(
            tingkat=data.tingkat,
            nama_kelas=data.nama_kelas,
            tahun_ajaran=data.tahun_ajaran,
            wali_kelas_id=data.wali_kelas_id,
        )
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="buat_kelas",
            nilai_sebelum=None,
            nilai_sesudah={"id": kelas.id, "nama_kelas": kelas.nama_kelas},
            alasan=_alasan_efektif(None, f"Kelas {data.nama_kelas} dibuat lewat data master"),
        )
        return kelas

    def ubah(self, id_kelas: int, *, data, id_pengguna: int):
        kelas = self._kelas_repo.cari_by_id(id_kelas)
        if kelas is None:
            raise KelasTidakDitemukan(id_kelas)
        self._pastikan_wali_ada(data.wali_kelas_id)

        nilai_sebelum = {"nama_kelas": kelas.nama_kelas, "wali_kelas_id": kelas.wali_kelas_id}
        kelas = self._kelas_repo.ubah(
            id_kelas,
            tingkat=data.tingkat,
            nama_kelas=data.nama_kelas,
            tahun_ajaran=data.tahun_ajaran,
            wali_kelas_id=data.wali_kelas_id,
        )
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="ubah_kelas",
            nilai_sebelum=nilai_sebelum,
            nilai_sesudah={"nama_kelas": kelas.nama_kelas, "wali_kelas_id": kelas.wali_kelas_id},
            alasan=_alasan_efektif(None, f"Data kelas {kelas.nama_kelas} diubah lewat data master"),
        )
        return kelas

    def arsipkan(self, id_kelas: int, *, id_pengguna: int):
        """Hapus fisik bila kosong, arsipkan (aktif=False) bila masih ada
        siswa aktif - lihat KelasRepository.hapus_atau_nonaktifkan."""
        kelas = self._kelas_repo.cari_by_id(id_kelas)
        if kelas is None:
            raise KelasTidakDitemukan(id_kelas)
        nama = kelas.nama_kelas
        hasil = self._kelas_repo.hapus_atau_nonaktifkan(id_kelas)
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="arsipkan_kelas",
            nilai_sebelum={"id": id_kelas, "nama_kelas": nama},
            nilai_sesudah={"dihapus": hasil is None},
            alasan=_alasan_efektif(None, f"Kelas {nama} diarsipkan/dihapus lewat data master"),
        )
        return hasil

    def cari_by_id(self, id_kelas: int):
        return self._kelas_repo.cari_by_id(id_kelas)

    def daftar(self, *, hanya_aktif: bool = True):
        return self._kelas_repo.daftar(hanya_aktif=hanya_aktif)


class KartuService:
    def __init__(
        self,
        *,
        rfid_card_repo: RfidCardRepository,
        student_repo: StudentRepository,
        teacher_repo: TeacherRepository,
        audit_repo: AuditLogRepository,
    ) -> None:
        self._rfid_card_repo = rfid_card_repo
        self._student_repo = student_repo
        self._teacher_repo = teacher_repo
        self._audit_repo = audit_repo

    def _pastikan_pemilik_ada(self, *, id_pemilik: int, jenis_pemilik: JenisPemilik) -> None:
        if jenis_pemilik is JenisPemilik.SISWA:
            ada = self._student_repo.cari_by_id(id_pemilik) is not None
        else:
            ada = self._teacher_repo.cari_by_id(id_pemilik) is not None
        if not ada:
            raise PemilikTidakDitemukan(id_pemilik)

    def terbitkan(self, *, data, id_pengguna: int) -> RfidCard:
        """Kartu terikat ORANG bukan kelas (PRD §12.6) - id_pemilik di sini
        selalu id siswa/guru, tidak pernah id kelas. uid_mentah TIDAK
        disimpan di rfid_cards (kolomnya tidak diminta PRD §12 untuk tabel
        ini - beda dari attendance_events §17), tapi ikut tercatat di
        audit log demi fidelitas diagnosis."""
        jenis_pemilik = JenisPemilik(data.jenis_pemilik)
        self._pastikan_pemilik_ada(id_pemilik=data.id_pemilik, jenis_pemilik=jenis_pemilik)
        if self._rfid_card_repo.cari_by_uid(data.uid_kartu) is not None:
            raise UidKartuSudahDipakai(data.uid_kartu)

        kartu = self._rfid_card_repo.terbitkan(
            uid_kartu=data.uid_kartu,
            id_pemilik=data.id_pemilik,
            jenis_pemilik=jenis_pemilik,
            tanggal_terbit=data.tanggal_terbit or now_wita().date(),
        )
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="terbitkan_kartu",
            nilai_sebelum=None,
            nilai_sesudah={
                "id_kartu": kartu.id,
                "uid_kartu": kartu.uid_kartu,
                "uid_mentah": data.uid_mentah,
                "id_pemilik": kartu.id_pemilik,
                "jenis_pemilik": jenis_pemilik.value,
            },
            alasan=_alasan_efektif(None, "Kartu diterbitkan lewat data master"),
        )
        return kartu

    def nonaktifkan(self, id_kartu: int, *, alasan: str | None, id_pengguna: int) -> RfidCard:
        kartu = self._rfid_card_repo.cari_by_id(id_kartu)
        if kartu is None:
            raise KartuTidakDitemukan(id_kartu)
        status_sebelum = kartu.status
        kartu = self._rfid_card_repo.ubah_status(id_kartu, StatusKartu.NONAKTIF)
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="nonaktifkan_kartu",
            nilai_sebelum={"id_kartu": id_kartu, "status": status_sebelum.value},
            nilai_sesudah={"id_kartu": id_kartu, "status": StatusKartu.NONAKTIF.value},
            alasan=_alasan_efektif(alasan, "Kartu dinonaktifkan lewat data master"),
        )
        return kartu

    def cetak_ulang(self, id_kartu_lama: int, *, data, id_pengguna: int) -> RfidCard:
        """Kartu lama ditandai DIGANTI (tidak dihapus - PRD §12.6), kartu
        baru diterbitkan untuk pemilik yang sama."""
        lama = self._rfid_card_repo.cari_by_id(id_kartu_lama)
        if lama is None:
            raise KartuTidakDitemukan(id_kartu_lama)
        if self._rfid_card_repo.cari_by_uid(data.uid_kartu_baru) is not None:
            raise UidKartuSudahDipakai(data.uid_kartu_baru)

        self._rfid_card_repo.ubah_status(id_kartu_lama, StatusKartu.DIGANTI)
        baru = self._rfid_card_repo.terbitkan(
            uid_kartu=data.uid_kartu_baru,
            id_pemilik=lama.id_pemilik,
            jenis_pemilik=lama.jenis_pemilik,
            tanggal_terbit=now_wita().date(),
        )
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="cetak_ulang_kartu",
            nilai_sebelum={"id_kartu_lama": id_kartu_lama, "uid_kartu_lama": lama.uid_kartu},
            nilai_sesudah={
                "id_kartu_baru": baru.id,
                "uid_kartu_baru": baru.uid_kartu,
                "uid_mentah_baru": data.uid_mentah_baru,
            },
            alasan=_alasan_efektif(data.alasan, "Kartu dicetak ulang lewat data master"),
        )
        return baru

    def daftar_by_pemilik(self, *, id_pemilik: int, jenis_pemilik: JenisPemilik) -> list[RfidCard]:
        return self._rfid_card_repo.daftar_by_pemilik(id_pemilik, jenis_pemilik)

    def cek_uid(self, uid_kartu: str) -> "HasilCekUid":
        """Layar Uji Baca Kartu (PRD §18) dan langkah verifikasi Wizard
        Ganti Jalur (§12.4, lihat app.server.perangkat_service) - MURNI
        pencarian, tidak pernah memanggil app.core.engine.MesinAbsensi.
        proses_scan maupun membentuk baris absensi apa pun."""
        kartu = self._rfid_card_repo.cari_by_uid(uid_kartu)
        if kartu is None:
            return HasilCekUid(uid=uid_kartu, terdaftar=False, status=None, nama_pemilik=None)

        if kartu.jenis_pemilik is JenisPemilik.SISWA:
            pemilik = self._student_repo.cari_by_id(kartu.id_pemilik)
        else:
            pemilik = self._teacher_repo.cari_by_id(kartu.id_pemilik)
        return HasilCekUid(
            uid=uid_kartu,
            terdaftar=True,
            status=kartu.status.value,
            nama_pemilik=pemilik.nama_lengkap if pemilik is not None else None,
        )


@dataclass(frozen=True)
class HasilCekUid:
    uid: str
    terdaftar: bool
    status: str | None
    nama_pemilik: str | None
