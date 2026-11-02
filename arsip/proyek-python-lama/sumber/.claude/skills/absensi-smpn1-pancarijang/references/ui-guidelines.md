# Panduan Antarmuka

Baca berkas ini sebelum membuat layar apa pun. Pengguna secara tegas meminta tampilan
yang ramah, profesional, mudah dimengerti, dan **tidak terkesan dibuat AI**.

## Apa yang membuat tampilan terkesan dibuat AI

Bukan soal warna atau kerapian. Yang membuat sebuah antarmuka terasa dihasilkan mesin
adalah tanda-tanda bahwa perancangnya tidak pernah melihat pekerjaan yang sebenarnya:

- Kartu-kartu seragam bersudut membulat disusun dalam kisi tiga kolom, semuanya
  berukuran sama, tanpa ada yang lebih penting dari yang lain.
- Emoji dipakai sebagai ikon.
- Gradien ungu ke biru, atau warna aksen yang sama dengan yang dipakai semua orang.
- Judul raksasa dengan ruang kosong berlebihan, sementara tabel data justru sempit.
- Istilah generik hasil terjemahan: "Manajemen Pengguna", "Kelola Data", "Item".
- Setiap angka diberi kartu sendiri lengkap dengan ikon dan persentase perubahan yang
  tidak berarti apa-apa.

Lawannya bukan tampilan yang lebih cantik, melainkan tampilan yang jelas dibuat oleh
orang yang paham cara kerja sekolah.

## Cara membuatnya terasa dibuat manusia

**Pakai bahasa sekolah Indonesia yang sesungguhnya.** Bukan "Manajemen Siswa" tapi
"Data Siswa". Bukan "Kelas" saja tapi "Kelas 7A", "Rombel". Pakai NISN, NIS, wali
kelas, tahun pelajaran, semester ganjil dan genap. Istilah yang tepat langsung
memberi kesan bahwa aplikasi ini dibuat untuk sekolah, bukan disalin dari templat
aplikasi kantor.

**Susun layar mengikuti alur kerja, bukan mengikuti struktur basis data.** Operator
tidak berpikir "saya mau membuka tabel students". Dia berpikir "anak ini kartunya
hilang" atau "wali kelas 8B minta rekap bulan lalu". Beri jalan pintas untuk kejadian
yang benar-benar sering terjadi.

**Beri bobot berbeda pada hal yang berbeda pentingnya.** Jumlah siswa yang belum
absen jauh lebih penting daripada total siswa terdaftar. Tampilkan begitu.

**Rapatkan tabel.** Daftar 500 siswa dengan tinggi baris 60 piksel adalah siksaan.
Tabel yang rapat justru terasa profesional karena menghormati waktu penggunanya.

## Layar absensi

Layar ini yang dipakai setiap pagi saat ratusan siswa mengantre. Aturannya berbeda
dari layar lain.

Harus bekerja **tanpa disentuh sama sekali**. Tidak ada dialog yang perlu ditutup,
tidak ada tombol konfirmasi, tidak ada fokus bidang isian yang bisa hilang.

Umpan balik harus terbaca dari jarak dua meter dalam waktu kurang dari sedetik: nama
siswa berukuran besar, kelasnya, jam scan, dan status. Foto siswa bila tersedia —
ini membantu operator menangkap kartu yang dipinjamkan.

Bedakan hasil lewat warna sekaligus bentuk, jangan warna saja. Hijau untuk hadir,
kuning untuk terlambat, merah untuk kartu tidak dikenal, masing-masing dengan
tampilan yang berbeda jelas.

Tambahkan bunyi berbeda untuk berhasil dan gagal. Operator tidak selalu menatap
layar; telinganya yang bekerja.

Riwayat scan beberapa menit terakhir tetap terlihat di sisi layar, supaya operator
bisa memastikan bahwa alat memang bekerja.

Status pembaca RFID selalu terpampang. Kalau kabel tercabut, itu harus ketahuan
dalam hitungan detik, bukan setelah lima puluh siswa lewat tanpa tercatat.

## Dashboard

Untuk kepala sekolah dan admin. Yang ingin diketahui kepala sekolah bukan angka hari
ini saja, melainkan pola: kelas mana yang keterlambatannya menonjol, apakah hari
Senin selalu lebih buruk, siswa mana yang alpanya berulang.

Sediakan angka hari berjalan secukupnya, lalu satu grafik tren keterlambatan per
kelas, dan satu daftar siswa dengan alpa terbanyak dalam rentang waktu yang dipilih.
Daftar yang bisa ditindaklanjuti jauh lebih berguna daripada tiga grafik lingkaran.

Jangan menampilkan persentase perubahan tanpa pembanding yang jelas.

## Tipografi dan warna

Satu keluarga font saja. Inter atau Segoe UI aman untuk Windows. Bangun hierarki
lewat berat huruf dan jarak, bukan lewat ukuran yang melompat-lompat.

Satu warna aksen untuk tindakan utama. Warna status dipakai konsisten di seluruh
aplikasi: hijau hadir, kuning terlambat, merah alpa, biru izin, ungu sakit. Sekali
ditetapkan, jangan pernah berubah arti di layar lain.

Latar terang sebagai bawaan. Mode gelap boleh ditambahkan, tapi bukan prioritas —
aplikasi ini dipakai di ruangan terang pada pagi hari.

## Catatan teknis PySide6

Tampilan bawaan PySide6 kaku, jadi penataan lewat QSS memang perlu usaha. Tulis satu
berkas QSS terpusat, jangan menyebar gaya di dalam kode tiap layar.

Untuk daftar panjang, pakai `QTableView` dengan model kustom, bukan `QTableWidget` —
`QTableWidget` akan tersendat pada ribuan baris.

Jalankan pembacaan RFID dan pengiriman notifikasi di thread terpisah. Antarmuka yang
membeku setengah detik saat kartu ditempel akan langsung terasa oleh operator.

Sediakan pintasan papan ketik untuk tindakan yang sering dipakai. Operator sekolah
cepat sekali hafal, dan ini membuat aplikasi terasa dibuat untuk mereka.
