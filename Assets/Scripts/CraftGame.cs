using System;
using System.Threading.Tasks;
using ITKamianets.Engine.Account;
using ITKamianets.Engine.Core;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using Temnorid.Village;
using UnityEngine;

namespace Temnorid.Craft
{
	/// <summary>
	/// Holds the game's state for the scene: the session (backpack, the lord's stock, goal), the village the world is drawn from, the
	/// messages shown to the player, pausing, and saving. Everything the player does ends up as a call here.
	/// <para>
	/// <b>The world is the player's Lords village and its farm.</b> A Google player with a Lords save sees their own village and the stock is
	/// the shared Lords stock (see <see cref="StockSyncer"/>: only the <c>stock</c> field of the save is ever written); a guest, a signed-out or
	/// offline player, or someone with no Lords save sees the standard village and keeps the stock in Craft's own save. The farm plots are the
	/// gather spots (<see cref="ResourceNode"/>) and the village's warehouse is the place to store.
	/// </para>
	/// <para>
	/// Online (<see cref="CraftNet"/>) the world is shared: the host's village is shown to everyone (the host sends it as one event), the host
	/// owns the stock and is the only one that writes to Lords, a guest's gather and store are requests to the host, and each person's backpack
	/// stays with that person. A guest never loads a village of its own and never reads or writes the single-player save.
	/// </para>
	/// </summary>
	public sealed class CraftGame : MonoBehaviour
	{
		/// <summary>Where saves go instead of the persistent data path. Tests set it before loading the scene.</summary>
		public static string SaveRootOverride;

		/// <summary>Tests (and anything that wants another source) set this to load the village their own way; null uses the signed-in account.</summary>
		public static Func<Amounts, Task<VillageSession>> VillageLoader;

		public bool SaveEnabled = true;

		const string Log = "Craft";
		/// <summary>How long to wait for a sign-in that is still running when the scene starts.</summary>
		const float AccountWaitSeconds = 3f;

		CraftSaveStore _store;
		bool _goalKnown;
		StockSyncer _syncer;
		bool _villageApplied;
		string _hostVillageJson;

		public CraftSession Session { get; private set; }
		public bool Paused { get; private set; }

		/// <summary>Another place of the shell (stock, profile) is open over the settlement.</summary>
		public bool UiHold { get; private set; }

		/// <summary>The village is still loading: the game waits for it, but only for a few seconds.</summary>
		public bool Loading { get; private set; }

		/// <summary>The village and its stock; null while it loads.</summary>
		public VillageSession Village { get; private set; }

		/// <summary>The drawn village.</summary>
		public VillageView View { get; private set; }

		/// <summary>The link to other players, or null when playing alone.</summary>
		public CraftNet Net { get; private set; }

		/// <summary>A short message for the player, and until when to show it.</summary>
		public string ToastText { get; private set; } = "";
		public float ToastUntil { get; private set; }

		/// <summary>The goal banner is shown until this time.</summary>
		public float BannerUntil { get; private set; }

		/// <summary>Raised when the village has been drawn and the nodes made.</summary>
		public event Action VillageReady;

		void Awake()
		{
			Time.timeScale = 1f;
			EngineInput.Default.PlayerEnabled = true;

			Session = new CraftSession();
			_store = new CraftSaveStore(SaveRootOverride);
			View = FindAnyObjectByType<VillageView>();
			var net = NetSession.Instance;
			Amounts local = null;
			if (net.IsClient)
			{
				// A guest plays in somebody else's world: its own single-player save is neither read nor written,
				// and the village comes from the host.
				SaveEnabled = false;
			}
			else
			{
				var saved = _store.Load();
				if (saved != null)
				{
					Session.Load(saved);
					local = CraftSession.LocalStockOf(saved);
				}
			}
			if (net.IsOnline) Net = gameObject.AddComponent<CraftNet>().Init(this);

			if (net.IsClient)
			{
				SetLoading(true);
			}
			else if (NeedsNetwork())
			{
				SetLoading(true);
				StartCoroutine(LoadVillage(local));
			}
			else
			{
				// Nothing to wait for (guest, signed out, tests): the standard village is there in the first frame.
				var reason = GameAccount.IsSignedIn ? "Guest account: showing the standard village. Sign in with Google to see your own." : "Not signed in: showing the standard village.";
				ApplyVillage(VillageSource.CreateDefault(local, reason, SharedStock.SystemClock()));
			}
		}

		// A Google player's village has to be read from Lords; a test may bring its own loader.
		static bool NeedsNetwork() => VillageLoader != null || (GameAccount.IsSignedIn && !GameAccount.IsGuest) || GameAccount.Busy;

		System.Collections.IEnumerator LoadVillage(Amounts local)
		{
			// A sign-in that is still running (the shell restores the account in the background) gets a few seconds; never longer.
			var end = Time.unscaledTime + AccountWaitSeconds;
			while (GameAccount.Busy && Time.unscaledTime < end) yield return null;

			Task<VillageSession> task;
			try
			{
				task = VillageLoader != null ? VillageLoader(local) : VillageSource.ForAccount().LoadAsync(local);
			}
			catch (Exception exception)
			{
				task = Task.FromResult(VillageSource.CreateDefault(local, "Could not load the village (" + exception.GetType().Name + "): showing the standard village.", SharedStock.SystemClock()));
			}
			while (!task.IsCompleted) yield return null;
			var session = task.IsFaulted || task.IsCanceled
				? VillageSource.CreateDefault(local, "Could not load the village: showing the standard village.", SharedStock.SystemClock())
				: task.Result;
			ApplyVillage(session);
		}

		void SetLoading(bool loading)
		{
			Loading = loading;
			ApplyHold();
		}

		// ---------------------------------------------------------------- the village

		/// <summary>Draws the village, makes the gather spots and the warehouse, and starts the stock's link to Lords.</summary>
		void ApplyVillage(VillageSession session)
		{
			Village = session;
			Session.UseStock(session.Stock);
			if (View == null) View = new GameObject("Village").AddComponent<VillageView>();
			View.Build(session.Data);
			MakeNodes();
			PlacePlayer();

			if (session.Stock.IsShared)
			{
				_syncer = gameObject.AddComponent<StockSyncer>();
				_syncer.Stock = session.Stock;
			}
			EngineLog.Info(Log, session.IsLords ? "Showing the player's Lords village." : "Showing the standard village.");
			_villageApplied = true;
			SetLoading(false);
			if (Net != null) Net.VillageReady();
			VillageReady?.Invoke();
		}

		/// <summary>Online guest: the host's village arrived.</summary>
		public void ApplyClientVillage(VillageData data)
		{
			if (_villageApplied) return;
			ApplyVillage(VillageSession.ForClient(data, data.IsLords ? "The host's Lords village." : "The host's standard village."));
		}

		/// <summary>The village data to send to a guest, once and for all.</summary>
		public string VillageJson => Village == null ? null : _hostVillageJson ?? (_hostVillageJson = Village.Data.ToJson());

		/// <summary>True once a village is drawn.</summary>
		public bool HasVillage => _villageApplied;

		void MakeNodes()
		{
			// Every plot of the farm is a gather spot: one resource each, a harvest that grows with the plot's level.
			foreach (var plot in View.Plots)
			{
				var node = plot.Root.gameObject.AddComponent<ResourceNode>();
				node.ItemId = plot.Data.Resource;
				node.AmountPerGather = CraftSession.AmountForLevel(plot.Data.Level);
				node.GatherSeconds = GatherSeconds(plot.Data.Resource);
				node.Harvests = 3;
				node.Visual = plot.Visual;
			}
			var store = View.StoreBuilding;
			if (store != null && store.GetComponent<Warehouse>() == null) store.gameObject.AddComponent<Warehouse>();
		}

		static float GatherSeconds(string resource)
		{
			switch (resource)
			{
				case Items.Iron: return 1.6f;
				case Items.Clay: return 1.4f;
				case Items.Water: case Items.Grain: return 1f;
				default: return 1.2f;
			}
		}

		void PlacePlayer()
		{
			var worker = FindAnyObjectByType<PlayerWorker>();
			if (worker == null) return;
			var net = NetSession.Instance;
			var slot = 0;
			var count = 1;
			if (net.IsOnline)
			{
				var players = net.Players;
				count = Mathf.Max(1, players.Count);
				for (var i = 0; i < players.Count; i++) if (players[i].Id == net.LocalId) slot = i;
			}
			// Workers start side by side, in the order of the roster (the host first).
			var spot = View.SpawnPosition + Vector3.right * ((slot - (count - 1) * 0.5f) * 1.6f);
			var controller = worker.GetComponent<CharacterController>();
			if (controller != null) controller.enabled = false;
			worker.transform.position = spot;
			if (controller != null) controller.enabled = true;
		}

		// ---------------------------------------------------------------- unity

		void OnEnable() => ShellFlow.BeforeQuit += Leave;

		void OnDisable() => ShellFlow.BeforeQuit -= Leave;

		void Update()
		{
			if (!UiHold && !Loading && EngineInput.Default.UI.Pause.WasPressedThisFrame()) SetPaused(!Paused);
		}

		void OnApplicationQuit() => Leave();

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
			EngineInput.Default.PlayerEnabled = !hold && !Loading;
		}

		// ---------------------------------------------------------------- doing things

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

		/// <summary>The player stores the backpack in the stores (online guest: the host is asked).</summary>
		public void Deposit()
		{
			if (Net != null && Net.IsClient)
			{
				Net.RequestDeposit();
				return;
			}
			var moved = Session.Deposit(out var goalReached);
			var left = Session.Backpack.TotalItems;
			if (moved > 0)
			{
				ShowToast(left > 0 ? $"Stored {moved} items. The stores are full: you keep {left}." : $"Stored {moved} items");
				Save();
			}
			else if (left > 0)
			{
				ShowToast("The stores are full: you keep your load.");
			}
			if (goalReached) GoalReachedNow();
		}

		/// <summary>The goal has just been met (here, or the host says so).</summary>
		public void GoalReachedNow()
		{
			BannerUntil = Time.unscaledTime + 6f;
			EngineLog.Info(Log, "Goal reached: enough of every resource was stored this visit.");
		}

		/// <summary>Online client: the stock as the host reports it for one resource.</summary>
		public void MirrorStock(string itemId, int count, bool goalReached)
		{
			Session.MirrorStock(itemId, count);
			if (goalReached && !Session.GoalReached)
			{
				Session.MirrorGoal(true);
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

		/// <summary>Saves the backpack and, for the standard village, the stock. A Lords stock is written by <see cref="StockSyncer"/>, not here.</summary>
		public void Save()
		{
			if (SaveEnabled && Session != null) _store.Save(Session.ToSave());
		}

		/// <summary>Saves, and writes what is pending to Lords (best effort): on leaving the game, pausing the app or quitting.</summary>
		public void Leave()
		{
			Save();
			if (_syncer != null) _syncer.Flush();
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
