using ITKamianets.Engine.Shell;
using UnityEngine;

namespace Temnorid.Craft
{
	/// <summary>What the shared shell (splash, landing, lobby, top and bottom bars) needs to know about this game.</summary>
	public static class CraftShell
	{
		public const string Repo = "WebArtWork/craft.temnorid.com";

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void Configure()
		{
			ShellConfig.Current = new ShellConfig
			{
				Title = "Temnorid Craft",
				Tagline = "Gather. Carry. Store.",
				GameId = "craft",
				Repo = Repo,
				InstallerSuffix = "-Setup.exe",
				MaxPlayers = 4,
				Multiplayer = true,
				GameScene = "Main",
				Accent = new Color(0.55f, 0.85f, 0.4f),
				Places =
				{
					new ShellPlace("world", "Settlement", "S"),
					new ShellPlace("stock", "Stock", "B"),
				},
			};
		}
	}
}
