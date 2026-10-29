using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using RDR2;
using RDR2.Native;

namespace NpcAiTalk;

public static class VoiceSynthesizer
{
	private const ulong PlayPedAmbientSpeechNativeHash = 10233584479118828642uL;
	private static int _speechCounter;

	public static unsafe void PlayNativePedSpeech(Ped speaker, Ped listener, string intentKind, string actionTag)
	{
		if (speaker == null || !speaker.Exists() || speaker.IsDead)
		{
			return;
		}

		string[] candidateContexts;
		if (actionTag == "ATTACK" || intentKind == "threaten")
		{
			candidateContexts = new[] { "CHALLENGE_THREATEN", "GENERIC_INSULT_HIGH", "GENERIC_WAR_CRY" };
		}
		else if (actionTag == "SURRENDER" || actionTag == "FLEE")
		{
			candidateContexts = new[] { "GENERIC_FRIGHTENED_HIGH", "GENERIC_SHOCKED_HIGH", "PLEAD" };
		}
		else if (intentKind == "bribe" || actionTag == "CALM")
		{
			candidateContexts = new[] { "GENERIC_THANKS", "GREET_GENERAL_STRANGER", "GENERIC_YES" };
		}
		else
		{
			candidateContexts = new[] { "GREET_GENERAL_STRANGER", "GENERIC_HOWS_IT_GOING", "CHAT_STATE" };
		}

		try
		{
			if (AUDIO.IS_ANY_SPEECH_PLAYING(speaker.Handle))
			{
				AUDIO.STOP_PED_SPEAKING(speaker.Handle, p1: false);
			}
		}
		catch
		{
		}

		foreach (string ctx in candidateContexts)
		{
			if (TryPlayContextSpeech(speaker, listener, ctx))
			{
				break;
			}
		}
	}

	private static unsafe bool TryPlayContextSpeech(Ped speaker, Ped listener, string speechContext)
	{
		IntPtr ctxPtr = IntPtr.Zero;
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(speechContext);
			ctxPtr = Marshal.AllocHGlobal(bytes.Length + 1);
			Marshal.Copy(bytes, 0, ctxPtr, bytes.Length);
			Marshal.WriteByte(ctxPtr, bytes.Length, 0);

			ulong* ptr = stackalloc ulong[8];
			for (int i = 0; i < 8; i++)
			{
				ptr[i] = 0uL;
			}
			ptr[0] = (ulong)ctxPtr.ToInt64();
			ptr[1] = 0uL;
			ptr[3] = MISC.GET_HASH_KEY("SPEECH_PARAMS_FORCE_NORMAL_CLEAR");
			ptr[4] = (listener != null && listener.Exists()) ? (uint)listener.Handle : 0u;
			ptr[6] = 1uL;
			ptr[7] = 1uL;

			return Function.Call<bool>(PlayPedAmbientSpeechNativeHash, new InputArgument[2]
			{
				speaker.Handle,
				new InputArgument((ulong)ptr)
			});
		}
		catch
		{
			return false;
		}
		finally
		{
			if (ctxPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(ctxPtr);
			}
		}
	}

	public static void SpeakLineAsync(
		string text,
		bool isMale,
		AutoBootstrapper bootstrapper,
		NpcAiSettings settings,
		string rootDir)
	{
		if (!settings.EnableVoice || string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		Task.Run(() =>
		{
			try
			{
				string audioDir = Path.Combine(rootDir, "audio");
				Directory.CreateDirectory(audioDir);
				string wavPath = Path.Combine(audioDir, $"npc_reply_{++_speechCounter % 4}.wav");

				// Layer 1: Neural Piper ONNX TTS if binary & voice model are present
				if (settings.UsePiperNeuralTts && File.Exists(bootstrapper.PiperExePath) && File.Exists(bootstrapper.VoiceOnnxPath))
				{
					if (TrySynthesizeWithPiper(text, bootstrapper.PiperExePath, bootstrapper.VoiceOnnxPath, wavPath))
					{
						PlayWavFile(wavPath);
						return;
					}
				}

				// Layer 2: Zero-Download Windows Offline SAPI Voice Synthesis
				if (settings.UseWindowsSapiFallback)
				{
					SpeakWithWindowsSapi(text, isMale, settings.VoiceVolume, settings.VoiceRate);
				}
			}
			catch
			{
			}
		});
	}

	private static bool TrySynthesizeWithPiper(string text, string piperExe, string modelOnnx, string outWav)
	{
		try
		{
			if (File.Exists(outWav))
			{
				File.Delete(outWav);
			}
			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = piperExe,
				Arguments = $"--model \"{modelOnnx}\" --output_file \"{outWav}\"",
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardInput = true,
				WindowStyle = ProcessWindowStyle.Hidden
			};

			using (Process p = Process.Start(psi))
			{
				if (p == null)
				{
					return false;
				}
				p.StandardInput.WriteLine(text);
				p.StandardInput.Close();
				p.WaitForExit(4000);
			}
			return File.Exists(outWav) && new FileInfo(outWav).Length > 512;
		}
		catch
		{
			return false;
		}
	}

	private static void PlayWavFile(string wavPath)
	{
		try
		{
			using (SoundPlayer player = new SoundPlayer(wavPath))
			{
				player.PlaySync();
			}
		}
		catch
		{
		}
	}

	private static void SpeakWithWindowsSapi(string text, bool isMale, int volume, int rate)
	{
		try
		{
			string sanitized = text.Replace("\"", "'").Replace("`", "'").Replace("$", "").Replace("\r", " ").Replace("\n", " ");
			string genderHint = isMale ? "Male" : "Female";
			string psCmd = $"Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; $s.SelectVoiceByHints([System.Speech.Synthesis.VoiceGender]::{genderHint}); $s.Volume = {Math.Max(10, Math.Min(100, volume))}; $s.Rate = {Math.Max(-5, Math.Min(5, rate))}; $s.Speak(\"{sanitized}\");";

			ProcessStartInfo psi = new ProcessStartInfo
			{
				FileName = "powershell.exe",
				Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{psCmd}\"",
				CreateNoWindow = true,
				UseShellExecute = false,
				WindowStyle = ProcessWindowStyle.Hidden
			};

			using (Process p = Process.Start(psi))
			{
				p?.WaitForExit(10000);
			}
		}
		catch
		{
		}
	}
}
