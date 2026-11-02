"""KlienPengguna: pembungkus HTTP ke endpoint Manajemen Akun & Peran
(PRD §10) - buat akun, daftar, reset sandi, atur kelas diampu. Juga
membaca daftar kelas (endpoint Data Master, slice sebelumnya) untuk
mengisi pilihan kelas diampu di layar.

Sama seperti klien_kalender/klien_pengaturan/klien_data_master: online-
only, murni Python + httpx tanpa PySide6, login lazy dengan satu retry
pada 401. ``peran`` disimpan setelah login - dipakai layar membatasi
pilihan peran yang ditawarkan saat membuat akun (kenyamanan tampilan;
penegakan sesungguhnya di server, lihat app.server.pengguna_service).
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


class KlienPengguna:
    def __init__(self, *, base_url: str, username: str, password: str, timeout: float = 5.0) -> None:
        self._client = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)
        self._username = username
        self._password = password
        self._token: str | None = None
        self.peran: str | None = None

    def tutup(self) -> None:
        self._client.close()

    # ------------------------------------------------------------------
    # Akun
    # ------------------------------------------------------------------

    def buat_akun(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("POST", "/pengguna", json=payload)

    def daftar_akun(self) -> list[dict[str, Any]]:
        return self._panggil("GET", "/pengguna")

    def reset_password(self, id_target: int, *, password_baru: str) -> dict[str, Any]:
        return self._panggil("POST", f"/pengguna/{id_target}/reset-password", json={"password_baru": password_baru})

    def atur_kelas_diampu(self, id_target: int, *, id_kelas_diampu: int | None) -> dict[str, Any]:
        return self._panggil(
            "PUT", f"/pengguna/{id_target}/kelas-diampu", json={"id_kelas_diampu": id_kelas_diampu}
        )

    # ------------------------------------------------------------------
    # Kelas (Data Master, slice sebelumnya) - untuk pilihan kelas diampu
    # ------------------------------------------------------------------

    def daftar_kelas(self) -> list[dict[str, Any]]:
        return self._panggil("GET", "/kelas")

    # ------------------------------------------------------------------
    # Inti - login lazy, satu retry pada 401.
    # ------------------------------------------------------------------

    def _panggil(self, method: str, path: str, *, json: dict[str, Any] | None = None) -> Any:
        respons = self._minta(method, path, json=json)
        if respons.status_code == 401:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
            respons = self._minta(method, path, json=json)

        if respons.status_code in (200, 201):
            return respons.json()
        raise ServerMenolak(respons.status_code, self._detail(respons))

    def _minta(self, method: str, path: str, *, json: dict[str, Any] | None) -> httpx.Response:
        if self._token is None:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
        try:
            return self._client.request(method, path, json=json, headers=self._headers())
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
