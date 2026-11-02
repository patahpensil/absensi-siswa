from datetime import date
from enum import Enum

from sqlalchemy import Date, ForeignKey, Index, String, text
from sqlalchemy import Enum as SAEnum
from sqlalchemy.orm import Mapped, mapped_column, relationship

from app.data.base import Base
from app.data.models.enums import JenisPemilik


class JenisKelamin(str, Enum):
    L = "L"
    P = "P"


class StatusSiswa(str, Enum):
    AKTIF = "AKTIF"
    PINDAH = "PINDAH"
    LULUS = "LULUS"
    KELUAR = "KELUAR"


class StatusKartu(str, Enum):
    AKTIF = "AKTIF"
    HILANG = "HILANG"
    DIGANTI = "DIGANTI"
    NONAKTIF = "NONAKTIF"


class Kelas(Base):
    """Kelas atau rombongan belajar. Dibuat manual oleh admin (PRD §11.3:
    tingkat, nama rombel ketik bebas, wali kelas, tahun ajaran).

    Hapus hanya boleh bila tidak ada siswa aktif di dalamnya; kalau ada,
    tandai tidak aktif lewat kolom aktif - kelas ber-riwayat tak pernah
    dihapus, hanya diarsipkan.

    ``wali_kelas_id`` menunjuk identitas guru (fakta data master untuk
    laporan/cetak), TERPISAH dari ``User.id_kelas_diampu`` yang menegakkan
    hak akses login wali kelas - satu kelas bisa punya wali kelas tercatat
    di sini walau akun loginnya belum dibuat (§11.3: "wali kelas belum
    ada -> koreksi ditangani admin").
    """

    __tablename__ = "classes"

    id: Mapped[int] = mapped_column(primary_key=True)
    tingkat: Mapped[int]
    nama_kelas: Mapped[str] = mapped_column(String(20))
    wali_kelas_id: Mapped[int | None] = mapped_column(ForeignKey("teachers.id"))
    tahun_ajaran: Mapped[str] = mapped_column(String(20))
    aktif: Mapped[bool] = mapped_column(default=True)

    siswa: Mapped[list["Student"]] = relationship(back_populates="kelas")


class Student(Base):
    """Siswa. Tidak pernah dihapus fisik - riwayat absensinya harus tetap terbaca.

    Kolom sesuai PRD §11.1 persis. ``nisn`` adalah kunci utama (wajib,
    unik) - PRD tidak menyebut "NIS" sama sekali, hanya NISN dan NIPD;
    kolom ``nis`` lama (peninggalan draf data-model.md pra-PRD v2.0)
    dihapus lewat migrasi, tidak dipakai modul lain manapun.

    ``id_kelas`` nullable: siswa tanpa kelas masuk kelompok "Belum
    ditempatkan", absensinya tetap tercatat normal (§11.3).

    NIK tidak ditandai "Wajib" di tabel PRD (beda dari Nama/NISN/Tempat
    lahir/Tanggal lahir/Alamat/Nama ortu yang eksplisit "Wajib") - dibuat
    nullable+unik apa adanya sesuai bacaan literal PRD, bukan ditebak.
    """

    __tablename__ = "students"

    id: Mapped[int] = mapped_column(primary_key=True)
    nisn: Mapped[str] = mapped_column(String(10), unique=True)
    nipd: Mapped[str | None] = mapped_column(String(20))
    nama_lengkap: Mapped[str] = mapped_column(String(120))
    id_kelas: Mapped[int | None] = mapped_column(ForeignKey("classes.id"))
    jenis_kelamin: Mapped[JenisKelamin] = mapped_column(SAEnum(JenisKelamin, native_enum=False))
    tempat_lahir: Mapped[str] = mapped_column(String(80))
    tanggal_lahir: Mapped[date] = mapped_column(Date)
    nik: Mapped[str | None] = mapped_column(String(16), unique=True)
    alamat: Mapped[str] = mapped_column(String(255))
    status_siswa: Mapped[StatusSiswa] = mapped_column(
        SAEnum(StatusSiswa, native_enum=False), default=StatusSiswa.AKTIF
    )
    aktif: Mapped[bool] = mapped_column(default=True)

    kelas: Mapped["Kelas | None"] = relationship(back_populates="siswa")
    wali: Mapped[list["Guardian"]] = relationship(back_populates="siswa")


class Teacher(Base):
    """Guru dan tenaga administrasi (PRD §11.2: nama, NIP, pangkat/jabatan).

    TODO(PRD Bagian 34 butir 2 dan 3, draf lama): status kehadiran dan
    toleransi keterlambatan guru belum ditetapkan sekolah - ALPA tidak
    cocok untuk konteks kepegawaian. Jangan diberi enum/CHECK constraint
    sampai keputusan turun; ``status_kepegawaian`` disimpan sebagai teks
    bebas dulu dan SENGAJA TIDAK DISENTUH slice data master ini - topiknya
    beda dari ``pangkat_jabatan`` (PRD v2.0 §11.2, kolom baru) walau
    namanya mirip.
    """

    __tablename__ = "teachers"

    id: Mapped[int] = mapped_column(primary_key=True)
    nip: Mapped[str | None] = mapped_column(String(30), unique=True)
    nama_lengkap: Mapped[str] = mapped_column(String(120))
    pangkat_jabatan: Mapped[str | None] = mapped_column(String(80))
    status_kepegawaian: Mapped[str | None] = mapped_column(String(50))
    aktif: Mapped[bool] = mapped_column(default=True)


class Guardian(Base):
    """Orang tua atau wali. Satu siswa boleh punya lebih dari satu penerima notifikasi.

    ``nomor_whatsapp`` nullable: PRD §11.1 "Nomor telepon atau WA: boleh
    dikosongkan, dirapikan otomatis ke format baku" - hanya "nama salah
    satu orang tua" yang wajib, bukan nomornya.
    """

    __tablename__ = "guardians"

    id: Mapped[int] = mapped_column(primary_key=True)
    id_siswa: Mapped[int] = mapped_column(ForeignKey("students.id"))
    nama: Mapped[str] = mapped_column(String(120))
    hubungan: Mapped[str] = mapped_column(String(30))
    # Format +628xxxxxxxxxx, dinormalkan saat disimpan, bukan saat ditampilkan.
    nomor_whatsapp: Mapped[str | None] = mapped_column(String(20))
    menerima_notifikasi: Mapped[bool] = mapped_column(default=True)

    siswa: Mapped["Student"] = relationship(back_populates="wali")


class RfidCard(Base):
    """Kartu RFID. UID unik di seluruh tabel.

    Kartu yang diganti dinonaktifkan (status DIGANTI), tidak dihapus, supaya
    absensi lama tetap tertelusur.
    """

    __tablename__ = "rfid_cards"
    __table_args__ = (
        Index(
            "ix_rfid_cards_satu_aktif_per_pemilik",
            "id_pemilik",
            "jenis_pemilik",
            unique=True,
            sqlite_where=text("status = 'AKTIF'"),
        ),
    )

    id: Mapped[int] = mapped_column(primary_key=True)
    uid_kartu: Mapped[str] = mapped_column(String(64), unique=True)
    # Polimorfik: merujuk ke students.id atau teachers.id tergantung
    # jenis_pemilik. Tidak ada FK tunggal karena dua kemungkinan tabel induk.
    id_pemilik: Mapped[int]
    jenis_pemilik: Mapped[JenisPemilik] = mapped_column(SAEnum(JenisPemilik, native_enum=False))
    status: Mapped[StatusKartu] = mapped_column(SAEnum(StatusKartu, native_enum=False), default=StatusKartu.AKTIF)
    tanggal_terbit: Mapped[date] = mapped_column(Date)
