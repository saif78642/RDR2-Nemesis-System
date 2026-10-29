using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace NpcAiTalk;

public sealed class AiServerProcess : IDisposable
{
	private Process _process;
	private readonly AutoBootstrapper _bootstrapper;
	private readonly NpcAiSettings _settings;

	public int ActivePort { get; private set; }
	public volatile bool IsServerReady;
	public volatile string ServerBackendName = "Initializing";

	public AiServerProcess(AutoBootstrapper bootstrapper, NpcAiSettings settings)
	{
		_bootstrapper = bootstrapper;
		_settings = settings;
		ActivePort = settings.ServerPort;
		AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
	}

	private void OnProcessExit(object sender, EventArgs e)
	{
		Dispose();
	}

	public async Task<bool> StartOrAttachAsync()
	{
		ActivePort = _settings.ServerPort;

		// 1. Check if llama-server is already running on ActivePort
		if (await CheckHealthAsync(ActivePort))
		{
			IsServerReady = true;
			ServerBackendName = $"llama-server (127.0.0.1:{ActivePort})";
			_bootstrapper.Log($"Attached to existing llama-server at 127.0.0.1:{ActivePort}");
			return true;
		}

		// 2. Spawn our bundled/auto-downloaded llama-server.exe with the GGUF model
		string serverExe = _bootstrapper.ServerExePath;
		string modelPath = _bootstrapper.ModelFilePath;

		if (File.Exists(serverExe) && File.Exists(modelPath))
		{
			try
			{
				string args = $"-m \"{modelPath}\" -c {_settings.ContextSize} -t {_settings.CpuThreads} -ngl {_settings.GpuLayers} --port {ActivePort} --host 127.0.0.1 --log-disable";
				ProcessStartInfo psi = new ProcessStartInfo
				{
					FileName = serverExe,
					Arguments = args,
					WorkingDirectory = Path.GetDirectoryName(serverExe) ?? "",
					CreateNoWindow = true,
					UseShellExecute = false,
					WindowStyle = ProcessWindowStyle.Hidden
				};

				_process = Process.Start(psi);
				if (_process != null && !_process.HasExited)
				{
					try
					{
						_process.PriorityClass = ProcessPriorityClass.BelowNormal;
					}
					catch
					{
					}
				}

				_bootstrapper.Log($"Spawned llama-server.exe (PID {_process?.Id}) on port {ActivePort}");

				for (int i = 0; i < 30; i++)
				{
					if (_process != null && _process.HasExited)
					{
						_bootstrapper.Log($"llama-server.exe exited early with code {_process.ExitCode}");
						break;
					}
					if (await CheckHealthAsync(ActivePort))
					{
						IsServerReady = true;
						ServerBackendName = $"{_settings.ModelPreset} (Local CPU/GGUF :{ActivePort})";
						_bootstrapper.Log("llama-server.exe health check passed and ready.");
						return true;
					}
					await Task.Delay(500);
				}
			}
			catch (Exception ex)
			{
				_bootstrapper.Log("Failed to start llama-server.exe: " + ex.Message);
			}
		}

		// 3. Check if local Ollama is running on port 11434 as an automatic secondary backend
		if (await CheckHealthAsync(11434, "/api/tags"))
		{
			ActivePort = 11434;
			IsServerReady = true;
			ServerBackendName = "Ollama Local Server (:11434)";
			_bootstrapper.Log("Detected active local Ollama server on port 11434.");
			return true;
		}

		IsServerReady = false;
		ServerBackendName = "1899 Contextual Engine (Zero-Latency)";
		return false;
	}

	public async Task<bool> CheckHealthAsync(int port, string endpoint = "/health")
	{
		try
		{
			using (HttpClient client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(900) })
			{
				HttpResponseMessage res = await client.GetAsync($"http://127.0.0.1:{port}{endpoint}");
				return res.IsSuccessStatusCode;
			}
		}
		catch
		{
			return false;
		}
	}

	public void Dispose()
	{
		IsServerReady = false;
		try
		{
			if (_process != null && !_process.HasExited)
			{
				_process.Kill();
				_process.Dispose();
			}
		}
		catch
		{
		}
		_process = null;
	}
}
