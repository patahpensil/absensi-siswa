"""Hash kata sandi (argon2). Satu-satunya tempat yang boleh menyentuh
argon2_cffi langsung - jangan panggil PasswordHasher dari tempat lain.
"""

from argon2 import PasswordHasher
from argon2.exceptions import VerifyMismatchError

_hasher = PasswordHasher()


def hash_password(password_polos: str) -> str:
    return _hasher.hash(password_polos)


def verifikasi_password(password_polos: str, password_hash: str) -> bool:
    try:
        return _hasher.verify(password_hash, password_polos)
    except VerifyMismatchError:
        return False
