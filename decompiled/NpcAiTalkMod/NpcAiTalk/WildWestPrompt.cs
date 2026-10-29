using System;
using System.Collections.Generic;
using System.Text;

namespace NpcAiTalk;

public sealed class DialogueOptionItem
{
	public string Key { get; set; } = "";
	public string Label { get; set; } = "";
	public string PlayerSpokenLine { get; set; } = "";
	public string IntentKind { get; set; } = "chat";
	public bool Enabled { get; set; } = true;
}

public static class WildWestPrompt
{
	public static string BuildSystemPrompt(NpcContextProfile c)
	{
		StringBuilder sb = new StringBuilder(1024);
		sb.AppendLine("You are a living character in Red Dead Redemption 2 (American Frontier, 1899). Stay 100% in-character.");
		sb.AppendLine($"Your Name: {c.Name} | Role: {c.Role} | Faction: {c.Faction} | Temperament: {c.Personality}");
		if (!string.IsNullOrEmpty(c.Backstory))
		{
			sb.AppendLine($"Background: {c.Backstory}");
		}
		if (c.IsNemesis)
		{
			sb.AppendLine($"Nemesis Status: Rank {c.Rank} in {c.Faction}. Hate toward {c.PlayerName}: {c.Hate}/100. Fear of {c.PlayerName}: {c.Fear}/100.");
			if (!string.IsNullOrEmpty(c.GrudgeSummary))
			{
				sb.AppendLine($"Personal Grudge: {c.GrudgeSummary}.");
			}
			if (!string.IsNullOrEmpty(c.BossName))
			{
				sb.AppendLine($"Your Gang Boss/Leader: {c.BossName}.");
			}
			if (!string.IsNullOrEmpty(c.Scars) || c.HasLimp)
			{
				sb.AppendLine($"Wounds/Scars from {c.PlayerName}: {c.Scars}{(c.HasLimp ? " (walks with a permanent limp)" : "")}.");
			}
			if (!string.IsNullOrEmpty(c.HistorySummary))
			{
				sb.AppendLine($"Past History with {c.PlayerName}: {c.HistorySummary}");
			}
		}

		sb.AppendLine($"Location: {c.Town} ({c.District}) | Time: {c.TimeOfDay} | Weather: {c.Weather}");
		sb.AppendLine($"Town Politics ({c.Town}): Mayor is {c.TownMayor} ({c.TownRegime} regime, Tax {c.TownTaxRate}%, Corruption {c.TownCorruption}%). Sheriff is {c.TownSheriff}. Detective is {c.TownDetective}.");
		if (!string.IsNullOrEmpty(c.ActiveBounty))
		{
			sb.AppendLine($"Local Wanted Bounty: {c.ActiveBounty}.");
		}
		if (!string.IsNullOrEmpty(c.CourtPrisoner))
		{
			sb.AppendLine($"County Jail Docket: {c.CourtPrisoner}.");
		}

		sb.AppendLine($"Speaking To: {c.PlayerName} (Honor: {c.PlayerHonor}, Local Warrant: {c.TownWarrant}, Weapon Status: {(c.IsPlayerAimingAtNpc ? "GUN AIMED AT YOUR HEAD" : c.IsPlayerArmed ? "Weapon drawn in hand" : "Holstered")}).");
		sb.AppendLine($"Your Current Disposition Toward {c.PlayerName}: {c.Memory.Disposition} (-100 hostile to +100 friendly).");

		sb.AppendLine("Rules:");
		sb.AppendLine("1. Reply ONLY with your spoken words in authentic 1899 Wild West vernacular (1 to 2 sentences, max 32 words).");
		sb.AppendLine("2. Never mention AI, video games, or modern concepts. Reference your town, mayor, sheriff, gang boss, or scars naturally when relevant.");
		sb.AppendLine("3. At the very end of your reply, append ONE tag: [ACTION:NONE], [ACTION:ATTACK], [ACTION:SURRENDER], [ACTION:FLEE], [ACTION:INTEL], or [ACTION:CALM].");
		return sb.ToString();
	}

	public static List<DialogueOptionItem> BuildDialogueChoices(NpcContextProfile c, int bribeDollars)
	{
		List<DialogueOptionItem> list = new List<DialogueOptionItem>
		{
			new DialogueOptionItem
			{
				Key = "custom",
				Label = "Type Custom Words (On-Screen Keyboard)...",
				PlayerSpokenLine = "",
				IntentKind = "custom"
			},
			new DialogueOptionItem
			{
				Key = "greet",
				Label = c.IsNemesis
					? $"Acknowledge {c.Name} (\"Well now, look who's still breathin'.\")"
					: $"Greet & Make Small Talk (\"Howdy. Fine day in {c.Town}, ain't it?\")",
				PlayerSpokenLine = c.IsNemesis
					? $"Well now, {c.Name}. Look who's still breathin'. You got somethin' to say to me?"
					: $"Howdy. How are things treating you around {c.Town} today?",
				IntentKind = "greet"
			},
			new DialogueOptionItem
			{
				Key = "politics",
				Label = $"Ask About {c.Town} Politics ({c.TownMayor} & {c.TownSheriff})",
				PlayerSpokenLine = $"What do folks around {c.Town} make of {c.TownMayor} and {c.TownSheriff} running things lately?",
				IntentKind = "politics"
			}
		};

		if (c.IsNemesis || (!string.IsNullOrEmpty(c.FactionId) && c.FactionId != "Independent"))
		{
			list.Add(new DialogueOptionItem
			{
				Key = "intel",
				Label = $"Press for {c.Faction} Intel & Hideouts",
				PlayerSpokenLine = $"Start talking, {c.Name}. Where are the rest of the {c.Faction} holed up, and what is {(!string.IsNullOrEmpty(c.BossName) ? c.BossName : "your boss")} planning?",
				IntentKind = "intel"
			});
		}
		else
		{
			list.Add(new DialogueOptionItem
			{
				Key = "rumors",
				Label = $"Ask About Outlaw Gangs, Bounties & Court Hangings",
				PlayerSpokenLine = $"Heard any word about outlaw gangs, wanted bounties, or prisoners in the {c.Town} jail?",
				IntentKind = "intel"
			});
		}

		list.Add(new DialogueOptionItem
		{
			Key = "threaten",
			Label = $"Threaten / Intimidate {c.Name}",
			PlayerSpokenLine = $"You best watch your tongue around me, {c.Name}, or they'll be measuring you for a pine box before sundown.",
			IntentKind = "threaten"
		});

		bool canAffordBribe = c.PlayerCashDollars >= bribeDollars;
		list.Add(new DialogueOptionItem
		{
			Key = "bribe",
			Label = $"Offer ${bribeDollars} to Make Peace / Buy Information{(canAffordBribe ? "" : " (Not Enough Cash)")}",
			PlayerSpokenLine = $"Take this ${bribeDollars}, friend. Let's keep things peaceful between us and tell me what I ought to know.",
			IntentKind = "bribe",
			Enabled = canAffordBribe
		});

		list.Add(new DialogueOptionItem
		{
			Key = "leave",
			Label = "Step Back & End Conversation [Esc]",
			PlayerSpokenLine = "I'll be moving along now.",
			IntentKind = "leave"
		});

		return list;
	}
}
