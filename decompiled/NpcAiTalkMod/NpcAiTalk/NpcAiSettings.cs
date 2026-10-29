using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace NpcAiTalk;

public sealed class NpcAiSettings
{
	public string IniPath { get; }

	// [Core]
	public bool AutoDownloadAndSetup { get; set; } = true;
	public int ServerPort { get; set; } = 8089;
	public int CpuThreads { get; set; } = 4;
	public int GpuLayers { get; set; } = 0;
	public int ContextSize { get; set; } = 1024;
	public int MaxTokens { get; set; } = 96;
	public float Temperature { get; set; } = 0.72f;
	public int RequestTimeoutMs { get; set; } = 9000;

	// [Model]
	public string ModelPreset { get; set; } = "LFM3-1.7B-Uncensored";
	public string ModelFileName { get; set; } = "LFM3-1.7B-Uncensored-AD-Q4_K_M.gguf";
	public string ModelDownloadUrl { get; set; } = "https://huggingface.co/AtomicChat/LFM3-1.7B-Uncensored-GGUF/resolve/main/LFM3-1.7B-Uncensored-AD-Q4_K_M.gguf";
	public string ServerDownloadUrl { get; set; } = "https://github.com/ggml-org/llama.cpp/releases/download/b11968/llama-b11968-bin-win-cpu-x64.zip";
	public string PiperVoiceModelUrl { get; set; } = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ryan/medium/en_US-ryan-medium.onnx";
	public string PiperVoiceConfigUrl { get; set; } = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ryan/medium/en_US-ryan-medium.onnx.json";

	// [Controls]
	public Keys TalkKey { get; set; } = Keys.Y;
	public Keys QuickGreetKey { get; set; } = Keys.G;
	public Keys CustomInputKey { get; set; } = Keys.T;
	public Keys StatusMenuKey { get; set; } = Keys.F9;
	public float MaxTalkDistance { get; set; } = 8.0f;
	public bool ShowTargetPrompt { get; set; } = true;
	public float PromptAutoHideSeconds { get; set; } = 3.0f;

	// [Voice]
	public bool EnableVoice { get; set; } = true;
	public bool EnableNativeSpeechAnim { get; set; } = true;
	public bool UsePiperNeuralTts { get; set; } = true;
	public bool UseWindowsSapiFallback { get; set; } = true;
	public int VoiceVolume { get; set; } = 90;
	public int VoiceRate { get; set; } = 0;

	// [Integration]
	public bool EnableNemesisModBridge { get; set; } = true;
	public bool AllowDynamicNpcActions { get; set; } = true;
	public int BribeCostDollars { get; set; } = 10;
	public bool SaveNpcMemories { get; set; } = true;

	public NpcAiSettings(string iniPath)
	{
		IniPath = iniPath;
		LoadOrCreate();
	}

	public void LoadOrCreate()
	{
		try
		{
			if (!File.Exists(IniPath))
			{
				SaveDefaultIni();
				return;
			}
			string[] lines = File.ReadAllLines(IniPath);
			Dictionary<string, string> kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (string raw in lines)
			{
				string line = raw.Trim();
				if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("["))
				{
					continue;
				}
				int eq = line.IndexOf('=');
				if (eq <= 0)
				{
					continue;
				}
				string key = line.Substring(0, eq).Trim();
				string val = line.Substring(eq + 1).Trim();
				int commentIdx = val.IndexOf(" ;", StringComparison.Ordinal);
				if (commentIdx >= 0)
				{
					val = val.Substring(0, commentIdx).Trim();
				}
				kv[key] = val;
			}

			AutoDownloadAndSetup = ReadBool(kv, "AutoDownloadAndSetup", AutoDownloadAndSetup);
			ServerPort = ReadInt(kv, "ServerPort", ServerPort, 1024, 65530);
			CpuThreads = ReadInt(kv, "CpuThreads", CpuThreads, 1, 32);
			GpuLayers = ReadInt(kv, "GpuLayers", GpuLayers, 0, 128);
			ContextSize = ReadInt(kv, "ContextSize", ContextSize, 512, 8192);
			MaxTokens = ReadInt(kv, "MaxTokens", MaxTokens, 20, 256);
			Temperature = ReadFloat(kv, "Temperature", Temperature, 0.1f, 1.5f);
			RequestTimeoutMs = ReadInt(kv, "RequestTimeoutMs", RequestTimeoutMs, 1500, 30000);

			ModelPreset = ReadString(kv, "ModelPreset", ModelPreset);
			ModelFileName = ReadString(kv, "ModelFileName", ModelFileName);
			ModelDownloadUrl = ReadString(kv, "ModelDownloadUrl", ModelDownloadUrl);
			ApplyPresetDefaults(ModelPreset);
			ServerDownloadUrl = ReadString(kv, "ServerDownloadUrl", ServerDownloadUrl);
			if (ServerDownloadUrl.IndexOf("b4792", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				ServerDownloadUrl = "https://github.com/ggml-org/llama.cpp/releases/download/b11968/llama-b11968-bin-win-cpu-x64.zip";
			}

			TalkKey = ReadKey(kv, "TalkKey", TalkKey);
			QuickGreetKey = ReadKey(kv, "QuickGreetKey", QuickGreetKey);
			CustomInputKey = ReadKey(kv, "CustomInputKey", CustomInputKey);
			StatusMenuKey = ReadKey(kv, "StatusMenuKey", StatusMenuKey);
			MaxTalkDistance = ReadFloat(kv, "MaxTalkDistance", MaxTalkDistance, 2.5f, 25.0f);
			ShowTargetPrompt = ReadBool(kv, "ShowTargetPrompt", ShowTargetPrompt);
			PromptAutoHideSeconds = ReadFloat(kv, "PromptAutoHideSeconds", PromptAutoHideSeconds, 0.5f, 30.0f);

			EnableVoice = ReadBool(kv, "EnableVoice", EnableVoice);
			EnableNativeSpeechAnim = ReadBool(kv, "EnableNativeSpeechAnim", EnableNativeSpeechAnim);
			UsePiperNeuralTts = ReadBool(kv, "UsePiperNeuralTts", UsePiperNeuralTts);
			UseWindowsSapiFallback = ReadBool(kv, "UseWindowsSapiFallback", UseWindowsSapiFallback);
			VoiceVolume = ReadInt(kv, "VoiceVolume", VoiceVolume, 0, 100);
			VoiceRate = ReadInt(kv, "VoiceRate", VoiceRate, -5, 5);

			EnableNemesisModBridge = ReadBool(kv, "EnableNemesisModBridge", EnableNemesisModBridge);
			AllowDynamicNpcActions = ReadBool(kv, "AllowDynamicNpcActions", AllowDynamicNpcActions);
			BribeCostDollars = ReadInt(kv, "BribeCostDollars", BribeCostDollars, 1, 500);
			SaveNpcMemories = ReadBool(kv, "SaveNpcMemories", SaveNpcMemories);
		}
		catch
		{
		}
	}

	public void ApplyPresetDefaults(string preset)
	{
		if (string.IsNullOrEmpty(preset))
		{
			return;
		}
		switch (preset.Trim().ToLowerInvariant())
		{
		case "lfm3-1.7b-uncensored":
		case "lfm3-1.7b":
			ModelFileName = "LFM3-1.7B-Uncensored-AD-Q4_K_M.gguf";
			ModelDownloadUrl = "https://huggingface.co/AtomicChat/LFM3-1.7B-Uncensored-GGUF/resolve/main/LFM3-1.7B-Uncensored-AD-Q4_K_M.gguf";
			break;
		case "lfm3-1.7b-uncensored-q6":
			ModelFileName = "LFM3-1.7B-Uncensored-AD-Q6_K.gguf";
			ModelDownloadUrl = "https://huggingface.co/AtomicChat/LFM3-1.7B-Uncensored-GGUF/resolve/main/LFM3-1.7B-Uncensored-AD-Q6_K.gguf";
			break;
		case "smollm2-360m":
			ModelFileName = "smollm2-360m-instruct-q8_0.gguf";
			ModelDownloadUrl = "https://huggingface.co/HuggingFaceTB/SmolLM2-360M-Instruct-GGUF/resolve/main/smollm2-360m-instruct-q8_0.gguf";
			break;
		case "qwen2.5-1.5b":
			ModelFileName = "qwen2.5-1.5b-instruct-q4_k_m.gguf";
			ModelDownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf";
			break;
		case "llama-3.2-1b":
			ModelFileName = "Llama-3.2-1B-Instruct-Q4_K_M.gguf";
			ModelDownloadUrl = "https://huggingface.co/bartowski/Llama-3.2-1B-Instruct-GGUF/resolve/main/Llama-3.2-1B-Instruct-Q4_K_M.gguf";
			break;
		case "qwen2.5-0.5b":
			ModelFileName = "qwen2.5-0.5b-instruct-q4_k_m.gguf";
			ModelDownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF/resolve/main/qwen2.5-0.5b-instruct-q4_k_m.gguf";
			break;
		}
	}

	public void SaveDefaultIni()
	{
		try
		{
			string dir = Path.GetDirectoryName(IniPath);
			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}
			File.WriteAllText(IniPath, BuildIniContent());
		}
		catch
		{
		}
	}

	public string BuildIniContent()
	{
		return $@"; ============================================================================
; NpcAiTalk — 1899 Frontier AI NPC Dialogue Mod (v1.0.0)
; Zero-Manual-Setup Small Language Model (SLM) Dialogue System for RDR2
; Works both 100% Standalone AND Integrated with EnRAGE (NemesisMod.dll)
; ============================================================================

[Core]
; Automatically download the AI server binary & lightweight model on first launch
AutoDownloadAndSetup={AutoDownloadAndSetup.ToString().ToLowerInvariant()}

; Local port for the background C++ llama-server inference process
ServerPort={ServerPort}

; Number of CPU threads used for generation (4 is fast and leaves CPU headroom for RDR2)
CpuThreads={CpuThreads}

; GPU layers to offload (0 = 100% CPU RAM, takes 0 MB VRAM away from RDR2)
GpuLayers={GpuLayers}

; Prompt context window in tokens
ContextSize={ContextSize}

; Maximum tokens per NPC spoken reply (64 tokens = ~1-2 concise frontier sentences)
MaxTokens={MaxTokens}

; Creativity / sampling temperature
Temperature={Temperature.ToString("0.00", CultureInfo.InvariantCulture)}

; HTTP timeout in milliseconds before seamless fallback
RequestTimeoutMs={RequestTimeoutMs}

[Model]
; Preset choices: LFM3-1.7B-Uncensored (1.1 GB, Q4_K_M), LFM3-1.7B-Uncensored-Q6 (1.6 GB, Q6_K), Qwen2.5-0.5B (352 MB), SmolLM2-360M (250 MB), Qwen2.5-1.5B (980 MB), Llama-3.2-1B (750 MB)
ModelPreset={ModelPreset}

; Model GGUF filename stored in scripts\NpcAiTalk\models\
ModelFileName={ModelFileName}

; Direct Hugging Face CDN URL for automatic first-run download
ModelDownloadUrl={ModelDownloadUrl}

; Precompiled native llama.cpp Windows x64 server zip URL (auto-extracted to scripts\NpcAiTalk\bin\)
ServerDownloadUrl={ServerDownloadUrl}

[Controls]
; Open the AI Dialogue Box with the targeted or nearest NPC
TalkKey={TalkKey}

; Quick instant AI greeting without opening the full dialogue menu
QuickGreetKey={QuickGreetKey}

; Directly open the On-Screen Keyboard to type custom words to the NPC
CustomInputKey={CustomInputKey}

; Open the AI Engine Status, Model & Settings Overlay
StatusMenuKey={StatusMenuKey}

; Maximum distance in meters to talk to an NPC
MaxTalkDistance={MaxTalkDistance.ToString("0.0", CultureInfo.InvariantCulture)}

; Show contextual HUD prompt when aiming at or standing near an NPC
ShowTargetPrompt={ShowTargetPrompt.ToString().ToLowerInvariant()}

; Seconds before the contextual HUD prompt automatically hides when standing near an NPC
PromptAutoHideSeconds={PromptAutoHideSeconds.ToString("0.0", CultureInfo.InvariantCulture)}

[Voice]
; Enable synthesized spoken audio + RDR2 native lip-sync animations
EnableVoice={EnableVoice.ToString().ToLowerInvariant()}

; Trigger RDR2 native ped facial speech & gestures during AI replies
EnableNativeSpeechAnim={EnableNativeSpeechAnim.ToString().ToLowerInvariant()}

; Use neural Piper ONNX TTS if present in scripts\NpcAiTalk\bin\
UsePiperNeuralTts={UsePiperNeuralTts.ToString().ToLowerInvariant()}

; Use zero-download Windows offline SAPI voice synthesis when Piper is not active
UseWindowsSapiFallback={UseWindowsSapiFallback.ToString().ToLowerInvariant()}

; Voice playback volume (0-100)
VoiceVolume={VoiceVolume}

; Voice speech rate (-5 to +5)
VoiceRate={VoiceRate}

[Integration]
; Automatically link with EnRAGE (NemesisMod.dll) for full Nemesis grudges, scars, gangs & Town Politics
EnableNemesisModBridge={EnableNemesisModBridge.ToString().ToLowerInvariant()}

; Allow AI replies to trigger live NPC reactions (Attack, Surrender, Flee, Give Intel, Calm Down)
AllowDynamicNpcActions={AllowDynamicNpcActions.ToString().ToLowerInvariant()}

; Dollar amount offered when selecting the 'Bribe / Make Peace' option
BribeCostDollars={BribeCostDollars}

; Persist NPC conversation histories and relationship scores across sessions
SaveNpcMemories={SaveNpcMemories.ToString().ToLowerInvariant()}
";
	}

	private static string ReadString(Dictionary<string, string> kv, string key, string def)
	{
		if (kv.TryGetValue(key, out string v) && !string.IsNullOrWhiteSpace(v))
		{
			return v;
		}
		return def;
	}

	private static bool ReadBool(Dictionary<string, string> kv, string key, bool def)
	{
		if (kv.TryGetValue(key, out string v))
		{
			if (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1" || v.Equals("yes", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (v.Equals("false", StringComparison.OrdinalIgnoreCase) || v == "0" || v.Equals("no", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}
		return def;
	}

	private static int ReadInt(Dictionary<string, string> kv, string key, int def, int min, int max)
	{
		if (kv.TryGetValue(key, out string v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
		{
			return Math.Max(min, Math.Min(max, parsed));
		}
		return def;
	}

	private static float ReadFloat(Dictionary<string, string> kv, string key, float def, float min, float max)
	{
		if (kv.TryGetValue(key, out string v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
		{
			return Math.Max(min, Math.Min(max, parsed));
		}
		return def;
	}

	private static Keys ReadKey(Dictionary<string, string> kv, string key, Keys def)
	{
		if (kv.TryGetValue(key, out string v) && Enum.TryParse<Keys>(v, ignoreCase: true, out Keys parsed))
		{
			return parsed;
		}
		return def;
	}
}
