"""Format tampilan cakupan dan jenis tindakan untuk layar Riwayat
Tindakan Massal - murni, tanpa Qt.

"Kelas ID {n}" dan bukan nama kelas: layar ini sama seperti Riwayat Scan
dan Koreksi Satuan, memakai ID mentah untuk rujukan kelas/siswa (lihat
README, catatan slice Riwayat Scan) - bukan keterbatasan izin di sini,
melainkan konsistensi dengan pola yang sudah ada, supaya layar ini tidak
perlu bergantung ke endpoint Data Master hanya untuk memformat satu baris
riwayat.
"""

from __future__ import annotations

_LABEL_LINGKUP = {
    "SEKOLAH": "Seluruh sekolah",
}

_LABEL_JENIS_TINDAKAN = {
    "UBAH_STATUS": "Ubah Status",
    "TANDAI_IZIN_PULANG_CEPAT": "Tandai Izin Pulang Cepat",
    "BATALKAN_KONVERSI_ALPA_PULANG": "Batalkan Konversi Alpa Pulang",
}


def format_cakupan(penyaring: dict) -> str:
    mulai = penyaring.get("tanggal_mulai")
    selesai = penyaring.get("tanggal_selesai")
    rentang = mulai if mulai == selesai else f"{mulai} s/d {selesai}"

    lingkup = penyaring.get("lingkup")
    if lingkup == "TINGKAT":
        cakupan_orang = f"Tingkat {penyaring.get('tingkat')}"
    elif lingkup == "KELAS":
        cakupan_orang = f"Kelas ID {penyaring.get('id_kelas')}"
    elif lingkup == "SISWA_TERPILIH":
        jumlah = len(penyaring.get("id_siswa_terpilih") or [])
        cakupan_orang = f"{jumlah} siswa terpilih"
    else:
        cakupan_orang = _LABEL_LINGKUP.get(lingkup, lingkup or "-")

    status = ", ".join(penyaring.get("status_termasuk") or [])
    return f"{rentang} - {cakupan_orang} - Status: {status or '-'}"


def format_jenis_tindakan(jenis_tindakan: str, status_baru: str | None) -> str:
    label = _LABEL_JENIS_TINDAKAN.get(jenis_tindakan, jenis_tindakan)
    if jenis_tindakan == "UBAH_STATUS" and status_baru:
        return f"{label} -> {status_baru}"
    return label
