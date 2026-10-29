using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using RDR2;

namespace NpcAiTalk;

public static class NemesisBridge
{
	private static Assembly _nemesisAsm;
	private static Type _nemesisModType;
	private static Type _nemesisRecordType;
	private static PropertyInfo _instanceProp;
	private static MethodInfo _exportContextMethod;
	private static MethodInfo _recordOutcomeMethod;
	private static MethodInfo _recordAtMethod;
	private static MethodInfo _whoIsMethod;
	private static int _lastLookupTick = -100000;

	public static bool IsNemesisModLoaded { get; private set; }

	private static object ResolveLiveInstance()
	{
		try
		{
			if (_nemesisModType == null && Environment.TickCount - _lastLookupTick > 3000)
			{
				_lastLookupTick = Environment.TickCount;
				_nemesisAsm = AppDomain.CurrentDomain.GetAssemblies()
					.FirstOrDefault(a => a.GetName().Name.Equals("NemesisMod", StringComparison.OrdinalIgnoreCase));

				if (_nemesisAsm != null)
				{
					_nemesisModType = _nemesisAsm.GetType("NemesisSystem.NemesisMod");
					_nemesisRecordType = _nemesisAsm.GetType("NemesisSystem.NemesisRecord");
					if (_nemesisModType != null)
					{
						_instanceProp = _nemesisModType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
						_exportContextMethod = _nemesisModType.GetMethod("ExportAiNpcContext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
						_recordOutcomeMethod = _nemesisModType.GetMethod("RecordAiDialogueOutcome", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
						_recordAtMethod = _nemesisModType.GetMethod("RecordAt", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
						_whoIsMethod = _nemesisModType.GetMethod("WhoIs", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					}
				}
			}

			if (_nemesisModType == null)
			{
				IsNemesisModLoaded = false;
				return null;
			}

			object inst = _instanceProp?.GetValue(null);
			if (inst == null && _nemesisRecordType != null)
			{
				FieldInfo clockField = _nemesisRecordType.GetField("GameClock", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
				if (clockField?.GetValue(null) is Delegate del && del.Target != null)
				{
					if (_nemesisModType.IsInstanceOfType(del.Target))
					{
						inst = del.Target;
					}
					else
					{
						FieldInfo capturedThis = del.Target.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
							.FirstOrDefault(f => _nemesisModType.IsAssignableFrom(f.FieldType));
						inst = capturedThis?.GetValue(del.Target);
					}
				}
			}

			IsNemesisModLoaded = inst != null;
			return inst;
		}
		catch
		{
			IsNemesisModLoaded = false;
			return null;
		}
	}

	public static NpcContextProfile BuildFullNpcContext(Ped npc, bool enableBridge, string baseScriptsDir)
	{
		// Always start with a complete, valid standalone profile
		NpcContextProfile profile = StandaloneWorld.BuildStandaloneProfile(npc);
		if (!enableBridge || npc == null || !npc.Exists())
		{
			return profile;
		}

		int handle = npc.Handle;
		object liveInstance = ResolveLiveInstance();

		if (liveInstance != null)
		{
			try
			{
				if (_exportContextMethod != null)
				{
					if (_exportContextMethod.Invoke(liveInstance, new object[] { handle }) is Dictionary<string, string> ctx)
					{
						ApplyExportedDictionary(profile, ctx);
						return profile;
					}
				}

				// Fallback reflection for older NemesisMod.dll builds without ExportAiNpcContext
				ApplyDirectReflection(profile, liveInstance, handle);
				return profile;
			}
			catch
			{
			}
		}

		// Tier 2 Fallback: Check on-disk enRAGE save files if present
		TryEnrichFromDiskFiles(profile, baseScriptsDir);
		return profile;
	}

	private static void ApplyExportedDictionary(NpcContextProfile p, Dictionary<string, string> ctx)
	{
		p.IsEnrageConnected = true;
		if (ctx.TryGetValue("player_name", out string pName) && !string.IsNullOrEmpty(pName)) p.PlayerName = pName;
		if (ctx.TryGetValue("town", out string town) && !string.IsNullOrEmpty(town)) p.Town = town;
		if (ctx.TryGetValue("npc_name", out string nName) && !string.IsNullOrEmpty(nName))
		{
			p.Name = nName;
			p.Memory.Name = nName;
		}
		if (ctx.TryGetValue("npc_title", out string title)) p.Title = title;
		if (ctx.TryGetValue("npc_role", out string role) && !string.IsNullOrEmpty(role))
		{
			p.Role = role;
			p.Memory.Role = role;
		}
		if (ctx.TryGetValue("npc_faction", out string fac) && !string.IsNullOrEmpty(fac))
		{
			p.Faction = fac;
			p.Memory.Faction = fac;
		}
		if (ctx.TryGetValue("npc_faction_id", out string fid)) p.FactionId = fid;
		if (ctx.TryGetValue("npc_personality", out string pers) && !string.IsNullOrEmpty(pers))
		{
			p.Personality = pers;
			p.Memory.Personality = pers;
		}
		if (ctx.TryGetValue("is_nemesis", out string isNem) && isNem == "true")
		{
			p.IsNemesis = true;
			if (ctx.TryGetValue("nemesis_id", out string nid) && !string.IsNullOrEmpty(nid))
			{
				p.NpcKey = "nem_" + nid;
			}
			if (ctx.TryGetValue("npc_rank", out string rStr) && int.TryParse(rStr, out int rVal)) p.Rank = rVal;
			if (ctx.TryGetValue("npc_hate", out string hStr) && int.TryParse(hStr, out int hVal)) p.Hate = hVal;
			if (ctx.TryGetValue("npc_fear", out string fStr) && int.TryParse(fStr, out int fVal)) p.Fear = fVal;
			if (ctx.TryGetValue("npc_grudge", out string grd)) p.GrudgeSummary = grd;
			if (ctx.TryGetValue("npc_scars", out string scr)) p.Scars = scr;
			if (ctx.TryGetValue("npc_quirks", out string qrk)) p.Quirks = qrk;
			if (ctx.TryGetValue("npc_backstory", out string bks) && !string.IsNullOrEmpty(bks)) p.Backstory = bks;
			if (ctx.TryGetValue("npc_boss", out string bss)) p.BossName = bss;
			if (ctx.TryGetValue("npc_history", out string hst)) p.HistorySummary = hst;
			if (ctx.TryGetValue("npc_limp", out string lmp)) p.HasLimp = lmp == "true";
		}

		if (ctx.TryGetValue("town_mayor", out string tMayor) && !string.IsNullOrEmpty(tMayor)) p.TownMayor = tMayor;
		if (ctx.TryGetValue("town_sheriff", out string tSher) && !string.IsNullOrEmpty(tSher)) p.TownSheriff = tSher;
		if (ctx.TryGetValue("town_detective", out string tDet) && !string.IsNullOrEmpty(tDet)) p.TownDetective = tDet;
		if (ctx.TryGetValue("town_challenger", out string tChal) && !string.IsNullOrEmpty(tChal)) p.TownChallenger = tChal;
		if (ctx.TryGetValue("town_regime", out string tReg) && !string.IsNullOrEmpty(tReg)) p.TownRegime = tReg;
		if (ctx.TryGetValue("town_corruption", out string tCor) && int.TryParse(tCor, out int cVal)) p.TownCorruption = cVal;
		if (ctx.TryGetValue("town_tax_rate", out string tTax) && int.TryParse(tTax, out int tVal)) p.TownTaxRate = tVal;
		if (ctx.TryGetValue("town_warrant", out string tWar) && !string.IsNullOrEmpty(tWar)) p.TownWarrant = tWar;
		if (ctx.TryGetValue("town_case_heat", out string tHeat) && int.TryParse(tHeat, out int chVal)) p.TownCaseHeat = chVal;
		if (ctx.TryGetValue("town_backed_faction", out string tBack)) p.TownBackedFaction = tBack;
		if (ctx.TryGetValue("active_bounty", out string bnty)) p.ActiveBounty = bnty;
		if (ctx.TryGetValue("court_prisoner", out string pris)) p.CourtPrisoner = pris;
	}

	private static void ApplyDirectReflection(NpcContextProfile p, object liveInstance, int handle)
	{
		p.IsEnrageConnected = true;
		object rec = _recordAtMethod?.Invoke(liveInstance, new object[] { handle });
		if (rec != null)
		{
			p.IsNemesis = true;
			Type rt = rec.GetType();
			string fullName = rt.GetProperty("FullName")?.GetValue(rec) as string;
			string name = rt.GetProperty("Name")?.GetValue(rec) as string;
			if (!string.IsNullOrEmpty(fullName ?? name)) p.Name = fullName ?? name;
			p.Title = (rt.GetProperty("Title")?.GetValue(rec) as string) ?? "";
			p.Rank = (int)(rt.GetProperty("Rank")?.GetValue(rec) ?? 1);
			p.Hate = (int)(rt.GetProperty("Hate")?.GetValue(rec) ?? 0);
			p.Fear = (int)(rt.GetProperty("Fear")?.GetValue(rec) ?? 0);
			p.GrudgeSummary = (rt.GetProperty("GrudgeSummary")?.GetValue(rec) as string) ?? "";
			string gangId = rt.GetProperty("Gang")?.GetValue(rec) as string;
			if (!string.IsNullOrEmpty(gangId))
			{
				p.FactionId = gangId;
				p.Faction = gangId;
			}
			object persObj = rt.GetProperty("Personality")?.GetValue(rec);
			if (persObj != null) p.Personality = persObj.ToString();
		}
		else if (_whoIsMethod != null)
		{
			string who = _whoIsMethod.Invoke(liveInstance, new object[] { handle }) as string;
			if (!string.IsNullOrEmpty(who) && who != "a stranger")
			{
				p.Name = who.Replace(" (stranger)", "");
			}
		}
	}

	private static void TryEnrichFromDiskFiles(NpcContextProfile p, string baseScriptsDir)
	{
		try
		{
			string[] civicCandidates = new[]
			{
				Path.Combine(baseScriptsDir, "enRAGE", "civic_state.json"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Rockstar Games", "Red Dead Redemption 2", "enRAGE", "civic_state.json"),
				Path.Combine(baseScriptsDir, "..", "config_templates", "politics.json")
			};

			foreach (string path in civicCandidates)
			{
				if (File.Exists(path))
				{
					JObject root = JObject.Parse(File.ReadAllText(path));
					JToken townTok = root["Towns"]?[p.Town];
					if (townTok != null)
					{
						p.TownMayor = townTok["MayorName"]?.ToString() ?? p.TownMayor;
						p.TownSheriff = townTok["SheriffName"]?.ToString() ?? p.TownSheriff;
						p.TownDetective = townTok["DetectiveName"]?.ToString() ?? p.TownDetective;
						p.TownRegime = townTok["Regime"]?.ToString() ?? p.TownRegime;
						p.TownCorruption = townTok["Corruption"]?.Value<int>() ?? p.TownCorruption;
						p.TownTaxRate = townTok["TaxRatePct"]?.Value<int>() ?? p.TownTaxRate;
						p.TownWarrant = townTok["Warrant"]?.ToString() ?? p.TownWarrant;
					}
					break;
				}
			}
		}
		catch
		{
		}
	}

	public static void SyncOutcomeToNemesisMod(int pedHandle, string playerLine, string npcReply, int hateDelta, int fearDelta, bool revealedCamp)
	{
		try
		{
			object inst = ResolveLiveInstance();
			if (inst != null && _recordOutcomeMethod != null)
			{
				_recordOutcomeMethod.Invoke(inst, new object[] { pedHandle, playerLine, npcReply, hateDelta, fearDelta, revealedCamp });
			}
		}
		catch
		{
		}
	}
}
