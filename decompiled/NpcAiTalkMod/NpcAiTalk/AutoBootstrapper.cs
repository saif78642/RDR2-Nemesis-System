using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NpcAiTalk;

public sealed class AutoBootstrapper
{
	private readonly string _rootDir;
	private readonly string _binDir;
	private readonly string _modelsDir;
	private readonly string _logsDir;
	private readonly NpcAiSettings _settings;

	public volatile bool IsRunningSetup;
	public volatile bool IsSetupComplete;
	public volatile bool HasError;
	public volatile int ProgressPct;
	public volatile string CurrentTask = "Idle";
	public volatile string StatusBanner = "";
	public volatile string LastError = "";
	public double DownloadedMb { get; private set; }
	public double TotalMb { get; private set; }

	public string ServerExePath => Path.Combine(_binDir, "llama-server.exe");
	public string ServerVersionStampPath => Path.Combine(_binDir, "server_version.txt");
	public string PiperExePath => Path.Combine(_binDir, "piper.exe");
	public string ModelFilePath => Path.Combine(_modelsDir, _settings.ModelFileName);
	public string VoiceOnnxPath => Path.Combine(_modelsDir, "voice-en-ryan.onnx");
	public string VoiceJsonPath => Path.Combine(_modelsDir, "voice-en-ryan.onnx.json");

	public AutoBootstrapper(string rootDir, NpcAiSettings settings)
	{
		_rootDir = rootDir;
		_settings = settings;
		_binDir = Path.Combine(_rootDir, "bin");
		_modelsDir = Path.Combine(_rootDir, "models");
		_logsDir = Path.Combine(_rootDir, "logs");

		try
		{
			Directory.CreateDirectory(_rootDir);
			Directory.CreateDirectory(_binDir);
			Directory.CreateDirectory(_modelsDir);
			Directory.CreateDirectory(_logsDir);
			Directory.CreateDirectory(Path.Combine(_rootDir, "audio"));
			Directory.CreateDirectory(Path.Combine(_rootDir, "memory"));
		}
		catch
		{
		}
	}

	public void Log(string msg)
	{
		try
		{
			string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}{Environment.NewLine}";
			File.AppendAllText(Path.Combine(_logsDir, "NpcAiTalk.log"), line);
		}
		catch
		{
		}
	}

	private bool IsServerBinaryUpToDate()
	{
		if (!File.Exists(ServerExePath))
		{
			return false;
		}
		try
		{
			if (!File.Exists(ServerVersionStampPath))
			{
				return false;
			}
			string installedUrl = File.ReadAllText(ServerVersionStampPath).Trim();
			return string.Equals(installedUrl, _settings.ServerDownloadUrl, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	public bool CheckAlreadyInstalled()
	{
		bool modelOk = File.Exists(ModelFilePath) && new FileInfo(ModelFilePath).Length > 50 * 1024 * 1024;
		bool serverOk = IsServerBinaryUpToDate();
		IsSetupComplete = modelOk && serverOk;
		return IsSetupComplete;
	}

	public async Task<bool> EnsureAllInstalledAsync()
	{
		if (IsRunningSetup)
		{
			return IsSetupComplete;
		}
		IsRunningSetup = true;
		HasError = false;
		LastError = "";

		try
		{
			// Enable TLS 1.2 and TLS 1.3 for Hugging Face & GitHub CDN on .NET Framework 4.8
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
			ServicePointManager.DefaultConnectionLimit = 8;

			if (CheckAlreadyInstalled())
			{
				Log("Bootstrap check: AI server binary and GGUF model already installed.");
				CurrentTask = "Ready";
				ProgressPct = 100;
				StatusBanner = "";
				IsRunningSetup = false;
				return true;
			}

			if (!_settings.AutoDownloadAndSetup)
			{
				Log("AutoDownloadAndSetup is disabled in NpcAiTalk.ini; operating in built-in 1899 contextual mode.");
				CurrentTask = "Contextual Mode (AutoDownload=false)";
				IsRunningSetup = false;
				return false;
			}

			// Step 1: Ensure llama-server.exe is present and up-to-date in scripts\NpcAiTalk\bin\
			if (!IsServerBinaryUpToDate())
			{
				Log("Step 1/3: Downloading native llama-server.exe runtime...");
				CurrentTask = "Downloading AI Engine Binary (llama-server)";
				ProgressPct = 0;
				await DownloadAndExtractServerZipAsync(_settings.ServerDownloadUrl);
			}

			// Step 2: Ensure GGUF Small Language Model is present in scripts\NpcAiTalk\models\
			if (!File.Exists(ModelFilePath) || new FileInfo(ModelFilePath).Length < 50 * 1024 * 1024)
			{
				Log($"Step 2/3: Downloading SLM {_settings.ModelFileName} from {_settings.ModelDownloadUrl}...");
				CurrentTask = $"Downloading {_settings.ModelPreset} Model";
				ProgressPct = 0;
				await DownloadFileStreamingAsync(_settings.ModelDownloadUrl, ModelFilePath, _settings.ModelPreset);
			}

			// Step 3: Download lightweight Piper Voice model files (~15 MB) if missing
			if (_settings.UsePiperNeuralTts && !File.Exists(VoiceOnnxPath))
			{
				try
				{
					Log("Step 3/3: Downloading 1899 Frontier Voice Pack (15 MB)...");
					CurrentTask = "Downloading Frontier Voice Pack";
					ProgressPct = 0;
					await DownloadFileStreamingAsync(_settings.PiperVoiceConfigUrl, VoiceJsonPath, "Voice Config");
					await DownloadFileStreamingAsync(_settings.PiperVoiceModelUrl, VoiceOnnxPath, "Voice Pack");
				}
				catch (Exception vex)
				{
					Log("Optional voice pack download skipped (Windows SAPI + Native Speech active): " + vex.Message);
				}
			}

			IsSetupComplete = CheckAlreadyInstalled();
			CurrentTask = IsSetupComplete ? "Ready" : "Fallback Mode";
			ProgressPct = 100;
			StatusBanner = IsSetupComplete ? "AI Model Setup Complete! Ready to talk." : "";
			Log("Bootstrap completed successfully. IsSetupComplete=" + IsSetupComplete);
			return IsSetupComplete;
		}
		catch (Exception ex)
		{
			HasError = true;
			LastError = ex.Message;
			CurrentTask = "Offline / Contextual Engine Active";
			StatusBanner = "";
			Log("Bootstrap exception (seamless contextual engine will handle dialogues): " + ex);
			return false;
		}
		finally
		{
			IsRunningSetup = false;
		}
	}

	private async Task DownloadAndExtractServerZipAsync(string zipUrl)
	{
		string tempZip = Path.Combine(_binDir, "llama_bin_download.tmp.zip");
		await DownloadFileStreamingAsync(zipUrl, tempZip, "AI Engine Runtime");

		CurrentTask = "Extracting AI Engine Runtime...";
		StatusBanner = "[AI Talk Mod] Extracting native C++ AI runtime...";

		using (ZipArchive archive = ZipFile.OpenRead(tempZip))
		{
			foreach (ZipArchiveEntry entry in archive.Entries)
			{
				if (string.IsNullOrEmpty(entry.Name))
				{
					continue;
				}
				string ext = Path.GetExtension(entry.Name).ToLowerInvariant();
				if (ext == ".exe" || ext == ".dll")
				{
					string destFile = Path.Combine(_binDir, entry.Name);
					entry.ExtractToFile(destFile, overwrite: true);
				}
			}
		}

		try
		{
			File.WriteAllText(ServerVersionStampPath, zipUrl ?? "");
			if (File.Exists(tempZip))
			{
				File.Delete(tempZip);
			}
		}
		catch
		{
		}
	}

	private async Task DownloadFileStreamingAsync(string url, string destinationPath, string displayName)
	{
		string tempPath = destinationPath + ".downloading";
		using (HttpClientHandler handler = new HttpClientHandler { AllowAutoRedirect = true })
		using (HttpClient client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(30) })
		{
			client.DefaultRequestHeaders.UserAgent.ParseAdd("RDR2-NpcAiTalkMod/1.0");
			using (HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
			{
				response.EnsureSuccessStatusCode();
				long? contentLength = response.Content.Headers.ContentLength;
				TotalMb = contentLength.HasValue ? (contentLength.Value / (1024.0 * 1024.0)) : 0.0;

				using (Stream netStream = await response.Content.ReadAsStreamAsync())
				using (FileStream fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
				{
					byte[] buffer = new byte[65536];
					long totalRead = 0L;
					int read;
					int lastReportTick = Environment.TickCount;

					while ((read = await netStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
					{
						await fileStream.WriteAsync(buffer, 0, read);
						totalRead += read;
						DownloadedMb = totalRead / (1024.0 * 1024.0);

						if (contentLength.HasValue && contentLength.Value > 0)
						{
							ProgressPct = (int)Math.Min(99L, (totalRead * 100L) / contentLength.Value);
						}

						if (Environment.TickCount - lastReportTick >= 200)
						{
							lastReportTick = Environment.TickCount;
							if (TotalMb > 0.1)
							{
								StatusBanner = $"[AI Talk Mod] First-Time Setup: Downloading {displayName}... {ProgressPct}% ({DownloadedMb:0.0} / {TotalMb:0.0} MB)";
							}
							else
							{
								StatusBanner = $"[AI Talk Mod] First-Time Setup: Downloading {displayName}... ({DownloadedMb:0.0} MB)";
							}
						}
					}
				}
			}
		}

		if (File.Exists(destinationPath))
		{
			File.Delete(destinationPath);
		}
		File.Move(tempPath, destinationPath);
		ProgressPct = 100;
	}
}
