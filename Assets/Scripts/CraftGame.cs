using ITKamianets.Engine.Core;
using ITKamianets.Engine.Input;
using UnityEngine;

namespace Temnorid.Craft
{
	/// <summary>
	/// Holds the game's state for the scene: the session (backpack, warehouse, goal), the messages shown
	/// to the player, pausing, and saving. Everything the player does ends up as a call here.
	/// </summary>
	public sealed class CraftGame : MonoBehaviour
	{
		/// <summary>Where saves go instead of the persistent data path. Tests set it before loading the scene.</summary>
		public static string SaveRootOverride;

		public bool SaveEnabled = true;

		const string Log = "Craft";

		CraftSaveStore _store;

		public CraftSession Session { get; private set; }
		public bool Paused { get; private set; }

		/// <summary>A short message for the player, and until when to show it.</summary>
		public string ToastText { get; private set; } = "";
		public float ToastUntil { get; private set; }

		/// <summary>The goal banner is shown until this time.</summary>
		public float BannerUntil { get; private set; }

		void Awake()
		{
			Session = new CraftSession();
			_store = new CraftSaveStore(SaveRootOverride);
			var saved = _store.Load();
			if (saved != null) Session.Load(saved);
		}

		void Update()
		{
			if (EngineInput.Default.UI.Pause.WasPressedThisFrame()) SetPaused(!Paused);
		}

		void OnApplicationQuit() => Save();

		public void SetPaused(bool paused)
		{
			Paused = paused;
			Time.timeScale = paused ? 0f : 1f;
			EngineInput.Default.PlayerEnabled = !paused;
			if (paused) Save();
		}

		/// <summary>The player has gathered from a node: its resources go to the backpack.</summary>
		public void Gather(ResourceNode node)
		{
			var added = Session.Gather(node.ItemId, node.AmountPerGather);
			if (added < 1)
			{
				ShowToast("Backpack full");
				return;
			}
			node.Harvest();
			ShowToast($"+{added} {node.DisplayName}");
		}

		/// <summary>The player stores the backpack in the warehouse.</summary>
		public void Deposit()
		{
			var moved = Session.Deposit(out var goalReached);
			if (moved > 0)
			{
				ShowToast($"Stored {moved} items");
				Save();
			}
			if (goalReached)
			{
				BannerUntil = Time.unscaledTime + 6f;
				EngineLog.Info(Log, "Goal reached: every resource is stored in the warehouse.");
			}
		}

		public void ShowToast(string text)
		{
			ToastText = text;
			ToastUntil = Time.unscaledTime + 2.5f;
		}

		public void Save()
		{
			if (SaveEnabled && Session != null) _store.Save(Session.ToSave());
		}

		public void SaveAndQuit()
		{
			Save();
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#else
			Application.Quit();
#endif
		}
	}
}
