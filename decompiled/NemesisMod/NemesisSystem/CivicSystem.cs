using System;
using System.Collections.Generic;

namespace NemesisSystem;

public enum PoliticalRegime
{
	Reformist,
	CorruptMachine,
	FrontierPopulist,
	MartialLaw
}

public enum WarrantTier
{
	Clear,
	Misdemeanor,
	Felony,
	DeadOrAlive
}

public enum BusinessKind
{
	Saloon,
	Gunsmith,
	GeneralStore,
	Doctor,
	Hotel
}

public enum BusinessControl
{
	Independent,
	GangExtorted,
	PlayerProtected,
	PlayerPartner,
	PlayerRacket
}

public sealed class TownBusiness
{
	public string Id { get; set; }

	public string Town { get; set; }

	public string Name { get; set; }

	public BusinessKind Kind { get; set; }

	public BusinessControl Control { get; set; }

	public string ExtortingGang { get; set; }

	public int DailyIncome { get; set; } = 15;

	public int PartnerCost { get; set; } = 120;

	public int Level { get; set; } = 1;

	public bool HasMoonshineStill { get; set; }
}

public sealed class TownPoliticState
{
	public string Town { get; set; }

	public string DistrictKey { get; set; }

	public string MayorName { get; set; }

	public string SheriffName { get; set; }

	public string ChallengerName { get; set; }

	public string DetectiveName { get; set; }

	public PoliticalRegime Regime { get; set; }

	public PoliticalRegime ChallengerRegime { get; set; }

	public int Approval { get; set; } = 55;

	public int Corruption { get; set; } = 25;

	public int ChallengerSupport { get; set; } = 45;

	public int TaxRatePct { get; set; } = 6;

	public string BackedFaction { get; set; }

	public int ElectionDay { get; set; }

	public int MartialLawUntilDay { get; set; }

	public long SheriffBribedUntilMin { get; set; }

	public int CaseHeat { get; set; }

	public WarrantTier Warrant { get; set; }

	public string CustomHeadline { get; set; }
}

public sealed class PrisonerRecord
{
	public string Id { get; set; }

	public string Name { get; set; }

	public string Gang { get; set; }

	public int Rank { get; set; } = 2;

	public string Town { get; set; }

	public string NemesisId { get; set; }

	public bool IsCompanion { get; set; }

	public int BailDollars { get; set; } = 65;

	public int ExecutionDay { get; set; }

	public string Sentence { get; set; } = "Public Hanging";
}

public sealed class BountyContract
{
	public string Id { get; set; }

	public string TargetName { get; set; }

	public string Gang { get; set; }

	public int Rank { get; set; } = 3;

	public string Town { get; set; }

	public string NemesisId { get; set; }

	public int AliveReward { get; set; } = 120;

	public int DeadReward { get; set; } = 65;

	public string Crime { get; set; } = "Armed Stagecoach Robbery & Murder";
}

public sealed class CivicState
{
	public string About { get; set; } = "Real-world Police, Politics, Martial Law, Courts, Bounty Guild, Payroll Heists, Press Propaganda, Saloon Fight Clubs & Business Economy state (CivicSystem.cs / NemesisMod.Civic.cs). Press F8 anywhere or use any Telegraph Office to open the Civic & Politics Desk.";

	public int Version { get; set; } = 2;

	public int BankBalanceDollars { get; set; }

	public int BankLoanDollars { get; set; }

	public int LastDailyTickDay { get; set; }

	public int TotalTaxesCollected { get; set; }

	public int TotalBusinessIncome { get; set; }

	public int TotalBribesPaid { get; set; }

	public int BountiesCapturedAlive { get; set; }

	public int BountiesKilled { get; set; }

	public int PayrollHeistsCompleted { get; set; }

	public int PrizeFightsWon { get; set; }

	public Dictionary<string, TownPoliticState> Towns { get; set; } = new Dictionary<string, TownPoliticState>(StringComparer.OrdinalIgnoreCase);

	public List<TownBusiness> Businesses { get; set; } = new List<TownBusiness>();

	public List<PrisonerRecord> Prisoners { get; set; } = new List<PrisonerRecord>();

	public List<BountyContract> Bounties { get; set; } = new List<BountyContract>();
}
