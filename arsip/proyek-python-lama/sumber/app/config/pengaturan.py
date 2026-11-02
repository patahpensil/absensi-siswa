from dataclasses import dataclass, field
from datetime import time

from app.config.keys import KunciPengaturan
from app.core.validasi_data_master import normalisasi_nomor_whatsapp
from app.data.repositories.setting import SettingRepository

# Hari dalam seminggu mengikuti datetime.weekday(): Senin=0 ... Minggu=6.
# Dipakai app.core.mesin_absensi dan app.core.kalender - bukan angka ajaib
# yang diketik ulang di berkas lain.
SENIN, SELASA, RABU, KAMIS, JUMAT, SABTU, MINGGU = range(7)
HARI_SEKOLAH = (SENIN, SELASA, RABU, KAMIS, JUMAT, SABTU)

_DEFAULT_JENDELA_SCAN_BERULANG_DETIK = 10.0
_DEFAULT_MODUL_WHATSAPP_AKTIF = False
_DEFAULT_PROVIDER_WHATSAPP = "NULL"
_DEFAULT_BATAS_PERCOBAAN_NOTIFIKASI = 3
_DEFAULT_GURU_BATAS_TERLAMBAT = time(9, 30)
_DEFAULT_AMBANG_TAHAN_KONVERSI_PULANG = 0.30
_DEFAULT_JEDA_KIRIM_ALPA_PULANG_MENIT = 60
_DEFAULT_MODUL_ABSEN_PULANG = True
_DEFAULT_JALUR_ALAT_AKTIF = "PCSC"
#: PRD §12.4: izin darurat alat paling lama 1x24 jam. Parameter teknis
#: (batas keselamatan, bukan keputusan sekolah yang menunggu), jadi punya
#: default nyata - sama pola dengan _DEFAULT_BATAS_PERCOBAAN_NOTIFIKASI.
_DEFAULT_MAKS_JAM_IZIN_DARURAT_ALAT = 24
#: Bawaan NONAKTIF - gerbang keamanan yang sesungguhnya adalah
#: modul_whatsapp_aktif (bawaan False) DAN provider_whatsapp (bawaan
#: "NULL"): keduanya harus diubah admin secara sadar sebelum pesan
#: sungguhan bisa terkirim ke siapa pun, mode uji atau tidak. PRD §13.3
#: mewajibkan mode uji dipakai SEBELUM fitur diaktifkan ke orang tua -
#: itu prosedur operasional admin (nyalakan mode uji, uji, lalu
#: matikan), bukan alasan untuk membuat instalasi baru mulai dengan
#: mode uji menyala tanpa nomor admin (yang justru akan menahan SEMUA
#: pengiriman - termasuk saat modul_whatsapp_aktif sengaja dites lewat
#: LogProvider).
_DEFAULT_MODE_UJI_WHATSAPP_AKTIF = False


@dataclass(frozen=True)
class IdentitasSekolah:
    """PRD §14.2: nama, NPSN, alamat, logo, nama+NIP kepala sekolah -
    diisi sekali di pengaturan, dipakai kop laporan (laporan sendiri
    belum dibangun - lihat README, "Yang belum dikerjakan"). ``logo``
    disimpan sebagai data URI/base64 teks (kolom settings.nilai adalah
    Text, tidak terbatas) - format/ukurannya urusan lapisan pemanggil,
    bukan divalidasi di sini.

    Seluruh field None berarti belum diisi - ditampilkan apa adanya
    (kosong), bukan ditebak."""

    nama: str | None = None
    npsn: str | None = None
    alamat: str | None = None
    logo: str | None = None
    nama_kepala_sekolah: str | None = None
    nip_kepala_sekolah: str | None = None


@dataclass(frozen=True)
class JadwalHarian:
    """Jadwal untuk satu hari dalam seminggu.

    Ini nilai AWAL/bawaan dipakai app.core.mesin_absensi (fungsi murni,
    tanpa basis data) dan pengujiannya. Untuk keputusan status kehadiran
    sungguhan, sumber otoritatif tetap baris ScheduleRepository/
    SchoolSchedule yang berlaku pada tanggal absensi - lihat catatan di
    Pengaturan.jadwal_untuk() di bawah.

    ``batas_terlambat`` bersifat inklusif ke bawah: scan <= nilai ini
    HADIR, lebih dari itu TERLAMBAT.
    """

    jam_masuk: time
    batas_terlambat: time
    cutoff_alpa: time
    pulang_mulai: time
    pulang_selesai: time  # sekaligus cutoff ALPA pulang

    def dalam_jendela_masuk(self, jam: time) -> bool:
        return jam < self.cutoff_alpa

    def dalam_jendela_pulang(self, jam: time) -> bool:
        return self.pulang_mulai <= jam <= self.pulang_selesai


def _jadwal_biasa() -> JadwalHarian:
    """Senin-Kamis dan Sabtu."""
    return JadwalHarian(
        jam_masuk=time(7, 15),
        batas_terlambat=time(7, 20, 59),
        cutoff_alpa=time(7, 30),
        pulang_mulai=time(13, 20),
        pulang_selesai=time(15, 0),
    )


def _jadwal_jumat() -> JadwalHarian:
    return JadwalHarian(
        jam_masuk=time(7, 15),
        batas_terlambat=time(7, 20, 59),
        cutoff_alpa=time(7, 30),
        pulang_mulai=time(11, 0),
        pulang_selesai=time(14, 0),
    )


def _jadwal_siswa_bawaan() -> dict[int, JadwalHarian]:
    return {
        SENIN: _jadwal_biasa(),
        SELASA: _jadwal_biasa(),
        RABU: _jadwal_biasa(),
        KAMIS: _jadwal_biasa(),
        JUMAT: _jadwal_jumat(),
        SABTU: _jadwal_biasa(),
    }


class Pengaturan:
    """Satu pintu masuk untuk parameter waktu global, toleransi, ambang, dan
    feature flag.

    Dua cara pakai:

    - ``Pengaturan()`` - tanpa basis data. Seluruh atribut berisi nilai AWAL
      yang sudah dikunci di SKILL.md (lihat "Keputusan terkunci"). Dipakai
      app.core.mesin_absensi (fungsi murni) dan tes yang tidak menyentuh
      basis data sama sekali - lihat tests/test_mesin_absensi.py.
    - ``Pengaturan(SettingRepository(session))`` - dibaca dari tabel
      settings saat dibuat, dengan atribut di atas sebagai fallback kalau
      kuncinya belum di-seed. Method ``atur_...()`` menulis ke tabel
      settings (dicatat beserta alasan) SEKALIGUS memperbarui atribut
      instance ini, supaya pemanggil yang membaca lewat atribut langsung
      (mesin absensi murni) maupun lewat method (scheduler, notify, UI
      admin) selalu melihat nilai yang sama dalam satu proses.

    PENTING: jam_masuk(), batas_terlambat(), dan jam_pulang() siswa di
    kelas ini, serta atribut jadwal_siswa/jadwal_untuk(), TIDAK dipakai
    mesin absensi (app.core.engine) untuk menentukan status. Sumber
    otoritatif untuk jadwal per hari adalah ScheduleRepository (tabel
    school_schedules) - satu-satunya yang punya riwayat tanggal berlaku
    dan variasi per hari. jadwal_untuk() di sini tetap ada sebagai nilai
    AWAL untuk mesin absensi murni (yang tidak membaca basis data) dan
    sebagai nilai bawaan saat mengisi form jadwal baru di antarmuka.
    """

    def __init__(self, setting_repo: SettingRepository | None = None) -> None:
        self._setting_repo = setting_repo

        # --- Jadwal siswa bawaan, per hari dalam seminggu ---
        self.jadwal_siswa: dict[int, JadwalHarian] = _jadwal_siswa_bawaan()

        # --- Guru ---
        # Toleransi keterlambatan guru 09:30 (dikonfirmasi sekolah, bukan
        # salah ketik). Guru tidak mengenal ALPA, karena itu tidak ada
        # cutoff_alpa untuk guru.
        self.guru_jam_masuk: time = time(7, 15)
        self.guru_batas_terlambat: time = self._muat_waktu(
            KunciPengaturan.BATAS_TERLAMBAT_GURU, _DEFAULT_GURU_BATAS_TERLAMBAT
        )

        # --- Perilaku scan ---
        #: Scan UID yang sama dalam rentang ini hanya dihitung sekali.
        #: Diterapkan GLOBAL per UID oleh app.core.mesin_absensi.scan_duplikat
        #: (bukan per jalur alat) - lihat app/rfid/base.py.
        self.jeda_antiduplikat_detik: float = self._muat_float(
            KunciPengaturan.JENDELA_SCAN_BERULANG, _DEFAULT_JENDELA_SCAN_BERULANG_DETIK
        )

        # --- Cutoff pulang ---
        #: Tidak scan pulang sampai cutoff dihitung ALPA (keputusan sekolah,
        #: dikunci di SKILL.md - bukan lagi opsi yang perlu diaktifkan admin).
        self.tidak_scan_pulang_jadi_alpa: bool = True
        #: Bila proporsi yang tidak scan pulang melebihi ambang ini, konversi
        #: ditahan dan admin diminta konfirmasi - melindungi dari alat mati.
        self.ambang_tahan_konversi_pulang: float = self._muat_float(
            KunciPengaturan.AMBANG_TAHAN_KONVERSI_PULANG, _DEFAULT_AMBANG_TAHAN_KONVERSI_PULANG
        )

        # --- Notifikasi ---
        #: Jeda setelah cutoff pulang sebelum notifikasi dikirim, memberi
        #: waktu admin mengoreksi lebih dulu. Status diperiksa ulang sebelum
        #: kirim (lihat app.notify.processor).
        self.jeda_kirim_alpa_pulang_menit: int = self._muat_int(
            KunciPengaturan.JEDA_KIRIM_ALPA_PULANG_MENIT, _DEFAULT_JEDA_KIRIM_ALPA_PULANG_MENIT
        )

        # --- Saklar modul ---
        self.modul_absen_pulang: bool = self._muat_bool(
            KunciPengaturan.MODUL_ABSEN_PULANG, _DEFAULT_MODUL_ABSEN_PULANG
        )
        #: Belum dibaca kode mana pun - modul absensi guru, dashboard, dan
        #: sinkronisasi online belum dibangun. Dipertahankan di sini sebagai
        #: nilai awal dari SKILL.md supaya tidak hilang/harus ditebak ulang
        #: saat modulnya dibangun nanti. Belum disambungkan ke SettingRepository
        #: (belum ada KunciPengaturan untuk ini) karena belum ada pemanggil.
        self.modul_absensi_guru: bool = True
        self.modul_dashboard: bool = True
        self.modul_sinkronisasi_online: bool = False

        # --- Alat RFID ---
        #: Nilai JalurAlat (app.rfid.base). Dikunci saat pemasangan; alat
        #: jalur lain ditolak (lihat periksa_kesesuaian_jalur).
        self.jalur_alat_aktif: str = self._muat_str(
            KunciPengaturan.JALUR_ALAT_AKTIF, _DEFAULT_JALUR_ALAT_AKTIF
        )
        #: Belum dibaca kode mana pun - lihat catatan di atas.
        self.jumlah_jalur_lobi: int = 4

        # --- Backup - belum dibaca kode mana pun, modul backup belum dibangun.
        # Lihat catatan di atas: nilai awal dari SKILL.md, belum disambungkan
        # ke SettingRepository karena belum ada pemanggil.
        self.backup_jam: time = time(22, 0)
        self.backup_tujuan: tuple = ("HARDISK_EKSTERNAL", "FOLDER_PC_LAIN")
        self.backup_simpan_harian: int = 30
        self.backup_simpan_bulanan: int = 12

        # --- Koreksi massal - belum dibaca kode mana pun, modulnya belum
        # dibangun. Lihat catatan di atas.
        #: Snapshot otomatis sebelum tindakan massal yang menyentuh baris sebanyak ini.
        self.ambang_snapshot_sebelum_massal: int = 5000
        #: Tindakan massal di atas ambang ini menuntut konfirmasi ketik ulang.
        self.ambang_konfirmasi_ketik: int = 5000

    def jadwal_untuk(self, weekday: int) -> JadwalHarian | None:
        """Jadwal siswa AWAL untuk satu hari; None bila bukan hari sekolah.

        Lihat catatan di docstring kelas - ini bukan sumber otoritatif
        untuk penentuan status, hanya nilai bawaan.
        """
        return self.jadwal_siswa.get(weekday)

    # --- Parameter waktu sekolah - default None, menunggu keputusan sekolah ---
    # (jam_masuk/jam_pulang siswa: nilai tampilan/default saja, lihat docstring
    # kelas - bukan sumber otoritatif untuk mesin absensi)

    def jam_masuk(self) -> time | None:
        return self._ambil_waktu(KunciPengaturan.JAM_MASUK)

    def atur_jam_masuk(self, nilai: time, *, id_pengguna: int, alasan: str) -> None:
        self._atur_waktu(KunciPengaturan.JAM_MASUK, nilai, id_pengguna=id_pengguna, alasan=alasan)

    def batas_terlambat(self) -> time | None:
        return self._ambil_waktu(KunciPengaturan.BATAS_TERLAMBAT)

    def atur_batas_terlambat(self, nilai: time, *, id_pengguna: int, alasan: str) -> None:
        self._atur_waktu(KunciPengaturan.BATAS_TERLAMBAT, nilai, id_pengguna=id_pengguna, alasan=alasan)

    def cutoff_alpa(self) -> time | None:
        return self._ambil_waktu(KunciPengaturan.CUTOFF_ALPA)

    def atur_cutoff_alpa(self, nilai: time, *, id_pengguna: int, alasan: str) -> None:
        self._atur_waktu(KunciPengaturan.CUTOFF_ALPA, nilai, id_pengguna=id_pengguna, alasan=alasan)

    def waktu_kirim_notif_alpa(self) -> time | None:
        return self._ambil_waktu(KunciPengaturan.WAKTU_KIRIM_NOTIF_ALPA)

    def atur_waktu_kirim_notif_alpa(self, nilai: time, *, id_pengguna: int, alasan: str) -> None:
        self._atur_waktu(KunciPengaturan.WAKTU_KIRIM_NOTIF_ALPA, nilai, id_pengguna=id_pengguna, alasan=alasan)

    def jam_pulang(self) -> time | None:
        return self._ambil_waktu(KunciPengaturan.JAM_PULANG)

    def atur_jam_pulang(self, nilai: time, *, id_pengguna: int, alasan: str) -> None:
        self._atur_waktu(KunciPengaturan.JAM_PULANG, nilai, id_pengguna=id_pengguna, alasan=alasan)

    # --- Parameter lain - punya default nyata ---

    def jendela_scan_berulang_detik(self) -> float:
        return self.jeda_antiduplikat_detik

    def atur_jendela_scan_berulang_detik(self, nilai: float, *, id_pengguna: int, alasan: str) -> None:
        self._setting_repo_wajib().atur(
            kunci=KunciPengaturan.JENDELA_SCAN_BERULANG,
            nilai=str(nilai),
            id_pengguna=id_pengguna,
            alasan=alasan,
        )
        self.jeda_antiduplikat_detik = nilai

    def modul_whatsapp_aktif(self) -> bool:
        nilai = self._cari_str(KunciPengaturan.MODUL_WHATSAPP_AKTIF)
        if nilai is None:
            return _DEFAULT_MODUL_WHATSAPP_AKTIF
        return nilai.strip().lower() == "true"

    def atur_modul_whatsapp_aktif(self, aktif: bool, *, id_pengguna: int, alasan: str) -> None:
        self._setting_repo_wajib().atur(
            kunci=KunciPengaturan.MODUL_WHATSAPP_AKTIF,
            nilai="true" if aktif else "false",
            id_pengguna=id_pengguna,
            alasan=alasan,
        )

    def provider_whatsapp(self) -> str:
        """Nama provider yang dipilih admin - NULL, LOG, atau META_CLOUD.

        Ini murni pilihan provider; modul_whatsapp_aktif() tetap jadi
        saklar utama yang harus dicek dulu di app/notify/ sebelum nilai
        ini dipakai untuk benar-benar mengirim.
        """
        nilai = self._cari_str(KunciPengaturan.PROVIDER_WHATSAPP)
        return nilai if nilai is not None else _DEFAULT_PROVIDER_WHATSAPP

    def atur_provider_whatsapp(self, nilai: str, *, id_pengguna: int, alasan: str) -> None:
        self._setting_repo_wajib().atur(
            kunci=KunciPengaturan.PROVIDER_WHATSAPP,
            nilai=nilai,
            id_pengguna=id_pengguna,
            alasan=alasan,
        )

    def mode_uji_whatsapp_aktif(self) -> bool:
        """PRD §13.3: mengalihkan SELURUH pesan ke satu nomor admin -
        dipakai app.core.notifikasi.tujuan_efektif() lewat app.notify.processor,
        bukan menyaring di sini."""
        nilai = self._cari_str(KunciPengaturan.MODE_UJI_WHATSAPP_AKTIF)
        if nilai is None:
            return _DEFAULT_MODE_UJI_WHATSAPP_AKTIF
        return nilai.strip().lower() == "true"

    def atur_mode_uji_whatsapp_aktif(self, aktif: bool, *, id_pengguna: int, alasan: str) -> None:
        self._setting_repo_wajib().atur(
            kunci=KunciPengaturan.MODE_UJI_WHATSAPP_AKTIF,
            nilai="true" if aktif else "false",
            id_pengguna=id_pengguna,
            alasan=alasan,
        )

    def nomor_admin_mode_uji(self) -> str | None:
        return self._cari_str(KunciPengaturan.NOMOR_ADMIN_MODE_UJI)

    def atur_nomor_admin_mode_uji(self, nilai: str, *, id_pengguna: int, alasan: str) -> None:
        """Nomor dirapikan ke format baku +628xxxxxxxxxx (PRD §13.3) lewat
        primitif yang sama dengan Data Master (app.core.validasi_data_master),
        bukan penjaga baru."""
        self._setting_repo_wajib().atur(
            kunci=KunciPengaturan.NOMOR_ADMIN_MODE_UJI,
            nilai=normalisasi_nomor_whatsapp(nilai),
            id_pengguna=id_pengguna,
            alasan=alasan,
        )

    def batas_terlambat_guru(self) -> time:
        return self.guru_batas_terlambat

    def atur_batas_terlambat_guru(self, nilai: time, *, id_pengguna: int, alasan: str) -> None:
        self._atur_waktu(KunciPengaturan.BATAS_TERLAMBAT_GURU, nilai, id_pengguna=id_pengguna, alasan=alasan)
        self.guru_batas_terlambat = nilai

    def batas_percobaan_notifikasi(self) -> int:
        """Jumlah maksimum percobaan kirim sebelum FAILED berhenti
        di-retry otomatis - parameter teknis, bukan angka yang menunggu
        keputusan sekolah, jadi punya default nyata (3).
        """
        nilai = self._cari_str(KunciPengaturan.BATAS_PERCOBAAN_NOTIFIKASI)
        if nilai is None:
            return _DEFAULT_BATAS_PERCOBAAN_NOTIFIKASI
        return int(nilai)

    def atur_batas_percobaan_notifikasi(self, nilai: int, *, id_pengguna: int, alasan: str) -> None:
        self._setting_repo_wajib().atur(
            kunci=KunciPengaturan.BATAS_PERCOBAAN_NOTIFIKASI,
            nilai=str(nilai),
            id_pengguna=id_pengguna,
            alasan=alasan,
        )

    def atur_ambang_tahan_konversi_pulang(self, nilai: float, *, id_pengguna: int, alasan: str) -> None:
        self._setting_repo_wajib().atur(
            kunci=KunciPengaturan.AMBANG_TAHAN_KONVERSI_PULANG,
            nilai=str(nilai),
            id_pengguna=id_pengguna,
            alasan=alasan,
        )
        self.ambang_tahan_konversi_pulang = nilai

    def atur_jeda_kirim_alpa_pulang_menit(self, nilai: int, *, id_pengguna: int, alasan: str) -> None:
        self._setting_repo_wajib().atur(
            kunci=KunciPengaturan.JEDA_KIRIM_ALPA_PULANG_MENIT,
            nilai=str(nilai),
            id_pengguna=id_pengguna,
            alasan=alasan,
        )
        self.jeda_kirim_alpa_pulang_menit = nilai

    def atur_modul_absen_pulang(self, aktif: bool, *, id_pengguna: int, alasan: str) -> None:
        self._setting_repo_wajib().atur(
            kunci=KunciPengaturan.MODUL_ABSEN_PULANG,
            nilai="true" if aktif else "false",
            id_pengguna=id_pengguna,
            alasan=alasan,
        )
        self.modul_absen_pulang = aktif

    def atur_jalur_alat_aktif(self, nilai: str, *, id_pengguna: int, alasan: str) -> None:
        self._setting_repo_wajib().atur(
            kunci=KunciPengaturan.JALUR_ALAT_AKTIF,
            nilai=nilai,
            id_pengguna=id_pengguna,
            alasan=alasan,
        )
        self.jalur_alat_aktif = nilai

    def maks_jam_izin_darurat_alat(self) -> int:
        """Batas lama izin darurat alat RFID (PRD §12.4: "paling lama 1x24
        jam") - ambang, bukan nilai tampilan, jadi selalu dibaca ulang
        dari settings, sama pola dengan batas_percobaan_notifikasi()."""
        nilai = self._cari_str(KunciPengaturan.MAKS_JAM_IZIN_DARURAT_ALAT)
        if nilai is None:
            return _DEFAULT_MAKS_JAM_IZIN_DARURAT_ALAT
        return int(nilai)

    def atur_maks_jam_izin_darurat_alat(self, nilai: int, *, id_pengguna: int, alasan: str) -> None:
        self._setting_repo_wajib().atur(
            kunci=KunciPengaturan.MAKS_JAM_IZIN_DARURAT_ALAT,
            nilai=str(nilai),
            id_pengguna=id_pengguna,
            alasan=alasan,
        )

    # --- Identitas sekolah (PRD §14.2) ---

    def identitas_sekolah(self) -> IdentitasSekolah:
        return IdentitasSekolah(
            nama=self._cari_str(KunciPengaturan.NAMA_SEKOLAH),
            npsn=self._cari_str(KunciPengaturan.NPSN_SEKOLAH),
            alamat=self._cari_str(KunciPengaturan.ALAMAT_SEKOLAH),
            logo=self._cari_str(KunciPengaturan.LOGO_SEKOLAH),
            nama_kepala_sekolah=self._cari_str(KunciPengaturan.NAMA_KEPALA_SEKOLAH),
            nip_kepala_sekolah=self._cari_str(KunciPengaturan.NIP_KEPALA_SEKOLAH),
        )

    def atur_identitas_sekolah(self, identitas: IdentitasSekolah, *, id_pengguna: int, alasan: str) -> None:
        """Menulis seluruh field yang diisi (bukan None) - field yang
        None di ``identitas`` dibiarkan apa adanya di basis data, TIDAK
        dikosongkan. Kirim string kosong "" secara eksplisit kalau
        pengguna memang ingin mengosongkan satu field."""
        pasangan = {
            KunciPengaturan.NAMA_SEKOLAH: identitas.nama,
            KunciPengaturan.NPSN_SEKOLAH: identitas.npsn,
            KunciPengaturan.ALAMAT_SEKOLAH: identitas.alamat,
            KunciPengaturan.LOGO_SEKOLAH: identitas.logo,
            KunciPengaturan.NAMA_KEPALA_SEKOLAH: identitas.nama_kepala_sekolah,
            KunciPengaturan.NIP_KEPALA_SEKOLAH: identitas.nip_kepala_sekolah,
        }
        repo = self._setting_repo_wajib()
        for kunci, nilai in pasangan.items():
            if nilai is not None:
                repo.atur(kunci=kunci, nilai=nilai, id_pengguna=id_pengguna, alasan=alasan)

    # --- Helper baca/tulis bersama ---

    def _setting_repo_wajib(self) -> SettingRepository:
        if self._setting_repo is None:
            raise RuntimeError(
                "Pengaturan dibuat tanpa SettingRepository - tidak bisa menulis "
                "ke basis data. Method ini hanya untuk Pengaturan(SettingRepository(session))."
            )
        return self._setting_repo

    def _cari_str(self, kunci: str) -> str | None:
        if self._setting_repo is None:
            return None
        return self._setting_repo.cari_by_kunci(kunci)

    def _ambil_waktu(self, kunci: str) -> time | None:
        nilai = self._cari_str(kunci)
        return time.fromisoformat(nilai) if nilai is not None else None

    def _atur_waktu(self, kunci: str, nilai: time, *, id_pengguna: int, alasan: str) -> None:
        self._setting_repo_wajib().atur(kunci=kunci, nilai=nilai.isoformat(), id_pengguna=id_pengguna, alasan=alasan)

    def _muat_waktu(self, kunci: str, bawaan: time) -> time:
        nilai = self._cari_str(kunci)
        return time.fromisoformat(nilai) if nilai is not None else bawaan

    def _muat_float(self, kunci: str, bawaan: float) -> float:
        nilai = self._cari_str(kunci)
        return float(nilai) if nilai is not None else bawaan

    def _muat_int(self, kunci: str, bawaan: int) -> int:
        nilai = self._cari_str(kunci)
        return int(nilai) if nilai is not None else bawaan

    def _muat_bool(self, kunci: str, bawaan: bool) -> bool:
        nilai = self._cari_str(kunci)
        return nilai.strip().lower() == "true" if nilai is not None else bawaan

    def _muat_str(self, kunci: str, bawaan: str) -> str:
        nilai = self._cari_str(kunci)
        return nilai if nilai is not None else bawaan
