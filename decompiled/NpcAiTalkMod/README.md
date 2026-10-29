# NpcAiTalk — Zero-Setup 1899 Frontier AI NPC Dialogue Mod (`v1.0.0`)

A self-bootstrapping, zero-manual-setup Small Language Model (SLM) NPC Dialogue Mod for *Red Dead Redemption 2* that runs **both 100% Standalone** AND **Deeply Integrated with EnRAGE (`NemesisMod.dll`)**.

---

## Key Features

1. **100% Zero-Manual-Setup (Automatic First-Run Bootstrapper)**:
   - No Python, no `pip`, and no manual Hugging Face downloads required.
   - When you drop `NpcAiTalk.dll` into `scripts/` and launch *Red Dead Redemption 2*, [`AutoBootstrapper.cs`](NpcAiTalk/AutoBootstrapper.cs) automatically downloads:
     1. Precompiled native C++ `llama-server.exe` runtime into `scripts\NpcAiTalk\bin\`.
     2. Quantized **`Qwen2.5-0.5B-Instruct` (`qwen2.5-0.5b-instruct-q4_k_m.gguf`, ~352 MB)** into `scripts\NpcAiTalk\models\`.
     3. Optional 1899 Frontier Piper ONNX voice pack into `scripts\NpcAiTalk\models\`.
   - Displays a real-time download progress bar on the top of the HUD (`[AI Talk Mod] Downloading Qwen2.5-0.5B Model... 45%`).
   - Automatically starts `llama-server.exe` silently in the background (`ProcessPriorityClass.BelowNormal`, `0 MB` GPU VRAM impact) and automatically terminates it when RDR2 exits.

2. **Dual-Mode Operation (Standalone + EnRAGE `NemesisMod` Integrated)**:
   - **When used alongside `NemesisMod.dll` (`EnRAGE LINKED` mode)**:
     - [`NemesisBridge.cs`](NpcAiTalk/NemesisBridge.cs) connects to `NemesisMod.Instance` via zero-dependency reflection (`ExportAiNpcContext` & `RecordAiDialogueOutcome`).
     - Every NPC conversation automatically incorporates the NPC's exact **Nemesis Record** (Full Name, Title, Rank, Gang/Faction, Boss Name, Hate `0–100`, Fear `0–100`, Personal Grudge, Scars, Crippled Limp, Quirks, 1899 Backstory, and Past Encounter History) AND live **Town Civic & Political State** (Sitting Mayor, Challenger, Political Regime, Tax Rate, Corruption, Sheriff, Lead Detective, Warrant Tier, Active Bounties, and County Jail Court Docket).
     - Conversation outcomes dynamically update the Nemesis's **Hate**, **Fear**, **Camp Intel (`CampKnown`)**, and **Encounter History** inside `NemesisMod`!
   - **When used completely independently without `NemesisMod.dll` (`STANDALONE 1899` mode)**:
     - [`StandaloneWorld.cs`](NpcAiTalk/StandaloneWorld.cs) provides its own complete 1899 Town & District coordinate resolver (Valentine, Rhodes, Saint Denis, Strawberry, Blackwater, Annesburg, Van Horn, Armadillo, Tumbleweed, Emerald Ranch, Lagras), Ped model & lawman classifier, deterministic 1899 Name Bank, Backstory generator, and persistent NPC conversation memory (`scripts\NpcAiTalk\memory\npc_memories.json`).

3. **Multi-Layer Voice & Lip-Sync Engine ([`VoiceSynthesizer.cs`](NpcAiTalk/VoiceSynthesizer.cs))**:
   - Triggers authentic RDR2 3D Ped facial animations and vocal speech contexts via native `PLAY_PED_AMBIENT_SPEECH_NATIVE` (`0x8E04FEDD28D42462`).
   - Synthesizes spoken audio via **Piper Neural ONNX TTS** (if present) or **Zero-Download Windows Offline SAPI Speech Synthesis** (works immediately on 100% of Windows 10/11 PCs with zero extra downloads).

4. **Dynamic AI-Driven In-Game NPC Reactions**:
   - Based on how you speak to an NPC, the AI model emits action tags (`[ACTION:ATTACK]`, `[ACTION:SURRENDER]`, `[ACTION:FLEE]`, `[ACTION:INTEL]`, `[ACTION:CALM]`) that trigger real 3D world behaviors on the NPC!

---

## In-Game Controls

| Key | Action | Config Key (`NpcAiTalk.ini`) |
| :---: | :--- | :--- |
| **`Y`** | Open **Western AI Dialogue Overlay** with aimed or nearest NPC | `TalkKey=Y` |
| **`G`** | **Quick AI Greet** (instant spoken AI greeting & toast reply) | `QuickGreetKey=G` |
| **`T`** | Open **RDR2 On-Screen Keyboard** to type custom words to the NPC | `CustomInputKey=T` |
| **`F9`** | Open **AI Engine Status, Model Preset Switcher & Control Panel** | `StatusMenuKey=F9` |
| **`1`–`7` / `Arrows + Enter`** | Select dialogue option in the conversation overlay | Built-in |
| **`Esc` / `Backspace`** | Close conversation or status menu | Built-in |

---

## Building from Source

Run [`build.cmd`](build.cmd) in this directory or the root [`build.cmd`](../../build.cmd) to compile `NpcAiTalk.dll` with zero errors and zero warnings.
