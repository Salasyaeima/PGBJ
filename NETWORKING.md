# Networking Decision Record — Coin Rush 2P

## 1. Installed Versions (Verified)

Quoted from project configuration files:

- **`ProjectSettings/ProjectVersion.txt`:**
  - `m_EditorVersion: 6000.6.10f1`
- **`Packages/manifest.json`:**
  - `"com.unity.netcode.gameobjects": "2.14.0"`
  - `"com.unity.services.multiplayer": "2.5.0"`
  - `"com.unity.multiplayer.playmode": "3.0.0"`
- **`Packages/packages-lock.json`:**
  - `"com.unity.netcode.gameobjects": { "version": "2.14.0" }`
  - `"com.unity.services.multiplayer": { "version": "2.5.0" }`
  - `"com.unity.multiplayer.playmode": { "version": "3.0.0" }`
  - `"com.unity.transport": { "version": "6.6.0" }`

## 2. Selected Stack, Topology, and Authority

- **Netcode Stack:** **Netcode for GameObjects (`com.unity.netcode.gameobjects` `2.14.0`)** with `NetworkObject`, `NetworkBehaviour`, `NetworkVariable<T>`, `[Rpc]`, and `NetworkTransform` (`AuthorityModes.Owner` on players; `AuthorityModes.Server` on spawned coins).
- **Session & Connectivity:** **Unity Multiplayer Services (`com.unity.services.multiplayer` `2.5.0`)** using `MultiplayerService.Instance.CreateSessionAsync` / `JoinSessionByCodeAsync` with `.WithRelayNetwork()` for zero-config P2P room-code connections across networks (plus anonymous `AuthenticationService` sign-in), and `NetworkManager.SceneManager.LoadScene` for synchronized scene transitions from `Lobby` to `CoinRush`.
- **Topology & Authority:** **Client-Hosted Listen Server (P2P Host-Client)**.
  - **Requirement deciding the stack:** 2-player casual top-down GameObject/Rigidbody game without hit-rewind/rollback needs, where short interpolation delay on remote collisions is acceptable (confirmed by user), and single-screen coin spawning/pickup/scoring requires authoritative single-writer resolution on the Host.

## 3. Replication & Lifecycle Design

| State / Event | Authority (Writer) | Replication Mechanism | Local Presentation |
| :--- | :--- | :--- | :--- |
| Player Movement (`WASD`) | Each Player (`IsOwner`) | `NetworkTransform` (`AuthorityModes.Owner`, interpolated) | `CharacterMotor` + `PlayerInputProvider` enabled only on `IsOwner` |
| Coin Spawn / Despawn | Host (`IsServer`) | `NetworkObject.Spawn` / `Despawn` on registered `Coin` prefab | Local spin/bob visual + `CoinPickupVFX` triggered via `[Rpc(SendTo.ClientsAndHost)]` |
| Coin Pickup & Score | Host (`IsServer`) | `NetworkVariable<int>` (`P1Score`, `P2Score`) | `GameHUD` updates on `OnValueChanged`; `SynthAudioManager.PlayCoinPickup` via RPC |
| Match State & Timer | Host (`IsServer`) | `NetworkVariable<int>` (`MatchState`), `NetworkVariable<float>` (`RemainingTime`), `NetworkVariable<FixedString32Bytes>` (`CountdownText`) | `GameHUD` countdown, timer pulse, result screen, `SynthAudioManager` cues via RPC |
| Player Names | Session / Host (`IsServer`) | `NetworkVariable<FixedString64Bytes>` (`P1Name`, `P2Name`) | Lobby slot list & HUD badges |
| Offline Testing Mode | Local Single-Process | Runs `GameManager` + `AIInputProvider` directly when `NetworkManager` is not listening | Identical gameplay & SFX without network requirement |

## 4. Verification Log

### Attempt 1 (Caught & Fixed)
- **Symptom:** In MPPM run (`Main Editor` + `Player 2`), Host spawned `NetworkPlayer`s immediately inside `NetworkMatchController.OnNetworkSpawn()` before `Player 2` finished loading `CoinRush.unity`, while `RemainingTime.Value` updated every frame flooded the 128-packet receive queue (`Receive queue is full`, `[Deferred OnSpawn]` for `id (2)`).
- **Fix:** Replaced per-frame `RemainingTime` `NetworkVariable<float>` updates with a single `MatchEndServerTime` (`NetworkVariable<double>`) deadline synced via `NetworkManager.ServerTime.Time`, and gated `TryBeginHostedMatch()` behind `NetworkManager.SceneManager.OnLoadEventCompleted` (with `WaitForSceneSyncFallbackRoutine`).

### Attempt 2 (Passing Cross-Process MPPM Verification)
- **Scenario:** `Assets/Settings/PlayMode/CoinRushMPPM.asset` (`Main Editor` = Host, `Player 2` = MPPM Clone `mppm15f22eaf`, `runInBackground = true`).
- **Relay Session & Connect:** Host created Relay session (`MPMQG7`, `IsHost: true`); `Player 2` joined via `JoinSessionByCodeAsync("MPMQG7")` (`IsConnectedClient: true` in `2727 ms`, `ConnectedClientsIds.Count == 2`). Host transitioned both instances to `CoinRush` (`MatchStateValue == 1` in `2914 ms`).
- **Gameplay Event & Replication Check (Full Match to `GameOver`):**
  - **Host (`Main Editor`):** `{"scene":"CoinRush","isHost":true,"isConnectedClient":true,"transport":"UnityTransport","localClientId":0,"connectedCount":2,"spawnedNetObjects":9,"hasNmc":true,"matchState":2,"p1Score":1,"p2Score":4,"p1Name":"Player","p2Name":"ClientP2","remainingTime":0.0,"activeCoins":6,"players":["slot=0,name=Player,owner=True,pos=(-3.77, 0.55, -3.43)","slot=1,name=ClientP2,owner=False,pos=(3.81, 0.55, 1.06)"]}`
  - **Client (`Player 2`):** `{"scene":"CoinRush","isHost":false,"isConnectedClient":true,"transport":"UnityTransport","localClientId":1,"connectedCount":-1,"spawnedNetObjects":9,"hasNmc":true,"matchState":2,"p1Score":1,"p2Score":4,"p1Name":"Player","p2Name":"ClientP2","remainingTime":0.0,"activeCoins":6,"players":["slot=1,name=ClientP2,owner=True,pos=(3.81, 0.55, 1.06)","slot=0,name=Player,owner=False,pos=(-3.78, 0.55, -3.43)"]}`
- **Console Check:** 0 errors on `Main Editor` (`--since 33 --level error`), 0 errors on `Player 2` (`--level error`).
- **Not Yet Run (Final Testing Gap):** Standalone player build (`Build Profiles`) across two separate physical machines, and artificial high-latency/packet-loss stress testing (`Multiplayer Tools` Network Simulator was not installed).

