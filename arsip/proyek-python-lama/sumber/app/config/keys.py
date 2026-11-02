class KunciPengaturan:
    """Nama kunci di tabel settings.

    Satu tempat rujukan supaya string kunci tidak diketik ulang tersebar
    di berbagai tempat dan gampang salah ketik. Namanya mengikuti tabel
    parameter konfigurasi di PRD.
    """

    JAM_MASUK = "JAM_MASUK"
    BATAS_TERLAMBAT = "BATAS_TERLAMBAT"
    CUTOFF_ALPA = "ATTENDANCE_CUTOFF_TIME"
    WAKTU_KIRIM_NOTIF_ALPA = "WAKTU_KIRIM_NOTIF_ALPA"
    JAM_PULANG = "JAM_PULANG"
    JENDELA_SCAN_BERULANG = "DUPLICATE_SCAN_WINDOW"
    MODUL_WHATSAPP_AKTIF = "MODUL_WHATSAPP_AKTIF"
    PROVIDER_WHATSAPP = "WHATSAPP_PROVIDER"
    BATAS_TERLAMBAT_GURU = "BATAS_TERLAMBAT_GURU"
    BATAS_PERCOBAAN_NOTIFIKASI = "BATAS_PERCOBAAN_NOTIFIKASI"
    # Cutoff pulang dan konversi ALPA pulang (menggantikan Tutup Hari - lihat
    # SKILL.md, "Absensi pulang dan ALPA pulang").
    AMBANG_TAHAN_KONVERSI_PULANG = "AMBANG_TAHAN_KONVERSI_PULANG"
    JEDA_KIRIM_ALPA_PULANG_MENIT = "JEDA_KIRIM_ALPA_PULANG_MENIT"
    MODUL_ABSEN_PULANG = "MODUL_ABSEN_PULANG"
    JALUR_ALAT_AKTIF = "JALUR_ALAT_AKTIF"
    # Perangkat (PRD §12.4): izin darurat alat maksimum 1x24 jam - ambang
    # ini yang membuat batasnya bisa disesuaikan tanpa menyentuh kode.
    MAKS_JAM_IZIN_DARURAT_ALAT = "MAKS_JAM_IZIN_DARURAT_ALAT"
    # Notifikasi WhatsApp - mode uji (PRD §13.3: wajib dipakai sebelum
    # fitur diaktifkan kepada orang tua).
    MODE_UJI_WHATSAPP_AKTIF = "MODE_UJI_WHATSAPP_AKTIF"
    NOMOR_ADMIN_MODE_UJI = "NOMOR_ADMIN_MODE_UJI"
    # Identitas sekolah (PRD §14.2: diisi sekali di pengaturan, dipakai
    # kop laporan - laporan sendiri belum dibangun, lihat README).
    NAMA_SEKOLAH = "NAMA_SEKOLAH"
    NPSN_SEKOLAH = "NPSN_SEKOLAH"
    ALAMAT_SEKOLAH = "ALAMAT_SEKOLAH"
    LOGO_SEKOLAH = "LOGO_SEKOLAH"
    NAMA_KEPALA_SEKOLAH = "NAMA_KEPALA_SEKOLAH"
    NIP_KEPALA_SEKOLAH = "NIP_KEPALA_SEKOLAH"
