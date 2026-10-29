using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using RDR2;
using RDR2.Math;
using RDR2.Native;
using RDR2.UI;

namespace NpcAiTalk;

public class NpcAiTalkScript : Script
{
	private readonly string _scriptsDir;
	private readonly string _modDataDir;
	private readonly NpcAiSettings _settings;
	private readonly AutoBootstrapper _bootstrapper;
	private readonly AiServerProcess _serverProcess;

	private Ped _hoveredPed;
	private NpcContextProfile _hoveredPreview;
	private int _nextHoverScanAt;
	private int _lastPromptPedHandle;
	private int _promptVisibleUntilTime;
	private readonly Dictionary<int, int> _pedPromptCooldownUntil = new Dictionary<int, int>();

	// Active Conversation State
	private bool _convoOpen;
	private Ped _convoPed;
	private NpcContextProfile _convoCtx;
	private List<DialogueOptionItem> _options = new List<DialogueOptionItem>();
	private int _selectedIndex;
	private string _lastPlayerLine = "";
	private string _currentNpcLine = "";
	private string _lastActionTag = "NONE";
	private bool _isGeneratingReply;

	// Non-blocking On-Screen Keyboard State
	private bool _keyboardActive;
	private int _keyboardStartedAt;

	// F9 Status Overlay State
	private bool _statusMenuOpen;
	private int _statusMenuSel;

	// Toast / Notification Banner
	private string _toastText = "";
	private int _toastUntilTick;
	private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _mainThreadQueue = new System.Collections.Concurrent.ConcurrentQueue<Action>();

	private static readonly string[] AvailablePresets = new[]
	{
		"LFM3-1.7B-Uncensored",
		"LFM3-1.7B-Uncensored-Q6",
		"Qwen2.5-0.5B",
		"SmolLM2-360M",
		"Llama-3.2-1B",
		"Qwen2.5-1.5B"
	};

	public NpcAiTalkScript()
	{
		_scriptsDir = !string.IsNullOrEmpty(BaseDirectory)
			? BaseDirectory.TrimEnd('\\', '/')
			: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts");

		_modDataDir = Path.Combine(_scriptsDir, "NpcAiTalk");
		string primaryIni = Path.Combine(_scriptsDir, "NpcAiTalk.ini");

		_settings = new NpcAiSettings(primaryIni);
		_bootstrapper = new AutoBootstrapper(_modDataDir, _settings);
		_serverProcess = new AiServerProcess(_bootstrapper, _settings);

		StandaloneWorld.InitMemoryStore(Path.Combine(_modDataDir, "memory", "npc_memories.json"));

		Tick += OnTick;
		KeyDown += OnKeyDown;
		Aborted += OnAborted;

		Task.Run(InitializeAiBackendAsync);
	}

	private async Task InitializeAiBackendAsync()
	{
		try
		{
			_bootstrapper.Log("NpcAiTalk v1.0.0 starting background initialization...");
			await _bootstrapper.EnsureAllInstalledAsync();
			bool ready = await _serverProcess.StartOrAttachAsync();
			if (ready)
			{
				ShowToast($"AI Dialogue Ready ({_settings.ModelPreset})! Press [{_settings.TalkKey}] near any NPC.", 5000);
			}
			else
			{
				ShowToast($"AI Talk Ready (1899 Engine Active). Press [{_settings.TalkKey}] near any NPC.", 4500);
			}
		}
		catch (Exception ex)
		{
			_bootstrapper.Log("InitializeAiBackendAsync error: " + ex.Message);
		}
	}

	private void OnAborted(object sender, EventArgs e)
	{
		try
		{
			StandaloneWorld.SaveMemoryStore();
			_serverProcess?.Dispose();
		}
		catch
		{
		}
	}

	private void ShowToast(string text, int durationMs = 4000)
	{
		_toastText = text;
		_toastUntilTick = Environment.TickCount + durationMs;
	}

	private void OnTick(object sender, EventArgs e)
	{
		try
		{
			while (_mainThreadQueue.TryDequeue(out var pendingAction))
			{
				try { pendingAction(); } catch { }
			}

			Ped player = Game.Player?.Character;
			if (player == null || !player.Exists() || player.IsDead || DLC.GET_IS_LOADING_SCREEN_ACTIVE())
			{
				if (_convoOpen)
				{
					CloseConversation(releasePed: true);
				}
				return;
			}

			// 1. Render First-Run Auto-Download Progress Banner if active
			if (_bootstrapper.IsRunningSetup && !string.IsNullOrEmpty(_bootstrapper.StatusBanner))
			{
				DrawBootstrapProgressHud();
			}
			else if (!string.IsNullOrEmpty(_toastText) && Environment.TickCount < _toastUntilTick)
			{
				DrawToastBanner(_toastText);
			}

			// 2. Poll Non-Blocking On-Screen Keyboard if active
			if (_keyboardActive)
			{
				PollOnScreenKeyboard();
				return;
			}

			// 3. Render F9 Status & Settings Panel if open
			if (_statusMenuOpen)
			{
				DrawStatusMenuOverlay();
				return;
			}

			// 4. Handle Active Conversation Overlay
			if (_convoOpen)
			{
				if (_convoPed == null || !_convoPed.Exists() || _convoPed.IsDead || _convoPed.Position.DistanceTo(player.Position) > _settings.MaxTalkDistance + 6f)
				{
					CloseConversation(releasePed: true);
					return;
				}
				DrawConversationOverlay();
				return;
			}

			// 5. Scan for Targeted or Nearby NPC for Contextual Prompt
			if (Game.GameTime >= _nextHoverScanAt)
			{
				_nextHoverScanAt = Game.GameTime + 180;
				PrunePromptCooldowns(player);
				_hoveredPed = FindCandidateNpc(player, out bool isDirectTarget);
				if (_hoveredPed != null)
				{
					int h = _hoveredPed.Handle;
					if (_hoveredPreview == null || _hoveredPreview.PedHandle != h)
					{
						_hoveredPreview = NemesisBridge.BuildFullNpcContext(_hoveredPed, _settings.EnableNemesisModBridge, _scriptsDir);
					}

					int durationMs = (int)(_settings.PromptAutoHideSeconds * 1000f);
					if (h != _lastPromptPedHandle)
					{
						_lastPromptPedHandle = h;
						bool onCooldown = _pedPromptCooldownUntil.TryGetValue(h, out int cdUntil) && Game.GameTime < cdUntil;
						if (!onCooldown || isDirectTarget)
						{
							_promptVisibleUntilTime = Game.GameTime + durationMs;
							_pedPromptCooldownUntil[h] = Game.GameTime + 60000;
						}
						else
						{
							_promptVisibleUntilTime = 0;
						}
					}
					else
					{
						// Keep refreshing cooldown while remaining near the same NPC so the prompt stays hidden after timing out
						_pedPromptCooldownUntil[h] = Game.GameTime + 60000;
					}
				}
				else
				{
					_hoveredPreview = null;
					_lastPromptPedHandle = 0;
					_promptVisibleUntilTime = 0;
				}
			}

			if (_hoveredPed != null && _hoveredPreview != null && _settings.ShowTargetPrompt && Game.GameTime < _promptVisibleUntilTime)
			{
				DrawTargetPromptBadge(_hoveredPreview);
			}
		}
		catch
		{
		}
	}

	private void PrunePromptCooldowns(Ped player)
	{
		if (_pedPromptCooldownUntil.Count == 0)
		{
			return;
		}
		float resetDist = _settings.MaxTalkDistance + 5.0f;
		Vector3 pPos = player.Position;
		List<int> toRemove = null;
		foreach (KeyValuePair<int, int> kv in _pedPromptCooldownUntil)
		{
			if (Game.GameTime >= kv.Value)
			{
				(toRemove ?? (toRemove = new List<int>())).Add(kv.Key);
				continue;
			}
			if (!(Entity.FromHandle(kv.Key) is Ped ped) || !ped.Exists() || ped.Position.DistanceTo(pPos) > resetDist)
			{
				(toRemove ?? (toRemove = new List<int>())).Add(kv.Key);
			}
		}
		if (toRemove != null)
		{
			foreach (int k in toRemove)
			{
				_pedPromptCooldownUntil.Remove(k);
			}
		}
	}

	private unsafe Ped FindCandidateNpc(Ped player, out bool isDirectTarget)
	{
		isDirectTarget = false;
		int pHandle = Game.Player.Handle;
		int entityHandle = 0;

		// Priority 1: Free-aiming or hard-targeting an entity with clear line-of-sight
		if (PLAYER.GET_ENTITY_PLAYER_IS_FREE_AIMING_AT(pHandle, &entityHandle) && entityHandle != 0)
		{
			if (Entity.FromHandle(entityHandle) is Ped aimedPed && IsValidTalkPed(aimedPed, player, _settings.MaxTalkDistance + 4f))
			{
				isDirectTarget = true;
				return aimedPed;
			}
		}

		entityHandle = 0;
		if (PLAYER.GET_PLAYER_TARGET_ENTITY(pHandle, &entityHandle) && entityHandle != 0)
		{
			if (Entity.FromHandle(entityHandle) is Ped targetPed && IsValidTalkPed(targetPed, player, _settings.MaxTalkDistance + 3f))
			{
				isDirectTarget = true;
				return targetPed;
			}
		}

		entityHandle = 0;
		if (PLAYER.GET_PLAYER_INTERACTION_TARGET_ENTITY(pHandle, &entityHandle, p2: false, p3: false) && entityHandle != 0)
		{
			if (Entity.FromHandle(entityHandle) is Ped interPed && IsValidTalkPed(interPed, player, _settings.MaxTalkDistance + 2f))
			{
				isDirectTarget = true;
				return interPed;
			}
		}

		// Priority 2: Closest visible human NPC directly in front of the player & camera within close conversational range
		Ped[] allPeds = World.GetAllPeds();
		if (allPeds == null)
		{
			return null;
		}

		Ped best = null;
		float bestDist = Math.Min(_settings.MaxTalkDistance, 4.2f);
		Vector3 pPos = player.Position;
		Vector3 pFwd = player.ForwardVector;
		Vector3 camDir = GameplayCamera.Direction;

		foreach (Ped ped in allPeds)
		{
			if (!IsValidTalkPed(ped, player, bestDist))
			{
				continue;
			}
			Vector3 diff = ped.Position - pPos;
			float dist = diff.Length();
			if (dist < bestDist)
			{
				Vector3 dir = Vector3.Normalize(diff);
				float dotFwd = Vector3.Dot(pFwd, dir);
				float dotCam = camDir.Length() > 0.01f ? Vector3.Dot(camDir, dir) : dotFwd;
				if (dotFwd > 0.35f && dotCam > 0.50f)
				{
					bestDist = dist;
					best = ped;
				}
			}
		}

		return best;
	}

	private static bool IsValidTalkPed(Ped ped, Ped player, float maxDist)
	{
		if (ped == null || !ped.Exists() || ped.IsPlayer || ped.IsDead || !ped.IsHuman || ped.IsAnimal)
		{
			return false;
		}
		if (ped.Position.DistanceTo(player.Position) > maxDist)
		{
			return false;
		}
		try
		{
			if (!ENTITY.HAS_ENTITY_CLEAR_LOS_TO_ENTITY(player.Handle, ped.Handle, 17))
			{
				return false;
			}
		}
		catch
		{
		}
		return true;
	}

	private void OnKeyDown(object sender, KeyEventArgs e)
	{
		try
		{
			if (_keyboardActive)
			{
				return;
			}

			if (e.KeyCode == _settings.StatusMenuKey)
			{
				_statusMenuOpen = !_statusMenuOpen;
				return;
			}

			if (_statusMenuOpen)
			{
				HandleStatusMenuKeys(e.KeyCode);
				return;
			}

			if (_convoOpen)
			{
				HandleConversationKeys(e.KeyCode);
				return;
			}

			if (e.KeyCode == _settings.TalkKey && _hoveredPed != null && _hoveredPed.Exists())
			{
				OpenConversation(_hoveredPed);
			}
			else if (e.KeyCode == _settings.QuickGreetKey && _hoveredPed != null && _hoveredPed.Exists())
			{
				TriggerQuickGreet(_hoveredPed);
			}
			else if (e.KeyCode == _settings.CustomInputKey && _hoveredPed != null && _hoveredPed.Exists())
			{
				OpenConversation(_hoveredPed);
				OpenCustomKeyboardInput();
			}
		}
		catch
		{
		}
	}

	private void OpenConversation(Ped npc)
	{
		_convoPed = npc;
		_convoCtx = NemesisBridge.BuildFullNpcContext(npc, _settings.EnableNemesisModBridge, _scriptsDir);
		_options = WildWestPrompt.BuildDialogueChoices(_convoCtx, _settings.BribeCostDollars);
		_selectedIndex = 0;
		_lastPlayerLine = "";
		_lastActionTag = "NONE";
		_convoOpen = true;

		try
		{
			Ped player = Game.Player.Character;
			npc.BlockPermanentEvents = true;
			TASK.TASK_STAND_STILL(npc.Handle, 20000);
			TASK.TASK_TURN_PED_TO_FACE_ENTITY(npc.Handle, player.Handle, 2000, 0f, 0f, 0f);
			TASK.TASK_LOOK_AT_ENTITY(npc.Handle, player.Handle, 20000, 0, 0, 0);
		}
		catch
		{
		}

		if (_convoCtx.Memory.Turns.Count > 0)
		{
			NpcTurnRecord last = _convoCtx.Memory.Turns[_convoCtx.Memory.Turns.Count - 1];
			_currentNpcLine = last.NpcReply;
		}
		else if (_convoCtx.IsNemesis)
		{
			_currentNpcLine = !string.IsNullOrEmpty(_convoCtx.GrudgeSummary)
				? $"You got a lot of nerve showing your face to me in {_convoCtx.Town}, {_convoCtx.PlayerName} — not after {_convoCtx.GrudgeSummary}."
				: $"Well now, {_convoCtx.PlayerName}. The {_convoCtx.Faction} have been expecting you.";
		}
		else
		{
			_currentNpcLine = $"What can I do for you here in {_convoCtx.Town}, {_convoCtx.PlayerName}?";
		}

		if (_settings.EnableNativeSpeechAnim)
		{
			VoiceSynthesizer.PlayNativePedSpeech(npc, Game.Player.Character, "greet", "NONE");
		}
	}

	private void CloseConversation(bool releasePed)
	{
		if (releasePed && _convoPed != null && _convoPed.Exists() && !_convoPed.IsDead)
		{
			try
			{
				_convoPed.BlockPermanentEvents = false;
				TASK.TASK_CLEAR_LOOK_AT(_convoPed.Handle);
				if (_lastActionTag != "ATTACK" && _lastActionTag != "SURRENDER" && _lastActionTag != "FLEE")
				{
					TASK.CLEAR_PED_TASKS(_convoPed.Handle, p1: true, p2: false);
				}
			}
			catch
			{
			}
		}

		_convoOpen = false;
		_convoPed = null;
		_convoCtx = null;
		if (_settings.SaveNpcMemories)
		{
			StandaloneWorld.SaveMemoryStore();
		}
	}

	private void HandleConversationKeys(Keys key)
	{
		if (key == Keys.Escape || key == Keys.Back)
		{
			CloseConversation(releasePed: true);
			return;
		}

		if (_isGeneratingReply)
		{
			return;
		}

		if (key == Keys.Up || key == Keys.W)
		{
			_selectedIndex = (_selectedIndex - 1 + _options.Count) % _options.Count;
			return;
		}
		if (key == Keys.Down || key == Keys.S)
		{
			_selectedIndex = (_selectedIndex + 1) % _options.Count;
			return;
		}

		if (key == _settings.CustomInputKey)
		{
			OpenCustomKeyboardInput();
			return;
		}

		if (key >= Keys.D1 && key <= Keys.D7)
		{
			int idx = (int)(key - Keys.D1);
			if (idx >= 0 && idx < _options.Count)
			{
				_selectedIndex = idx;
				ExecuteSelectedOption(_options[idx]);
			}
			return;
		}

		if (key == Keys.Enter || key == Keys.Space)
		{
			if (_selectedIndex >= 0 && _selectedIndex < _options.Count)
			{
				ExecuteSelectedOption(_options[_selectedIndex]);
			}
		}
	}

	private void ExecuteSelectedOption(DialogueOptionItem opt)
	{
		if (opt == null || !opt.Enabled)
		{
			return;
		}
		if (opt.IntentKind == "leave")
		{
			CloseConversation(releasePed: true);
			return;
		}
		if (opt.IntentKind == "custom")
		{
			OpenCustomKeyboardInput();
			return;
		}
		if (opt.IntentKind == "bribe")
		{
			int cents = _settings.BribeCostDollars * 100;
			if (MONEY._MONEY_GET_CASH_BALANCE() < cents)
			{
				ShowToast($"Need ${_settings.BribeCostDollars} cash to offer a bribe.", 3000);
				return;
			}
			MONEY._MONEY_DECREMENT_CASH_BALANCE(cents);
		}

		SubmitPlayerSpeech(opt.PlayerSpokenLine, opt.IntentKind);
	}

	private void OpenCustomKeyboardInput()
	{
		try
		{
			_keyboardActive = true;
			_keyboardStartedAt = Game.GameTime;
			MISC.DISPLAY_ONSCREEN_KEYBOARD(0, "FMMC_KEY_TIP8", "", "", "", "", "", 120);
		}
		catch
		{
			_keyboardActive = false;
		}
	}

	private void PollOnScreenKeyboard()
	{
		int state = MISC.UPDATE_ONSCREEN_KEYBOARD();
		if (state == 0)
		{
			if (Game.GameTime - _keyboardStartedAt > 60000)
			{
				MISC.CANCEL_ONSCREEN_KEYBOARD();
				_keyboardActive = false;
			}
			return;
		}

		_keyboardActive = false;
		if (state == 1)
		{
			string typed = MISC.GET_ONSCREEN_KEYBOARD_RESULT();
			if (!string.IsNullOrWhiteSpace(typed))
			{
				SubmitPlayerSpeech(typed.Trim(), "custom");
			}
		}
	}

	private void TriggerQuickGreet(Ped npc)
	{
		NpcContextProfile ctx = NemesisBridge.BuildFullNpcContext(npc, _settings.EnableNemesisModBridge, _scriptsDir);
		string playerLine = $"Howdy, {ctx.Name}. How are things in {ctx.Town}?";

		try
		{
			TASK.TASK_LOOK_AT_ENTITY(npc.Handle, Game.Player.Character.Handle, 5000, 0, 0, 0);
		}
		catch
		{
		}

		Task.Run(async () =>
		{
			AiReplyResult res = await AiDialogueClient.GenerateReplyAsync(ctx, playerLine, "greet", _serverProcess, _settings);
			ShowToast($"{ctx.Name}: \"{res.SpokenText}\"", 5500);
			VoiceSynthesizer.SpeakLineAsync(res.SpokenText, ctx.IsMale, _bootstrapper, _settings, _modDataDir);
			_mainThreadQueue.Enqueue(() => { NemesisBridge.SyncOutcomeToNemesisMod(ctx.PedHandle, playerLine, res.SpokenText, res.HateDelta, res.FearDelta, res.RevealedCampIntel); });
			if (_settings.SaveNpcMemories)
			{
				StandaloneWorld.SaveMemoryStore();
			}
		});

		if (_settings.EnableNativeSpeechAnim)
		{
			VoiceSynthesizer.PlayNativePedSpeech(npc, Game.Player.Character, "greet", "NONE");
		}
	}

	private void SubmitPlayerSpeech(string playerSpeech, string intentKind)
	{
		if (_convoCtx == null || _convoPed == null || !_convoPed.Exists())
		{
			return;
		}

		_lastPlayerLine = playerSpeech;
		_isGeneratingReply = true;
		_currentNpcLine = "Thinking...";

		NpcContextProfile snapshotCtx = _convoCtx;
		Ped snapshotPed = _convoPed;
		StandaloneWorld.PopulateLivePlayerAndWorldState(snapshotCtx, snapshotPed);

		Task.Run(async () =>
		{
			try
			{
				AiReplyResult res = await AiDialogueClient.GenerateReplyAsync(snapshotCtx, playerSpeech, intentKind, _serverProcess, _settings);
				_currentNpcLine = res.SpokenText;
				_lastActionTag = res.ActionTag;
				_isGeneratingReply = false;

				VoiceSynthesizer.SpeakLineAsync(res.SpokenText, snapshotCtx.IsMale, _bootstrapper, _settings, _modDataDir);
				_mainThreadQueue.Enqueue(() => { NemesisBridge.SyncOutcomeToNemesisMod(snapshotCtx.PedHandle, playerSpeech, res.SpokenText, res.HateDelta, res.FearDelta, res.RevealedCampIntel); });

				if (snapshotCtx.IsNemesis)
				{
					snapshotCtx.Hate = Math.Max(0, Math.Min(100, snapshotCtx.Hate + res.HateDelta));
					snapshotCtx.Fear = Math.Max(0, Math.Min(100, snapshotCtx.Fear + res.FearDelta));
				}

				if (_settings.SaveNpcMemories)
				{
					StandaloneWorld.SaveMemoryStore();
				}
			}
			catch
			{
				_currentNpcLine = "Reckon we said all there is to say right now, mister.";
				_isGeneratingReply = false;
			}
		});

		if (_settings.EnableNativeSpeechAnim)
		{
			VoiceSynthesizer.PlayNativePedSpeech(snapshotPed, Game.Player.Character, intentKind, _lastActionTag);
		}
	}

	private void ExecutePendingActionOnMainThread()
	{
		if (!_settings.AllowDynamicNpcActions || _convoPed == null || !_convoPed.Exists() || _convoPed.IsDead)
		{
			return;
		}

		Ped player = Game.Player.Character;
		string act = _lastActionTag;
		if (act == "NONE" || string.IsNullOrEmpty(act))
		{
			return;
		}

		_lastActionTag = "NONE";
		try
		{
			switch (act)
			{
			case "ATTACK":
				_convoPed.BlockPermanentEvents = false;
				TASK.CLEAR_PED_TASKS(_convoPed.Handle, p1: true, p2: false);
				TASK.TASK_COMBAT_PED(_convoPed.Handle, player.Handle, 0, 16);
				ShowToast($"{_convoCtx?.Name ?? "NPC"} drew their weapon to fight!", 4000);
				CloseConversation(releasePed: false);
				break;
			case "SURRENDER":
				TASK.CLEAR_PED_TASKS(_convoPed.Handle, p1: true, p2: false);
				TASK.TASK_HANDS_UP(_convoPed.Handle, 15000, player.Handle, -1, p4: false);
				ShowToast($"{_convoCtx?.Name ?? "NPC"} surrendered with hands raised!", 4000);
				break;
			case "FLEE":
				_convoPed.BlockPermanentEvents = false;
				TASK.CLEAR_PED_TASKS(_convoPed.Handle, p1: true, p2: false);
				TASK.TASK_SMART_FLEE_PED(_convoPed.Handle, player.Handle, 120f, 15000, 0, 2.0f, 0);
				ShowToast($"{_convoCtx?.Name ?? "NPC"} panicked and fled!", 4000);
				CloseConversation(releasePed: false);
				break;
			case "INTEL":
				ShowToast($"FRONTIER INTEL SHARED by {_convoCtx?.Name ?? "NPC"}!", 4500);
				break;
			}
		}
		catch
		{
		}
	}

	private void DrawBootstrapProgressHud()
	{
		float x = 0.27f;
		float y = 0.03f;
		float w = 0.46f;
		float h = 0.062f;

		WesternUi.DrawFramedPanel(x, y, w, h, WesternUi.Gold);
		WesternUi.DrawText(_bootstrapper.StatusBanner, x + 0.014f, y + 0.009f, 0.25f, WesternUi.Gold.r, WesternUi.Gold.g, WesternUi.Gold.b, 255, titleFont: true);

		float barX = x + 0.014f;
		float barY = y + 0.038f;
		float barW = w - 0.028f;
		float barH = 0.011f;
		WesternUi.DrawBox(barX, barY, barW, barH, 40, 36, 30, 255);

		float fill = Math.Max(0.02f, Math.Min(1.0f, _bootstrapper.ProgressPct / 100f));
		WesternUi.DrawBox(barX, barY, barW * fill, barH, WesternUi.Gold.r, WesternUi.Gold.g, WesternUi.Gold.b, 255);
	}

	private void DrawToastBanner(string msg)
	{
		float w = 0.50f;
		float x = 0.50f - w * 0.5f;
		float y = 0.032f;
		float h = 0.042f;

		WesternUi.DrawFramedPanel(x, y, w, h, WesternUi.Gold);
		WesternUi.DrawText(msg, x + 0.014f, y + 0.009f, 0.26f, WesternUi.Parchment.r, WesternUi.Parchment.g, WesternUi.Parchment.b, 255, titleFont: true);
	}

	private void DrawTargetPromptBadge(NpcContextProfile p)
	{
		float w = 0.36f;
		float h = 0.056f;
		float x = 0.61f;
		float y = 0.88f;

		var border = p.IsNemesis ? WesternUi.Crimson : WesternUi.Gold;
		WesternUi.DrawFramedPanel(x, y, w, h, border);

		string tag = p.IsNemesis ? $"[NEMESIS: {p.Faction.ToUpperInvariant()}]" : $"[{p.Role.ToUpperInvariant()}]";
		WesternUi.DrawText($"{p.Name}  {tag}", x + 0.012f, y + 0.007f, 0.24f, WesternUi.Gold.r, WesternUi.Gold.g, WesternUi.Gold.b, 255, titleFont: true);
		WesternUi.DrawText($"[{_settings.TalkKey}] Talk (AI Dialogue)   [{_settings.QuickGreetKey}] Quick Greet   [{_settings.CustomInputKey}] Type Custom", x + 0.012f, y + 0.030f, 0.21f, WesternUi.Parchment.r, WesternUi.Parchment.g, WesternUi.Parchment.b);
	}

	private void DrawConversationOverlay()
	{
		if (!_isGeneratingReply && _lastActionTag != "NONE")
		{
			ExecutePendingActionOnMainThread();
			if (!_convoOpen)
			{
				return;
			}
		}

		NpcContextProfile c = _convoCtx;
		if (c == null)
		{
			return;
		}

		// 1. Top NPC Header & EnRAGE Context Banner
		float topW = 0.64f;
		float topX = 0.50f - topW * 0.5f;
		float topY = 0.028f;
		float topH = c.IsNemesis ? 0.115f : 0.088f;

		var topBorder = c.IsNemesis ? WesternUi.Crimson : WesternUi.Gold;
		WesternUi.DrawFramedPanel(topX, topY, topW, topH, topBorder);

		string modeBadge = c.IsEnrageConnected ? "EnRAGE LINKED" : "STANDALONE 1899";
		string aiBadge = _serverProcess.IsServerReady ? _settings.ModelPreset : "1899 Engine";
		WesternUi.DrawText($"{c.Name.ToUpperInvariant()}  —  {c.Role.ToUpperInvariant()} ({c.Faction.ToUpperInvariant()})", topX + 0.015f, topY + 0.008f, 0.29f, WesternUi.Gold.r, WesternUi.Gold.g, WesternUi.Gold.b, 255, titleFont: true);
		WesternUi.DrawText($"[{modeBadge} | {aiBadge}]", topX + topW - 0.19f, topY + 0.010f, 0.20f, WesternUi.Emerald.r, WesternUi.Emerald.g, WesternUi.Emerald.b);

		string politicsLine = $"{c.Town} ({c.District})  |  Mayor: {c.TownMayor} ({c.TownRegime}, Tax {c.TownTaxRate}%)  |  Sheriff: {c.TownSheriff}  |  Disposition: {c.Memory.Disposition}";
		WesternUi.DrawText(politicsLine, topX + 0.015f, topY + 0.036f, 0.21f, WesternUi.DimText.r, WesternUi.DimText.g, WesternUi.DimText.b);

		if (c.IsNemesis)
		{
			string nemLine = $"NEMESIS RANK {c.Rank}  |  HATE: {c.Hate}/100  |  FEAR: {c.Fear}/100  |  TEMPER: {c.Personality}  |  BOSS: {(!string.IsNullOrEmpty(c.BossName) ? c.BossName : "None")}";
			WesternUi.DrawText(nemLine, topX + 0.015f, topY + 0.060f, 0.21f, 235, 125, 115);
			if (!string.IsNullOrEmpty(c.GrudgeSummary))
			{
				WesternUi.DrawText($"GRUDGE: {c.GrudgeSummary}", topX + 0.015f, topY + 0.084f, 0.20f, WesternUi.Parchment.r, WesternUi.Parchment.g, WesternUi.Parchment.b);
			}
		}
		else
		{
			WesternUi.DrawText($"BACKGROUND: {c.Backstory}  (Temperament: {c.Personality})", topX + 0.015f, topY + 0.060f, 0.20f, WesternUi.Parchment.r, WesternUi.Parchment.g, WesternUi.Parchment.b);
		}

		// 2. Bottom Spoken Dialogue & Interactive Choices Box
		List<string> npcWrapped = WesternUi.WrapLines($"{c.Name}: \"{_currentNpcLine}\"", 76, 3);
		int optCount = _options.Count;
		float botW = 0.64f;
		float botX = 0.50f - botW * 0.5f;
		float botH = 0.065f + (npcWrapped.Count * 0.027f) + (optCount * 0.028f) + (!string.IsNullOrEmpty(_lastPlayerLine) ? 0.026f : 0f);
		float botY = 0.955f - botH;

		WesternUi.DrawFramedPanel(botX, botY, botW, botH, WesternUi.Gold);
		float curY = botY + 0.012f;

		if (!string.IsNullOrEmpty(_lastPlayerLine))
		{
			WesternUi.DrawText($"{c.PlayerName}: \"{_lastPlayerLine}\"", botX + 0.016f, curY, 0.22f, WesternUi.SkyBlue.r, WesternUi.SkyBlue.g, WesternUi.SkyBlue.b);
			curY += 0.026f;
		}

		foreach (string line in npcWrapped)
		{
			WesternUi.DrawText(line, botX + 0.016f, curY, 0.26f, WesternUi.Parchment.r, WesternUi.Parchment.g, WesternUi.Parchment.b, 255, titleFont: true);
			curY += 0.027f;
		}

		// Divider line
		WesternUi.DrawBox(botX + 0.016f, curY + 0.004f, botW - 0.032f, 0.0018f, WesternUi.Crimson.r, WesternUi.Crimson.g, WesternUi.Crimson.b, 220);
		curY += 0.012f;

		for (int i = 0; i < optCount; i++)
		{
			DialogueOptionItem opt = _options[i];
			bool sel = i == _selectedIndex;
			if (sel)
			{
				WesternUi.DrawBox(botX + 0.014f, curY - 0.002f, botW - 0.028f, 0.025f, 155, 28, 28, 195);
			}

			var col = !opt.Enabled ? WesternUi.DimText : (sel ? WesternUi.Gold : WesternUi.Parchment);
			WesternUi.DrawText($"[{i + 1}]  {opt.Label}", botX + 0.022f, curY, 0.22f, col.r, col.g, col.b);
			curY += 0.028f;
		}

		WesternUi.DrawText("Controls: [1-7] or [Up/Down + Enter] Select  |  [T] Type Custom Keyboard  |  [F9] AI Status  |  [Esc] Close", botX + 0.016f, botY + botH - 0.024f, 0.19f, WesternUi.DimText.r, WesternUi.DimText.g, WesternUi.DimText.b);
	}

	private void DrawStatusMenuOverlay()
	{
		float w = 0.54f;
		float h = 0.36f;
		float x = 0.50f - w * 0.5f;
		float y = 0.26f;

		WesternUi.DrawFramedPanel(x, y, w, h, WesternUi.Gold);
		WesternUi.DrawText("NPC AI DIALOGUE MOD — ENGINE & INTEGRATION CONTROL PANEL", x + 0.018f, y + 0.012f, 0.28f, WesternUi.Gold.r, WesternUi.Gold.g, WesternUi.Gold.b, 255, titleFont: true);

		string bridgeState = NemesisBridge.IsNemesisModLoaded ? "CONNECTED (Full EnRAGE Roster, Grudges & Civic Politics)" : "STANDALONE MODE (Built-In 1899 Frontier World Engine)";
		WesternUi.DrawText($"AI Server Backend: {_serverProcess.ServerBackendName}", x + 0.018f, y + 0.048f, 0.22f, WesternUi.Emerald.r, WesternUi.Emerald.g, WesternUi.Emerald.b);
		WesternUi.DrawText($"Bootstrap Status: {_bootstrapper.CurrentTask} ({_bootstrapper.ProgressPct}%)", x + 0.018f, y + 0.074f, 0.22f, WesternUi.Parchment.r, WesternUi.Parchment.g, WesternUi.Parchment.b);
		WesternUi.DrawText($"EnRAGE Integration: {bridgeState}", x + 0.018f, y + 0.100f, 0.22f, WesternUi.SkyBlue.r, WesternUi.SkyBlue.g, WesternUi.SkyBlue.b);

		string[] rows = new[]
		{
			$"[1] Re-Verify / Restart Local AI Server & Model Bootstrap",
			$"[2] Cycle AI Model Preset: < {_settings.ModelPreset} > ({_settings.ModelFileName})",
			$"[3] Toggle Spoken Voice Synthesis (TTS): {(_settings.EnableVoice ? "ENABLED" : "DISABLED")}",
			$"[4] Toggle EnRAGE NemesisMod Bridge: {(_settings.EnableNemesisModBridge ? "ENABLED" : "DISABLED")}",
			$"[5] Toggle Dynamic NPC Combat/Surrender Actions: {(_settings.AllowDynamicNpcActions ? "ENABLED" : "DISABLED")}",
			$"[Esc / F9] Save Settings & Close Panel"
		};

		float ry = y + 0.142f;
		for (int i = 0; i < rows.Length; i++)
		{
			bool sel = i == _statusMenuSel;
			if (sel)
			{
				WesternUi.DrawBox(x + 0.016f, ry - 0.002f, w - 0.032f, 0.026f, 155, 28, 28, 195);
			}
			var c = sel ? WesternUi.Gold : WesternUi.Parchment;
			WesternUi.DrawText(rows[i], x + 0.024f, ry, 0.22f, c.r, c.g, c.b);
			ry += 0.031f;
		}
	}

	private void HandleStatusMenuKeys(Keys key)
	{
		if (key == Keys.Escape || key == Keys.Back)
		{
			_settings.SaveDefaultIni();
			_statusMenuOpen = false;
			return;
		}
		if (key == Keys.Up || key == Keys.W)
		{
			_statusMenuSel = (_statusMenuSel + 5) % 6;
			return;
		}
		if (key == Keys.Down || key == Keys.S)
		{
			_statusMenuSel = (_statusMenuSel + 1) % 6;
			return;
		}

		int actionIdx = -1;
		if (key >= Keys.D1 && key <= Keys.D6)
		{
			actionIdx = (int)(key - Keys.D1);
			_statusMenuSel = actionIdx;
		}
		else if (key == Keys.Enter || key == Keys.Space)
		{
			actionIdx = _statusMenuSel;
		}

		switch (actionIdx)
		{
		case 0:
			_serverProcess?.Dispose();
			Task.Run(InitializeAiBackendAsync);
			ShowToast("Re-verifying AI model & restarting server...", 3500);
			break;
		case 1:
		{
			int cur = Array.IndexOf(AvailablePresets, _settings.ModelPreset);
			int next = (cur + 1) % AvailablePresets.Length;
			_settings.ModelPreset = AvailablePresets[next];
			_settings.ApplyPresetDefaults(_settings.ModelPreset);
			_settings.SaveDefaultIni();
			ShowToast($"Switched AI Model Preset to {_settings.ModelPreset}. Press [1] to download/load.", 4000);
			break;
		}
		case 2:
			_settings.EnableVoice = !_settings.EnableVoice;
			_settings.SaveDefaultIni();
			break;
		case 3:
			_settings.EnableNemesisModBridge = !_settings.EnableNemesisModBridge;
			_settings.SaveDefaultIni();
			break;
		case 4:
			_settings.AllowDynamicNpcActions = !_settings.AllowDynamicNpcActions;
			_settings.SaveDefaultIni();
			break;
		case 5:
			_settings.SaveDefaultIni();
			_statusMenuOpen = false;
			break;
		}
	}
}
