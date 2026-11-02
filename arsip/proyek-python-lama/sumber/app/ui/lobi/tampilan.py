"""
Pemetaan hasil submit_scan -> tampilan layar absensi lobi.

Fungsi MURNI: tidak menyentuh Qt, httpx, atau thread apa pun - bisa diuji
tanpa QApplication (lihat tests/ui/lobi/test_modul_murni_tanpa_qt.py, pola
yang sama dengan tests/core/test_status.py untuk app/core/).

Memetakan TEPAT 5 nilai JenisEventScan yang benar-benar dikembalikan
app/server/routers/scan.py - bukan 8 nilai app.core.status.HasilScan.
Tiga di antaranya (KARTU_TIDAK_DIKENAL, KARTU_NONAKTIF, MANUAL_ENTRY) tidak
pernah dihasilkan app.core.mesin_absensi.klasifikasi_scan() saat ini
(diverifikasi sebelum modul ini ditulis - lihat riwayat percakapan/PR).
Jangan menambah cabang untuk nilai HasilScan di sini; kalau nanti backend
benar-benar mengekspornya (utang dicatat di README), tambahkan cabang baru
persis seperti lima yang sudah ada, bukan menebak sekarang.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum

from app.data.models.attendance import JenisEventScan
from app.ui.lobi.klien_scan import HasilSubmit


class WarnaStatus(str, Enum):
    """Warna status - begitu ditetapkan, jangan pernah berubah arti di
    layar lain (ui-guidelines.md)."""

    HIJAU = "HIJAU"      # hadir / sukses positif
    KUNING = "KUNING"    # terlambat
    MERAH = "MERAH"      # gagal - kartu tidak dikenal, gagal kirim
    NETRAL = "NETRAL"    # baca sukses tapi tidak membentuk baris (duplikat, di luar jendela)


@dataclass(frozen=True)
class KartuTampilan:
    """Satu kartu hasil untuk satu jalur - apa yang dirender di grid lobi."""

    nama_tampil: str        # nama orang, atau pesan (mis. "Kartu tidak dikenal")
    keterangan: str         # baris kedua: status/pesan
    warna: WarnaStatus
    berbunyi: bool          # True hanya untuk kegagalan baca/kirim (§12.5 "gagal")
    tandai_jalur_gagal: bool
    inisial: str | None     # untuk placeholder foto - None kalau tak ada nama


def _inisial(nama: str | None) -> str | None:
    if not nama:
        return None
    kata = [k for k in nama.split() if k]
    huruf = "".join(k[0] for k in kata[:2])
    return huruf.upper() or None


def petakan(hasil: HasilSubmit) -> KartuTampilan:
    """Petakan satu HasilSubmit ke tampilannya.

    §12.5: sukses = SENYAP; gagal = berbunyi + tandai jalur. Bunyi HANYA
    untuk kegagalan baca kartu atau kegagalan lokal yang sungguhan (lihat
    cabang diantrekan di bawah) - duplikat, di luar jendela, DAN scan yang
    sekadar diantrekan (offline, akan dikirim ulang oleh forwarder) adalah
    keadaan yang tidak butuh perhatian segera operator, tetap di jalur
    senyap ("senyap = semua jalur lancar").
    """
    if hasil.diantrekan:
        # §21: absensi SUDAH tersimpan durabel di antrean lokal - ini
        # bekerja sesuai desain, BUKAN kegagalan alat, jadi TIDAK menandai
        # jalur maupun berbunyi. Indikator terpisah (bukan per-kartu) yang
        # memberi tahu operator soal backlog - lihat app.ui.lobi.layar.
        return KartuTampilan(
            nama_tampil="Tercatat",
            keterangan="Menunggu koneksi",
            warna=WarnaStatus.NETRAL,
            berbunyi=False,
            tandai_jalur_gagal=False,
            inisial=None,
        )

    if not hasil.terkirim:
        # Kegagalan yang lebih serius dari sekadar offline: tap ini GAGAL
        # tersimpan bahkan di antrean lokal (lihat HasilSubmit.diantrekan).
        return KartuTampilan(
            nama_tampil="Gagal menyimpan scan",
            keterangan=hasil.pesan_galat or "Coba lagi",
            warna=WarnaStatus.MERAH,
            berbunyi=True,
            tandai_jalur_gagal=True,
            inisial=None,
        )

    nama = hasil.nama or "Tanpa nama"

    if hasil.jenis_event == JenisEventScan.UNKNOWN_CARD:
        return KartuTampilan(
            nama_tampil="Kartu tidak dikenal",
            keterangan=f"UID {hasil.uid_kartu}",
            warna=WarnaStatus.MERAH,
            berbunyi=True,
            tandai_jalur_gagal=True,
            inisial=None,
        )

    if hasil.jenis_event == JenisEventScan.DUPLICATE:
        return KartuTampilan(
            nama_tampil=nama,
            keterangan="Sudah tercatat",
            warna=WarnaStatus.NETRAL,
            berbunyi=False,
            tandai_jalur_gagal=False,
            inisial=_inisial(hasil.nama),
        )

    if hasil.jenis_event == JenisEventScan.DI_LUAR_PERIODE:
        return KartuTampilan(
            nama_tampil=nama,
            keterangan="Di luar jam absen",
            warna=WarnaStatus.NETRAL,
            berbunyi=False,
            tandai_jalur_gagal=False,
            inisial=_inisial(hasil.nama),
        )

    if hasil.jenis_event == JenisEventScan.PULANG:
        return KartuTampilan(
            nama_tampil=nama,
            keterangan="Absen pulang tercatat",
            warna=WarnaStatus.HIJAU,
            berbunyi=False,
            tandai_jalur_gagal=False,
            inisial=_inisial(hasil.nama),
        )

    if hasil.jenis_event == JenisEventScan.MASUK:
        if hasil.status_masuk == "HADIR":
            warna, keterangan = WarnaStatus.HIJAU, "Hadir"
        elif hasil.status_masuk == "TERLAMBAT":
            warna, keterangan = WarnaStatus.KUNING, "Terlambat"
        else:
            # Belum pernah teramati di data: MASUK hanya terjadi saat baris
            # baru dibuat (lihat catatan verifikasi di atas), yang
            # status_masuk-nya selalu HADIR/TERLAMBAT dari
            # tentukan_status_masuk[_guru]. Ditangani eksplisit, bukan
            # diam-diam dianggap salah satu warna sukses.
            warna, keterangan = WarnaStatus.NETRAL, hasil.status_masuk or "Tercatat"
        return KartuTampilan(
            nama_tampil=nama,
            keterangan=keterangan,
            warna=warna,
            berbunyi=False,
            tandai_jalur_gagal=False,
            inisial=_inisial(hasil.nama),
        )

    raise ValueError(f"jenis_event tidak dikenal: {hasil.jenis_event!r}")
