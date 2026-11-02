from datetime import date, datetime
from typing import Any

from sqlalchemy import JSON, Date, DateTime, ForeignKey, Index, String
from sqlalchemy.orm import Mapped, mapped_column

from app.core.timeutil import now_wita
from app.data.base import Base


class LaporanArsip(Base):
    """Satu laporan yang pernah dicetak (diekspor PDF) - PRD §9.3:
    "arsip PDF otomatis setiap laporan dicetak, cap waktu di setiap
    halaman". Ekspor Excel TIDAK diarsipkan di sini (§9.3 bicara soal
    "dicetak"/PDF - Excel murni kenyamanan unduh, bukan medium keabsahan
    laporan).

    ``waktu_cetak`` adalah patokan untuk layar "Koreksi Setelah Laporan
    Terbit" (lihat app.server.laporan_service.koreksi_setelah_terbit):
    koreksi apa pun pada baris attendance yang TERMASUK cakupan laporan
    ini (rentang tanggal + id_kelas/id_siswa) dengan waktu SETELAH
    ``waktu_cetak`` berarti laporan yang sudah dicetak itu kini basi.

    ``path_pdf`` path RELATIF terhadap direktori dasar (lihat
    app.server.laporan_storage, pola sama app.server.lampiran_storage),
    BUKAN isi berkas.
    """

    __tablename__ = "laporan_arsip"
    __table_args__ = (Index("ix_laporan_arsip_waktu_cetak", "waktu_cetak"),)

    id: Mapped[int] = mapped_column(primary_key=True)
    id_pengguna: Mapped[int] = mapped_column(ForeignKey("users.id"))
    label: Mapped[str] = mapped_column(String(120))
    tanggal_mulai: Mapped[date] = mapped_column(Date)
    tanggal_selesai: Mapped[date] = mapped_column(Date)
    id_kelas: Mapped[int | None] = mapped_column(ForeignKey("classes.id"))
    id_siswa: Mapped[int | None] = mapped_column(ForeignKey("students.id"))
    # Salinan penyaring lengkap (pola sama tindakan_massal.penyaring) -
    # kolom id_kelas/id_siswa di atas dipertahankan juga sebagai FK murni
    # supaya query "koreksi setelah terbit" bisa memakai indeks/JOIN,
    # bukan menggali dari JSON.
    penyaring: Mapped[dict[str, Any]] = mapped_column(JSON)
    path_pdf: Mapped[str] = mapped_column(String(255))
    waktu_cetak: Mapped[datetime] = mapped_column(DateTime, default=now_wita)
