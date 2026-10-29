using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NpcAiTalk;

public sealed class AiReplyResult
{
	public string SpokenText { get; set; } = "";
	public string ActionTag { get; set; } = "NONE";
	public int HateDelta { get; set; } = 0;
	public int FearDelta { get; set; } = 0;
	public bool RevealedCampIntel { get; set; } = false;
	public bool FromNeuralModel { get; set; } = false;
}

public static class AiDialogueClient
{
	private static readonly Regex ActionTagRegex = new Regex(@"\[ACTION\s*:\s*([A-Z_]+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static readonly Regex ThinkBlockRegex = new Regex(@"<think>.*?(?:</think>|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
	private static readonly Random Rng = new Random();

	public static async Task<AiReplyResult> GenerateReplyAsync(
		NpcContextProfile ctx,
		string playerSpeech,
		string intentKind,
		AiServerProcess server,
		NpcAiSettings settings)
	{
		if (server != null && server.IsServerReady)
		{
			try
			{
				AiReplyResult neural = await CallOpenAiCompatibleServerAsync(ctx, playerSpeech, intentKind, server.ActivePort, settings);
				if (neural != null && !string.IsNullOrWhiteSpace(neural.SpokenText))
				{
					ApplyIntentDeltas(neural, ctx, intentKind);
					RecordTurnInMemory(ctx, playerSpeech, neural);
					return neural;
				}
			}
			catch
			{
			}
		}

		// Instant Contextual 1899 Frontier Engine (used while model downloads/loads or if offline)
		AiReplyResult contextual = GenerateContextual1899Reply(ctx, playerSpeech, intentKind);
		ApplyIntentDeltas(contextual, ctx, intentKind);
		RecordTurnInMemory(ctx, playerSpeech, contextual);
		return contextual;
	}

	private static async Task<AiReplyResult> CallOpenAiCompatibleServerAsync(
		NpcContextProfile ctx,
		string playerSpeech,
		string intentKind,
		int port,
		NpcAiSettings settings)
	{
		string systemPrompt = WildWestPrompt.BuildSystemPrompt(ctx);
		List<object> messages = new List<object>
		{
			new { role = "system", content = systemPrompt }
		};

		if (ctx.Memory?.Turns != null && ctx.Memory.Turns.Count > 0)
		{
			foreach (NpcTurnRecord turn in ctx.Memory.Turns.Skip(Math.Max(0, ctx.Memory.Turns.Count - 3)))
			{
				messages.Add(new { role = "user", content = turn.PlayerText });
				messages.Add(new { role = "assistant", content = turn.NpcReply });
			}
		}

		messages.Add(new { role = "user", content = playerSpeech });

		var payload = new
		{
			model = settings.ModelPreset,
			messages = messages,
			max_tokens = settings.MaxTokens,
			temperature = settings.Temperature,
			chat_template_kwargs = new { enable_thinking = false },
			stop = new[] { "<|im_end|>", "<|eot_id|>", "\n\n", "Arthur:", "John:", "Player:" }
		};

		string jsonBody = JsonConvert.SerializeObject(payload);
		using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(settings.RequestTimeoutMs) })
		{
			using (StringContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
			{
				HttpResponseMessage res = await client.PostAsync($"http://127.0.0.1:{port}/v1/chat/completions", content);
				if (!res.IsSuccessStatusCode)
				{
					return null;
				}

				string rawJson = await res.Content.ReadAsStringAsync();
				JObject parsed = JObject.Parse(rawJson);
				string rawText = parsed["choices"]?[0]?["message"]?["content"]?.ToString();
				if (string.IsNullOrWhiteSpace(rawText))
				{
					return null;
				}

				return CleanAndParseReply(rawText, fromNeural: true);
			}
		}
	}

	private static AiReplyResult CleanAndParseReply(string rawText, bool fromNeural)
	{
		string cleaned = ThinkBlockRegex.Replace(rawText, "").Trim();
		string actionTag = "NONE";

		Match m = ActionTagRegex.Match(cleaned);
		if (m.Success)
		{
			actionTag = m.Groups[1].Value.ToUpperInvariant();
			cleaned = ActionTagRegex.Replace(cleaned, "").Trim();
		}

		cleaned = cleaned.Trim('"', '\'', ' ', '\r', '\n');
		int colonIdx = cleaned.IndexOf(':');
		if (colonIdx > 0 && colonIdx < 24 && !cleaned.Substring(0, colonIdx).Contains(" "))
		{
			cleaned = cleaned.Substring(colonIdx + 1).Trim('"', ' ');
		}

		return new AiReplyResult
		{
			SpokenText = cleaned,
			ActionTag = actionTag,
			FromNeuralModel = fromNeural
		};
	}

	private static AiReplyResult GenerateContextual1899Reply(NpcContextProfile c, string playerSpeech, string intentKind)
	{
		string lower = (playerSpeech ?? "").ToLowerInvariant();
		bool asksPolitics = intentKind == "politics" || lower.Contains("mayor") || lower.Contains("sheriff") || lower.Contains("tax") || lower.Contains("election") || lower.Contains("law");
		bool asksIntel = intentKind == "intel" || lower.Contains("camp") || lower.Contains("hideout") || lower.Contains("boss") || lower.Contains("bounty") || lower.Contains("jail") || lower.Contains("gang");
		bool isThreat = intentKind == "threaten" || lower.Contains("kill") || lower.Contains("shoot") || lower.Contains("dead") || lower.Contains("pine box") || lower.Contains("die");
		bool isBribe = intentKind == "bribe" || lower.Contains("dollar") || lower.Contains("money") || lower.Contains("peace");

		string action = "NONE";
		string text;

		if (isBribe)
		{
			action = "CALM";
			if (c.IsNemesis && c.Hate >= 75 && c.Personality != "Greedy")
			{
				text = $"Keep your blood money, {c.PlayerName}! After {(!string.IsNullOrEmpty(c.GrudgeSummary) ? c.GrudgeSummary : "what you did")}, a few crumpled bills won't square our ledger.";
			}
			else if (!string.IsNullOrEmpty(c.ActiveBounty))
			{
				action = "INTEL";
				text = $"Well now, cold cash talks loud in {c.Town}. Word is {c.ActiveBounty} — and {c.TownSheriff}'s deputies have been looking the other way.";
			}
			else
			{
				action = "INTEL";
				text = $"Much obliged, {c.PlayerName}. Between us, {c.TownMayor}'s administration has {c.TownCorruption}% corruption at Town Hall, and {(!string.IsNullOrEmpty(c.BossName) ? c.BossName : "the local outlaws")} have been moving supplies after dark.";
			}
		}
		else if (isThreat)
		{
			if (c.Fear >= 55 || c.Personality == "Coward" || c.IsPlayerAimingAtNpc)
			{
				action = (c.Fear >= 70 || c.IsPlayerAimingAtNpc) ? "SURRENDER" : "FLEE";
				text = $"Easy, {c.PlayerName}, easy! Put that iron down! I don't want no smoke with you — {(!string.IsNullOrEmpty(c.BossName) ? c.BossName + " rides out near the ridge!" : $"I'm just trying to get by in {c.Town}!")}";
			}
			else if (c.IsNemesis || c.Personality == "Hothead" || c.Hate >= 50)
			{
				action = "ATTACK";
				text = $"You think you can bark orders at me, {c.PlayerName}?! {(c.HasLimp || !string.IsNullOrEmpty(c.Scars) ? "I still carry the scars from last time — " : "")}Let's settle this right here in the dirt!";
			}
			else
			{
				text = $"Careful how you throw threats around {c.Town}, mister. {c.TownSheriff} and {c.TownDetective} have men watching every street corner.";
			}
		}
		else if (asksPolitics)
		{
			if (c.TownRegime == "MartialLaw")
			{
				text = $"Keep your voice down in {c.Town}! {c.TownMayor} and {c.TownSheriff} declared Martial Law — anyone caught armed or masked after ten o'clock gets shot on sight.";
			}
			else if (c.TownRegime == "CorruptMachine" || c.TownCorruption >= 55)
			{
				text = $"{c.TownMayor} runs {c.Town} like a private bank with a {c.TownTaxRate}% tax rate. Everyone knows Town Hall is {c.TownCorruption}% corrupt, and {c.TownChallenger} is gunning for the next election.";
			}
			else
			{
				text = $"{c.TownMayor} ({c.TownRegime}) has {c.Town} paying {c.TownTaxRate}% tax while {c.TownSheriff} and {c.TownDetective} keep the peace. Folks say {c.TownChallenger} might shake things up come election day.";
			}
		}
		else if (asksIntel)
		{
			if (c.IsNemesis)
			{
				if (c.Fear >= 50 || c.Memory.Bribed || c.Memory.Disposition > 20)
				{
					action = "INTEL";
					text = $"Don't tell {(!string.IsNullOrEmpty(c.BossName) ? c.BossName : "the boys")} you heard it from me... the {c.Faction} have a camp stocked with crates just outside {c.Town}.";
				}
				else
				{
					text = $"You think I'd rat out the {c.Faction} to you, {c.PlayerName}? {(!string.IsNullOrEmpty(c.BossName) ? c.BossName : "Our captain")} hangs informers from the tallest oak.";
				}
			}
			else
			{
				string bInfo = !string.IsNullOrEmpty(c.ActiveBounty) ? $"There's a notice up for {c.ActiveBounty}." : $"The {c.Town} bounty board has fresh outlaw contracts.";
				string pInfo = !string.IsNullOrEmpty(c.CourtPrisoner) ? $" And {c.CourtPrisoner}." : "";
				text = $"{bInfo}{pInfo} You can check the full docket at the {c.Town} Telegraph Office.";
			}
		}
		else
		{
			// Greeting or custom open-ended input
			if (c.IsNemesis)
			{
				if (!string.IsNullOrEmpty(c.GrudgeSummary))
				{
					text = $"I haven't forgotten how {c.GrudgeSummary}, {c.PlayerName}. {(c.HasLimp ? "My leg still aches every time it rains." : "You got a lot of nerve walking up to me in " + c.Town + ".")}";
				}
				else
				{
					text = $"Well if it ain't {c.PlayerName}. The {c.Faction} know your face from here to {c.Town}. Speak your piece.";
				}
			}
			else
			{
				string[] greetings = new[]
				{
					$"Afternoon, {c.PlayerName}. It's {c.Weather.ToLowerInvariant()} here in {c.Town}, and {c.TownSheriff}'s deputies have been patrolling since dawn.",
					$"Howdy, mister. Name's {c.Name} — {c.Role} here in {c.Town}. Long as you keep out of {c.TownSheriff}'s way, we'll get along fine.",
					$"Evenin', partner. Folks around {c.Town} are mostly talking about {c.TownMayor}'s {c.TownTaxRate}% tax and the outlaws riding through {c.District}."
				};
				text = greetings[Rng.Next(greetings.Length)];
			}
		}

		return new AiReplyResult
		{
			SpokenText = text,
			ActionTag = action,
			FromNeuralModel = false
		};
	}

	private static void ApplyIntentDeltas(AiReplyResult res, NpcContextProfile ctx, string intentKind)
	{
		switch (intentKind)
		{
		case "threaten":
			res.HateDelta += 12;
			res.FearDelta += ctx.IsPlayerAimingAtNpc ? 22 : 14;
			ctx.Memory.Disposition = Math.Max(-100, ctx.Memory.Disposition - 18);
			break;
		case "bribe":
			res.HateDelta -= 15;
			res.FearDelta -= 5;
			ctx.Memory.Bribed = true;
			ctx.Memory.Disposition = Math.Min(100, ctx.Memory.Disposition + 25);
			break;
		case "greet":
			ctx.Memory.Disposition = Math.Min(100, ctx.Memory.Disposition + 5);
			break;
		}

		if (res.ActionTag == "INTEL")
		{
			res.RevealedCampIntel = true;
			ctx.Memory.IntelShared = true;
		}
		else if (res.ActionTag == "ATTACK")
		{
			res.HateDelta = Math.Max(res.HateDelta, 20);
		}
		else if (res.ActionTag == "SURRENDER" || res.ActionTag == "FLEE")
		{
			res.FearDelta = Math.Max(res.FearDelta, 25);
		}
	}

	private static void RecordTurnInMemory(NpcContextProfile ctx, string playerSpeech, AiReplyResult res)
	{
		if (ctx?.Memory == null)
		{
			return;
		}
		ctx.Memory.TimesSpoken++;
		if (ctx.Memory.Turns == null)
		{
			ctx.Memory.Turns = new List<NpcTurnRecord>();
		}
		ctx.Memory.Turns.Add(new NpcTurnRecord
		{
			PlayerText = playerSpeech,
			NpcReply = res.SpokenText,
			ActionTag = res.ActionTag,
			Timestamp = DateTime.Now
		});
		if (ctx.Memory.Turns.Count > 8)
		{
			ctx.Memory.Turns.RemoveAt(0);
		}
	}
}
