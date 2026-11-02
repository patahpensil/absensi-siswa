"""KlienPerangkat: pembungkus HTTP ke endpoint Perangkat (PRD §12.4) dan
GET /kartu/cek/{uid_kartu} (Uji Baca Kartu, §18). Sama pola dengan klien
layar admin lainnya (mis. app.ui.koreksi_guru.klien_koreksi_guru):
online-only, murni Python + httpx tanpa PySide6, login lazy dengan satu
retry pada 401. Server yang menegakkan lingkup peran - klien ini hanya
meneruskan galatnya apa adanya.
"""

from __future__ import annotations

from typing import Any

import httpx


class GagalHubungiServer(Exception):
    pass


class ServerMenolak(Exception):
    def __init__(self, status_code: int, detail: str) -> None:
        self.status_code = status_code
        self.detail = detail
        super().__init__(f"Server menolak (kode {status_code}): {detail}")


class KlienPerangkat:
    def __init__(self, *, base_url: str, username: str, password: str, timeout: float = 5.0) -> None:
        self._client = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)
        self._username = username
        self._password = password
        self._token: str | None = None
        self.peran: str | None = None

    def tutup(self) -> None:
        self._client.close()

    # ------------------------------------------------------------------
    # Uji Baca Kartu (§18) - dipakai juga verifikasi wizard
    # ------------------------------------------------------------------

    def cek_uid(self, uid: str) -> dict[str, Any]:
        return self._panggil("GET", f"/kartu/cek/{uid}")

    # ------------------------------------------------------------------
    # Izin darurat alat (§12.4)
    # ------------------------------------------------------------------

    def beri_izin_darurat(self, *, station_id: str, jam: float, alasan: str) -> dict[str, Any]:
        return self._panggil(
            "POST", "/perangkat/izin-darurat", json={"station_id": station_id, "jam": jam, "alasan": alasan}
        )

    def perpanjang_izin_darurat(self, id_izin: int, *, jam: float, alasan: str) -> dict[str, Any]:
        return self._panggil(
            "POST", f"/perangkat/izin-darurat/{id_izin}/perpanjang", json={"jam": jam, "alasan": alasan}
        )

    def cabut_izin_darurat(self, id_izin: int, *, alasan: str) -> dict[str, Any]:
        return self._panggil("POST", f"/perangkat/izin-darurat/{id_izin}/cabut", json={"alasan": alasan})

    def daftar_izin_darurat_aktif(self) -> list[dict[str, Any]]:
        return self._panggil("GET", "/perangkat/izin-darurat")

    def cek_izin_darurat_aktif(self, station_id: str) -> dict[str, Any]:
        return self._panggil("GET", "/perangkat/izin-darurat/aktif", params={"station_id": station_id})

    # ------------------------------------------------------------------
    # Wizard ganti jalur (§12.4)
    # ------------------------------------------------------------------

    def ganti_jalur(self, *, jalur_baru: str, uid_kartu_verifikasi: list[str], alasan: str) -> dict[str, Any]:
        return self._panggil(
            "POST",
            "/perangkat/ganti-jalur",
            json={"jalur_baru": jalur_baru, "uid_kartu_verifikasi": uid_kartu_verifikasi, "alasan": alasan},
        )

    # ------------------------------------------------------------------
    # Inti - login lazy, satu retry pada 401.
    # ------------------------------------------------------------------

    def _panggil(
        self,
        method: str,
        path: str,
        *,
        json: dict[str, Any] | None = None,
        params: dict[str, Any] | None = None,
    ) -> Any:
        respons = self._minta(method, path, json=json, params=params)
        if respons.status_code == 401:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
            respons = self._minta(method, path, json=json, params=params)

        if respons.status_code in (200, 201):
            return respons.json()
        raise ServerMenolak(respons.status_code, self._detail(respons))

    def _minta(
        self, method: str, path: str, *, json: dict[str, Any] | None, params: dict[str, Any] | None
    ) -> httpx.Response:
        if self._token is None:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
        try:
            return self._client.request(method, path, json=json, params=params, headers=self._headers())
        except httpx.HTTPError as exc:
            raise GagalHubungiServer(f"Tidak terhubung ke server: {exc}") from exc

    def _headers(self) -> dict[str, str]:
        return {"Authorization": f"Bearer {self._token}"}

    def _login(self) -> str | None:
        try:
            respons = self._client.post(
                "/auth/login", json={"username": self._username, "password": self._password}
            )
        except httpx.HTTPError as exc:
            return f"Tidak bisa login ke server: {exc}"
        if respons.status_code != 200:
            return f"Login gagal (kode {respons.status_code}) - periksa kredensial akun"
        badan = respons.json()
        self._token = badan["token"]
        self.peran = badan["peran"]
        return None

    @staticmethod
    def _detail(respons: httpx.Response) -> str:
        try:
            badan = respons.json()
        except ValueError:
            return respons.text
        if isinstance(badan, dict) and "detail" in badan:
            return str(badan["detail"])
        return str(badan)
