# EnRAGE — Living World, Nemesis & Frontier Civic Expansion (`v0.9.5.3-civic`)

[![Build Status](https://img.shields.io/badge/build-passing%20(0%20errors%2C%200%20warnings)-brightgreen)](#building-from-source--verification)
[![Target Framework](https://img.shields.io/badge/.NET%20Framework-4.8%20(x64)-blue)](#building-from-source--verification)
[![API Verification](https://img.shields.io/badge/ScriptHookRDRNetAPI-405%2F405%20verified-success)](#repository-structure)

A complete reverse-engineered, decompiled, optimized, and expanded C# source repository for **EnRAGE — Living World and Nemesis System (`v0.9.5.3-civic`)** for *Red Dead Redemption 2*, featuring:
- **100% Compilable C# Source Tree** (`170` `.cs` files targeting `.NET Framework 4.8` `x64` with **0 errors and 0 warnings**).
- **9 Real-World Frontier, Police, Politics & Economy Subsystems** accessible anywhere via **`F8`** or any **Telegraph Office clerk (`T`)**.
- **2 New Law-Enforcement Factions**: **Pinkerton Detective Agency (`pinkertons`)** and **U.S. Marshals Service (`marshals`)**.
- **3D X-Ray Ballistics, Permanent Crippling & Human Arterial/Organ Bleedout**.
- **All 14 Modding & Configuration Templates** (`.ini` and `.json`) ready to customize in [`config_templates/`](config_templates/).

---

## Repository Structure

```text
├── build.cmd                         # One-click root build script (compiles & updates scripts\NemesisMod.dll)
├── verify.ps1                        # Automated 6-point build, API, INI, and JSON schema verification suite
├── README.md                         # Project overview, installation, controls, and build instructions
├── CHANGELOG.md                      # Detailed changelog for the v0.9.5.3-civic expansion
├── docs/
│   └── ARCHITECTURE_AND_GUIDE.md     # Complete reverse-engineering, mechanics formulas & architecture guide
├── scripts/                          # Ready-to-install RDR2 ScriptHookRDR2 .NET binary folder
│   ├── NemesisMod.dll                # Compiled EnRAGE v0.9.5.3-civic assembly
│   └── Newtonsoft.Json.dll           # JSON serialization library
├── config_templates/                 # All 14 default .ini & .json configuration and modding templates
│   ├── NemesisMod.ini                # Master INI settings (keybinds, ambush rates, cheat-death, gang wars)
│   ├── BulletImpact.ini              # 3D X-Ray ballistics & crippling settings
│   ├── politics.json                 # Default Civic, Politics, Police, Court & Business state (9 towns)
│   ├── factions.json                 # All 13 built-in factions (including Pinkertons & U.S. Marshals)
│   ├── places.json                   # 15 districts & 12 towns
│   ├── lives.json                    # Procedural NPC backstories & Civil War history
│   ├── world_life.json               # Autonomous NPC life events
│   ├── tales.json                    # World lore & faction tales
│   ├── questions.json                # Interrogation questions & fear thresholds
│   ├── wildcards.json                # WorldSim turmoil wildcards
│   ├── doors.json                    # 2,000+ unlockable RDR2 building doors
│   ├── camps.json                    # Custom gang camp definitions
│   ├── camps_builtin_reference.json  # Reference dump of all 97 built-in gang camps & hideouts
│   └── cards/
│       └── builtin.json              # Built-in dynamic encounter cards
└── decompiled/
    ├── NemesisMod/                   # Full C# source project (NemesisMod.csproj, build.ps1, 170 .cs files)
    │   ├── NemesisSystem/            # Core Nemesis, WorldSim, Factions, Companion & CivicSystem classes
    │   └── NemesisSystem/BulletImpact/ # 3D X-Ray ballistics, anatomy raycaster, crippling & bleedout
    ├── refs/                         # Synthesized ScriptHookRDRNetAPI.Stub.cs & verified .dll (405/405 APIs)
    └── diagrammer/                   # Interactive HTML/JS class relationship diagrammer (open index.html)
```

---

## Installation (In-Game Usage)

1. Ensure **ScriptHookRDR2** and **ScriptHookRDR2DotNet (v2.2.0.0)** are installed in your *Red Dead Redemption 2* root directory.
2. Copy the [`scripts/`](scripts/) folder (`NemesisMod.dll` and `Newtonsoft.Json.dll`) into your *Red Dead Redemption 2* game directory.
3. *(Optional)* Copy `NemesisMod.ini` and `BulletImpact.ini` from [`config_templates/`](config_templates/) into your `scripts/` folder to customize keybinds and gameplay balance.

---

## Default In-Game Controls & Hotkeys

| Key / Input | Action | Config Key (`NemesisMod.ini`) |
| :---: | :--- | :--- |
| **`F11`** | Open **Nemesis Roster, Faction Hierarchy & World Map Menu** | `MenuKey=F11` |
| **`F8`** | Open **Civic, Politics, Police, Bounties & Business Desk** anywhere | `CivicMenu=F8` |
| **`Alt + F11`** | Open In-Game Settings Overlay | `SettingsKey=F11` (`+Alt`) |
| **`T`** *(Near Clerk)* | Open **Telegraph Office** (includes *Civic, Politics & Police Desk*) | Contextual Prompt |
| **`N`** *(In Menu)* | Read **Town Newspaper Broadsheet** (includes *Politics, Law & Commerce*) | Menu Prompt |
| **`I`** / **Hold Aim** | **Interrogate / Provoke / Recruit / Bribe** targeted NPC or Nemesis | `InterrogateKey=I` |
| **`F10`** | Force a dynamic Nemesis / Faction Ambush (`DebugKeys=true`) | `AmbushKey=F10` |

---

## Key Features in `v0.9.5.3-civic`

### 1. Town Politics, Mayors, Elections & Regimes (`civic:politics`)
- Every major town (`Valentine`, `Rhodes`, `Saint Denis`, `Strawberry`, `Blackwater`, `Annesburg`, `Van Horn`, `Armadillo`, `Tumbleweed`) runs a live **7-day Election Cycle** with a sitting **Mayor**, **Challenger**, **Approval (`0–100%`)**, **Corruption (`0–100%`)**, and **Tax Rate**.
- **4 Political Regimes**:
  - `Reformist`: Heavy Pinkerton/Marshal patrols; intolerant of corruption.
  - `CorruptMachine`: Cheap Sheriff bribes; pays allied players a **`10%` daily cut of town tax revenue**.
  - `FrontierPopulist`: Low taxes and relaxed local law enforcement.
  - `MartialLaw`: Strict **`22:00–05:00` Night Curfew** (masks or drawn weapons trigger immediate lawman warnings) and armed roadblocks.
- **Mayoral Rally Encounter**: Summon the sitting Mayor and 3 Pinkerton bodyguards into the 3D world; assassinating the Mayor triggers an emergency succession where the Challenger immediately takes office.

### 2. Tiered Law Enforcement, Detectives & Bribery (`civic:police`)
- Added **Pinkerton Detective Agency (`pinkertons`)** and **U.S. Marshals Service (`marshals`)** to the faction hierarchy.
- Each town tracks **Case Heat (`0–100%`)**, an active **Lead Detective**, and a **Warrant Tier** (`Clear`, `Misdemeanor`, `Felony`, `DeadOrAlive`).
- **Bribe the Town Sheriff** for **24 in-game hours of local law immunity**, **Expunge Case Files** via corrupt clerks, **Buy Police Wiretap Intel** to expose a gang's Treasury, or **Dispatch a Marshal Posse** against a rival faction.

### 3. Bounty Hunting Guild & Live Hogtie Jail Turn-Ins (`civic:bounties`)
- Accept **Dead-or-Alive Wanted Contracts** on outlaw captains: capture and hogtie the target alive for the full **`100%–150%` Alive Payout + Town Reputation + County Court booking**, or kill them for the `50%` Dead payout.
- Bring *any* hogtied gang member or Nemesis within `22m` of a Town Sheriff / Civic Desk and select **`TURN IN HOGTIED CAPTIVE ALIVE`** for **`+\$160`**, `-20` Case Heat, `+Town Rep`, and automatic Court Docket booking.

### 4. County Court, Prison Wagon Convoys & Jailbreaks (`civic:court`)
- Captured outlaws, companions, and NPCs arrested by lawmen in front of you (`ArrestedByLaw`) appear on the **County Court Docket** with a scheduled **Public Hanging** date.
- **Pay Court Bail** to release a prisoner legally in exchange for gang Treasury intel, or **Ambush the Prison Wagon Convoy** (`wagonPrison01x` escorted by 4 mounted U.S. Marshals) in the 3D world.

### 5. Town Business Protection Rackets & Frontier Bank (`civic:business`, `civic:bank`)
- **45 Town Businesses** (`Saloon`, `Gunsmith`, `GeneralStore`, `Doctor`, `Hotel` across 9 towns) can be bought as **Legal Partnerships** (`100%` daily income, upgradable to Level 5), **Extorted as Protection Rackets** (`135%` daily income), or **Liberated from Rival Gang Enforcers** in a 3D turf shootout.
- **Frontier Bank & Trust** pays **`+5%` daily compound interest** on savings and offers commercial loans (`8%` daily interest).

### 6. Stagecoach Payroll Heists & Armed Escort Contracts (`civic:heists`)
- **Rob Armored Payroll Stagecoach**: Buy a `\$30` clerk tip to spawn a federal payroll coach (`coach2`) guarded by 4 Pinkerton/Marshal riflemen; eliminate the escort to loot **`\$250`** from the strongbox.
- **Accept Mayor's Payroll Escort Contract**: Defend the town's stagecoach against 5 ambushing gang road agents to earn **`\$135` + `+10` Mayor Approval + `-20` Case Heat**.

### 7. Newspaper Press Control & False-Flag Gang Wars (`civic:press`)
- Pay the town's newspaper editor to print a **Heroic Puff Piece (`\$45`)**, a **Mayoral Smear Campaign (`\$75`)**, or **Frame a Rival Gang (`\$110`)** with a false-flag massacre headline that immediately ignites a **Gang War** between two outlaw factions.

### 8. Saloon Prize-Fight Clubs & Moonshine Stills (`civic:saloon`)
- **Bare-Knuckle Championship Bout**: Stake **`\$50`** to fight a `320 HP` unarmed saloon bruiser 1-on-1 for a **`\$150` purse**.
- **Backroom Moonshine Still (`\$140`)**: Upgrades the town's Saloon to generate **`+\$25/day`** in illicit revenue.

---

## Building from Source & Verification

### Prerequisites
- Windows with **.NET Framework 4.8 Developer Pack / Runtime** and **Visual Studio 2022 / 2026** (or Build Tools with Roslyn `csc.exe`).

### One-Click Build
From the repository root, run:
```cmd
build.cmd
```
This compiles all `170` `.cs` files in [`decompiled/NemesisMod/`](decompiled/NemesisMod/) and copies the resulting `NemesisMod.dll` directly into [`scripts/NemesisMod.dll`](scripts/NemesisMod.dll).

### Automated 6-Point Verification Suite
To build from source and verify all `405` `RDR2.*` API bindings, all `13` factions, `.ini` settings loaders, and `.json` templates with zero errors, run:
```powershell
powershell -ExecutionPolicy Bypass -File verify.ps1
```

For deep-dive technical documentation, formulas, and architecture diagrams, see [`docs/ARCHITECTURE_AND_GUIDE.md`](docs/ARCHITECTURE_AND_GUIDE.md).
