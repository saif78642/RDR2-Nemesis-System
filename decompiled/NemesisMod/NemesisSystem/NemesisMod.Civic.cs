using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using RDR2;
using RDR2.Math;
using RDR2.Native;

namespace NemesisSystem;

public partial class NemesisMod : Script
{
	private CivicState _civic;

	private int _nextCivicTick;

	private int _nextCurfewCheck;

	private Vehicle _activePrisonWagon;

	private Ped _activePrisonerPed;

	private PrisonerRecord _activePrisonerRec;

	private readonly List<Ped> _activeConvoyGuards = new List<Ped>();

	private Ped _activeMayorPed;

	private string _activeMayorTown;

	private readonly List<Ped> _activeMayorGuards = new List<Ped>();

	private readonly List<Ped> _activeRacketEnforcers = new List<Ped>();

	private TownBusiness _activeRacketBiz;

	private readonly List<Ped> _activeCheckpointGuards = new List<Ped>();

	private int _lastCheckpointSpawnAt = -100000;

	private Ped _activeBountyPed;

	private BountyContract _activeBountyContract;

	private readonly List<Ped> _activeBountyGuards = new List<Ped>();

	private Vehicle _activePayrollCoach;

	private bool _activePayrollIsEscort;

	private string _activePayrollTown;

	private readonly List<Ped> _activePayrollNpcs = new List<Ped>();

	private Ped _activePrizeFighter;

	private string _activePrizeFightTown;

	private static readonly (string town, string district, PoliticalRegime regime, string mayor, string sheriff, string detective, string backedGang, int corruption, int tax)[] DefaultTownPolitics = new (string, string, PoliticalRegime, string, string, string, string, int, int)[9]
	{
		("Saint Denis", "bayou_nwa", PoliticalRegime.CorruptMachine, "Mayor Henri Lemieux", "Chief Benjamin Lambert", "Inspector Edgar Ross", "bronte", 78, 10),
		("Blackwater", "great_plains", PoliticalRegime.Reformist, "Mayor Elihu Dunbar", "Chief Oswald Fawcett", "Agent Andrew Milton", "pinkertons", 18, 8),
		("Valentine", "heartlands", PoliticalRegime.FrontierPopulist, "Mayor Proctoravin", "Sheriff Curtis Malloy", "Marshal Tom Davies", "odriscoll", 35, 5),
		("Rhodes", "scarlett_meadows", PoliticalRegime.CorruptMachine, "Mayor Alden Gray", "Sheriff Leigh Gray", "Detective Charles Vance", "lemoyneraiders", 62, 7),
		("Strawberry", "big_valley", PoliticalRegime.Reformist, "Mayor Nicholas Timmins", "Sheriff Farley", "Marshal Harlan Wade", "laramie", 28, 6),
		("Annesburg", "roanoke_ridge", PoliticalRegime.CorruptMachine, "Magistrate Leviticus Jameson", "Sheriff Agnes Jones", "Inspector Arthur Boyle", "murfree", 68, 9),
		("Van Horn", "roanoke_ridge", PoliticalRegime.FrontierPopulist, "Harbormaster Silas Pike", "Constable Jude", "Agent Caleb Thorne", null, 55, 4),
		("Armadillo", "cholla_springs", PoliticalRegime.FrontierPopulist, "Mayor E. H. Mowry", "Marshal Leigh Johnson", "Marshal Sam Freeman", "delobo", 42, 4),
		("Tumbleweed", "gaptooth_ridge", PoliticalRegime.MartialLaw, "Magistrate Sam Freeman", "Sheriff Sam Freeman", "Marshal Eli Dalton", "marshals", 10, 7)
	};

	private string CivicPath => Path.Combine(_saveDir, "civic_state.json");

	private string PoliticsTemplatePath => Path.Combine(ContentDir, "politics.json");

	internal CivicState Civic => _civic ?? (_civic = LoadOrCreateCivicState());

	private CivicState LoadOrCreateCivicState()
	{
		try
		{
			if (File.Exists(CivicPath))
			{
				CivicState loaded = JsonConvert.DeserializeObject<CivicState>(File.ReadAllText(CivicPath));
				if (loaded != null && loaded.Towns != null && loaded.Towns.Count > 0)
				{
					EnsureCivicDefaults(loaded);
					return loaded;
				}
			}
		}
		catch (Exception ex)
		{
			GrudgeLog("CIVIC load failed: " + ex.Message);
		}
		CivicState fresh = new CivicState();
		EnsureCivicDefaults(fresh);
		SaveCivicState(fresh);
		return fresh;
	}

	private void EnsureCivicDefaults(CivicState state)
	{
		int dayNow = Math.Max(1, GameDayKey());
		if (state.Bounties == null)
		{
			state.Bounties = new List<BountyContract>();
		}
		foreach (var def in DefaultTownPolitics)
		{
			if (!state.Towns.TryGetValue(def.town, out var tp) || tp == null)
			{
				tp = new TownPoliticState
				{
					Town = def.town,
					DistrictKey = def.district,
					MayorName = def.mayor,
					SheriffName = def.sheriff,
					DetectiveName = def.detective,
					ChallengerName = PickChallengerName(def.town),
					Regime = def.regime,
					ChallengerRegime = (def.regime == PoliticalRegime.CorruptMachine) ? PoliticalRegime.Reformist : PoliticalRegime.CorruptMachine,
					Approval = 58,
					Corruption = def.corruption,
					ChallengerSupport = 44,
					TaxRatePct = def.tax,
					BackedFaction = def.backedGang,
					ElectionDay = dayNow + 7,
					MartialLawUntilDay = ((def.regime == PoliticalRegime.MartialLaw) ? (dayNow + 5) : 0),
					CaseHeat = 0,
					Warrant = WarrantTier.Clear
				};
				state.Towns[def.town] = tp;
			}
			EnsureTownBusinesses(state, def.town, def.backedGang);
		}
		if (state.Prisoners.Count == 0)
		{
			SeedDefaultPrisoners(state, dayNow);
		}
		if (state.Bounties.Count == 0)
		{
			SeedDefaultBounties(state);
		}
		try
		{
			if (!File.Exists(PoliticsTemplatePath))
			{
				SafeFile.WriteLater(PoliticsTemplatePath, () => JsonConvert.SerializeObject(state, Formatting.Indented), keepBak: false, null);
			}
		}
		catch
		{
		}
	}

	private static string PickChallengerName(string town)
	{
		return town switch
		{
			"Saint Denis" => "Judge Jean-Marc Moreau",
			"Blackwater" => "Senator Orville Josiah",
			"Valentine" => "Alderman Harlan Malloy",
			"Rhodes" => "Judge Beau Braithwaite",
			"Strawberry" => "Councilman Elias Miller",
			"Annesburg" => "Alderman Otto Von Krauss",
			"Van Horn" => "Captain Gideon Vance",
			"Armadillo" => "Alderman Herbert Moon",
			_ => "Colonel Jeremiah Sterling",
		};
	}

	private static void EnsureTownBusinesses(CivicState state, string town, string defaultGang)
	{
		(BusinessKind kind, string suffix, int income, int cost)[] defs = new (BusinessKind, string, int, int)[5]
		{
			(BusinessKind.Saloon, "Saloon & Parlour", 24, 150),
			(BusinessKind.Gunsmith, "Arms & Gunsmith", 28, 180),
			(BusinessKind.GeneralStore, "General Mercantile", 18, 120),
			(BusinessKind.Doctor, "Surgeon & Apothecary", 20, 135),
			(BusinessKind.Hotel, "Hotel & Stage Livery", 22, 145)
		};
		foreach (var d in defs)
		{
			string id = (town + "_" + d.kind).ToLowerInvariant().Replace(" ", "_");
			if (!state.Businesses.Any((TownBusiness b) => b.Id == id))
			{
				state.Businesses.Add(new TownBusiness
				{
					Id = id,
					Town = town,
					Name = town + " " + d.suffix,
					Kind = d.kind,
					Control = ((defaultGang != null && (d.kind == BusinessKind.Saloon || d.kind == BusinessKind.Hotel)) ? BusinessControl.GangExtorted : BusinessControl.Independent),
					ExtortingGang = ((defaultGang != null && (d.kind == BusinessKind.Saloon || d.kind == BusinessKind.Hotel)) ? defaultGang : null),
					DailyIncome = d.income,
					PartnerCost = d.cost,
					Level = 1
				});
			}
		}
	}

	private void SeedDefaultPrisoners(CivicState state, int dayNow)
	{
		state.Prisoners.Add(new PrisonerRecord
		{
			Id = "pris_odriscoll_1",
			Name = "Colm's Lieutenant Seamus O'Neill",
			Gang = "odriscoll",
			Rank = 3,
			Town = "Valentine",
			BailDollars = 75,
			ExecutionDay = dayNow + 3,
			Sentence = "Public Hanging"
		});
		state.Prisoners.Add(new PrisonerRecord
		{
			Id = "pris_raider_1",
			Name = "Capt. Beauregard Vance",
			Gang = "lemoyneraiders",
			Rank = 4,
			Town = "Rhodes",
			BailDollars = 95,
			ExecutionDay = dayNow + 4,
			Sentence = "Sisika Prison Transfer"
		});
		state.Prisoners.Add(new PrisonerRecord
		{
			Id = "pris_delobo_1",
			Name = "Esteban Cortez",
			Gang = "delobo",
			Rank = 3,
			Town = "Saint Denis",
			BailDollars = 85,
			ExecutionDay = dayNow + 5,
			Sentence = "Public Hanging"
		});
	}

	private void SeedDefaultBounties(CivicState state)
	{
		state.Bounties.Add(new BountyContract
		{
			Id = "bnty_odriscoll_1",
			TargetName = "Finbar 'Red' McGinnis",
			Gang = "odriscoll",
			Rank = 3,
			Town = "Valentine",
			AliveReward = 135,
			DeadReward = 65,
			Crime = "Hold-up of the Cornwall Express & murder of two guards"
		});
		state.Bounties.Add(new BountyContract
		{
			Id = "bnty_raider_1",
			TargetName = "Maj. Jubal Early",
			Gang = "lemoyneraiders",
			Rank = 4,
			Town = "Rhodes",
			AliveReward = 165,
			DeadReward = 80,
			Crime = "Burning a federal tax office & ambushing U.S. Marshals"
		});
		state.Bounties.Add(new BountyContract
		{
			Id = "bnty_skinner_1",
			TargetName = "Caleb 'Scalper' Boone",
			Gang = "skinners",
			Rank = 3,
			Town = "Blackwater",
			AliveReward = 150,
			DeadReward = 75,
			Crime = "Butchering a surveyor party in Tall Trees"
		});
	}

	internal void BookArrestedPedIntoCourt(Ped him, NemesisRecord rec, string town)
	{
		if (him == null)
		{
			return;
		}
		string pName = rec?.FullName ?? NameOf(him.Handle);
		if (string.IsNullOrEmpty(pName) || Civic.Prisoners.Any((PrisonerRecord x) => x.Name == pName))
		{
			return;
		}
		string t = (!string.IsNullOrEmpty(town) && Civic.Towns.ContainsKey(town)) ? town : "Valentine";
		Civic.Prisoners.Add(new PrisonerRecord
		{
			Id = "pris_" + NewId(6),
			Name = pName,
			Gang = rec?.Gang ?? OutfitOfPed(him)?.Id ?? "odriscoll",
			Rank = rec?.Rank ?? 2,
			NemesisId = rec?.Id,
			Town = t,
			BailDollars = 55 + (rec?.Rank ?? 1) * 20,
			ExecutionDay = Math.Max(1, GameDayKey()) + 3,
			Sentence = "Public Hanging"
		});
		SaveCivicState();
		Feed($"COURT DOCKET: {pName} was booked into {t} County Jail awaiting trial!", Theme.Gold);
	}

	internal void SaveCivicState(CivicState st = null)
	{
		CivicState target = st ?? _civic;
		if (target == null)
		{
			return;
		}
		try
		{
			SafeFile.WriteLater(CivicPath, () => JsonConvert.SerializeObject(target, Formatting.Indented), keepBak: true, null);
		}
		catch
		{
		}
	}

	internal string ActiveCivicTown(Ped player)
	{
		string t = (_tg != null && !string.IsNullOrEmpty(_tg.Town)) ? _tg.Town : (TownAt(player.Position) ?? _currentTown ?? "Valentine");
		if (Civic.Towns.ContainsKey(t))
		{
			return t;
		}
		return "Valentine";
	}

	internal TownPoliticState TownPoliticsOf(string town)
	{
		if (string.IsNullOrEmpty(town) || !Civic.Towns.TryGetValue(town, out var tp))
		{
			return Civic.Towns.Values.FirstOrDefault();
		}
		return tp;
	}

	internal bool OpenCivicDeskHotkey()
	{
		Ped player = Game.Player.Character;
		if (player == null || player.IsDead || player.IsInCombat || _menu.IsOpen || _convo != null || _bulletImpact.ViewerOpen || _iniPanel.IsOpen)
		{
			return false;
		}
		if (_tg != null)
		{
			CloseTelegraph();
			return true;
		}
		string town = TownAt(player.Position) ?? _currentTown ?? "Valentine";
		if (!Civic.Towns.ContainsKey(town))
		{
			town = "Valentine";
		}
		_tg = new TelegraphPanel
		{
			Clerk = null,
			Town = town,
			Page = "civic:main"
		};
		TownPoliticState tp = TownPoliticsOf(town);
		_tg.Line = $"{town} Civic & Political Wire  |  Mayor: {tp.MayorName} ({tp.Regime})  |  Bank: ${Civic.BankBalanceDollars}";
		TelegraphRows();
		GrudgeLog($"CIVIC desk opened via hotkey for {town}");
		return true;
	}

	internal void AppendCivicTelegraphMainRow()
	{
		if (_tg != null && _tg.Page == "main")
		{
			string town = Civic.Towns.ContainsKey(_tg.Town ?? "") ? _tg.Town : "Valentine";
			TownPoliticState tp = TownPoliticsOf(town);
			int idx = Math.Max(0, _tg.Rows.Count - 1);
			_tg.Rows.Insert(idx, ("civic:main", $"Civic, Politics & Police Desk  ({tp.Regime}, Bank ${Civic.BankBalanceDollars})", true, null));
			_tg.Info.Insert(idx, null);
		}
	}

	private Ped FindNearbyHogtiedBountyTarget(Ped player)
	{
		if (player == null || !player.Exists())
		{
			return null;
		}
		foreach (Ped p in PedsAround(player))
		{
			if (p != null && p.Exists() && !p.IsDead && p.IsHuman && !p.IsPlayer && p.IsHogtied && p.Position.DistanceTo(player.Position) <= 22f)
			{
				return p;
			}
		}
		return null;
	}

	private bool BuildCivicTelegraphRows(TelegraphPanel tg)
	{
		if (tg == null || tg.Page == null || !tg.Page.StartsWith("civic:", StringComparison.Ordinal))
		{
			return false;
		}
		string town = Civic.Towns.ContainsKey(tg.Town ?? "") ? tg.Town : "Valentine";
		tg.Town = town;
		TownPoliticState tp = TownPoliticsOf(town);
		long nowMin = GameMinutesNow();
		int dayNow = Math.Max(1, GameDayKey());
		bool bribed = tp.SheriffBribedUntilMin > nowMin;
		bool martial = tp.Regime == PoliticalRegime.MartialLaw || tp.MartialLawUntilDay > dayNow;
		Ped player = Game.Player.Character;

		void AddRow(string key, string text, bool enabled = true, string note = null, TgInfo info = null)
		{
			tg.Rows.Add((key, text, enabled, note));
			tg.Info.Add(info);
		}

		switch (tg.Page)
		{
		case "civic:main":
		{
			int townBizIncome = Civic.Businesses.Where((TownBusiness b) => b.Town == town && (b.Control == BusinessControl.PlayerPartner || b.Control == BusinessControl.PlayerProtected || b.Control == BusinessControl.PlayerRacket)).Sum((TownBusiness b) => b.DailyIncome * b.Level + (b.HasMoonshineStill ? 25 : 0));
			int docketCount = Civic.Prisoners.Count;
			int bountyCount = Civic.Bounties.Count;
			tg.Line = $"{town.ToUpperInvariant()}: {tp.MayorName} ({tp.Regime}, {tp.Approval}% approval, Tax {tp.TaxRatePct}%)  |  Sheriff {tp.SheriffName}  |  Bank: ${Civic.BankBalanceDollars}";
			AddRow("civic:politics", $"Town Hall & Elections  (Election in {Math.Max(1, tp.ElectionDay - dayNow)}d, Backed: {tp.BackedFaction ?? "None"})");
			AddRow("civic:police", $"Police, Pinkertons & Warrants  (Heat: {tp.CaseHeat}/100, Warrant: {tp.Warrant}{(bribed ? ", IMMUNE" : "")})");
			AddRow("civic:bounties", $"Bounty Hunter Guild & Live Hogtie Turn-Ins  ({bountyCount} contracts, {Civic.BountiesCapturedAlive} captured alive)");
			AddRow("civic:court", $"County Court, Prison Wagons & Hangings  ({docketCount} on docket)");
			AddRow("civic:business", $"Town Businesses, Rackets & Frontier Bank  (+${townBizIncome}/day, Bank ${Civic.BankBalanceDollars})");
			AddRow("civic:heists", $"Stagecoach Payroll Heists & Armed Escorts  ({Civic.PayrollHeistsCompleted} completed)");
			AddRow("civic:press", $"Newspaper Press, Propaganda & Framing Rivals  (Rep: {TownReputation(town):+0;-0;0})");
			AddRow("civic:saloon", $"Saloon Prize-Fight Club & Moonshine Backroom  ({Civic.PrizeFightsWon} bouts won)");
			AddRow("civic:alltowns", $"Switch Telegraph Wire to Another Town  (Currently: {town})");
			AddRow("leave", "Close Desk");
			return true;
		}
		case "civic:politics":
		{
			int daysLeft = Math.Max(1, tp.ElectionDay - dayNow);
			tg.Line = $"MAYOR: {tp.MayorName} ({tp.Regime}, Approval {tp.Approval}%, Corruption {tp.Corruption}%) vs Challenger {tp.ChallengerName} ({tp.ChallengerRegime}, {tp.ChallengerSupport}%)";
			AddRow("civic:act:lobby", "Lobby Mayor for Tax Cut & Goodwill  ($25)", CashDollars >= 25, "need $25", new TgInfo
			{
				A = "Lobby Mayor",
				B = $"{tp.MayorName}",
				C = "$25",
				D = "+10 Approval, -1% Tax",
				Detail = "Backs the sitting Mayor publicly, lowering town business taxes and boosting your town goodwill."
			});
			AddRow("civic:act:ally_mayor", "Bribe Mayor for Machine Alliance  ($80)", CashDollars >= 80 && tp.BackedFaction != "you", (tp.BackedFaction == "you") ? "already allied with you" : "need $80", new TgInfo
			{
				A = "Buy Mayor's Office",
				B = "10% Daily Town Tax Share",
				C = "$80",
				D = (tp.BackedFaction == "you") ? "ALLIED" : "Corrupt Alliance",
				Detail = "Puts the Mayor on your payroll: shifts town to Corrupt Machine, pays you daily tax kickbacks, and tells police to ignore your rackets."
			});
			AddRow("civic:act:fund_challenger", $"Fund Challenger {tp.ChallengerName}  ($40)", CashDollars >= 40, "need $40", new TgInfo
			{
				A = "Fund Challenger",
				B = $"{tp.ChallengerName} ({tp.ChallengerRegime})",
				C = "$40",
				D = $"+18% Poll (Now {tp.ChallengerSupport}%)",
				Detail = $"Boosts {tp.ChallengerName}'s campaign in the upcoming election ({daysLeft} days away)."
			});
			AddRow("civic:act:rig_election", "Rig the Town Ballot Box & Force Election Now  ($60)", CashDollars >= 60, "need $60", new TgInfo
			{
				A = "Rig Ballot Box",
				B = $"Install {tp.ChallengerName}",
				C = "$60",
				D = "Immediate Regime Change",
				Detail = "Stuffs the ballot boxes tonight, immediately installing the challenger as Mayor and allying the new administration with you."
			});
			AddRow("civic:act:spawn_rally", $"Summon {tp.MayorName}'s Campaign Stagecoach in Town", _activeMayorPed == null || !_activeMayorPed.Exists(), "already in town", new TgInfo
			{
				A = "Town Campaign Rally",
				B = tp.MayorName,
				C = "LIVE EVENT",
				D = "Protect or Assassinate",
				Detail = "Spawns the Mayor and his armed Pinkerton/Police bodyguards nearby. Assassinating him triggers Martial Law and an emergency succession!"
			});
			AddRow("civic:main", "Back to Civic Desk");
			return true;
		}
		case "civic:police":
		{
			tg.Line = $"LAW ENFORCEMENT: {tp.SheriffName}  |  Lead Detective: {tp.DetectiveName}  |  Case Heat: {tp.CaseHeat}/100  |  Martial Law: {(martial ? "ACTIVE" : "No")}";
			AddRow("civic:act:bribe_sheriff", $"Bribe {tp.SheriffName} for 24h Police Immunity  ($35)", CashDollars >= 35 && !bribed, bribed ? "already bribed today" : "need $35", new TgInfo
			{
				A = "Bribe Town Sheriff",
				B = tp.SheriffName,
				C = "$35",
				D = bribed ? "IMMUNE (24h)" : "Clears Local Heat",
				Detail = "Pays off the local sheriff and deputies for 24 in-game hours: clears local wanted status and stops curfew/racket arrests."
			});
			AddRow("civic:act:burn_casefile", $"Destroy Pinkerton Case File ({tp.DetectiveName})  ($120)", CashDollars >= 120 && (tp.CaseHeat > 0 || tp.Warrant != WarrantTier.Clear), (tp.CaseHeat == 0 && tp.Warrant == WarrantTier.Clear) ? "no case file on you" : "need $120", new TgInfo
			{
				A = "Burn Pinkerton Dossier",
				B = tp.DetectiveName,
				C = "$120",
				D = $"Warrant: {tp.Warrant} -> Clear",
				Detail = "Bribes a clerk in the Pinkerton Agency to burn your active investigation file and drop all county warrants."
			});
			AddRow("civic:act:police_intel", "Buy Police Wiretap Intel on Rival Gang Holdings  ($40)", CashDollars >= 40, "need $40", new TgInfo
			{
				A = "Police Wiretap Intel",
				B = "Reveal Gang Treasury / Cache",
				C = "$40",
				D = "Map Marker",
				Detail = "Uses police informant files to immediately reveal an undiscovered gang Treasury or Supply Cache on your map."
			});
			if (martial)
			{
				AddRow("civic:act:lift_martial", "Bribe State Governor to Lift Martial Law  ($100)", CashDollars >= 100, "need $100", new TgInfo
				{
					A = "Lift Martial Law",
					B = town,
					C = "$100",
					D = "Ends Curfew & Checkpoints",
					Detail = "Wires a payment to the State Capitol to recall the Martial Law proclamation and stand down road checkpoints."
				});
			}
			else
			{
				AddRow("civic:act:declare_martial", "File False Riot Wire to Declare Martial Law  ($75)", CashDollars >= 75, "need $75", new TgInfo
				{
					A = "Provoke Martial Law",
					B = town,
					C = "$75",
					D = "22:00 Curfew + Checkpoints",
					Detail = "Reports an outlaw insurrection to the Governor, placing the town under Martial Law with night curfews and armed roadblocks."
				});
			}
			AddRow("civic:act:spawn_checkpoint", "Spawn Police / Pinkerton Road Checkpoint Nearby", _activeCheckpointGuards.Count == 0, "checkpoint already active", new TgInfo
			{
				A = "Deploy Road Checkpoint",
				B = tp.DetectiveName,
				C = "LIVE EVENT",
				D = "Roadblock",
				Detail = "Spawns an armed Pinkerton and U.S. Marshal inspection checkpoint on the nearest road."
			});
			AddRow("civic:main", "Back to Civic Desk");
			return true;
		}
		case "civic:bounties":
		{
			Ped nearbyHogtied = FindNearbyHogtiedBountyTarget(player);
			tg.Line = $"BOUNTY HUNTER GUILD ({tp.SheriffName}): Capture wanted gang leaders ALIVE (hogtied) for 150% payout + Court Docket booking, or eliminate them for Dead payout.";
			if (nearbyHogtied != null)
			{
				string hName = NameOf(nearbyHogtied.Handle);
				AddRow("civic:act:turnin_hogtied", $"TURN IN HOGTIED CAPTIVE ALIVE: {hName}  (+$160, +Town Rep, Books onto Court Docket)", enabled: true, null, new TgInfo
				{
					A = hName,
					B = "Hogtied Captive Nearby",
					C = "+$160 ALIVE",
					D = "Court Docket + Rep",
					Detail = $"Delivers {hName} alive into {tp.SheriffName}'s custody for $160, boosts your {town} reputation, and books him onto the County Court Docket!"
				});
			}
			foreach (BountyContract bc in Civic.Bounties)
			{
				string gangName = Gangs.ById(bc.Gang)?.Name ?? "Outlaw";
				AddRow("civic:bounty:" + bc.Id, $"WANTED: {bc.TargetName} ({gangName})  -  Alive ${bc.AliveReward} / Dead ${bc.DeadReward}", enabled: true, null, new TgInfo
				{
					A = bc.TargetName,
					B = $"{gangName} (Rank {bc.Rank})",
					C = $"${bc.AliveReward} Alive",
					D = $"${bc.DeadReward} Dead",
					Detail = $"Crime: {bc.Crime}. Press Enter to track down {bc.TargetName} and his posse on the road outside {bc.Town}—hogtie him for the full ${bc.AliveReward} Alive reward or shoot him for ${bc.DeadReward}!"
				});
			}
			AddRow("civic:act:post_bounty", "Request New Wanted Poster from U.S. Marshals Wire", Civic.Bounties.Count < 6, "board is full");
			AddRow("civic:main", "Back to Civic Desk");
			return true;
		}
		case "civic:court":
		{
			tg.Line = $"COUNTY COURT & DOCKET ({Civic.Prisoners.Count} prisoners awaiting transfer or hanging). Select a prisoner to Bail/Pardon or Ambush their Prison Wagon:";
			foreach (PrisonerRecord pr in Civic.Prisoners)
			{
				string gangName = Gangs.ById(pr.Gang)?.Name ?? "Outlaw";
				AddRow("civic:prisoner:" + pr.Id, $"{pr.Name} ({gangName})  -  Bail ${pr.BailDollars} or Ambush Convoy", enabled: true, null, new TgInfo
				{
					A = pr.Name,
					B = $"{gangName} (Rank {pr.Rank})",
					C = $"Bail ${pr.BailDollars}",
					D = $"{pr.Sentence} in {pr.Town}",
					Detail = $"Press Enter to pay ${pr.BailDollars} bail/pardon (if you can afford it) OR spawn their armored Prison Wagon Convoy on the road outside to break them out by force!"
				});
			}
			AddRow("civic:act:capture_bounty", "Book a Random Wanted Outlaw Captain onto the Docket", Civic.Prisoners.Count < 6, "docket is full");
			AddRow("civic:main", "Back to Civic Desk");
			return true;
		}
		case "civic:business":
		{
			tg.Line = $"{town.ToUpperInvariant()} BUSINESSES & FRONTIER BANK  |  Bank Account: ${Civic.BankBalanceDollars} (+5%/day)  |  Loan Debt: ${Civic.BankLoanDollars}";
			foreach (TownBusiness biz in Civic.Businesses.Where((TownBusiness b) => b.Town == town))
			{
				int daily = biz.DailyIncome * biz.Level + (biz.HasMoonshineStill ? 25 : 0);
				string status = biz.Control switch
				{
					BusinessControl.PlayerPartner => $"YOUR PARTNER (Lv.{biz.Level}{(biz.HasMoonshineStill ? "+Still" : "")}, +${daily}/d)",
					BusinessControl.PlayerProtected => $"PROTECTED BY YOU (+${daily}/d)",
					BusinessControl.PlayerRacket => $"EXTORTED BY YOU (+${daily + 12}/d)",
					BusinessControl.GangExtorted => $"Extorted by {Gangs.ById(biz.ExtortingGang)?.Name ?? biz.ExtortingGang}",
					_ => $"Independent (Buy Partner ${biz.PartnerCost})"
				};
				AddRow("civic:biz:" + biz.Id, $"{biz.Name}  -  {status}", enabled: true, null, new TgInfo
				{
					A = biz.Name,
					B = status,
					C = $"${biz.PartnerCost}",
					D = $"+${daily}/day",
					Detail = (biz.Control == BusinessControl.GangExtorted)
						? $"Press Enter to spawn the {Gangs.ById(biz.ExtortingGang)?.Name ?? "rival gang"} extortion crew in town—wipe them out to take over protection of {biz.Name}!"
						: ((biz.Control == BusinessControl.Independent)
							? $"Press Enter to buy a legal partnership (${biz.PartnerCost}) or start a protection racket (if under $" + biz.PartnerCost + ")."
							: $"Press Enter to upgrade {biz.Name} (Lv.{biz.Level} -> Lv.{biz.Level + 1}, $90) and increase daily payouts!")
				});
			}
			AddRow("civic:act:bank_deposit", "Deposit $50 into Frontier Bank  (Earns 5% Daily Interest)", CashDollars >= 50, "need $50 cash");
			AddRow("civic:act:bank_withdraw", $"Withdraw $50 from Frontier Bank  (Balance: ${Civic.BankBalanceDollars})", Civic.BankBalanceDollars >= 50, "insufficient bank balance");
			AddRow("civic:act:bank_loan", "Take $200 Frontier Bank Commercial Loan", Civic.BankLoanDollars == 0, "pay off existing loan first");
			if (Civic.BankLoanDollars > 0)
			{
				AddRow("civic:act:bank_repay", $"Repay Frontier Bank Loan  (${Civic.BankLoanDollars})", CashDollars >= Civic.BankLoanDollars, $"need ${Civic.BankLoanDollars}");
			}
			AddRow("civic:main", "Back to Civic Desk");
			return true;
		}
		case "civic:heists":
		{
			tg.Line = $"STAGECOACH PAYROLL HEISTS & ESCORT CONTRACTS ({town}): Rob armored Cornwall/Federal payroll coaches or guard them for Mayor {tp.MayorName}.";
			AddRow("civic:act:heist_rob", "Buy Telegraph Tip & Ambush Federal Payroll Stagecoach  ($30 Tip -> +$250 Loot)", CashDollars >= 30 && _activePayrollNpcs.Count == 0, (_activePayrollNpcs.Count > 0) ? "stagecoach already active" : "need $30", new TgInfo
			{
				A = "Payroll Stagecoach Heist",
				B = "Pinkerton & Army Escort",
				C = "$30 Tip",
				D = "+$250 Strongbox",
				Detail = "Bribing the station clerk reveals an armored payroll stagecoach on the road ahead. Eliminate the Pinkerton guards to loot $250 from the strongbox!"
			});
			AddRow("civic:act:heist_escort", $"Accept {tp.MayorName}'s Armed Payroll Escort Contract  (+$135 & +10 Approval)", _activePayrollNpcs.Count == 0, "stagecoach already active", new TgInfo
			{
				A = "Payroll Escort Contract",
				B = $"Defend {town} Stagecoach",
				C = "CONTRACT",
				D = "+$135 & -20 Heat",
				Detail = "Spawns the town payroll stagecoach and a wave of rival gang road agents trying to rob it. Wipe out the ambushers to collect $135, +10 Mayor Approval, and -20 Case Heat!"
			});
			AddRow("civic:main", "Back to Civic Desk");
			return true;
		}
		case "civic:press":
		{
			tg.Line = $"{town.ToUpperInvariant()} GAZETTE & PRESS OFFICE: Bribe the newspaper editor to shape public opinion, smear politicians, or spark a Gang War!";
			AddRow("civic:act:press_puff", $"Publish Heroic Front-Page Puff Piece in {town}  ($45)", CashDollars >= 45, "need $45", new TgInfo
			{
				A = "Heroic Puff Piece",
				B = $"{town} Gazette",
				C = "$45",
				D = "+Town Rep, -25 Heat",
				Detail = $"Prints a glowing front-page story praising your deeds in {town}, boosting your Town Reputation and dropping local Case Heat by 25."
			});
			AddRow("civic:act:press_smear", $"Print Corruption Exposé on {tp.MayorName}  ($75)", CashDollars >= 75, "need $75", new TgInfo
			{
				A = "Mayoral Smear Campaign",
				B = $"Target: {tp.MayorName}",
				C = "$75",
				D = "-22% Mayor, +18% Challenger",
				Detail = $"Prints leaked ledgers exposing {tp.MayorName}'s graft, tanking his approval by 22% and surging {tp.ChallengerName} ahead of Election Day!"
			});
			AddRow("civic:act:press_frame", "Plant False Newspaper Story to Spark a Rival Gang War  ($110)", CashDollars >= 110, "need $110", new TgInfo
			{
				A = "Frame Rival Gangs",
				B = "Incite Blood Feud",
				C = "$110",
				D = "Sparks Gang War",
				Detail = "Bribes the press to blame one major outlaw faction for massacring another faction's men, plunging their relationship into an all-out Gang War!"
			});
			AddRow("civic:main", "Back to Civic Desk");
			return true;
		}
		case "civic:saloon":
		{
			TownBusiness saloon = Civic.Businesses.FirstOrDefault((TownBusiness b) => b.Town == town && b.Kind == BusinessKind.Saloon);
			bool hasStill = saloon != null && saloon.HasMoonshineStill;
			tg.Line = $"{town.ToUpperInvariant()} SALOON PRIZE-FIGHT RING & SMUGGLING BACKROOM  |  Bouts Won: {Civic.PrizeFightsWon}  |  Moonshine Still: {(hasStill ? "OPERATIONAL (+$25/d)" : "Not Built")}";
			AddRow("civic:act:saloon_fight", "Enter High-Stakes Bare-Knuckle Prize Fight  ($50 Entry -> Win $150 Purse)", CashDollars >= 50 && (_activePrizeFighter == null || !_activePrizeFighter.Exists()), (_activePrizeFighter != null && _activePrizeFighter.Exists()) ? "bout already in progress" : "need $50 entry purse", new TgInfo
			{
				A = "Championship Prize Fight",
				B = $"{town} Saloon Champion",
				C = "$50 Stake",
				D = "Win $150 + Rep",
				Detail = "Steps into the ring against a heavy-handed bare-knuckle bruiser nearby. Knock him out with your fists to win the $150 purse and town fame!"
			});
			AddRow("civic:act:saloon_still", $"Build Illicit Moonshine Still in {town} Saloon Backroom  ($140)", CashDollars >= 140 && saloon != null && !hasStill, hasStill ? "moonshine still already built" : "need $140", new TgInfo
			{
				A = "Backroom Moonshine Still",
				B = saloon?.Name ?? (town + " Saloon"),
				C = "$140",
				D = "+$25/day Illicit Income",
				Detail = "Sets up a copper moonshine still and smuggling tunnel behind the saloon, permanently adding +$25/day to your business ledger and making you partner."
			});
			AddRow("civic:main", "Back to Civic Desk");
			return true;
		}
		case "civic:alltowns":
		{
			tg.Line = "SELECT A TOWN ON THE TELEGRAPH WIRE TO MANAGE ITS POLITICS, POLICE & BUSINESSES:";
			foreach (TownPoliticState tState in Civic.Towns.Values)
			{
				AddRow("civic:settown:" + tState.Town, $"{tState.Town}  -  {tState.MayorName} ({tState.Regime})", enabled: true, null, new TgInfo
				{
					A = tState.Town,
					B = tState.MayorName,
					C = tState.Regime.ToString(),
					D = $"Heat {tState.CaseHeat}% | Tax {tState.TaxRatePct}%",
					Detail = $" Sheriff: {tState.SheriffName} | Detective: {tState.DetectiveName} | Backed Faction: {tState.BackedFaction ?? "None"}"
				});
			}
			AddRow("civic:main", "Back to Civic Desk");
			return true;
		}
		default:
			return false;
		}
	}

	internal (string a, string b, string c, string d, float bx, float cx, float dx)? CivicTgColumns(string page)
	{
		if (page == null || !page.StartsWith("civic:", StringComparison.Ordinal))
		{
			return null;
		}
		return page switch
		{
			"civic:politics" => ("ACTION", "OFFICIAL / CANDIDATE", "COST", "EFFECT", 0.31f, 0.62f, 0.75f),
			"civic:police" => ("OPERATION", "AUTHORITY", "COST", "STATUS", 0.34f, 0.62f, 0.75f),
			"civic:bounties" => ("WANTED TARGET", "FACTION & RANK", "ALIVE", "DEAD", 0.32f, 0.60f, 0.75f),
			"civic:court" => ("PRISONER", "FACTION & RANK", "BAIL", "SENTENCE", 0.32f, 0.58f, 0.72f),
			"civic:business" => ("ESTABLISHMENT", "CONTROL / RACKET", "PARTNER", "INCOME", 0.32f, 0.64f, 0.77f),
			"civic:heists" => ("OPERATION", "TARGET / ESCORT", "STAKE", "REWARD", 0.32f, 0.60f, 0.74f),
			"civic:press" => ("HEADLINE", "PUBLIC TARGET", "BRIBE", "IMPACT", 0.32f, 0.58f, 0.72f),
			"civic:saloon" => ("VENTURE", "ESTABLISHMENT", "STAKE", "PAYOUT", 0.32f, 0.60f, 0.74f),
			"civic:alltowns" => ("TOWN", "SITTING MAYOR", "REGIME", "STATUS", 0.26f, 0.54f, 0.73f),
			_ => null
		};
	}

	private void BoostTownGoodwill(string town, float goodDelta, float clearWordDelta, string deedNote)
	{
		if (string.IsNullOrEmpty(town) || Chronicle == null)
		{
			return;
		}
		Dictionary<string, TownTalk> dict = Chronicle.TownWord ?? (Chronicle.TownWord = new Dictionary<string, TownTalk>());
		if (!dict.TryGetValue(town, out var tt) || tt == null)
		{
			tt = (dict[town] = new TownTalk());
		}
		tt.Good = Math.Min(30f, tt.Good + goodDelta);
		tt.Word = Math.Max(0f, tt.Word - clearWordDelta);
		_townRepCache.Remove(town);
		_townWordCache.Remove(town);
		SaveWorld();
		GrudgeLog($"CIVIC goodwill in {town}: +{goodDelta:0.0} good, -{clearWordDelta:0.0} ill word ({deedNote})");
	}

	internal bool HandleCivicTelegraphPick(string key)
	{
		if (_tg == null || key == null || !key.StartsWith("civic:", StringComparison.Ordinal))
		{
			return false;
		}
		TelegraphPanel tg = _tg;
		string town = Civic.Towns.ContainsKey(tg.Town ?? "") ? tg.Town : "Valentine";
		tg.Town = town;
		TownPoliticState tp = TownPoliticsOf(town);
		Ped player = Game.Player.Character;

		if (key == "civic:main" || key == "civic:politics" || key == "civic:police" || key == "civic:bounties" || key == "civic:court" || key == "civic:business" || key == "civic:heists" || key == "civic:press" || key == "civic:saloon" || key == "civic:alltowns")
		{
			tg.Page = key;
			tg.Sel = 0;
			tg.Top = 0;
			TelegraphRows();
			return true;
		}
		if (key.StartsWith("civic:settown:", StringComparison.Ordinal))
		{
			tg.Town = key.Substring("civic:settown:".Length);
			tg.Page = "civic:main";
			tg.Sel = 0;
			tg.Top = 0;
			TelegraphRows();
			return true;
		}
		if (key.StartsWith("civic:bounty:", StringComparison.Ordinal))
		{
			string bId = key.Substring("civic:bounty:".Length);
			BountyContract bc = Civic.Bounties.FirstOrDefault((BountyContract x) => x.Id == bId);
			if (bc != null)
			{
				CloseTelegraph();
				SpawnBountyTargetEncounter(player, bc);
				return true;
			}
		}
		if (key.StartsWith("civic:prisoner:", StringComparison.Ordinal))
		{
			string prisId = key.Substring("civic:prisoner:".Length);
			PrisonerRecord pr = Civic.Prisoners.FirstOrDefault((PrisonerRecord x) => x.Id == prisId);
			if (pr != null)
			{
				if (CashDollars >= pr.BailDollars && !player.IsInCombat)
				{
					MONEY._MONEY_DECREMENT_CASH_BALANCE(pr.BailDollars * 100);
					Civic.TotalBribesPaid += pr.BailDollars;
					Civic.Prisoners.Remove(pr);
					SaveCivicState();
					Feed($"COURT BAIL: Paid ${pr.BailDollars} to pardon {pr.Name} in {pr.Town}. He owes you his life!", Theme.Gold);
					GrudgeLog($"CIVIC court pardon: paid ${pr.BailDollars} to free {pr.Name} ({pr.Gang})");
					SpawnPrisonerConvoy(player, pr, bailedOut: true);
				}
				else
				{
					CloseTelegraph();
					SpawnPrisonerConvoy(player, pr, bailedOut: false);
					return true;
				}
			}
			TelegraphRows();
			return true;
		}
		if (key.StartsWith("civic:biz:", StringComparison.Ordinal))
		{
			string bizId = key.Substring("civic:biz:".Length);
			TownBusiness biz = Civic.Businesses.FirstOrDefault((TownBusiness b) => b.Id == bizId);
			if (biz != null)
			{
				if (biz.Control == BusinessControl.GangExtorted)
				{
					CloseTelegraph();
					SpawnRacketEnforcers(player, biz);
					return true;
				}
				if (biz.Control == BusinessControl.Independent)
				{
					if (CashDollars >= biz.PartnerCost)
					{
						MONEY._MONEY_DECREMENT_CASH_BALANCE(biz.PartnerCost * 100);
						biz.Control = BusinessControl.PlayerPartner;
						biz.ExtortingGang = null;
						tp.Approval = Math.Min(100, tp.Approval + 6);
						SaveCivicState();
						Feed($"BUSINESS PARTNER: Bought into {biz.Name} for ${biz.PartnerCost} (+${biz.DailyIncome}/day)", Theme.Gold);
					}
					else
					{
						biz.Control = BusinessControl.PlayerRacket;
						biz.ExtortingGang = null;
						if (tp.BackedFaction != "you" && tp.SheriffBribedUntilMin <= GameMinutesNow())
						{
							tp.CaseHeat = Math.Min(100, tp.CaseHeat + 22);
							UpdateTownWarrant(tp);
						}
						SaveCivicState();
						Feed($"PROTECTION RACKET: Extorting {biz.Name} (+${biz.DailyIncome + 12}/day, Case Heat {tp.CaseHeat}%)", Theme.Warn);
					}
				}
				else if (biz.Level < 5 && CashDollars >= 90)
				{
					MONEY._MONEY_DECREMENT_CASH_BALANCE(90 * 100);
					biz.Level++;
					SaveCivicState();
					Feed($"BUSINESS UPGRADED: {biz.Name} is now Level {biz.Level} (+${biz.DailyIncome * biz.Level}/day)", Theme.Gold);
				}
			}
			TelegraphRows();
			return true;
		}

		switch (key)
		{
		case "civic:act:lobby":
			if (CashDollars >= 25)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(25 * 100);
				Civic.TotalBribesPaid += 25;
				tp.Approval = Math.Min(100, tp.Approval + 10);
				tp.TaxRatePct = Math.Max(2, tp.TaxRatePct - 1);
				tp.CaseHeat = Math.Max(0, tp.CaseHeat - 10);
				BoostTownGoodwill(town, 6f, 8f, "civic donation");
				SaveCivicState();
				Feed($"POLITICS: Lobbied {tp.MayorName} in {town} (Approval {tp.Approval}%, Tax cut to {tp.TaxRatePct}%)", Theme.Gold);
			}
			break;
		case "civic:act:ally_mayor":
			if (CashDollars >= 80)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(80 * 100);
				Civic.TotalBribesPaid += 80;
				tp.Regime = PoliticalRegime.CorruptMachine;
				tp.Corruption = Math.Min(100, tp.Corruption + 25);
				tp.BackedFaction = "you";
				tp.CaseHeat = 0;
				tp.Warrant = WarrantTier.Clear;
				tp.SheriffBribedUntilMin = GameMinutesNow() + 2880;
				SaveCivicState();
				Feed($"POLITICAL MACHINE: {tp.MayorName} in {town} is now in your pocket! You collect 10% of town taxes daily.", Theme.Gold);
				GrudgeLog($"CIVIC mayor bribed: {tp.MayorName} in {town} allied with player");
			}
			break;
		case "civic:act:fund_challenger":
			if (CashDollars >= 40)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(40 * 100);
				tp.ChallengerSupport = Math.Min(95, tp.ChallengerSupport + 18);
				tp.Approval = Math.Max(10, tp.Approval - 12);
				SaveCivicState();
				Feed($"CAMPAIGN FUNDED: {tp.ChallengerName} surges to {tp.ChallengerSupport}% in {town}!", Theme.Gold);
			}
			break;
		case "civic:act:rig_election":
			if (CashDollars >= 60)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(60 * 100);
				Civic.TotalBribesPaid += 60;
				RunTownElection(tp, forcedRig: true);
				SaveCivicState();
			}
			break;
		case "civic:act:spawn_rally":
			CloseTelegraph();
			SpawnMayoralRally(player, tp);
			return true;
		case "civic:act:bribe_sheriff":
			if (CashDollars >= 35)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(35 * 100);
				Civic.TotalBribesPaid += 35;
				tp.SheriffBribedUntilMin = GameMinutesNow() + 1440;
				tp.CaseHeat = Math.Max(0, tp.CaseHeat - 35);
				UpdateTownWarrant(tp);
				_lawWarnings = 0;
				SaveCivicState();
				Feed($"POLICE BRIBED: {tp.SheriffName} granted you 24h police immunity in {town}!", Theme.Gold);
			}
			break;
		case "civic:act:burn_casefile":
			if (CashDollars >= 120)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(120 * 100);
				Civic.TotalBribesPaid += 120;
				tp.CaseHeat = 0;
				tp.Warrant = WarrantTier.Clear;
				_lawWarnings = 0;
				SaveCivicState();
				Feed($"CASE FILE BURNED: {tp.DetectiveName}'s Pinkerton dossier on you in {town} was destroyed!", Theme.Gold);
			}
			break;
		case "civic:act:police_intel":
			if (CashDollars >= 40)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(40 * 100);
				Gang targetGang = Gangs.All.Where((Gang g) => !g.StoryOnly && !g.OrdersNotFeelings && !IsDestroyed(g)).OrderBy((Gang _) => _rng.Next()).FirstOrDefault();
				if (targetGang != null)
				{
					RevealTreasury(targetGang, tp.SheriffName + "'s wiretap");
					Feed($"POLICE INTEL: {tp.SheriffName}'s wiretap revealed the {targetGang.Name} Treasury!", Theme.Gold);
				}
			}
			break;
		case "civic:act:lift_martial":
			if (CashDollars >= 100)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(100 * 100);
				tp.MartialLawUntilDay = 0;
				if (tp.Regime == PoliticalRegime.MartialLaw)
				{
					tp.Regime = PoliticalRegime.Reformist;
				}
				CleanupCheckpointGuards();
				SaveCivicState();
				Feed($"MARTIAL LAW LIFTED in {town} by order of the Governor.", Theme.Gold);
			}
			break;
		case "civic:act:declare_martial":
			if (CashDollars >= 75)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(75 * 100);
				tp.Regime = PoliticalRegime.MartialLaw;
				tp.MartialLawUntilDay = Math.Max(1, GameDayKey()) + 4;
				SaveCivicState();
				Feed($"MARTIAL LAW DECLARED in {town}! Night curfew (22:00-05:00) and armed checkpoints are now in effect.", Theme.Warn);
			}
			break;
		case "civic:act:spawn_checkpoint":
			CloseTelegraph();
			SpawnPoliceCheckpoint(player, tp);
			return true;
		case "civic:act:turnin_hogtied":
		{
			Ped captive = FindNearbyHogtiedBountyTarget(player);
			if (captive != null)
			{
				NemesisRecord rec = RecordFor(captive);
				string cName = rec?.FullName ?? NameOf(captive.Handle);
				BookArrestedPedIntoCourt(captive, rec, town);
				Civic.BountiesCapturedAlive++;
				PayPlayer(160 * 100);
				tp.CaseHeat = Math.Max(0, tp.CaseHeat - 20);
				UpdateTownWarrant(tp);
				BoostTownGoodwill(town, 10f, 12f, "delivered outlaw " + cName + " alive");
				try
				{
					captive.Delete();
				}
				catch
				{
				}
				SaveCivicState();
				Feed($"BOUNTY TURNED IN ALIVE! Delivered {cName} to {tp.SheriffName} for $160 (+Town Rep & booked onto Court Docket)!", Theme.Gold);
			}
			break;
		}
		case "civic:act:post_bounty":
		{
			Gang g = Gangs.All.Where((Gang x) => !x.StoryOnly && !x.OrdersNotFeelings).OrderBy((Gang _) => _rng.Next()).FirstOrDefault() ?? Gangs.ODriscolls;
			NemesisRecord cap = Roster.FirstOrDefault((NemesisRecord r) => r.IsAlive && r.Gang == g.Id && r.Rank >= 2);
			string bName = cap?.FullName ?? ($"{g.FirstNames[_rng.Next(g.FirstNames.Length)]} {g.LastNames[_rng.Next(g.LastNames.Length)]}");
			int alive = 120 + (cap?.Rank ?? 2) * 25;
			Civic.Bounties.Add(new BountyContract
			{
				Id = "bnty_" + NewId(6),
				TargetName = bName,
				Gang = g.Id,
				Rank = cap?.Rank ?? 3,
				NemesisId = cap?.Id,
				Town = town,
				AliveReward = alive,
				DeadReward = alive / 2,
				Crime = $"Armed stagecoach robbery & murder outside {town}"
			});
			SaveCivicState();
			Feed($"BOUNTY BOARD: {tp.SheriffName} posted a ${alive} Dead-or-Alive contract on {bName} ({g.Name})!", Theme.Gold);
			break;
		}
		case "civic:act:capture_bounty":
		{
			Gang g = Gangs.All.Where((Gang x) => !x.StoryOnly && !x.OrdersNotFeelings).OrderBy((Gang _) => _rng.Next()).FirstOrDefault() ?? Gangs.ODriscolls;
			NemesisRecord cap = Roster.FirstOrDefault((NemesisRecord r) => r.IsAlive && r.Gang == g.Id && r.Rank >= 2);
			string pName = cap?.FullName ?? ($"{g.FirstNames[_rng.Next(g.FirstNames.Length)]} {g.LastNames[_rng.Next(g.LastNames.Length)]}");
			Civic.Prisoners.Add(new PrisonerRecord
			{
				Id = "pris_" + NewId(6),
				Name = pName,
				Gang = g.Id,
				Rank = cap?.Rank ?? 3,
				NemesisId = cap?.Id,
				Town = town,
				BailDollars = 70 + _rng.Next(50),
				ExecutionDay = Math.Max(1, GameDayKey()) + 3,
				Sentence = (_rng.Next(2) == 0) ? "Public Hanging" : "Sisika Prison Transfer"
			});
			SaveCivicState();
			Feed($"COURT DOCKET: {pName} ({g.Name}) was captured by {tp.SheriffName} and booked in {town}!", Theme.Gold);
			break;
		}
		case "civic:act:bank_deposit":
			if (CashDollars >= 50)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(50 * 100);
				Civic.BankBalanceDollars += 50;
				SaveCivicState();
				Feed($"FRONTIER BANK: Deposited $50 (New Balance: ${Civic.BankBalanceDollars}, earning 5%/day)", Theme.Gold);
			}
			break;
		case "civic:act:bank_withdraw":
			if (Civic.BankBalanceDollars >= 50)
			{
				Civic.BankBalanceDollars -= 50;
				PayPlayer(50 * 100);
				SaveCivicState();
				Feed($"FRONTIER BANK: Withdrew $50 (Remaining Balance: ${Civic.BankBalanceDollars})", Theme.Gold);
			}
			break;
		case "civic:act:bank_loan":
			if (Civic.BankLoanDollars == 0)
			{
				Civic.BankLoanDollars = 220;
				PayPlayer(200 * 100);
				SaveCivicState();
				Feed("FRONTIER BANK: Received $200 commercial loan ($220 due with interest).", Theme.Gold);
			}
			break;
		case "civic:act:bank_repay":
			if (Civic.BankLoanDollars > 0 && CashDollars >= Civic.BankLoanDollars)
			{
				int amt = Civic.BankLoanDollars;
				MONEY._MONEY_DECREMENT_CASH_BALANCE(amt * 100);
				Civic.BankLoanDollars = 0;
				SaveCivicState();
				Feed($"FRONTIER BANK: Loan of ${amt} paid in full!", Theme.Gold);
			}
			break;
		case "civic:act:heist_rob":
			if (CashDollars >= 30)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(30 * 100);
				CloseTelegraph();
				SpawnPayrollStagecoachEncounter(player, tp, isEscort: false);
				return true;
			}
			break;
		case "civic:act:heist_escort":
			CloseTelegraph();
			SpawnPayrollStagecoachEncounter(player, tp, isEscort: true);
			return true;
		case "civic:act:press_puff":
			if (CashDollars >= 45)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(45 * 100);
				tp.CaseHeat = Math.Max(0, tp.CaseHeat - 25);
				UpdateTownWarrant(tp);
				tp.CustomHeadline = $"PUBLIC BENEFACTOR: Citizens of {town} praise a mysterious frontier philanthropist for restoring order to the county.";
				BoostTownGoodwill(town, 12f, 18f, "newspaper puff piece");
				SaveCivicState();
				Feed($"PRESS PROPAGANDA: {town} Gazette printed your heroic front-page story (+Town Rep, -25 Case Heat)!", Theme.Gold);
			}
			break;
		case "civic:act:press_smear":
			if (CashDollars >= 75)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(75 * 100);
				tp.Approval = Math.Max(5, tp.Approval - 22);
				tp.ChallengerSupport = Math.Min(95, tp.ChallengerSupport + 18);
				tp.Corruption = Math.Min(100, tp.Corruption + 15);
				tp.CustomHeadline = $"SCANDAL IN {town.ToUpperInvariant()}: Leaked ledgers tie {tp.MayorName} to treasury embezzlement; challenger {tp.ChallengerName} surges in polls!";
				SaveCivicState();
				Feed($"SMEAR CAMPAIGN: {tp.MayorName}'s approval plunged to {tp.Approval}% ({tp.ChallengerName} now at {tp.ChallengerSupport}%)!", Theme.Gold);
			}
			break;
		case "civic:act:press_frame":
			if (CashDollars >= 110)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(110 * 100);
				List<Gang> outlawGangs = Gangs.All.Where((Gang g) => !g.StoryOnly && !g.OrdersNotFeelings && !IsDestroyed(g)).OrderBy((Gang _) => _rng.Next()).Take(2).ToList();
				if (outlawGangs.Count >= 2)
				{
					Gang g1 = outlawGangs[0];
					Gang g2 = outlawGangs[1];
					ShiftRelation(g1.Id, g2.Id, -65, $"framed in the {town} Gazette", force: true);
					tp.CustomHeadline = $"BLOOD FEUD: {g1.Name} blames {g2.Name} for a midnight massacre outside {town}—open gang war erupts!";
					SaveCivicState();
					Feed($"FALSE FLAG: Framed {g2.Name} in the {town} press—{g1.Name} and {g2.Name} are now AT WAR!", Theme.Blood);
				}
			}
			break;
		case "civic:act:saloon_fight":
			if (CashDollars >= 50)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(50 * 100);
				CloseTelegraph();
				SpawnSaloonPrizeFight(player, town);
				return true;
			}
			break;
		case "civic:act:saloon_still":
		{
			TownBusiness saloon = Civic.Businesses.FirstOrDefault((TownBusiness b) => b.Town == town && b.Kind == BusinessKind.Saloon);
			if (saloon != null && !saloon.HasMoonshineStill && CashDollars >= 140)
			{
				MONEY._MONEY_DECREMENT_CASH_BALANCE(140 * 100);
				saloon.HasMoonshineStill = true;
				if (saloon.Control == BusinessControl.Independent || saloon.Control == BusinessControl.GangExtorted)
				{
					saloon.Control = BusinessControl.PlayerPartner;
					saloon.ExtortingGang = null;
				}
				SaveCivicState();
				Feed($"MOONSHINE STILL BUILT: {saloon.Name} now distills illicit backroom shine (+$25/day extra revenue)!", Theme.Gold);
			}
			break;
		}
		}
		TelegraphRows();
		return true;
	}

	private static void UpdateTownWarrant(TownPoliticState tp)
	{
		if (tp.CaseHeat >= 80)
		{
			tp.Warrant = WarrantTier.DeadOrAlive;
		}
		else if (tp.CaseHeat >= 50)
		{
			tp.Warrant = WarrantTier.Felony;
		}
		else if (tp.CaseHeat >= 25)
		{
			tp.Warrant = WarrantTier.Misdemeanor;
		}
		else
		{
			tp.Warrant = WarrantTier.Clear;
		}
	}

	private void RunTownElection(TownPoliticState tp, bool forcedRig)
	{
		int dayNow = Math.Max(1, GameDayKey());
		bool challengerWins = forcedRig || (tp.ChallengerSupport + _rng.Next(-10, 15) > tp.Approval);
		if (challengerWins)
		{
			string oldMayor = tp.MayorName;
			PoliticalRegime oldRegime = tp.Regime;
			tp.MayorName = tp.ChallengerName;
			tp.Regime = tp.ChallengerRegime;
			tp.ChallengerName = oldMayor;
			tp.ChallengerRegime = oldRegime;
			tp.Approval = 62;
			tp.ChallengerSupport = 38;
			if (forcedRig)
			{
				tp.BackedFaction = "you";
			}
			Feed($"ELECTION IN {tp.Town.ToUpperInvariant()}: {tp.MayorName} ({tp.Regime}) defeated {oldMayor}!", Theme.Gold);
			GrudgeLog($"CIVIC election in {tp.Town}: {tp.MayorName} ({tp.Regime}) elected over {oldMayor}");
		}
		else
		{
			tp.Approval = Math.Min(85, tp.Approval + 8);
			tp.ChallengerSupport = 40;
			Feed($"ELECTION IN {tp.Town.ToUpperInvariant()}: Incumbent {tp.MayorName} ({tp.Regime}) won re-election!", Theme.Gold);
			GrudgeLog($"CIVIC election in {tp.Town}: {tp.MayorName} re-elected");
		}
		tp.ElectionDay = dayNow + 7;
	}

	private void SpawnMayoralRally(Ped player, TownPoliticState tp)
	{
		CleanupMayoralRally();
		Vector3 spawnPos = SnapToRoad(player.Position + player.ForwardVector * 38f, 45f);
		if (spawnPos.X == 0f && spawnPos.Y == 0f)
		{
			spawnPos = Ground(player.Position + player.ForwardVector * 32f);
		}
		Ped mayor = SpawnPed((PedHash)1553588674u, spawnPos, HeadingTo(spawnPos, player.Position));
		if (mayor == null)
		{
			mayor = SpawnPed((PedHash)737910671u, spawnPos, HeadingTo(spawnPos, player.Position));
		}
		if (mayor == null)
		{
			ShowNotification("enRAGE: Could not spawn the Mayor's entourage here.");
			return;
		}
		MakeOurs(mayor.Handle);
		_activeMayorPed = mayor;
		_activeMayorTown = tp.Town;
		PedHash[] guardModels = new PedHash[3] { (PedHash)737910671u, (PedHash)2403720683u, (PedHash)1830978939u };
		for (int i = 0; i < 3; i++)
		{
			Vector3 gPos = Ground(spawnPos + new Vector3((i - 1) * 2.5f, 2.2f, 0f));
			Ped g = SpawnPed(guardModels[i % guardModels.Length], gPos, mayor.Heading);
			if (g != null)
			{
				MakeOurs(g.Handle);
				g.GiveWeapon((eWeapon)1676963302u, 60, (eWeaponAttachPoint)0, equipNow: true, isAmmoLoaded: true, a6: false);
				TASK.TASK_STAND_STILL(g.Handle, -1);
				_activeMayorGuards.Add(g);
			}
		}
		Feed($"POLITICAL RALLY: {tp.MayorName} ({tp.Town}) has arrived nearby with his Pinkerton bodyguards!", Theme.Gold);
		GrudgeLog($"CIVIC spawned Mayor {tp.MayorName} and {_activeMayorGuards.Count} guards near player");
	}

	private void CleanupMayoralRally()
	{
		_activeMayorPed = null;
		_activeMayorTown = null;
		_activeMayorGuards.Clear();
	}

	private void SpawnPoliceCheckpoint(Ped player, TownPoliticState tp)
	{
		CleanupCheckpointGuards();
		Vector3 roadPos = SnapToRoad(player.Position + player.ForwardVector * 55f, 60f);
		if (roadPos.X == 0f && roadPos.Y == 0f)
		{
			roadPos = Ground(player.Position + player.ForwardVector * 45f);
		}
		float hdg = HeadingTo(roadPos, player.Position);
		Vehicle wagon = SpawnVehicle((VehicleHash)MISC.GET_HASH_KEY("wagon02x"), roadPos, hdg + 90f);
		if (wagon != null)
		{
			MakeOurs(wagon.Handle);
			VEHICLE.SET_VEHICLE_ON_GROUND_PROPERLY(wagon.Handle, p1: false);
		}
		PedHash[] models = new PedHash[4] { (PedHash)737910671u, (PedHash)1830978939u, (PedHash)2403720683u, (PedHash)4105441914u };
		for (int i = 0; i < 4; i++)
		{
			Vector3 p = Ground(roadPos + new Vector3((i - 1.5f) * 2.6f, 2f, 0f));
			Ped officer = SpawnPed(models[i], p, hdg);
			if (officer != null)
			{
				MakeOurs(officer.Handle);
				officer.GiveWeapon((eWeapon)1676963302u, 80, (eWeaponAttachPoint)0, equipNow: true, isAmmoLoaded: true, a6: false);
				TASK.TASK_START_SCENARIO_IN_PLACE_HASH(officer.Handle, MISC.GET_HASH_KEY("WORLD_HUMAN_GUARD_STAND"), -1, p3: true, 0u, 0f, p6: false);
				_activeCheckpointGuards.Add(officer);
			}
		}
		_lastCheckpointSpawnAt = Game.GameTime;
		Feed($"CHECKPOINT: {tp.DetectiveName} established a Pinkerton/Marshal roadblock ahead!", Theme.Warn);
		GrudgeLog($"CIVIC spawned road checkpoint for {tp.Town} with {_activeCheckpointGuards.Count} officers");
	}

	private void CleanupCheckpointGuards()
	{
		_activeCheckpointGuards.Clear();
	}

	private void SpawnPrisonerConvoy(Ped player, PrisonerRecord pr, bool bailedOut)
	{
		Vector3 roadPos = SnapToRoad(player.Position + player.ForwardVector * (bailedOut ? 22f : 55f), 60f);
		if (roadPos.X == 0f && roadPos.Y == 0f)
		{
			roadPos = Ground(player.Position + player.ForwardVector * (bailedOut ? 20f : 45f));
		}
		float hdg = HeadingTo(roadPos, player.Position);
		Gang prisGang = Gangs.ById(pr.Gang) ?? Gangs.ODriscolls;
		PedHash prisModel = (prisGang.TroopModels != null && prisGang.TroopModels.Length != 0) ? prisGang.TroopModels[0] : ((PedHash)347599949u);
		if (bailedOut)
		{
			Ped freed = SpawnPed(prisModel, roadPos, hdg);
			if (freed != null)
			{
				MakeOurs(freed.Handle);
				TASK.TASK_GO_TO_ENTITY(freed.Handle, player.Handle, 15000, 3f, 2f, 0f, 0);
				RevealTreasury(prisGang, pr.Name + " (grateful for bail)");
				Feed($"RELEASED: {pr.Name} stepped out of the {pr.Town} jailhouse and tipped you off to the {prisGang.Name} Treasury!", Theme.Gold);
			}
			return;
		}
		_activeConvoyGuards.Clear();
		uint wagonHash = MISC.GET_HASH_KEY("wagonPrison01x");
		if (!STREAMING.IS_MODEL_IN_CDIMAGE(wagonHash))
		{
			wagonHash = MISC.GET_HASH_KEY("wagon02x");
		}
		Vehicle wagon = SpawnVehicle((VehicleHash)wagonHash, roadPos, hdg);
		Ped prisoner = SpawnPed(prisModel, roadPos + new Vector3(0f, 0f, 1f), hdg);
		if (wagon != null && prisoner != null)
		{
			MakeOurs(wagon.Handle);
			MakeOurs(prisoner.Handle);
			VEHICLE.SET_VEHICLE_ON_GROUND_PROPERLY(wagon.Handle, p1: false);
			PED.SET_PED_INTO_VEHICLE(prisoner.Handle, wagon.Handle, 1);
			prisoner.BlockPermanentEvents = true;
			_activePrisonWagon = wagon;
			_activePrisonerPed = prisoner;
			_activePrisonerRec = pr;
		}
		PedHash[] marshalModels = new PedHash[3] { (PedHash)1830978939u, (PedHash)737910671u, (PedHash)4105441914u };
		for (int i = 0; i < 4; i++)
		{
			Vector3 gPos = Ground(roadPos + new Vector3((i - 1.5f) * 3f, -4f, 0f));
			Ped guard = SpawnPed(marshalModels[i % marshalModels.Length], gPos, hdg);
			if (guard != null)
			{
				MakeOurs(guard.Handle);
				guard.GiveWeapon((eWeapon)1676963302u, 90, (eWeaponAttachPoint)0, equipNow: true, isAmmoLoaded: true, a6: false);
				if (i == 0 && wagon != null)
				{
					PED.SET_PED_INTO_VEHICLE(guard.Handle, wagon.Handle, -1);
				}
				else
				{
					MountOnHorse(guard);
					TASK.TASK_COMBAT_PED(guard.Handle, player.Handle, 0, 16);
				}
				_activeConvoyGuards.Add(guard);
			}
		}
		Feed($"PRISON WAGON CONVOY: Eliminate the U.S. Marshal escort to break {pr.Name} out of the prison wagon!", Theme.Warn);
		GrudgeLog($"CIVIC spawned prison wagon convoy for {pr.Name} ({pr.Gang})");
	}

	private void SpawnRacketEnforcers(Ped player, TownBusiness biz)
	{
		_activeRacketEnforcers.Clear();
		_activeRacketBiz = biz;
		Gang gang = Gangs.ById(biz.ExtortingGang) ?? Gangs.ODriscolls;
		PedHash[] models = (gang.TroopModels != null && gang.TroopModels.Length != 0) ? gang.TroopModels : new PedHash[1] { (PedHash)347599949u };
		Vector3 basePos = Ground(player.Position + player.ForwardVector * 28f);
		float hdg = HeadingTo(basePos, player.Position);
		for (int i = 0; i < 4; i++)
		{
			Vector3 p = Ground(basePos + new Vector3((i - 1.5f) * 2.2f, 1.5f, 0f));
			Ped thug = SpawnPed(models[i % models.Length], p, hdg);
			if (thug != null)
			{
				MakeOurs(thug.Handle);
				thug.GiveWeapon((eWeapon)132728264u, 70, (eWeaponAttachPoint)0, equipNow: true, isAmmoLoaded: true, a6: false);
				TASK.TASK_COMBAT_PED(thug.Handle, player.Handle, 0, 16);
				_activeRacketEnforcers.Add(thug);
			}
		}
		Feed($"TURF WAR: Wipe out the {gang.Name} extortion crew to claim protection of {biz.Name}!", Theme.Warn);
		GrudgeLog($"CIVIC spawned {_activeRacketEnforcers.Count} {gang.Name} racket enforcers for {biz.Name}");
	}

	private void SpawnBountyTargetEncounter(Ped player, BountyContract bc)
	{
		_activeBountyGuards.Clear();
		_activeBountyContract = bc;
		Gang gang = Gangs.ById(bc.Gang) ?? Gangs.ODriscolls;
		PedHash[] models = (gang.TroopModels != null && gang.TroopModels.Length != 0) ? gang.TroopModels : new PedHash[1] { (PedHash)347599949u };
		Vector3 basePos = SnapToRoad(player.Position + player.ForwardVector * 48f, 55f);
		if (basePos.X == 0f && basePos.Y == 0f)
		{
			basePos = Ground(player.Position + player.ForwardVector * 40f);
		}
		float hdg = HeadingTo(basePos, player.Position);
		Ped target = SpawnPed(models[0], basePos, hdg);
		if (target == null)
		{
			ShowNotification("enRAGE: Could not spawn bounty target here.");
			return;
		}
		MakeOurs(target.Handle);
		target.MaxHealth = 220;
		target.Health = 220;
		target.GiveWeapon((eWeapon)1676963302u, 90, (eWeaponAttachPoint)0, equipNow: true, isAmmoLoaded: true, a6: false);
		TASK.TASK_COMBAT_PED(target.Handle, player.Handle, 0, 16);
		_activeBountyPed = target;
		for (int i = 0; i < 3; i++)
		{
			Vector3 gPos = Ground(basePos + new Vector3((i - 1) * 2.5f, -3f, 0f));
			Ped g = SpawnPed(models[i % models.Length], gPos, hdg);
			if (g != null)
			{
				MakeOurs(g.Handle);
				g.GiveWeapon((eWeapon)132728264u, 70, (eWeaponAttachPoint)0, equipNow: true, isAmmoLoaded: true, a6: false);
				TASK.TASK_COMBAT_PED(g.Handle, player.Handle, 0, 16);
				_activeBountyGuards.Add(g);
			}
		}
		Feed($"BOUNTY HUNT: {bc.TargetName} ({gang.Name}) spotted ahead! Hogtie him for ${bc.AliveReward} ALIVE or kill him for ${bc.DeadReward}!", Theme.Gold);
		GrudgeLog($"CIVIC spawned bounty target {bc.TargetName} ({bc.Gang})");
	}

	private void SpawnPayrollStagecoachEncounter(Ped player, TownPoliticState tp, bool isEscort)
	{
		_activePayrollNpcs.Clear();
		_activePayrollIsEscort = isEscort;
		_activePayrollTown = tp.Town;
		Vector3 roadPos = SnapToRoad(player.Position + player.ForwardVector * 45f, 55f);
		if (roadPos.X == 0f && roadPos.Y == 0f)
		{
			roadPos = Ground(player.Position + player.ForwardVector * 38f);
		}
		float hdg = HeadingTo(roadPos, player.Position);
		uint coachHash = MISC.GET_HASH_KEY("coach2");
		if (!STREAMING.IS_MODEL_IN_CDIMAGE(coachHash))
		{
			coachHash = MISC.GET_HASH_KEY("wagon02x");
		}
		Vehicle coach = SpawnVehicle((VehicleHash)coachHash, roadPos, hdg);
		if (coach != null)
		{
			MakeOurs(coach.Handle);
			VEHICLE.SET_VEHICLE_ON_GROUND_PROPERLY(coach.Handle, p1: false);
			_activePayrollCoach = coach;
		}
		if (!isEscort)
		{
			PedHash[] lawModels = new PedHash[4] { (PedHash)737910671u, (PedHash)2403720683u, (PedHash)1830978939u, (PedHash)4105441914u };
			for (int i = 0; i < 4; i++)
			{
				Vector3 p = Ground(roadPos + new Vector3((i - 1.5f) * 2.8f, -3.5f, 0f));
				Ped guard = SpawnPed(lawModels[i], p, hdg);
				if (guard != null)
				{
					MakeOurs(guard.Handle);
					guard.GiveWeapon((eWeapon)1676963302u, 90, (eWeaponAttachPoint)0, equipNow: true, isAmmoLoaded: true, a6: false);
					if (i == 0 && coach != null)
					{
						PED.SET_PED_INTO_VEHICLE(guard.Handle, coach.Handle, -1);
					}
					else
					{
						TASK.TASK_COMBAT_PED(guard.Handle, player.Handle, 0, 16);
					}
					_activePayrollNpcs.Add(guard);
				}
			}
			Feed("PAYROLL STAGECOACH HEIST: Wipe out the Pinkerton escort to loot $250 from the payroll strongbox!", Theme.Warn);
			GrudgeLog($"CIVIC spawned payroll stagecoach heist near {tp.Town}");
		}
		else
		{
			Gang banditGang = Gangs.ById(tp.BackedFaction) ?? Gangs.ODriscolls;
			if (banditGang.OrdersNotFeelings)
			{
				banditGang = Gangs.ODriscolls;
			}
			PedHash[] banditModels = (banditGang.TroopModels != null && banditGang.TroopModels.Length != 0) ? banditGang.TroopModels : new PedHash[1] { (PedHash)347599949u };
			for (int i = 0; i < 5; i++)
			{
				Vector3 p = Ground(roadPos + new Vector3((i - 2) * 3f, 8f, 0f));
				Ped raider = SpawnPed(banditModels[i % banditModels.Length], p, hdg + 180f);
				if (raider != null)
				{
					MakeOurs(raider.Handle);
					raider.GiveWeapon((eWeapon)132728264u, 80, (eWeaponAttachPoint)0, equipNow: true, isAmmoLoaded: true, a6: false);
					TASK.TASK_COMBAT_PED(raider.Handle, player.Handle, 0, 16);
					_activePayrollNpcs.Add(raider);
				}
			}
			Feed($"PAYROLL ESCORT: Defend the {tp.Town} stagecoach from the {banditGang.Name} road agents (+$135 reward)!", Theme.Gold);
			GrudgeLog($"CIVIC spawned payroll stagecoach escort defense against {banditGang.Name}");
		}
	}

	private void SpawnSaloonPrizeFight(Ped player, string town)
	{
		Vector3 fightPos = Ground(player.Position + player.ForwardVector * 8f);
		float hdg = HeadingTo(fightPos, player.Position);
		Ped bruiser = SpawnPed((PedHash)347599949u, fightPos, hdg);
		if (bruiser == null)
		{
			ShowNotification("enRAGE: Could not spawn Saloon Prize Fighter here.");
			return;
		}
		MakeOurs(bruiser.Handle);
		WEAPON.REMOVE_ALL_PED_WEAPONS(bruiser.Handle, p1: true, p2: true);
		bruiser.MaxHealth = 320;
		bruiser.Health = 320;
		TASK.TASK_COMBAT_PED(bruiser.Handle, player.Handle, 0, 16);
		_activePrizeFighter = bruiser;
		_activePrizeFightTown = town;
		Feed($"SALOON PRIZE FIGHT: Knock out the {town} Champion with your fists to win the $150 purse!", Theme.Gold);
		GrudgeLog($"CIVIC spawned saloon prize fighter in {town}");
	}

	internal void TickCivicSystem(Ped player)
	{
		if (player == null || player.IsDead)
		{
			return;
		}
		if (Game.GameTime >= _nextCivicTick)
		{
			_nextCivicTick = Game.GameTime + 2500;
			TickCivicDailyEconomyAndElections();
			TickCivicLiveEncounters(player);
		}
		if (Game.GameTime >= _nextCurfewCheck)
		{
			_nextCurfewCheck = Game.GameTime + 4000;
			TickMartialLawCurfew(player);
		}
	}

	private void TickCivicDailyEconomyAndElections()
	{
		int dayNow = Math.Max(1, GameDayKey());
		if (Civic.LastDailyTickDay == 0)
		{
			Civic.LastDailyTickDay = dayNow;
			SaveCivicState();
			return;
		}
		if (dayNow <= Civic.LastDailyTickDay)
		{
			return;
		}
		Civic.LastDailyTickDay = dayNow;
		int bizPayout = 0;
		int taxKickback = 0;
		foreach (TownBusiness biz in Civic.Businesses)
		{
			int stillBonus = biz.HasMoonshineStill ? 25 : 0;
			if (biz.Control == BusinessControl.PlayerPartner || biz.Control == BusinessControl.PlayerProtected)
			{
				bizPayout += biz.DailyIncome * biz.Level + stillBonus;
			}
			else if (biz.Control == BusinessControl.PlayerRacket)
			{
				bizPayout += (biz.DailyIncome + 12) * biz.Level + stillBonus;
				TownPoliticState tp = TownPoliticsOf(biz.Town);
				if (tp != null && tp.BackedFaction != "you" && tp.SheriffBribedUntilMin <= GameMinutesNow())
				{
					tp.CaseHeat = Math.Min(100, tp.CaseHeat + 8);
					UpdateTownWarrant(tp);
				}
			}
		}
		foreach (TownPoliticState tp in Civic.Towns.Values)
		{
			if (tp.BackedFaction == "you")
			{
				taxKickback += tp.TaxRatePct * 4;
			}
			if (dayNow >= tp.ElectionDay)
			{
				RunTownElection(tp, forcedRig: false);
			}
			if (tp.MartialLawUntilDay > 0 && dayNow >= tp.MartialLawUntilDay)
			{
				tp.MartialLawUntilDay = 0;
				if (tp.Regime == PoliticalRegime.MartialLaw)
				{
					tp.Regime = PoliticalRegime.Reformist;
				}
				Feed($"MARTIAL LAW ended in {tp.Town}.", Theme.Gold);
			}
		}
		int bankInterest = (Civic.BankBalanceDollars > 0) ? Math.Max(1, Civic.BankBalanceDollars / 20) : 0;
		Civic.BankBalanceDollars += bankInterest;
		int totalCashPaid = bizPayout + taxKickback;
		if (totalCashPaid > 0)
		{
			PayPlayer(totalCashPaid * 100);
			Civic.TotalBusinessIncome += bizPayout;
			Civic.TotalTaxesCollected += taxKickback;
			Feed($"DAILY LEDGER: +${bizPayout} business/moonshine income, +${taxKickback} mayor tax share, +${bankInterest} bank interest!", Theme.Gold);
		}
		for (int i = Civic.Prisoners.Count - 1; i >= 0; i--)
		{
			PrisonerRecord pr = Civic.Prisoners[i];
			if (dayNow >= pr.ExecutionDay)
			{
				Civic.Prisoners.RemoveAt(i);
				Feed($"COURT EXECUTION: {pr.Name} ({pr.Sentence}) was carried out at dawn in {pr.Town}.", Theme.Warn);
			}
		}
		SaveCivicState();
	}

	private void TickCivicLiveEncounters(Ped player)
	{
		if (_activeMayorPed != null)
		{
			if (!_activeMayorPed.Exists() || _activeMayorPed.IsDead)
			{
				string town = _activeMayorTown ?? "Valentine";
				TownPoliticState tp = TownPoliticsOf(town);
				CleanupMayoralRally();
				if (tp != null)
				{
					string deadMayor = tp.MayorName;
					tp.MayorName = tp.ChallengerName;
					tp.ChallengerName = PickChallengerName(town);
					tp.Regime = PoliticalRegime.MartialLaw;
					tp.MartialLawUntilDay = Math.Max(1, GameDayKey()) + 5;
					tp.CaseHeat = 100;
					tp.Warrant = WarrantTier.DeadOrAlive;
					SaveCivicState();
					Feed($"POLITICAL ASSASSINATION! {deadMayor} was killed in {town}! Martial Law declared & {tp.MayorName} sworn in!", Theme.Warn);
					GrudgeLog($"CIVIC Mayor {deadMayor} assassinated in {town}; Martial Law declared");
				}
			}
		}
		if (_activeRacketBiz != null && _activeRacketEnforcers.Count > 0)
		{
			_activeRacketEnforcers.RemoveAll((Ped p) => p == null || !p.Exists() || p.IsDead);
			if (_activeRacketEnforcers.Count == 0)
			{
				TownBusiness biz = _activeRacketBiz;
				_activeRacketBiz = null;
				biz.Control = BusinessControl.PlayerProtected;
				biz.ExtortingGang = null;
				SaveCivicState();
				PayPlayer(45 * 100);
				Feed($"BUSINESS LIBERATED! {biz.Name} now pays you +${biz.DailyIncome}/day in protection!", Theme.Gold);
				GrudgeLog($"CIVIC liberated business {biz.Name} in {biz.Town}");
			}
		}
		if (_activePrisonerRec != null && _activeConvoyGuards.Count > 0)
		{
			_activeConvoyGuards.RemoveAll((Ped p) => p == null || !p.Exists() || p.IsDead);
			if (_activeConvoyGuards.Count == 0)
			{
				PrisonerRecord pr = _activePrisonerRec;
				_activePrisonerRec = null;
				Civic.Prisoners.RemoveAll((PrisonerRecord x) => x.Id == pr.Id);
				if (_activePrisonerPed != null && _activePrisonerPed.Exists() && !_activePrisonerPed.IsDead)
				{
					TASK.TASK_LEAVE_ANY_VEHICLE(_activePrisonerPed.Handle, 0, 0);
				}
				int reward = 85 + pr.Rank * 25;
				PayPlayer(reward * 100);
				Gang g = Gangs.ById(pr.Gang);
				if (g != null)
				{
					RevealTreasury(g, pr.Name + " (jailbreak)");
				}
				SaveCivicState();
				Feed($"JAILBREAK COMPLETE! Rescued {pr.Name} from the U.S. Marshals! He gave you ${reward} and gang intel!", Theme.Gold);
				GrudgeLog($"CIVIC prison wagon jailbreak succeeded for {pr.Name}");
			}
		}
		if (_activeBountyContract != null && _activeBountyPed != null)
		{
			if (!_activeBountyPed.Exists())
			{
				_activeBountyPed = null;
				_activeBountyContract = null;
				_activeBountyGuards.Clear();
			}
			else if (_activeBountyPed.IsHogtied)
			{
				BountyContract bc = _activeBountyContract;
				Ped capPed = _activeBountyPed;
				_activeBountyPed = null;
				_activeBountyContract = null;
				_activeBountyGuards.Clear();
				Civic.Bounties.RemoveAll((BountyContract x) => x.Id == bc.Id);
				Civic.BountiesCapturedAlive++;
				PayPlayer(bc.AliveReward * 100);
				BookArrestedPedIntoCourt(capPed, null, bc.Town);
				BoostTownGoodwill(bc.Town, 10f, 10f, "captured bounty " + bc.TargetName + " alive");
				SaveCivicState();
				Feed($"BOUNTY CAPTURED ALIVE! Hogtied {bc.TargetName} for ${bc.AliveReward} (+Town Rep & booked onto {bc.Town} Court Docket)!", Theme.Gold);
			}
			else if (_activeBountyPed.IsDead)
			{
				BountyContract bc = _activeBountyContract;
				_activeBountyPed = null;
				_activeBountyContract = null;
				_activeBountyGuards.Clear();
				Civic.Bounties.RemoveAll((BountyContract x) => x.Id == bc.Id);
				Civic.BountiesKilled++;
				PayPlayer(bc.DeadReward * 100);
				SaveCivicState();
				Feed($"BOUNTY ELIMINATED: Collected ${bc.DeadReward} Dead-or-Alive reward on {bc.TargetName}!", Theme.Gold);
			}
		}
		if (_activePayrollNpcs.Count > 0)
		{
			_activePayrollNpcs.RemoveAll((Ped p) => p == null || !p.Exists() || p.IsDead);
			if (_activePayrollNpcs.Count == 0)
			{
				string pTown = _activePayrollTown ?? "Valentine";
				TownPoliticState tp = TownPoliticsOf(pTown);
				Civic.PayrollHeistsCompleted++;
				if (!_activePayrollIsEscort)
				{
					PayPlayer(250 * 100);
					if (tp != null && tp.BackedFaction != "you" && tp.SheriffBribedUntilMin <= GameMinutesNow())
					{
						tp.CaseHeat = Math.Min(100, tp.CaseHeat + 35);
						UpdateTownWarrant(tp);
					}
					Feed($"PAYROLL HEIST COMPLETE! Looted $250 from the armored stagecoach strongbox!", Theme.Gold);
				}
				else
				{
					PayPlayer(135 * 100);
					if (tp != null)
					{
						tp.Approval = Math.Min(100, tp.Approval + 10);
						tp.CaseHeat = Math.Max(0, tp.CaseHeat - 20);
						UpdateTownWarrant(tp);
					}
					BoostTownGoodwill(pTown, 8f, 10f, "defended payroll stagecoach");
					Feed($"PAYROLL ESCORT COMPLETE! Earned $135, +10 Mayor Approval, and cleared 20 Case Heat in {pTown}!", Theme.Gold);
				}
				SaveCivicState();
			}
		}
		if (_activePrizeFighter != null)
		{
			if (!_activePrizeFighter.Exists())
			{
				_activePrizeFighter = null;
			}
			else if (_activePrizeFighter.IsDead || _activePrizeFighter.IsIncapacitated || LooksKnockedOut(_activePrizeFighter))
			{
				string fTown = _activePrizeFightTown ?? "Valentine";
				_activePrizeFighter = null;
				Civic.PrizeFightsWon++;
				PayPlayer(150 * 100);
				BoostTownGoodwill(fTown, 6f, 4f, "won saloon prize fight");
				SaveCivicState();
				Feed($"KNOCKOUT VICTORY! Won the $150 Saloon Prize-Fight Purse in {fTown} (Bouts won: {Civic.PrizeFightsWon})!", Theme.Gold);
			}
		}
	}

	private void TickMartialLawCurfew(Ped player)
	{
		string town = TownAt(player.Position);
		if (string.IsNullOrEmpty(town) || !Civic.Towns.TryGetValue(town, out var tp))
		{
			return;
		}
		long nowMin = GameMinutesNow();
		if (tp.SheriffBribedUntilMin > nowMin || tp.BackedFaction == "you")
		{
			return;
		}
		int hour = CLOCK.GET_CLOCK_HOURS();
		bool isNight = hour >= 22 || hour < 5;
		bool martial = tp.Regime == PoliticalRegime.MartialLaw || tp.MartialLawUntilDay > GameDayKey();
		if (martial && isNight && (_maskedNow || WEAPON._GET_PED_CURRENT_HELD_WEAPON(player.Handle) != 2725352035u))
		{
			_lawWarnings++;
			tp.CaseHeat = Math.Min(100, tp.CaseHeat + 10);
			UpdateTownWarrant(tp);
			if (_lawWarnings == 1)
			{
				Feed($"CURFEW WARNING ({town}): {tp.SheriffName}'s curfew is in effect (22:00-05:00). Holster weapons & unmask!", Theme.Warn);
			}
			else if (_lawWarnings >= 3 && Game.GameTime - _lastCheckpointSpawnAt > 60000)
			{
				_lawWarnings = 0;
				SpawnPoliceCheckpoint(player, tp);
			}
		}
	}

	internal void PopulateCivicNewspaper(Newspaper paper, string town)
	{
		if (paper == null)
		{
			return;
		}
		TownPoliticState tp = TownPoliticsOf(town);
		if (tp != null)
		{
			int daysLeft = Math.Max(1, tp.ElectionDay - Math.Max(1, GameDayKey()));
			paper.About.Insert(0, $"TOWN HALL: {tp.MayorName} ({tp.Regime}) holds {tp.Approval}% approval (Tax rate {tp.TaxRatePct}%). Challenger {tp.ChallengerName} polls at {tp.ChallengerSupport}% with elections in {daysLeft} days.");
			if (!string.IsNullOrEmpty(tp.CustomHeadline))
			{
				paper.About.Insert(0, "SPECIAL EDITION: " + tp.CustomHeadline);
			}
			if (tp.Regime == PoliticalRegime.MartialLaw || tp.MartialLawUntilDay > GameDayKey())
			{
				paper.About.Insert(0, $"PROCLAMATION: {tp.Town} remains under MARTIAL LAW. Citizens out after 10 o'clock at night with masks or long arms will be shot on sight by order of {tp.SheriffName}.");
			}
			if (tp.Warrant != WarrantTier.Clear)
			{
				paper.Wanted.Insert(0, $"PINKERTON DOSSIER ({tp.DetectiveName}): A {tp.Warrant} warrant (Heat {tp.CaseHeat}/100) is active in {tp.Town}.");
			}
		}
		foreach (BountyContract bc in Civic.Bounties.Take(2))
		{
			paper.Wanted.Add($"BOUNTY BOARD ({bc.Town}): ${bc.AliveReward} Alive / ${bc.DeadReward} Dead for {bc.TargetName} ({bc.Crime}).");
		}
		foreach (PrisonerRecord pr in Civic.Prisoners.Take(2))
		{
			paper.Wanted.Add($"COURT DOCKET ({pr.Town}): {pr.Name} is scheduled for {pr.Sentence} in {Math.Max(1, pr.ExecutionDay - Math.Max(1, GameDayKey()))} days (Bail: ${pr.BailDollars}).");
		}
	}

	private static NemesisMod _liveInstance;

	public static NemesisMod Instance => _liveInstance ?? (NemesisRecord.GameClock?.Target as NemesisMod);

	public Dictionary<string, string> ExportAiNpcContext(int pedHandle)
	{
		_liveInstance = this;
		Dictionary<string, string> ctx = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		ctx["enrage_active"] = "true";
		Ped player = Game.Player?.Character;
		string town = (player != null && player.Exists()) ? (TownAt(player.Position) ?? _currentTown ?? "Valentine") : (_currentTown ?? "Valentine");
		ctx["town"] = town;
		ctx["player_name"] = InterroLines.PlayerName ?? YouShort ?? "Arthur Morgan";

		NemesisRecord rec = RecordAt(pedHandle) ?? Roster.FirstOrDefault((NemesisRecord r) => r != null && r.IsAlive && r.LastHandle == pedHandle);
		if (_activeMayorPed != null && _activeMayorPed.Exists() && _activeMayorPed.Handle == pedHandle)
		{
			TownPoliticState mp = TownPoliticsOf(_activeMayorTown ?? town);
			ctx["npc_name"] = mp?.MayorName ?? "Mayor";
			ctx["npc_role"] = $"Sitting Mayor of {_activeMayorTown ?? town} ({mp?.Regime})";
			ctx["npc_faction"] = mp?.BackedFaction ?? "Town Government";
			ctx["npc_personality"] = "Proud, political, calculating";
			ctx["is_special_civic"] = "Mayor";
		}
		else if (_activePrisonerPed != null && _activePrisonerPed.Exists() && _activePrisonerPed.Handle == pedHandle && _activePrisonerRec != null)
		{
			ctx["npc_name"] = _activePrisonerRec.Name;
			ctx["npc_role"] = $"Captured Outlaw Prisoner (Scheduled for {_activePrisonerRec.Sentence} in {_activePrisonerRec.Town})";
			ctx["npc_faction"] = Gangs.ById(_activePrisonerRec.Gang)?.Name ?? _activePrisonerRec.Gang ?? "Outlaw";
			ctx["npc_personality"] = "Desperate, defiant captive";
			ctx["is_special_civic"] = "Prisoner";
		}
		else if (_activeBountyPed != null && _activeBountyPed.Exists() && _activeBountyPed.Handle == pedHandle && _activeBountyContract != null)
		{
			ctx["npc_name"] = _activeBountyContract.TargetName;
			ctx["npc_role"] = $"Wanted Bounty Target (${_activeBountyContract.AliveReward} Alive / ${_activeBountyContract.DeadReward} Dead for {_activeBountyContract.Crime})";
			ctx["npc_faction"] = Gangs.ById(_activeBountyContract.Gang)?.Name ?? _activeBountyContract.Gang ?? "Outlaw";
			ctx["npc_personality"] = "Ruthless, hunted outlaw";
			ctx["is_special_civic"] = "BountyTarget";
		}

		if (rec != null)
		{
			ctx["is_nemesis"] = "true";
			ctx["nemesis_id"] = rec.Id ?? "";
			ctx["npc_name"] = rec.FullName ?? rec.Name ?? "Unknown Outlaw";
			ctx["npc_title"] = rec.Title ?? "";
			ctx["npc_rank"] = rec.Rank.ToString();
			Gang g = Gangs.ById(rec.Gang);
			ctx["npc_faction_id"] = rec.Gang ?? "";
			ctx["npc_faction"] = g?.Name ?? rec.Affiliation ?? "Independent";
			ctx["npc_role"] = (g != null) ? $"{g.Name} (Rank {rec.Rank})" : (rec.Trade ?? "Frontier Drifter");
			ctx["npc_personality"] = (rec.Personality ?? Personality.Cold).ToString();
			ctx["npc_hate"] = rec.Hate.ToString();
			ctx["npc_fear"] = EffectiveFear(rec).ToString();
			ctx["npc_grudge"] = rec.GrudgeSummary ?? "";
			ctx["npc_origin"] = rec.Origin.ToString();
			ctx["npc_times_killed_player"] = rec.TimesKilledPlayer.ToString();
			ctx["npc_times_cheated_death"] = rec.TimesCheatedDeath.ToString();
			ctx["npc_times_spared"] = rec.TimesSpared.ToString();
			ctx["npc_limp"] = rec.Limp ? "true" : "false";
			ctx["npc_scars"] = (rec.Scars != null && rec.Scars.Count > 0) ? string.Join(", ", rec.Scars.Select((Scar s) => s.Description ?? s.Wound ?? "scarred")) : "";
			ctx["npc_quirks"] = (rec.Quirks != null && rec.Quirks.Count > 0) ? string.Join(", ", rec.Quirks) : "";
			ctx["npc_backstory"] = $"Born {rec.BornYear} in {rec.Birthplace ?? "the frontier"}. Trade: {rec.Trade ?? "gunhand"}. Family: {rec.Family ?? "none"}. Faith: {rec.Faith ?? "none"}.";
			NemesisRecord boss = BossOf(rec);
			if (boss != null)
			{
				ctx["npc_boss"] = boss.FullName;
			}
			if (rec.History != null && rec.History.Count > 0)
			{
				ctx["npc_history"] = string.Join(" | ", rec.History.Skip(Math.Max(0, rec.History.Count - 4)).Select((HistoryEntry h) => h.Text));
			}
		}
		else if (!ctx.ContainsKey("npc_name"))
		{
			string who = WhoIs(pedHandle);
			if (!string.IsNullOrEmpty(who) && who != "a stranger")
			{
				ctx["npc_name"] = who.Replace(" (stranger)", "");
			}
			if (ENTITY.DOES_ENTITY_EXIST(pedHandle))
			{
				uint modelHash = ENTITY.GET_ENTITY_MODEL(pedHandle);
				Gang mg = Gangs.ForModel(modelHash);
				if (mg != null)
				{
					ctx["npc_faction_id"] = mg.Id;
					ctx["npc_faction"] = mg.Name;
					ctx["npc_role"] = mg.Name + " Member";
				}
			}
		}

		TownPoliticState tp = TownPoliticsOf(town);
		if (tp != null)
		{
			ctx["town_mayor"] = tp.MayorName ?? "";
			ctx["town_sheriff"] = tp.SheriffName ?? "";
			ctx["town_detective"] = tp.DetectiveName ?? "";
			ctx["town_challenger"] = tp.ChallengerName ?? "";
			ctx["town_regime"] = tp.Regime.ToString();
			ctx["town_corruption"] = tp.Corruption.ToString();
			ctx["town_tax_rate"] = tp.TaxRatePct.ToString();
			ctx["town_warrant"] = tp.Warrant.ToString();
			ctx["town_case_heat"] = tp.CaseHeat.ToString();
			ctx["town_backed_faction"] = Gangs.ById(tp.BackedFaction)?.Name ?? tp.BackedFaction ?? "None";
		}
		if (Civic.Bounties != null && Civic.Bounties.Count > 0)
		{
			BountyContract b = Civic.Bounties.FirstOrDefault((BountyContract x) => x.Town == town) ?? Civic.Bounties[0];
			ctx["active_bounty"] = $"{b.TargetName} (${b.AliveReward} Alive in {b.Town} for {b.Crime})";
		}
		if (Civic.Prisoners != null && Civic.Prisoners.Count > 0)
		{
			PrisonerRecord p = Civic.Prisoners.FirstOrDefault((PrisonerRecord x) => x.Town == town) ?? Civic.Prisoners[0];
			ctx["court_prisoner"] = $"{p.Name} awaiting {p.Sentence} in {p.Town} Jail (Bail ${p.BailDollars})";
		}
		return ctx;
	}

	public void RecordAiDialogueOutcome(int pedHandle, string playerLine, string npcReply, int hateDelta, int fearDelta, bool revealedCamp)
	{
		try
		{
			NemesisRecord rec = RecordAt(pedHandle) ?? Roster.FirstOrDefault((NemesisRecord r) => r != null && r.IsAlive && r.LastHandle == pedHandle);
			if (rec != null)
			{
				rec.Touch();
				if (hateDelta != 0)
				{
					rec.AddHate(hateDelta);
				}
				if (fearDelta != 0)
				{
					rec.AddFear(fearDelta);
				}
				if (revealedCamp)
				{
					rec.CampKnown = true;
					rec.GaveCamp = true;
				}
				if (rec.History == null)
				{
					rec.History = new List<HistoryEntry>();
				}
				rec.History.Add(new HistoryEntry
				{
					At = DateTime.Now,
					Text = $"Spoke with you: \"{playerLine}\" -> \"{npcReply}\""
				});
				SaveRoster();
			}
		}
		catch
		{
		}
	}
}
