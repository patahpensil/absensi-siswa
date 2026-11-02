"""KlienDashboard: pembungkus HTTP ke endpoint Dashboard (PRD §14.1).
Sama pola dengan klien layar admin lainnya (mis.
app.ui.koreksi_guru.klien_koreksi_guru): online-only, murni Python +
httpx tanpa PySide6, login lazy dengan satu retry pada 401. Server yang
menegakkan lingkup peran - klien ini hanya meneruskan galatnya apa
adanya.
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


class KlienDashboard:
    def __init__(self, *, base_url: str, username: str, password: str, timeout: float = 5.0) -> None:
        self._client = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)
        self._username = username
        self._password = password
        self._token: str | None = None
        self.peran: str | None = None

    def tutup(self) -> None:
        self._client.close()

    def ringkasan(self, *, tanggal: str | None = None) -> dict[str, Any]:
        params = {"tanggal": tanggal} if tanggal else None
        return self._panggil("GET", "/dashboard/ringkasan", params=params)

    def grafik(
        self, *, tanggal_mulai: str, tanggal_selesai: str, granularitas: str = "HARIAN", id_kelas: int | None = None
    ) -> list[dict[str, Any]]:
        params: dict[str, Any] = {
            "tanggal_mulai": tanggal_mulai, "tanggal_selesai": tanggal_selesai, "granularitas": granularitas,
        }
        if id_kelas is not None:
            params["id_kelas"] = id_kelas
        return self._panggil("GET", "/dashboard/grafik", params=params)

    def tren_keterlambatan(
        self, *, tanggal_mulai: str, tanggal_selesai: str, id_kelas: int | None = None
    ) -> list[dict[str, Any]]:
        params: dict[str, Any] = {"tanggal_mulai": tanggal_mulai, "tanggal_selesai": tanggal_selesai}
        if id_kelas is not None:
            params["id_kelas"] = id_kelas
        return self._panggil("GET", "/dashboard/tren-keterlambatan", params=params)

    def perbandingan_kelas(self, *, tanggal_mulai: str, tanggal_selesai: str) -> list[dict[str, Any]]:
        return self._panggil(
            "GET", "/dashboard/perbandingan-kelas",
            params={"tanggal_mulai": tanggal_mulai, "tanggal_selesai": tanggal_selesai},
        )

    def alpa_terbanyak(
        self, *, tanggal_mulai: str, tanggal_selesai: str, id_kelas: int | None = None, limit: int = 10
    ) -> list[dict[str, Any]]:
        params: dict[str, Any] = {"tanggal_mulai": tanggal_mulai, "tanggal_selesai": tanggal_selesai, "limit": limit}
        if id_kelas is not None:
            params["id_kelas"] = id_kelas
        return self._panggil("GET", "/dashboard/alpa-terbanyak", params=params)

    def status_operasional(self) -> dict[str, Any]:
        return self._panggil("GET", "/dashboard/status-operasional")

    def daftar_kelas(self) -> list[dict[str, Any]]:
        """GET /kelas (kepsek/admin saja, lihat app.server.routers.kelas)
        - dipakai layar Dashboard menyusun satu garis tren per kelas.
        Wali kelas TIDAK PERNAH memanggil ini (lihat
        app.ui.dashboard.layar.LayarDashboard._daftar_kelas_untuk_tren -
        satu kelasnya sendiri sudah cukup, server yang membatasi)."""
        return self._panggil("GET", "/kelas")

    # ------------------------------------------------------------------
    # Inti - login lazy, satu retry pada 401.
    # ------------------------------------------------------------------

    def _panggil(self, method: str, path: str, *, params: dict[str, Any] | None = None) -> Any:
        respons = self._minta(method, path, params=params)
        if respons.status_code == 401:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
            respons = self._minta(method, path, params=params)

        if respons.status_code == 200:
            return respons.json()
        raise ServerMenolak(respons.status_code, self._detail(respons))

    def _minta(self, method: str, path: str, *, params: dict[str, Any] | None) -> httpx.Response:
        if self._token is None:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
        try:
            return self._client.request(method, path, params=params, headers=self._headers())
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
