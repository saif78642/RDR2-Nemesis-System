using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using RDR2;
using RDR2.Native;

namespace NpcAiTalk;

public static class WesternUi
{
	private sealed class PinnedUtf8
	{
		public IntPtr Ptr;
		public int LastFrame;
	}

	private const ulong VarStringHash = 18055593637008912569uL;
	private const ulong BgDisplayTextHash = 1619411321388727128uL;

	private static readonly Dictionary<string, PinnedUtf8> Cache = new Dictionary<string, PinnedUtf8>();
	private static int _nextSweepFrame;

	public static readonly (int r, int g, int b) Gold = (218, 178, 92);
	public static readonly (int r, int g, int b) Parchment = (242, 236, 222);
	public static readonly (int r, int g, int b) DimText = (172, 165, 152);
	public static readonly (int r, int g, int b) Crimson = (182, 34, 34);
	public static readonly (int r, int g, int b) Emerald = (95, 185, 115);
	public static readonly (int r, int g, int b) SkyBlue = (140, 180, 225);

	private static ulong PinString(string s)
	{
		if (s == null)
		{
			s = "";
		}
		int frame = Game.FrameCount;
		if (!Cache.TryGetValue(s, out PinnedUtf8 entry))
		{
			byte[] utf8 = Encoding.UTF8.GetBytes(s);
			IntPtr ptr = Marshal.AllocHGlobal(utf8.Length + 1);
			Marshal.Copy(utf8, 0, ptr, utf8.Length);
			Marshal.WriteByte(ptr, utf8.Length, 0);
			entry = new PinnedUtf8 { Ptr = ptr };
			Cache[s] = entry;
		}
		entry.LastFrame = frame;

		if (frame >= _nextSweepFrame)
		{
			_nextSweepFrame = frame + 600;
			foreach (string staleKey in Cache.Where(kv => frame - kv.Value.LastFrame > 360).Select(kv => kv.Key).ToList())
			{
				Marshal.FreeHGlobal(Cache[staleKey].Ptr);
				Cache.Remove(staleKey);
			}
		}
		return (ulong)entry.Ptr.ToInt64();
	}

	public static void DrawBox(float x, float y, float w, float h, int r = 14, int g = 12, int b = 10, int a = 232)
	{
		GRAPHICS.DRAW_RECT(x + w * 0.5f, y + h * 0.5f, w, h, r, g, b, a, p8: true, p9: false);
	}

	public static void DrawFramedPanel(float x, float y, float w, float h, (int r, int g, int b) borderColor)
	{
		DrawBox(x, y, w, h, 14, 12, 10, 238);
		float bw = 0.0015f;
		float bh = 0.0026f;
		DrawBox(x, y, w, bh, borderColor.r, borderColor.g, borderColor.b, 225);
		DrawBox(x, y + h - bh, w, bh, borderColor.r, borderColor.g, borderColor.b, 225);
		DrawBox(x, y, bw, h, borderColor.r, borderColor.g, borderColor.b, 225);
		DrawBox(x + w - bw, y, bw, h, borderColor.r, borderColor.g, borderColor.b, 225);
	}

	public static void DrawText(string text, float x, float y, float scale, int r = 240, int g = 235, int b = 222, int a = 255, bool titleFont = false)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		string clean = text.Replace("~", "").Replace("<", "(").Replace(">", ")");
		string fontFace = titleFont ? "$title" : "$body";
		string wrapped = $"<FONT FACE='{fontFace}'>{clean}</FONT>";

		UIDEBUG._BG_SET_TEXT_SCALE(scale, scale);
		UIDEBUG._BG_SET_TEXT_COLOR(r, g, b, a);

		ulong inner = Function.Call<ulong>(VarStringHash, new InputArgument[3]
		{
			10,
			new InputArgument(PinString("LITERAL_STRING")),
			new InputArgument(PinString(wrapped))
		});
		ulong colored = Function.Call<ulong>(VarStringHash, new InputArgument[4]
		{
			42,
			new InputArgument(PinString("COLOR_STRING")),
			0,
			new InputArgument(inner)
		});
		Function.Call(BgDisplayTextHash, new InputArgument(colored), x, y);
	}

	public static List<string> WrapLines(string text, int maxCharsPerLine = 68, int maxLines = 4)
	{
		List<string> lines = new List<string>();
		if (string.IsNullOrWhiteSpace(text))
		{
			lines.Add("...");
			return lines;
		}

		string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		StringBuilder current = new StringBuilder();

		foreach (string w in words)
		{
			if (current.Length + w.Length + 1 > maxCharsPerLine && current.Length > 0)
			{
				lines.Add(current.ToString());
				current.Clear();
				if (lines.Count >= maxLines - 1)
				{
					current.Append(w);
					continue;
				}
			}
			if (current.Length > 0)
			{
				current.Append(' ');
			}
			current.Append(w);
		}

		if (current.Length > 0 && lines.Count < maxLines)
		{
			lines.Add(current.ToString());
		}
		return lines;
	}
}
