"""Layanan Laporan (PRD §14.2, §9.3): agregasi rekap/detail absensi
siswa, ekspor Excel/PDF berkop sekolah, arsip PDF otomatis, dan
Koreksi Setelah Laporan Terbit. Lapisan integrasi DB (sama seperti
app.server.riwayat_scan_service). TIDAK menyentuh app.core.engine/
mesin_absensi - murni membaca attendance yang SUDAH dihitung, tidak
pernah menghitung ulang status.

Siswa SAJA - PRD §14.2 hanya menyebut "per kelas"/"per siswa", konsep
yang tidak berlaku untuk guru (sama seperti Koreksi Massal). Laporan
guru di luar cakupan slice ini, dicatat sebagai utang.

Dua bentuk keluaran (lihat app.core.laporan untuk penjelasan lengkap):
REKAP (harian/mingguan/bulanan/per kelas/seluruh sekolah - satu bentuk,
dibedakan rentang tanggal + saring kelas) dan DETAIL PER SISWA (log
harian satu siswa).

NIK TIDAK PERNAH disertakan di struktur data mana pun di sini (Aturan
Global #6, PRD §11.1) - bukan disamarkan, memang tidak pernah dipilih
dari Student sama sekali.

Lingkup peran (PRD §10, "Dashboard dan laporan"): kepala sekolah/admin
bebas; wali kelas HANYA kelasnya sendiri lewat
app.core.akses_kelas.pastikan_akses_kelas() (primitif yang sama dipakai
Riwayat Scan/Koreksi Satuan, bukan penjaga baru); operator TIDAK
BERWENANG SAMA SEKALI - beda dari Riwayat Scan yang terbuka untuknya
(PRD §10: sel operator di baris "Dashboard dan laporan" hanya berisi
"riwayat scan").
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date, datetime
from pathlib import Path

from app.config.pengaturan import Pengaturan
from app.core.akses_kelas import pastikan_akses_kelas
from app.core.laporan import (
    AksesLaporanDitolak,
    BarisDetailHarian,
    HasilLaporan,
    LaporanDetailSiswa,
    RekapSiswa,
    hitung_rekap_siswa,
    pastikan_berwenang,
)
from app.core.status import Peran
from app.core.timeutil import now_wita
from app.data.models.laporan import LaporanArsip
from app.data.models.system import User
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.koreksi_satuan import KoreksiSatuanRepository
from app.data.repositories.laporan import LaporanRepository
from app.data.repositories.mass_correction import MassCorrectionRepository
from app.data.repositories.student import StudentRepository
from app.server import laporan_export
from app.server.laporan_storage import simpan_arsip_laporan


class SiswaTidakDitemukan(LookupError):
    pass


class KelasTidakDitemukan(LookupError):
    pass


class ArsipTidakDitemukan(LookupError):
    pass


class WaliKelasBelumDiampu(Exception):
    """Wali kelas belum ditugaskan ke kelas mana pun - tidak ada laporan
    yang boleh dilihat (bukan celah "tanpa kelas berarti bebas akses")."""


@dataclass(frozen=True)
class KoreksiSetelahTerbit:
    sumber: str  # "SATUAN" atau "MASSAL"
    id_absensi: int
    nama_siswa: str
    tanggal: date
    waktu_koreksi: datetime
    id_pengguna: int
    nilai_sebelum: dict
    nilai_sesudah: dict
    alasan: str


_NAMA_BULAN = (
    "", "Januari", "Februari", "Maret", "April", "Mei", "Juni", "Juli", "Agustus", "September", "Oktober",
    "November", "Desember",
)


def _format_tanggal(tanggal: date) -> str:
    """Portabel Windows/Linux - sengaja tidak memakai strftime('%-d ...')
    karena flag itu tidak didukung strftime bawaan Windows."""
    return f"{tanggal.day} {_NAMA_BULAN[tanggal.month]} {tanggal.year}"


def _format_rentang(tanggal_mulai: date, tanggal_selesai: date) -> str:
    if tanggal_mulai == tanggal_selesai:
        return _format_tanggal(tanggal_mulai)
    return f"{_format_tanggal(tanggal_mulai)} s.d. {_format_tanggal(tanggal_selesai)}"


class LaporanService:
    def __init__(
        self,
        *,
        laporan_repo: LaporanRepository,
        student_repo: StudentRepository,
        kelas_repo: KelasRepository,
        koreksi_satuan_repo: KoreksiSatuanRepository,
        mass_correction_repo: MassCorrectionRepository,
        pengaturan: Pengaturan,
    ) -> None:
        self._laporan_repo = laporan_repo
        self._student_repo = student_repo
        self._kelas_repo = kelas_repo
        self._koreksi_satuan_repo = koreksi_satuan_repo
        self._mass_correction_repo = mass_correction_repo
        self._pengaturan = pengaturan

    # ------------------------------------------------------------------
    # Agregasi
    # ------------------------------------------------------------------

    def hasilkan(
        self,
        *,
        pelaku: User,
        tanggal_mulai: date,
        tanggal_selesai: date,
        id_kelas: int | None,
        id_siswa: int | None,
    ) -> HasilLaporan:
        peran = Peran(pelaku.peran.value)
        pastikan_berwenang(peran)

        if id_siswa is not None:
            return self._hasilkan_detail_siswa(
                peran=peran, pelaku=pelaku, tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai,
                id_siswa=id_siswa,
            )
        return self._hasilkan_rekap(
            peran=peran, pelaku=pelaku, tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai,
            id_kelas=id_kelas,
        )

    def _hasilkan_detail_siswa(
        self, *, peran: Peran, pelaku: User, tanggal_mulai: date, tanggal_selesai: date, id_siswa: int
    ) -> HasilLaporan:
        siswa = self._student_repo.cari_by_id(id_siswa)
        if siswa is None:
            raise SiswaTidakDitemukan(id_siswa)

        if peran == Peran.WALI_KELAS:
            id_kelas_diampu = self._id_kelas_diampu_wajib(pelaku)
            pastikan_akses_kelas(peran=peran, id_kelas_diampu=id_kelas_diampu, id_kelas_target=siswa.id_kelas or -1)

        baris_absensi = self._laporan_repo.cari_absensi_untuk_laporan(
            tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_siswa=id_siswa
        )
        nama_kelas = siswa.kelas.nama_kelas if siswa.kelas is not None else None
        detail = LaporanDetailSiswa(
            nama_lengkap=siswa.nama_lengkap,
            nisn=siswa.nisn,
            nama_kelas=nama_kelas,
            baris=[
                BarisDetailHarian(
                    tanggal=b.tanggal,
                    status_masuk=b.status_masuk,
                    status_final=b.status_final,
                    alasan_final=b.alasan_final,
                )
                for b in baris_absensi
            ],
        )
        return HasilLaporan(
            label=f"Laporan Per Siswa - {siswa.nama_lengkap} ({_format_rentang(tanggal_mulai, tanggal_selesai)})",
            tanggal_mulai=tanggal_mulai,
            tanggal_selesai=tanggal_selesai,
            id_kelas=siswa.id_kelas,
            nama_kelas=nama_kelas,
            id_siswa=id_siswa,
            rekap=None,
            detail=detail,
        )

    def _hasilkan_rekap(
        self, *, peran: Peran, pelaku: User, tanggal_mulai: date, tanggal_selesai: date, id_kelas: int | None
    ) -> HasilLaporan:
        if peran == Peran.WALI_KELAS:
            id_kelas_diampu = self._id_kelas_diampu_wajib(pelaku)
            if id_kelas is not None:
                pastikan_akses_kelas(peran=peran, id_kelas_diampu=id_kelas_diampu, id_kelas_target=id_kelas)
            # Tanpa saring eksplisit, wali kelas selalu dibatasi ke
            # kelasnya sendiri (PRD §10) - tidak pernah lihat seluruh
            # sekolah lewat jalur ini.
            id_kelas = id_kelas_diampu

        nama_kelas: str | None = None
        if id_kelas is not None:
            kelas = self._kelas_repo.cari_by_id(id_kelas)
            if kelas is None:
                raise KelasTidakDitemukan(id_kelas)
            nama_kelas = kelas.nama_kelas

        baris_absensi = self._laporan_repo.cari_absensi_untuk_laporan(
            tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_kelas=id_kelas
        )

        per_siswa: dict[int, list[str | None]] = {}
        for b in baris_absensi:
            per_siswa.setdefault(b.id_pemilik, []).append(b.status_final)

        rekap: list[RekapSiswa] = []
        for id_siswa_baris, daftar_status in per_siswa.items():
            siswa = self._student_repo.cari_by_id(id_siswa_baris)
            if siswa is None:
                continue
            rekap.append(
                hitung_rekap_siswa(
                    id_siswa=siswa.id,
                    nama_lengkap=siswa.nama_lengkap,
                    nama_kelas=siswa.kelas.nama_kelas if siswa.kelas is not None else None,
                    daftar_status_final=daftar_status,
                )
            )
        rekap.sort(key=lambda r: (r.nama_kelas or "", r.nama_lengkap))

        lingkup = f" - Kelas {nama_kelas}" if nama_kelas else " - Seluruh Sekolah"
        return HasilLaporan(
            label=f"Laporan {_format_rentang(tanggal_mulai, tanggal_selesai)}{lingkup}",
            tanggal_mulai=tanggal_mulai,
            tanggal_selesai=tanggal_selesai,
            id_kelas=id_kelas,
            nama_kelas=nama_kelas,
            id_siswa=None,
            rekap=rekap,
            detail=None,
        )

    def _id_kelas_diampu_wajib(self, pelaku: User) -> int:
        if pelaku.id_kelas_diampu is None:
            raise WaliKelasBelumDiampu("Akun ini belum ditugaskan ke kelas mana pun")
        return pelaku.id_kelas_diampu

    # ------------------------------------------------------------------
    # Ekspor
    # ------------------------------------------------------------------

    def ekspor_excel(
        self, *, pelaku: User, tanggal_mulai: date, tanggal_selesai: date, id_kelas: int | None, id_siswa: int | None
    ) -> bytes:
        hasil = self.hasilkan(
            pelaku=pelaku, tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_kelas=id_kelas,
            id_siswa=id_siswa,
        )
        return laporan_export.buat_excel(hasil, identitas=self._pengaturan.identitas_sekolah())

    def ekspor_pdf(
        self,
        *,
        pelaku: User,
        tanggal_mulai: date,
        tanggal_selesai: date,
        id_kelas: int | None,
        id_siswa: int | None,
        laporan_dir: Path,
    ) -> tuple[bytes, LaporanArsip]:
        """Menghasilkan PDF SEKALIGUS mengarsipkannya (PRD §9.3: "arsip
        PDF otomatis setiap laporan dicetak") - tidak ada jalur ekspor
        PDF yang melewati arsip, supaya keabsahan laporan tidak bisa
        diam-diam dilewati.
        """
        hasil = self.hasilkan(
            pelaku=pelaku, tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_kelas=id_kelas,
            id_siswa=id_siswa,
        )
        waktu_cetak = now_wita()
        isi_pdf = laporan_export.buat_pdf(
            hasil, identitas=self._pengaturan.identitas_sekolah(), waktu_cetak=waktu_cetak,
            dicetak_oleh=pelaku.username,
        )
        path_relatif = simpan_arsip_laporan(isi_pdf, base_dir=laporan_dir)
        arsip = self._laporan_repo.catat_arsip(
            id_pengguna=pelaku.id,
            label=hasil.label,
            tanggal_mulai=tanggal_mulai,
            tanggal_selesai=tanggal_selesai,
            id_kelas=hasil.id_kelas,
            id_siswa=hasil.id_siswa,
            penyaring={
                "tanggal_mulai": tanggal_mulai.isoformat(),
                "tanggal_selesai": tanggal_selesai.isoformat(),
                "id_kelas": hasil.id_kelas,
                "id_siswa": hasil.id_siswa,
            },
            path_pdf=path_relatif,
            waktu_cetak=waktu_cetak,
        )
        return isi_pdf, arsip

    # ------------------------------------------------------------------
    # Arsip & Koreksi Setelah Laporan Terbit (PRD §9.3)
    # ------------------------------------------------------------------

    def daftar_arsip(self, *, pelaku: User) -> list[LaporanArsip]:
        peran = Peran(pelaku.peran.value)
        pastikan_berwenang(peran)
        if peran == Peran.WALI_KELAS:
            if pelaku.id_kelas_diampu is None:
                return []
            return self._laporan_repo.daftar_arsip(id_kelas=pelaku.id_kelas_diampu)
        return self._laporan_repo.daftar_arsip()

    def koreksi_setelah_terbit(self, *, pelaku: User, id_arsip: int) -> list[KoreksiSetelahTerbit]:
        peran = Peran(pelaku.peran.value)
        pastikan_berwenang(peran)

        arsip = self._laporan_repo.cari_arsip_by_id(id_arsip)
        if arsip is None:
            raise ArsipTidakDitemukan(id_arsip)

        if peran == Peran.WALI_KELAS:
            if arsip.id_kelas is None:
                # Arsip seluruh sekolah - tidak pernah untuk wali kelas.
                raise AksesLaporanDitolak(peran)
            id_kelas_diampu = self._id_kelas_diampu_wajib(pelaku)
            pastikan_akses_kelas(peran=peran, id_kelas_diampu=id_kelas_diampu, id_kelas_target=arsip.id_kelas)

        # Baris attendance dalam cakupan arsip ini SAAT INI (bisa berbeda
        # dari saat dicetak kalau ada siswa baru masuk kelas itu belakangan
        # - itu bukan "koreksi", jadi tidak relevan di sini, hanya dipakai
        # untuk tahu id_absensi mana yang perlu dicek riwayat koreksinya).
        baris_absensi = self._laporan_repo.cari_absensi_untuk_laporan(
            tanggal_mulai=arsip.tanggal_mulai,
            tanggal_selesai=arsip.tanggal_selesai,
            id_kelas=arsip.id_kelas if arsip.id_siswa is None else None,
            id_siswa=arsip.id_siswa,
        )
        peta_nama: dict[int, str] = {}
        peta_absensi: dict[int, date] = {}
        for b in baris_absensi:
            peta_absensi[b.id] = b.tanggal
        ids_absensi = list(peta_absensi.keys())

        hasil: list[KoreksiSetelahTerbit] = []

        for koreksi in self._koreksi_satuan_repo.daftar_setelah(sejak=arsip.waktu_cetak, ids_absensi=ids_absensi):
            nama = peta_nama.get(koreksi.id_absensi) or self._nama_siswa_untuk_absensi(koreksi.id_absensi)
            peta_nama[koreksi.id_absensi] = nama
            hasil.append(
                KoreksiSetelahTerbit(
                    sumber="SATUAN",
                    id_absensi=koreksi.id_absensi,
                    nama_siswa=nama,
                    tanggal=peta_absensi.get(koreksi.id_absensi, arsip.tanggal_mulai),
                    waktu_koreksi=koreksi.waktu,
                    id_pengguna=koreksi.id_pengguna,
                    nilai_sebelum=koreksi.nilai_sebelum,
                    nilai_sesudah=koreksi.nilai_sesudah,
                    alasan=koreksi.alasan,
                )
            )

        for baris_massal, tindakan in self._mass_correction_repo.daftar_baris_setelah(
            sejak=arsip.waktu_cetak, ids_absensi=ids_absensi
        ):
            nama = peta_nama.get(baris_massal.id_absensi) or self._nama_siswa_untuk_absensi(baris_massal.id_absensi)
            peta_nama[baris_massal.id_absensi] = nama
            hasil.append(
                KoreksiSetelahTerbit(
                    sumber="MASSAL",
                    id_absensi=baris_massal.id_absensi,
                    nama_siswa=nama,
                    tanggal=peta_absensi.get(baris_massal.id_absensi, arsip.tanggal_mulai),
                    waktu_koreksi=tindakan.waktu,
                    id_pengguna=tindakan.id_pengguna,
                    nilai_sebelum=baris_massal.nilai_sebelum,
                    nilai_sesudah=baris_massal.nilai_sesudah,
                    alasan=tindakan.alasan,
                )
            )

        hasil.sort(key=lambda k: k.waktu_koreksi, reverse=True)
        return hasil

    def _nama_siswa_untuk_absensi(self, id_absensi: int) -> str:
        """Baris attendance-nya sendiri tidak menyimpan nama - dicari
        lewat repo laporan yang sudah tahu join Attendance+Student."""
        return self._laporan_repo.cari_nama_siswa_untuk_absensi(id_absensi) or "(tidak diketahui)"
