using ITKamianets.Engine.Core;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using UnityEngine;

namespace Temnorid.Craft
{
	/// <summary>
	/// Holds the game's state for the scene: the session (backpack, warehouse, goal), the messages shown
	/// to the player, pausing, and saving. Everything the player does ends up as a call here.
	/// <para>
	/// Online (<see cref="CraftNet"/>) the world is shared: the host owns the nodes and the warehouse, a guest's gather and store
	/// are requests to the host, and each person's backpack stays with that person. A guest never reads or writes the
	/// single-player save; the host saves the world it hosts in its own slot.
	/// </para>
	/// </summary>
	public sealed class CraftGame : MonoBehaviour
	{
		/// <summary>Where saves go instead of the persistent data path. Tests set it before loading the scene.</summary>
		public static string SaveRootOverride;

		public bool SaveEnabled = true;

		const string Log = "Craft";

		CraftSaveStore _store;
		bool _goalKnown;

		public CraftSession Session { get; private set; }
		public bool Paused { get; private set; }

		/// <summary>Another place of the shell (stock, profile) is open over the settlement.</summary>
		public bool UiHold { get; private set; }

		/// <summary>The link to other players, or null when playing alone.</summary>
		public CraftNet Net { get; private set; }

		/// <summary>A short message for the player, and until when to show it.</summary>
		public string ToastText { get; private set; } = "";
		public float ToastUntil { get; private set; }

		/// <summary>The goal banner is shown until this time.</summary>
		public float BannerUntil { get; private set; }

		void Awake()
		{
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;

			Session = new CraftSession();
			_store = new CraftSaveStore(SaveRootOverride);
			var net = NetSession.Instance;
			if (net.IsClient)
			{
				// A guest plays in somebody else's world: its own single-player save is neither read nor written.
				SaveEnabled = false;
			}
			else
			{
				var saved = _store.Load();
				if (saved != null) Session.Load(saved);
			}
			if (net.IsOnline) Net = gameObject.AddComponent<CraftNet>().Init(this);
		}

		void OnEnable() => ShellFlow.BeforeQuit += Save;

		void OnDisable() => ShellFlow.BeforeQuit -= Save;

		void Update()
		{
			if (!UiHold && EngineInput.Default.UI.Pause.WasPressedThisFrame()) SetPaused(!Paused);
		}

		void OnApplicationQuit() => Save();

		public void SetPaused(bool paused)
		{
			Paused = paused;
			ApplyHold();
			if (paused) Save();
		}

		/// <summary>Another place of the shell is open over the settlement.</summary>
		public void SetUiHold(bool hold)
		{
			if (UiHold == hold) return;
			UiHold = hold;
			ApplyHold();
		}

		// Online the world never stops for one person: the menus open over it.
		void ApplyHold()
		{
			var hold = Paused || UiHold;
			Time.timeScale = hold && Net == null ? 0f : 1f;
			EngineInput.Default.PlayerEnabled = !hold;
		}

		/// <summary>The player has gathered from a node: its resources go to the backpack (online guest: the host is asked).</summary>
		public void Gather(ResourceNode node)
		{
			if (Net != null && Net.IsClient)
			{
				Net.RequestGather(node);
				return;
			}
			var added = Session.Gather(node.ItemId, node.AmountPerGather);
			if (added < 1)
			{
				ShowToast("Backpack full");
				return;
			}
			node.Harvest();
			ShowToast($"+{added} {node.DisplayName}");
		}

		/// <summary>The player stores the backpack in the warehouse (online guest: the host is asked).</summary>
		public void Deposit()
		{
			if (Net != null && Net.IsClient)
			{
				Net.RequestDeposit();
				return;
			}
			var moved = Session.Deposit(out var goalReached);
			if (moved > 0)
			{
				ShowToast($"Stored {moved} items");
				Save();
			}
			if (goalReached) GoalReachedNow();
		}

		/// <summary>The goal has just been met (here, or the host says so).</summary>
		public void GoalReachedNow()
		{
			BannerUntil = Time.unscaledTime + 6f;
			EngineLog.Info(Log, "Goal reached: every resource is stored in the warehouse.");
		}

		/// <summary>Online client: the warehouse as the host reports it.</summary>
		public void MirrorWarehouse(string itemId, int count, bool goalReached)
		{
			Session.MirrorWarehouse(itemId, count);
			if (goalReached && !Session.GoalReached)
			{
				Session.GoalReached = true;
				if (_goalKnown) GoalReachedNow();
			}
			_goalKnown = true;
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

		/// <summary>Saves, then leaves for the landing screen (closing any online game).</summary>
		public void QuitToMenu()
		{
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;
			ShellFlow.QuitToMenu(); // saves first, through ShellFlow.BeforeQuit
		}
	}
}
