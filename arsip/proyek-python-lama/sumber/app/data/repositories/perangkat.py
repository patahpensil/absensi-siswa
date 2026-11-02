from datetime import datetime

from sqlalchemy import select

from app.core.timeutil import now_wita
from app.data.models.perangkat import IzinDaruratAlat, TelemetriLobi
from app.data.models.system import AuditLog
from app.data.repositories.audit_log import pastikan_alasan_terisi
from app.data.repositories.base import BaseRepository


class IzinDaruratAlatRepository(BaseRepository):
    """Akses data untuk izin_darurat_alat.

    Setiap penulisan (buat/perpanjang/cabut) langsung menyertakan baris
    audit_logs dalam commit yang SAMA - pola yang sama dengan
    app.data.repositories.setting.SettingRepository.atur() - karena PRD
    §15.2 mewajibkan "pengaktifan izin darurat perangkat" teraudit, dan
    ini satu-satunya jalur penulisan tabel ini (tidak ada jalur lain yang
    bisa lupa mengaudit).
    """

    def cari_by_id(self, id_izin: int) -> IzinDaruratAlat | None:
        return self._session.get(IzinDaruratAlat, id_izin)

    def cari_aktif_by_station(self, station_id: str, *, sekarang: datetime) -> IzinDaruratAlat | None:
        """Izin yang sedang berlaku untuk satu station - dipakai
        app.rfid.base.periksa_kesesuaian_jalur (bypass) dan banner MODE
        DARURAT lobi. Paling banyak SATU baris aktif per station pada
        satu waktu (ditegakkan app.core.perangkat, bukan constraint DB)."""
        stmt = select(IzinDaruratAlat).where(
            IzinDaruratAlat.station_id == station_id,
            IzinDaruratAlat.dicabut.is_(False),
            IzinDaruratAlat.waktu_berakhir > sekarang,
        )
        return self._session.execute(stmt).scalars().first()

    def daftar_aktif(self, *, sekarang: datetime) -> list[IzinDaruratAlat]:
        stmt = select(IzinDaruratAlat).where(
            IzinDaruratAlat.dicabut.is_(False),
            IzinDaruratAlat.waktu_berakhir > sekarang,
        )
        return list(self._session.execute(stmt).scalars())

    def buat(
        self,
        *,
        station_id: str,
        id_pengguna_pemberi: int,
        waktu_mulai: datetime,
        waktu_berakhir: datetime,
        alasan: str,
    ) -> IzinDaruratAlat:
        alasan = pastikan_alasan_terisi(alasan)
        izin = IzinDaruratAlat(
            station_id=station_id,
            id_pengguna_pemberi=id_pengguna_pemberi,
            waktu_mulai=waktu_mulai,
            waktu_berakhir=waktu_berakhir,
            alasan=alasan,
        )
        self._session.add(izin)
        self._session.add(
            AuditLog(
                id_pengguna=id_pengguna_pemberi,
                aksi="izin_darurat_alat_diberikan",
                nilai_sebelum=None,
                nilai_sesudah={
                    "station_id": station_id,
                    "waktu_mulai": waktu_mulai.isoformat(),
                    "waktu_berakhir": waktu_berakhir.isoformat(),
                },
                alasan=alasan,
                waktu=now_wita(),
            )
        )
        self._commit()
        return izin

    def perpanjang(
        self, izin: IzinDaruratAlat, *, waktu_berakhir_baru: datetime, id_pengguna: int, alasan: str
    ) -> IzinDaruratAlat:
        alasan = pastikan_alasan_terisi(alasan)
        waktu_berakhir_lama = izin.waktu_berakhir
        izin.waktu_berakhir = waktu_berakhir_baru
        self._session.add(
            AuditLog(
                id_pengguna=id_pengguna,
                aksi="izin_darurat_alat_diperpanjang",
                nilai_sebelum={"id_izin": izin.id, "waktu_berakhir": waktu_berakhir_lama.isoformat()},
                nilai_sesudah={"id_izin": izin.id, "waktu_berakhir": waktu_berakhir_baru.isoformat()},
                alasan=alasan,
                waktu=now_wita(),
            )
        )
        self._commit()
        return izin

    def cabut(self, izin: IzinDaruratAlat, *, id_pengguna: int, sekarang: datetime, alasan: str) -> IzinDaruratAlat:
        alasan = pastikan_alasan_terisi(alasan)
        izin.dicabut = True
        izin.id_pengguna_pencabut = id_pengguna
        izin.waktu_dicabut = sekarang
        self._session.add(
            AuditLog(
                id_pengguna=id_pengguna,
                aksi="izin_darurat_alat_dicabut",
                nilai_sebelum={"id_izin": izin.id, "dicabut": False},
                nilai_sesudah={"id_izin": izin.id, "dicabut": True},
                alasan=alasan,
                waktu=now_wita(),
            )
        )
        self._commit()
        return izin


class TelemetriLobiRepository(BaseRepository):
    """Akses data untuk telemetri_lobi (PRD §14.1, status operasional
    Dashboard). Bukan data sensitif/reversibel seperti izin darurat -
    murni angka operasional yang ditimpa tiap laporan, jadi TIDAK lewat
    audit_logs (beda dari IzinDaruratAlatRepository di atas)."""

    def lapor(self, *, station_id: str, jumlah_pending: int, id_pengguna: int) -> TelemetriLobi:
        baris = self._session.get(TelemetriLobi, station_id)
        if baris is None:
            baris = TelemetriLobi(
                station_id=station_id, jumlah_pending=jumlah_pending, id_pengguna=id_pengguna, waktu_lapor=now_wita()
            )
            self._session.add(baris)
        else:
            baris.jumlah_pending = jumlah_pending
            baris.id_pengguna = id_pengguna
            baris.waktu_lapor = now_wita()
        self._commit()
        return baris

    def daftar(self) -> list[TelemetriLobi]:
        return list(self._session.execute(select(TelemetriLobi)).scalars())
