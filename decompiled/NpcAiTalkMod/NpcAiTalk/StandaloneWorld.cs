using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using RDR2;
using RDR2.Math;
using RDR2.Native;

namespace NpcAiTalk;

public sealed class NpcTurnRecord
{
	public string PlayerText { get; set; } = "";
	public string NpcReply { get; set; } = "";
	public string ActionTag { get; set; } = "NONE";
	public DateTime Timestamp { get; set; } = DateTime.Now;
}

public sealed class PersistentNpcMemory
{
	public string NpcKey { get; set; } = "";
	public string Name { get; set; } = "";
	public string Role { get; set; } = "";
	public string Faction { get; set; } = "";
	public string Personality { get; set; } = "";
	public string Backstory { get; set; } = "";
	public int Disposition { get; set; } = 0;
	public int TimesSpoken { get; set; } = 0;
	public bool Bribed { get; set; } = false;
	public bool IntelShared { get; set; } = false;
	public List<NpcTurnRecord> Turns { get; set; } = new List<NpcTurnRecord>();
}

public sealed class NpcContextProfile
{
	public int PedHandle { get; set; }
	public string NpcKey { get; set; } = "";
	public string Name { get; set; } = "Frontier Stranger";
	public string Title { get; set; } = "";
	public string Role { get; set; } = "Townsperson";
	public string Faction { get; set; } = "Independent";
	public string FactionId { get; set; } = "";
	public string Personality { get; set; } = "Stern";
	public bool IsMale { get; set; } = true;

	// EnRAGE / NemesisMod Details (populated when NemesisMod is present or via standalone synthesis)
	public bool IsEnrageConnected { get; set; }
	public bool IsNemesis { get; set; }
	public int Rank { get; set; } = 1;
	public int Hate { get; set; } = 0;
	public int Fear { get; set; } = 10;
	public string GrudgeSummary { get; set; } = "";
	public string Scars { get; set; } = "";
	public string Quirks { get; set; } = "";
	public string Backstory { get; set; } = "";
	public string BossName { get; set; } = "";
	public string HistorySummary { get; set; } = "";
	public bool HasLimp { get; set; }

	// Town & Political Details
	public string Town { get; set; } = "Valentine";
	public string District { get; set; } = "The Heartlands";
	public string TownMayor { get; set; } = "Mayor Proctoravin";
	public string TownSheriff { get; set; } = "Sheriff Curtis Malloy";
	public string TownDetective { get; set; } = "Marshal Tom Davies";
	public string TownChallenger { get; set; } = "Alderman Harlan Malloy";
	public string TownRegime { get; set; } = "FrontierPopulist";
	public int TownCorruption { get; set; } = 35;
	public int TownTaxRate { get; set; } = 5;
	public string TownWarrant { get; set; } = "Clear";
	public int TownCaseHeat { get; set; } = 0;
	public string TownBackedFaction { get; set; } = "None";
	public string ActiveBounty { get; set; } = "";
	public string CourtPrisoner { get; set; } = "";

	// Live World & Player State
	public string PlayerName { get; set; } = "Arthur Morgan";
	public string PlayerHonor { get; set; } = "Neutral";
	public int PlayerCashDollars { get; set; } = 100;
	public int PlayerBountyDollars { get; set; } = 0;
	public bool IsPlayerArmed { get; set; }
	public bool IsPlayerAimingAtNpc { get; set; }
	public string TimeOfDay { get; set; } = "12:00 Noon";
	public string Weather { get; set; } = "Clear Skies";

	// Memory
	public PersistentNpcMemory Memory { get; set; } = new PersistentNpcMemory();
}

public static class StandaloneWorld
{
	private static readonly (string town, string district, float x, float y, float radius, string mayor, string sheriff, string detective, string regime, int corruption, int tax)[] Towns = new[]
	{
		("Valentine", "The Heartlands", -282.0f, 780.0f, 450.0f, "Mayor Proctoravin", "Sheriff Curtis Malloy", "Marshal Tom Davies", "FrontierPopulist", 35, 5),
		("Rhodes", "Scarlett Meadows", 1320.0f, -1310.0f, 450.0f, "Mayor Alden Gray", "Sheriff Leigh Gray", "Detective Charles Vance", "CorruptMachine", 62, 7),
		("Saint Denis", "Bayou Nwa", 2630.0f, -1225.0f, 850.0f, "Mayor Henri Lemieux", "Chief Benjamin Lambert", "Inspector Edgar Ross", "CorruptMachine", 78, 10),
		("Strawberry", "Big Valley", -1790.0f, -388.0f, 400.0f, "Mayor Nicholas Timmins", "Sheriff Farley", "Marshal Harlan Wade", "Reformist", 28, 6),
		("Blackwater", "Great Plains", -815.0f, -1320.0f, 520.0f, "Mayor Elihu Dunbar", "Chief Oswald Fawcett", "Agent Andrew Milton", "Reformist", 18, 8),
		("Annesburg", "Roanoke Ridge", 2915.0f, 1345.0f, 450.0f, "Magistrate Leviticus Jameson", "Sheriff Agnes Jones", "Inspector Arthur Boyle", "CorruptMachine", 68, 9),
		("Van Horn", "Roanoke Ridge", 2965.0f, 545.0f, 380.0f, "Harbormaster Silas Pike", "Constable Jude", "Agent Caleb Thorne", "FrontierPopulist", 55, 4),
		("Armadillo", "Cholla Springs", -3685.0f, -2605.0f, 450.0f, "Mayor E. H. Mowry", "Marshal Leigh Johnson", "Marshal Sam Freeman", "FrontierPopulist", 42, 4),
		("Tumbleweed", "Gaptooth Ridge", -5515.0f, -2935.0f, 450.0f, "Magistrate Sam Freeman", "Sheriff Sam Freeman", "Marshal Eli Dalton", "MartialLaw", 10, 7),
		("Emerald Ranch", "The Heartlands", 1418.0f, 315.0f, 350.0f, "Eugene Wegner", "Sheriff Curtis Malloy", "Marshal Tom Davies", "FrontierPopulist", 30, 4),
		("Lagras", "Bayou Nwa", 2115.0f, -625.0f, 320.0f, "Mayor Henri Lemieux", "Chief Benjamin Lambert", "Inspector Edgar Ross", "FrontierPopulist", 40, 3)
	};

	private static readonly string[] MaleFirstNames = new[]
	{
		"Silas", "Caleb", "Eli", "Gideon", "Harlan", "Amos", "Josiah", "Rufus", "Ezekiel", "Barnaby",
		"Cormac", "Seamus", "Liam", "Padraig", "Virgil", "Otis", "Beau", "Clement", "Abner", "Thaddeus",
		"wade", "Emmett", "Luther", "Cornelius", "Hector", "Zachariah", "Jedediah", "Clive", "Orville", "fenton"
	};

	private static readonly string[] FemaleFirstNames = new[]
	{
		"Clara", "Martha", "Abigail", "Evelyn", "Hattie", "Maeve", "Beatrice", "Clementine", "Eleanor", "Rosalie",
		"Miriam", "Ada", "Florence", "Matilda", "grace", "Lillian", "Nora", "Ruth", "Constance", "Cordelia"
	};

	private static readonly string[] LastNames = new[]
	{
		"Calloway", "Vance", "Thorne", "McAllister", "O'Shea", "Gallagher", "holloway", "Mercer", "Wilkins", "tucker",
		"Braddock", "kincaid", "Sutter", "boone", "Dalton", "finch", "Garrick", "Hawkins", "langley", "Preston",
		"Calhoun", "bates", "Crenshaw", "Doyle", "kavanagh", "slater", "Wheeler", "Yates", "stoddard", "carver"
	};

	private static readonly string[] Personalities = new[]
	{
		"Stern", "Friendly", "Suspicious", "Hothead", "Coward", "Proud", "Weary", "Greedy", "Honorable", "Cunning"
	};

	private static readonly string[] Trades = new[]
	{
		"Livestock Drover", "Railroad Laborer", "Blacksmith's Hand", "Stagecoach Driver", "Fur Trapper",
		"Saloon Regular", "Telegraph Lineman", "Timber Sawyer", "Coal Miner", "General Store Clerk",
		"Freight Teamster", "Homesteader", "Card Sharp", "Bounty Tracker", "Horse Breaker"
	};

	private static readonly string[] Birthplaces = new[]
	{
		"Ohio", "Kentucky", "Virginia", "County Cork, Ireland", "Missouri", "Tennessee",
		"Pennsylvania", "Georgia", "Texas", "Boston", "Illinois", "North Carolina"
	};

	private static readonly Dictionary<int, PersistentNpcMemory> LiveHandleCache = new Dictionary<int, PersistentNpcMemory>();
	private static Dictionary<string, PersistentNpcMemory> _diskMemories = new Dictionary<string, PersistentNpcMemory>(StringComparer.OrdinalIgnoreCase);
	private static string _memoryPath = "";

	public static void InitMemoryStore(string memoryFilePath)
	{
		_memoryPath = memoryFilePath;
		try
		{
			if (File.Exists(_memoryPath))
			{
				var loaded = JsonConvert.DeserializeObject<Dictionary<string, PersistentNpcMemory>>(File.ReadAllText(_memoryPath));
				if (loaded != null)
				{
					_diskMemories = new Dictionary<string, PersistentNpcMemory>(loaded, StringComparer.OrdinalIgnoreCase);
				}
			}
		}
		catch
		{
		}
	}

	public static void SaveMemoryStore()
	{
		if (string.IsNullOrEmpty(_memoryPath))
		{
			return;
		}
		try
		{
			string dir = Path.GetDirectoryName(_memoryPath);
			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}
			File.WriteAllText(_memoryPath, JsonConvert.SerializeObject(_diskMemories, Formatting.Indented));
		}
		catch
		{
		}
	}

	public static (string town, string district, string mayor, string sheriff, string detective, string regime, int corruption, int tax) ResolveTown(Vector3 pos)
	{
		var best = Towns[0];
		float bestDist = float.MaxValue;
		foreach (var t in Towns)
		{
			float dx = pos.X - t.x;
			float dy = pos.Y - t.y;
			float dist = (float)Math.Sqrt(dx * dx + dy * dy);
			if (dist < bestDist)
			{
				bestDist = dist;
				best = t;
			}
		}
		return (best.town, best.district, best.mayor, best.sheriff, best.detective, best.regime, best.corruption, best.tax);
	}

	public static NpcContextProfile BuildStandaloneProfile(Ped npc)
	{
		NpcContextProfile profile = new NpcContextProfile();
		if (npc == null || !npc.Exists())
		{
			return profile;
		}

		int handle = npc.Handle;
		profile.PedHandle = handle;

		Ped player = Game.Player?.Character;
		Vector3 refPos = (player != null && player.Exists()) ? player.Position : npc.Position;
		var townInfo = ResolveTown(refPos);

		profile.Town = townInfo.town;
		profile.District = townInfo.district;
		profile.TownMayor = townInfo.mayor;
		profile.TownSheriff = townInfo.sheriff;
		profile.TownDetective = townInfo.detective;
		profile.TownRegime = townInfo.regime;
		profile.TownCorruption = townInfo.corruption;
		profile.TownTaxRate = townInfo.tax;

		uint modelHash = ENTITY.GET_ENTITY_MODEL(handle);
		bool isMale = true;
		try
		{
			isMale = PED.IS_PED_MALE(handle);
		}
		catch
		{
		}
		profile.IsMale = isMale;

		int seed = Math.Abs((int)modelHash ^ (handle * 397));
		string npcKey = $"npc_{modelHash:X8}_{Math.Abs((int)Math.Round(npc.Position.X / 45f))}_{Math.Abs((int)Math.Round(npc.Position.Y / 45f))}";
		profile.NpcKey = npcKey;

		if (!LiveHandleCache.TryGetValue(handle, out PersistentNpcMemory mem))
		{
			if (!_diskMemories.TryGetValue(npcKey, out mem))
			{
				var roleFaction = ClassifyPedModel(modelHash, isMale, profile.Town, seed);
				string first = isMale
					? Capitalize(MaleFirstNames[seed % MaleFirstNames.Length])
					: Capitalize(FemaleFirstNames[seed % FemaleFirstNames.Length]);
				string last = Capitalize(LastNames[(seed / 17) % LastNames.Length]);
				string pers = Personalities[(seed / 31) % Personalities.Length];
				string trade = roleFaction.role;
				string birthplace = Birthplaces[(seed / 53) % Birthplaces.Length];
				int bornYear = 1842 + (seed % 34);

				mem = new PersistentNpcMemory
				{
					NpcKey = npcKey,
					Name = $"{first} {last}",
					Role = trade,
					Faction = roleFaction.faction,
					Personality = pers,
					Backstory = $"Born {bornYear} in {birthplace}. Works as a {trade} around {profile.Town}."
				};
				_diskMemories[npcKey] = mem;
			}
			LiveHandleCache[handle] = mem;
		}

		profile.Memory = mem;
		profile.Name = mem.Name;
		profile.Role = mem.Role;
		profile.Faction = mem.Faction;
		profile.Personality = mem.Personality;
		profile.Backstory = mem.Backstory;

		PopulateLivePlayerAndWorldState(profile, npc);
		return profile;
	}

	public static unsafe void PopulateLivePlayerAndWorldState(NpcContextProfile profile, Ped npc)
	{
		try
		{
			Ped player = Game.Player?.Character;
			if (player != null && player.Exists())
			{
				uint pModel = ENTITY.GET_ENTITY_MODEL(player.Handle);
				// player_three is John Marston
				profile.PlayerName = (pModel == MISC.GET_HASH_KEY("player_three")) ? "John Marston" : "Arthur Morgan";
				profile.PlayerCashDollars = Math.Max(0, MONEY._MONEY_GET_CASH_BALANCE() / 100);
				profile.PlayerBountyDollars = Math.Max(0, LAW.GET_BOUNTY(Game.Player.Handle));
				profile.IsPlayerArmed = WEAPON.IS_PED_ARMED(player.Handle, 7);
				profile.IsPlayerAimingAtNpc = npc != null && npc.Exists() && PLAYER.IS_PLAYER_FREE_AIMING_AT_ENTITY(Game.Player.Handle, npc.Handle);
				profile.PlayerHonor = (profile.PlayerBountyDollars > 100) ? "Low / Wanted Outlaw" : "High / Respectable";
			}

			int hour = CLOCK.GET_CLOCK_HOURS();
			int min = CLOCK.GET_CLOCK_MINUTES();
			string period = (hour < 6) ? "Late Night" : (hour < 12) ? "Morning" : (hour < 18) ? "Afternoon" : (hour < 21) ? "Evening" : "Night";
			profile.TimeOfDay = $"{hour:00}:{min:00} ({period})";

			uint wPrev = 0u, wNext = 0u;
			float wInterp = 0f;
			MISC.GET_CURR_WEATHER_STATE(&wPrev, &wNext, &wInterp);
			profile.Weather = DescribeWeatherHash(wNext != 0 ? wNext : wPrev);
		}
		catch
		{
		}
	}

	private static (string role, string faction) ClassifyPedModel(uint modelHash, bool isMale, string town, int seed)
	{
		switch ((PedHash)modelHash)
		{
		case PedHash.s_m_m_pinlaw_01:
			return ("Pinkerton National Detective", "Pinkerton Detective Agency");
		case PedHash.s_m_m_ambientblwpolice_01:
		case PedHash.s_m_m_ambientsdpolice_01:
		case PedHash.u_m_m_sdpolicechief_01:
			return ($"{town} Police Officer", $"{town} Law Enforcement");
		case PedHash.s_m_m_ambientlawrural_01:
		case PedHash.s_m_m_valdeputy_01:
		case PedHash.s_m_m_asbdeputies_01:
			return ($"Deputy Sheriff of {town}", $"{town} Sheriff's Office");
		}

		if (!isMale)
		{
			string[] femaleRoles = new[] { $"Townswoman of {town}", "Boarding House Keeper", "Seamstress", "Saloon Hostess", "Homesteader's Wife", "Market Vendor" };
			return (femaleRoles[seed % femaleRoles.Length], $"{town} Citizens");
		}

		string trade = Trades[seed % Trades.Length];
		return (trade, $"{town} Citizens");
	}

	private static string DescribeWeatherHash(uint hash)
	{
		if (hash == MISC.GET_HASH_KEY("RAIN") || hash == MISC.GET_HASH_KEY("THUNDERSTORM") || hash == MISC.GET_HASH_KEY("DRIZZLE"))
		{
			return "Rainy and Muddy";
		}
		if (hash == MISC.GET_HASH_KEY("FOG") || hash == MISC.GET_HASH_KEY("MISTY"))
		{
			return "Thick Frontier Fog";
		}
		if (hash == MISC.GET_HASH_KEY("SNOW") || hash == MISC.GET_HASH_KEY("BLIZZARD"))
		{
			return "Freezing Snow";
		}
		if (hash == MISC.GET_HASH_KEY("OVERCAST") || hash == MISC.GET_HASH_KEY("CLOUDS"))
		{
			return "Overcast Skies";
		}
		return "Clear Frontier Sun";
	}

	private static string Capitalize(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		return char.ToUpperInvariant(s[0]) + s.Substring(1);
	}
}
