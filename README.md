# Tugas Besar IF25-21013 Strategi Algoritma 2026
> **Pemanfaatan Algoritma Greedy dalam Pembuatan Bot Permainan Robocode Tank Royale**

1. [Deskripsi Program](#-deskripsi-program)
2. [Spesifikasi Bot dan Strategi Greedy](#-spesifikasi-bot-dan-strategi-greedy)
3. [Kebutuhan Perangkat Lunak (Prerequisites)](#-kebutuhan-perangkat-lunak-prerequisites)
4. [Langkah Mengompilasi dan Menjalankan Bot](#-langkah-mengompilasi-dan-menjalankan-bot)
5. [Struktur Repositori](#-struktur-repositori)
6. [Anggota Kelompok](#-anggota-kelompok)

## Deskripsi Program
Program ini merupakan proyek  bot tank virtual untuk game Robocode Tank Royale. Seluruh bot dikembangkan menggunakan bahasa pemrograman C# (.NET) dengan menerapkan variasi komponen Algoritma Greedy berbasis turn-by-turn. Proyek ini mencakup 1 bot utama terbaik (TPG) dan 3 bot alternatif dengan pendekatan fungsi heuristik yang unik dan berbeda satu sama lain demi mendominasi papan skor pertempuran (*Battle Royale*).

## Spesifikasi Bot dan Strategi Greedy

### 1. Bot Utama: TPG (Chaser & RamFire Hybrid)
* **Fungsi Heuristik:** Jarak Minimum Lokasi Musuh ($\min(\text{enemyDist})$).
* **Fungsi Objektif:** Maksimalisasi *Bullet Damage Score* dan *Ram Score*.
* **Strategi Logika:** * **Fase Patrol:** Berputar melingkar non-blocking secara kontinu (`SetTurnLeft` & `SetForward`) dengan membatasi `MaxSpeed = 5` agar sapuan radar mencakup seluruh arena secara konstan.
  * **Fase Chase:** Begitu musuh terdeteksi di bawah `RangeChase (400px)`, bot mengunci koordinat target (`enemyX`, `enemyY`) dan melaju agresif mengejar musuh. Jika jarak sangat dekat (`RangeClose < 150px`), bot mengaktifkan mode *RamFire* dengan menembakkan peluru berdaya penuh (`Fire(3.0)`) dan menabrak target demi bonus kerusakan instan. 
  * **Pencegahan Dinding:** Menggunakan fungsi `IsNearWall()` untuk mendeteksi tepi peta, memaksa bot berbalik ke tengah arena agar terhindar dari *wall damage*.

### 2. Bot Alternatif 1: BotTheLast (Wall-Smoother & Survival Guard)
* **Fungsi Heuristik:** Maksimalisasi Jarak Aman Minimal dan Keamanan Tepi Dinding.
* **Fungsi Objektif:** Maksimalisasi *Survival Score* dan *Last Survival Bonus*.
* **Strategi Logika:** * Bot ini didesain defensif dengan memprioritaskan kelangsungan hidup. Ia melacak musuh secara pasif menggunakan koleksi `daftarMusuh` (`Dictionary`).
  * Bergerak taktis mengitari arena dengan memanfaatkan status `MengikutiDinding` dan `MenjagaJarak` untuk menghindari episentrum pertempuran jarak dekat yang berbahaya.
  * Jika terkena tembakan peluru (`OnHitByBullet`), fungsi heuristiknya memaksa bot melakukan manuver menghindar darurat secara tegak lurus ($90^\circ$) dari arah datangnya peluru untuk meminimalkan kerusakan beruntun demi mengamankan poin *Survival*.

### 3. Bot Alternatif 2: Proto (Orbit Targeter & Energy Awareness)
* **Fungsi Heuristik:** Pemetaan Kecepatan Vektor Musuh (Linear Predictive Targeting) dan Manajemen Energi Internal.
* **Fungsi Objektif:** Efisiensi Akurasi Tembakan (*Bullet Hit Rate*) dan Penghematan Energi.
* **Strategi Logika:**
  * **Gerakan Orbit:** Menjaga jarak konstan di sekitar musuh menggunakan radius tertentu (`OrbitRadius = 250px`). Arah orbit dapat berbalik arah (`orbitDir *= -1`) secara dinamis apabila mendeteksi risiko terjebak atau menabrak dinding.
  * **Linear Predictive:** Menghitung pergerakan masa depan musuh berdasarkan selisih posisi sebelumnya (`prevEnemyX/Y`) dan kecepatan peluru (`BulletSpeed`).
  * **Greedy Power Scaling:** Penentuan daya tembak dikalkulasi melalui fungsi pintar `CalcFirePower()`. Jika energi bot melimpah, ia menembak keras (`Fire(3.0)`), namun jika energi bot kritis (`< 30` atau `< 15`), daya tembak secara otomatis diturunkan (`Fire(1.5)` atau `Fire(1.0)`) secara *greedy* agar bot tidak mati kehabisan energi akibat panas senjata sendiri.

### 4. Bot Alternatif 3: BotGwTuh (Dying Target Executioner)
* **Fungsi Heuristik:** Pencarian Musuh Sekarat Berdasarkan Batas Energi Lokal ($\text{Energy} \le 20.0$).
* **Fungsi Objektif:** Maksimalisasi *Kill Score* (Mengamankan poin eliminasi tercepat).
* **Strategi Logika:**
  * Selama fase penjelajahan, bot bergerak melingkar konstan seraya mendaftarkan status sisa energi lawan ke dalam map `trackedBots`.
  * Melalui fungsi seleksi `HasDyingTarget()`, bot terus memantau apakah ada musuh di arena yang memiliki energi di bawah ambang batas kritis (`DYING_THRESHOLD = 20.0`).
  * Jika kondisi terpenuhi, bot mengalihkan fokus secara *greedy*, langsung mengejar target sekarat tersebut, mengunci rotasi senjata (`gunTurn`), dan melepaskan tembakan bertubi-tubi dengan kekuatan mutlak `Fire(3.0)` untuk memastikan bot kita yang mendapatkan poin eliminasi (*kill credit*).

---

## Kebutuhan Perangkat Lunak
Sebelum melakukan kompilasi, pastikan mesin komputasi Anda telah memenuhi dependensi berikut:
* **.NET SDK 10.0** atau versi yang lebih baru (dapat diunduh melalui [dotnet.microsoft.com](https://dotnet.microsoft.com/))
* **Robocode Tank Royale Game Engine GUI** (Versi resmi hasil modifikasi asisten praktikum)
* Sistem Operasi compatible: **Windows** atau **Linux**

---

## Langkah Mengompilasi dan Menjalankan Bot

### 1. Kloning Repositori
Buka terminal/command prompt, lakukan klon terhadap repositori GitHub publik kelompok kami:
git clone [https://github.com/](https://github.com/)[UsernameGitHub]/Tubes_NamaKelompok.git
cd Tubes_NamaKelompok/src

### 2. Kompilasi Program (Build)
Masuk ke direktori proyek bot yang ingin Anda kompilasi, lalu jalankan perintah kompilasi dotnet build:
cd TPG
dotnet build

### 3. Menjalankan Bot di Arena Tank Royale
Pastikan aplikasi server utama game engine Robocode Tank Royale telah aktif. Untuk meluncurkan bot ke dalam antrean partisipan, jalankan perintah:
dotnet run

## Struktur Repositori

Tubes_NamaKelompok/
│
├── doc/
│   └── Laporan_Tubes_Stima.pdf       # Dokumentasi Laporan Resmi Tugas Besar (PDF)
│
├── src/
│   ├── TPG/                          # Direktori Kode Sumber Bot Utama
│   │   ├── TPG.cs
│   │   ├── TPG.csproj
│   │   └── TPG.json
│   │
│   ├── BotTheLast/                   # Direktori Kode Sumber Bot Alternatif 1
│   │   ├── BotTheLast.cs
│   │   ├── BotTheLast.csproj
│   │   └── BotTheLast.json
│   │
│   ├── Proto/                        # Direktori Kode Sumber Bot Alternatif 2
│   │   ├── Proto.cs
│   │   ├── Proto.csproj
│   │   └── Proto.json
│   │
│   └── BotGwTuh/                     # Direktori Kode Sumber Bot Alternatif 3
│       ├── BotGwTuh.cs
│       ├── BotGwTuh.csproj
│       └── BotGwTuh.json
│
└── README.md

## Anggota Kelompok 
Baginda parulian Siregar - NIM 1 - [@usernameGitHub1]
M FAATHIR - NIM 2 - [@usernameGitHub2]
Daniel Putra Nugraha - NIM 3 - [@usernameGitHub3]
