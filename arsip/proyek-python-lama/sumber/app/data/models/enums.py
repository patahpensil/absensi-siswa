from enum import Enum


class JenisPemilik(str, Enum):
    """Jenis pemilik kartu RFID atau baris absensi: siswa atau guru.

    Dipakai di beberapa tabel (rfid_cards, attendance_events, attendance)
    untuk relasi polimorfik ke students atau teachers.
    """

    SISWA = "SISWA"
    GURU = "GURU"
