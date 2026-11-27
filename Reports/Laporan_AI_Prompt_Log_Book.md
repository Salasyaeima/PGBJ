# AI Prompt Log Book — Coin Rush 2P

**Project Generative AI for Game: Video Tutorial Series**

- **Judul Prototipe Game:** Coin Rush 2P (Top-Down 3D Arcade Collector: Vs AI & Peer-to-Peer)
- **Identitas Penyusun:** Nama Lengkap / NRP. ________________________ (Program Studi Teknologi Game, PENS 2026)
- **Engine & Paket Utama:** Unity `6000.6.10f1` (URP `17.6.0`), Input System `1.20.1`, Netcode for GameObjects `2.14.0`, Multiplayer Services `2.5.0`, Multiplayer Play Mode `3.0.0`
- **Area Generative AI yang Diterapkan (4 Area):** GDD & Desain Sistem, Visual Assets & Rig 3D/UI, Audio Synthesis Prosedural (SFX), serta Code & Debugging Assistant.

---

## 1. Daftar Tools AI yang Digunakan

| Tool / Model AI | Kategori Area GenAI | Fungsi Utama dalam Proyek | Output Nyata di Proyek |
| :--- | :--- | :--- | :--- |
| **Antigravity IDE + Gemini 4 Hydrogen** | Ideation, GDD & Code Assistant | Penyusunan GDD, arsitektur dual-mode (Vs AI & P2P), penulisan C# Unity, dan analisis error. | `GDD_CoinRush2P.md`, `NETWORKING.md`, dan 15 skrip C# di `Assets/Scripts/`. |
| **Unity Agent Plugin & Unity CLI (`1.0.1`)** | Engine Integration & Debugging | Kendali langsung Unity Editor (kompilasi, instalasi UPM, edit scene/prefab, MPPM, dan build). | Scene `Lobby.unity` & `CoinRush.unity`, `CoinRushMPPM.asset`, build `CoinRush2P.exe`. |
| **AI-Driven DSP Audio Synthesis** | Audio & Atmosphere (SFX) | Sintesis 10 efek suara elektronik secara prosedural lewat `AudioClip.Create` tanpa aset audio eksternal. | `SynthAudioManager.cs` (10 klip `44100 Hz`: koin, countdown, tabrakan, fanfare). |
| **AI-Driven 3D Rig & uGUI Builder** | Visual Assets & UI | Perakitan rig karakter 3D multi-bagian, material URP Lit, partikel pickup, dan layout uGUI Lobi/HUD. | `CharacterVisualController.cs`, `NetworkPlayer.prefab`, `LobbyUI.cs`, `GameHUD.cs`. |

---

## 2. Catatan Perbandingan: Initial Prompt vs Final Refined Prompt

### Iterasi 1 — Ideasi Gim, Kontrol WASD & Mode Uji AI (GDD & Arsitektur)

- **Initial Prompt:** *"bantu aku mendesain gim sederhana tentang 2 player yang berlomba mengumpulkan koin selama batas waktu tertentu sebanyak banyaknya, kontrol nya menggunakan wasd seperti biasa dengan new input system, gamenya topdown, area nya hanya sederhana sepetak tanah..."*
- **Kelemahan Awal:** Belum ditentukan apakah dua pemain memakai satu keyboard atau bermain daring, serta belum diputuskan gaya visual 2D atau 3D.
- **Final Refined Prompt:** *"aku berencana membuatnya menjadi multiplayer peer to peer, namun untuk pengetesan aku butuh agar lawannya menggunakan AI biasa saja"* + *"3d top down"*.
- **Hasil & Kurasi Manual:** Mengunci tampilan 3D *top-down* dan memisahkan kontrak `IInputProvider` sehingga `PlayerInputProvider` (`WASD`) dan `AIInputProvider` memakai motor gerak `CharacterMotor` yang sama. Ditambahkan jeda keputusan AI (`0.18s`) dan *sidestep* ringan agar bot tidak macet saat bersenggolan.

### Iterasi 2 — Audio Feedback Mekanik (Audio Synthesis Prosedural)

- **Initial Prompt:** *"aku butuh suara sfx untuk feedback mekanik, buat ini dari syntesizer elektronik didalam game"*
- **Kelemahan Awal:** Belum menyebutkan daftar kejadian mekanik yang membutuhkan suara, karakter gelombang, maupun penanganan *pop/clipping*.
- **Final Refined Prompt & Spesifikasi:** Diuraikan menjadi 10 klip audio sintetis `44100 Hz` pada `SynthAudioManager.cs`: pickup koin P1 (`B5 -> E6` *SoftSquare*), pickup koin lawan (*Triangle* lebih pelan), *countdown* (`3-2-1`), `GO!`, detak 10 detik terakhir, *bump* tabrakan (`165 -> 62 Hz`), klik UI, serta fanfare menang/kalah/seri.
- **Hasil & Kurasi Manual:** Menerapkan *attack/release envelope* agar bebas bunyi klik, membatasi amplitudo puncak di bawah `0.55f`, dan menambahkan *cooldown* `0.25s` pada bunyi tabrakan.

### Iterasi 3 — Lobi P2P Kode Room & Troubleshooting Sinkronisasi (Code & Debugging)

- **Initial Prompt:** *"lanjut ke fase 2, aku juga ingin loby gamenya juga ... jika bermain online cukup sampai peer to peer saja, aku tidak tahu bagaimana cara menyambungkan kedua pemain ini"*
- **Kelemahan Awal:** Koneksi P2P IP langsung sulit dilakukan antar-jaringan tanpa *port forwarding* dan rentan *desync* ketika dua pemain berebut koin yang sama.
- **Final Refined Prompt & Arsitektur:** Memakai **Netcode for GameObjects (`2.14.0`)** + **Unity Multiplayer Services (`2.5.0`)** dengan sistem **Kode Room 6 karakter melalui Unity Relay**, kontrol input hanya di pemilik (`IsOwner`), serta validasi spawn/pickup/skor otoritatif di Host (`IsServer`).
- **Troubleshooting Saat Uji Lintas Proses (MPPM):**
  1. **Bug Awal:** Uji MPPM pertama memunculkan `[Deferred OnSpawn]` dan `Receive queue is full (128)` sehingga `NetworkPlayer` tidak muncul di Client, akibat spawn terlalu dini sebelum `OnLoadEventCompleted` dan update `RemainingTime` dikirim setiap frame.
  2. **Solusi:** Mengganti timer menjadi satu nilai `MatchEndServerTime` berbasis `NetworkManager.ServerTime.Time` dan menunggu sinkronisasi scene (`OnLoadEventCompleted`) sebelum spawn pemain.
  3. **Audit Build:** Menghapus `moveAction.action.Disable()` di `PlayerInputProvider.OnDisable()` agar menonaktifkan proxy pemain lain tidak ikut mematikan kontrol `WASD` pemain lokal.

### Iterasi 4 — Model Karakter 3D Beridentitas & Kesiapan Build (Visual & Hardening)

- **Initial Prompt:** *"perbarui model karakter, tambahkan detail atau identitas yang lebih menonjol"* dan *"siapkan agar project ini aman untuk dibuild dan tidak ada kendala"*
- **Kelemahan Awal:** Kapsul polos kurang memperjelas arah hadap maupun identitas pemain dari kamera *top-down*; proyek juga belum memiliki tombol keluar aplikasi dan penanganan *disconnect* pada build `.exe`.
- **Final Refined Prompt & Kurasi:** Merakit rig 3D multi-bagian pada `CharacterVisualController.cs` (cincin tim bercahaya, tas koin emas, visor bermata menyala, antena *commander* P1 biru vs tanduk kembar P2/AI merah, *name tag* menghadap kamera, dan animasi lari), ditambah tombol **Keluar Gim**, **Kembali ke Lobi**, penanganan Host/Client *disconnect*, serta verifikasi build penuh `Builds/CoinRush2P.exe` (`0 error, 0 warning`).

**Gambar 1.** *Hasil iterasi visual karakter 3D beridentitas (Player 1 Antena Biru vs Lawan Tanduk Merah).*

**Gambar 2.** *Hasil pengujian multiplayer P2P lintas proses (MPPM) pada scene CoinRush.*

---

## 3. Evaluasi Limitasi, Kurasi Manual & Refleksi Penggunaan AI

### A. Kurasi Manusia & Verifikasi Teknis
- Memastikan seluruh API jaringan (`[Rpc(SendTo.*)]`, `NetworkTransform.AuthorityModes.Owner`, `WithRelayNetwork()`) diverifikasi langsung pada `Library/PackageCache` agar sesuai versi paket terpasang.
- Menemukan dan memperbaiki bug yang tidak terlihat hanya dari membaca kode: memperbaiki teks *placeholder* Kode Room yang terpotong, memperbaiki `GameObject.Find("ResultPanel")` yang gagal pada panel non-aktif, serta memvalidasi paritas state pada Host dan Client lewat MPPM.

### B. Limitasi Teknis Model AI & Penanganannya
1. **Update Jaringan Berlebih:** Model AI awalnya memperbarui *timer* `NetworkVariable` setiap frame; diatasi dengan sinkronisasi *deadline* `NetworkManager.ServerTime.Time` satu kali.
2. **Perbedaan API Versi Unity:** Peringatan `FindFirstObjectByType` usang pada Unity `6000.6.10f1` ditemukan lewat `unity recompile` dan diperbarui ke `FindAnyObjectByType`.
3. **Pengujian Build Nyata:** Tetap dilakukan build penuh `StandaloneWindows64` (`CoinRush2P.exe`) untuk memastikan tidak ada aset atau skrip Editor yang mematahkan kompilasi rilis.

### C. Catatan Hak Cipta, Etika & Refleksi Tim
- **Orisinalitas & Hak Cipta:** Seluruh skrip C#, material URP, rig karakter 3D dari primitif Unity, tata letak uGUI, dan 10 klip SFX synthesizer prosedural dibuat langsung di proyek tanpa mengambil aset berhak cipta pihak ketiga. Proses iterasi prompt dan hasil uji dicatat secara transparan pada `NETWORKING.md` dan dokumen ini.
- **Refleksi:** Pendekatan *prompting* bertahap (GDD -> Prototipe Offline/AI -> Audio & Visual -> Lobi & P2P -> Audit Build) jauh lebih efektif dan mudah di-*debug* dibanding meminta seluruh gim sekaligus, namun pengujian langsung di Editor, MPPM, dan build `.exe` tetap wajib dilakukan untuk menjamin kualitas akhir.
