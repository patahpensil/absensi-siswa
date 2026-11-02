from datetime import datetime
from enum import Enum
from typing import Any

from sqlalchemy import JSON, DateTime, ForeignKey, String, Text
from sqlalchemy import Enum as SAEnum
from sqlalchemy.orm import Mapped, mapped_column

from app.core.timeutil import now_wita
from app.data.base import Base


class PeranPengguna(str, Enum):
    KEPALA_SEKOLAH = "KEPALA_SEKOLAH"
    ADMIN = "ADMIN"
    WALI_KELAS = "WALI_KELAS"
    OPERATOR = "OPERATOR"


class ModePerangkat(str, Enum):
    SERIAL = "SERIAL"
    HID = "HID"


class User(Base):
    """Pengguna aplikasi. Kata sandi disimpan hash (argon2), tidak pernah polos,
    tidak pernah bisa dilihat siapa pun termasuk kepala sekolah - hanya direset
    (SKILL.md "Peran dan hak akses").
    """

    __tablename__ = "users"

    id: Mapped[int] = mapped_column(primary_key=True)
    username: Mapped[str] = mapped_column(String(50), unique=True)
    password_hash: Mapped[str] = mapped_column(String(255))
    peran: Mapped[PeranPengguna] = mapped_column(SAEnum(PeranPengguna, native_enum=False))
    # Hanya diisi untuk peran wali_kelas. Pembatasan kelas ditegakkan di
    # lapisan data, bukan sekadar disembunyikan di antarmuka.
    id_kelas_diampu: Mapped[int | None] = mapped_column(ForeignKey("classes.id"))
    aktif: Mapped[bool] = mapped_column(default=True)
    waktu_login_terakhir: Mapped[datetime | None] = mapped_column(DateTime)
    # Kata sandi awal wajib diganti saat login pertama (SKILL.md).
    wajib_ganti_password: Mapped[bool] = mapped_column(default=True)
    # Salah 5 kali memberi jeda 30 detik (SKILL.md). Direset ke 0 begitu
    # login berhasil - lihat app.server.auth_service.
    percobaan_gagal_berturut: Mapped[int] = mapped_column(default=0)
    terkunci_sampai: Mapped[datetime | None] = mapped_column(DateTime)


class SesiKlien(Base):
    """Token yang dibawa satu klien setelah login (SKILL.md "Tiap klien
    membawa token sendiri"). Token buram (bukan JWT) - dicabut cukup
    dengan menghapus/menonaktifkan baris ini, tidak perlu daftar hitam
    terpisah.
    """

    __tablename__ = "sesi_klien"

    id: Mapped[int] = mapped_column(primary_key=True)
    token: Mapped[str] = mapped_column(String(64), unique=True)
    id_pengguna: Mapped[int] = mapped_column(ForeignKey("users.id"))
    dibuat: Mapped[datetime] = mapped_column(DateTime, default=now_wita)
    kadaluarsa: Mapped[datetime] = mapped_column(DateTime)
    terakhir_dipakai: Mapped[datetime] = mapped_column(DateTime, default=now_wita)
    dicabut: Mapped[bool] = mapped_column(default=False)


class Device(Base):
    """Pembaca RFID terdaftar."""

    __tablename__ = "devices"

    id: Mapped[int] = mapped_column(primary_key=True)
    nama: Mapped[str] = mapped_column(String(80))
    mode: Mapped[ModePerangkat] = mapped_column(SAEnum(ModePerangkat, native_enum=False))
    port: Mapped[str | None] = mapped_column(String(50))
    lokasi: Mapped[str | None] = mapped_column(String(120))
    aktif: Mapped[bool] = mapped_column(default=True)


class Setting(Base):
    """Pasangan kunci-nilai untuk seluruh konfigurasi.

    Toleransi, jendela scan berulang, waktu cutoff, waktu kirim notifikasi
    ALPA, penyedia WhatsApp yang dipakai, dan feature flag modul. Belum ada
    seed data - nilai jam masih menunggu keputusan sekolah.
    """

    __tablename__ = "settings"

    kunci: Mapped[str] = mapped_column(String(100), primary_key=True)
    nilai: Mapped[str | None] = mapped_column(Text)


class AuditLog(Base):
    """Jejak audit: siapa, apa, kapan, nilai sebelum, nilai sesudah, alasan."""

    __tablename__ = "audit_logs"

    id: Mapped[int] = mapped_column(primary_key=True)
    id_pengguna: Mapped[int | None] = mapped_column(ForeignKey("users.id"))
    aksi: Mapped[str] = mapped_column(String(100))
    nilai_sebelum: Mapped[dict[str, Any] | None] = mapped_column(JSON)
    nilai_sesudah: Mapped[dict[str, Any] | None] = mapped_column(JSON)
    alasan: Mapped[str | None] = mapped_column(Text)
    waktu: Mapped[datetime] = mapped_column(DateTime, default=now_wita)
