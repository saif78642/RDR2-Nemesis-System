# Changelog

All notable changes to the **EnRAGE — Living World, Nemesis & Frontier Civic Expansion** repository are documented in this file.

## [0.9.5.3-civic] - 2026-10-28

### Added
- **Civic, Politics & Police Desk (`F8` Hotkey & Telegraph Office Integration)**:
  - Added [`CivicSystem.cs`](decompiled/NemesisMod/NemesisSystem/CivicSystem.cs) and [`NemesisMod.Civic.cs`](decompiled/NemesisMod/NemesisSystem/NemesisMod.Civic.cs) (`CivicState` schema `Version = 2`, persisted to `scripts/NemesisData/politics.json`).
  - Added `CivicMenuKey` (`CivicMenu=F8` in `[Keys]` of `NemesisMod.ini`) and a **Civic, Politics & Police Desk** entry in all Telegraph Office (`T`) menus.
  - Added a **Politics, Law & Commerce** column to in-game Town Newspapers (`PopulateCivicNewspaper`).
- **2 New Law-Enforcement Factions (`Gangs.cs`)**:
  - **Pinkerton Detective Agency (`pinkertons`)**: Led by Agent Edgar Ross (`INSPECTORS` -> `DETECTIVES` -> `AGENTS`).
  - **U.S. Marshals Service (`marshals`)**: Led by Marshal Tom Davies (`U.S. MARSHALS` -> `SHERIFFS` -> `DEPUTIES`).
- **9 Frontier Civic & Underworld Subsystems**:
  1. **Town Politics, Mayors, Elections & Regimes**: 9 towns (`Valentine`, `Rhodes`, `Saint Denis`, `Strawberry`, `Blackwater`, `Annesburg`, `Van Horn`, `Armadillo`, `Tumbleweed`), 4 political regimes (`Reformist`, `CorruptMachine`, `FrontierPopulist`, `MartialLaw`), 7-day elections, ballot rigging, and live 3D Mayoral Rally encounters with emergency succession on assassination.
  2. **Tiered Law Enforcement, Detectives & Bribery**: Town-level Case Heat (`0–100%`), Warrant Tiers (`Clear`, `Misdemeanor`, `Felony`, `DeadOrAlive`), 24-hour Sheriff Immunity Bribes, Clerk Case Expungement, Police Wiretap Intel, and Marshal Posse Dispatches.
  3. **Martial Law, Night Curfews & Road Checkpoints**: `22:00–05:00` Night Curfew enforcement against masks/drawn firearms in town, plus physical Pinkerton/Marshal roadblock checkpoints (`wagon02x` + 4 riflemen).
  4. **Bounty Hunting Guild & Live Hogtie Jail Turn-Ins**: Dead-or-Alive `BountyContract` targets (`100%–150%` payout when hogtied alive vs. `50%` dead) and contextual live turn-in for any hogtied gang member or Nemesis within `22m` of a Town Sheriff / Civic Desk.
  5. **County Court, Prison Wagon Convoys & Jailbreaks**: Scheduled public hangings on the County Court Docket, legal bailouts for gang Treasury intel, and physical 3D `wagonPrison01x` prison convoy ambushes escorted by 4 mounted U.S. Marshals.
  6. **Town Business Protection Rackets & Frontier Bank**: 45 town shops (`Saloon`, `Gunsmith`, `GeneralStore`, `Doctor`, `Hotel`) supporting legal partnerships (`100%` daily yield, upgradable to Level 5), protection rackets (`135%` daily yield), 3D turf liberation shootouts against rival gang enforcers, and a Frontier Bank (`+5%/day` savings interest, `8%/day` commercial loans).
  7. **Stagecoach Payroll Heists & Armed Escort Contracts**: Spawn physical armored payroll stagecoaches (`coach2` + 4 Pinkerton/Marshal guards) to rob for `\$250`, or escort town payrolls against 5 ambushing road agents for `\$135`.
  8. **Newspaper Press Control, Propaganda & False-Flag Framing**: Pay newspaper editors to print Heroic Puff Pieces, Mayoral Smear Campaigns, or False-Flag Gang Framing headlines (`ShiftRelation(..., -65, force: true)`) that immediately ignite Gang Wars.
  9. **Saloon Prize-Fight Clubs & Moonshine Stills**: Bet `\$50` to win `\$150` in 1-on-1 bare-knuckle saloon championship bouts, and install backroom Moonshine Stills (`+\$25/day` illicit saloon revenue).
- **Human Arterial & Organ Bleedout (`BulletImpactModule.cs`)**:
  - Extended 3D X-Ray anatomical bleedout (`CheckHumanBleed` / `UpdateHumanBleeds`) to human NPCs when a bullet severs the carotid artery (`6s`), femoral artery (`9s`), or pierces the trachea / both lungs (`10s`).
- **Compilable Decompiled Project, Reference Stubs & Tooling**:
  - Added synthesized `ScriptHookRDRNetAPI.Stub.cs` and `ScriptHookRDRNetAPI.dll` (`405/405` `RDR2.*` API members verified by `ApiCheck.Missing`).
  - Added root `build.cmd` and `verify.ps1` for one-command compilation and 6-point verification.
  - Added interactive HTML class diagrammer (`decompiled/diagrammer/index.html`) and all 14 `.ini`/`.json` modding templates in `config_templates/`.

### Fixed & Restored
- **Live Lawman Arrests (`ArrestedByLaw`)**: NPCs and Nemeses arrested by town lawmen in front of the player are now automatically booked onto the nearest town's County Court Docket (`BookArrestedPedIntoCourt`).
- **Companion Mask Outfit Restoration (`Companion.SavedOutfit`)**: `SetMaskItem` now records `PED._GET_PED_META_OUTFIT_HASH` before equipping a bandana so `SetMask(c, on: false)` restores the companion's original outfit.
- **Physical Cash Handover Prop (`MoneyProp` & `Convo.PayAmount`)**: Initialized `MoneyProp = Joaat.Of("p_foldedbillssmall01x")` and wired `c.PayAmount` during interrogation bribes.
- **Provocation Insult Streak (`InsultStreak` & `InsultLimitReached`)**: Consecutive insults now increment `InsultStreak` (`+4` bonus grudge on the 3rd insult, `InsultLimitReached = true` at 4+).
- **Interrogation Camp & Kin Tracking (`AskedCamp` & `AskedKin`)**: Wired `it.AskedKin = true` in `ConfessKin` and `it.AskedCamp = true` in `Answer("holedup")`.
- **Funeral Wagon Procession (`FuneralWagon`) & Burned Supply Crates (`LiveSupply.Burned`)**: Restored coffin-wagon spawning in `StartRideIn` and set `ls.Burned = true` in `SetCrateAlight`.
- **Zero-Allocation Hot-Path Optimizations**: Replaced per-ped `Game.GetModelDimensions` calls with `PedsAround(Ped)` spatial caching and replaced the `CS9236` nested LINQ lambda in `XRayRenderer.cs` with zero-allocation `for` loops (**0 compiler warnings, 0 errors**).
