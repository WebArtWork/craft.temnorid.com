using System;
using System.Collections.Generic;
using ITKamianets.Engine.Inventory;
using ITKamianets.Engine.Save;
using Temnorid.Village;

namespace Temnorid.Craft
{
	/// <summary>The kinds of resource in the settlement. Ids are what is stored in the inventories and the save.</summary>
	public static class Items
	{
		public const string Wood = "wood";
		public const string Stone = "stone";
		public const string Iron = "iron";
		public const string Water = "water";
		public const string Grain = "grain";
		public const string Clay = "clay";

		/// <summary>The same six resources, with the same ids and order, as the Lords game (resource.data.ts) and <see cref="Res.All"/>.</summary>
		public static readonly string[] All = { Wood, Stone, Iron, Water, Grain, Clay };

		public static string Name(string itemId)
		{
			switch (itemId)
			{
				case Wood: return "Wood";
				case Stone: return "Stone";
				case Iron: return "Iron";
				case Water: return "Water";
				case Grain: return "Grain";
				case Clay: return "Clay";
				default: return itemId;
			}
		}
	}

	/// <summary>What is kept between sessions.</summary>
	[Serializable]
	public sealed class CraftSave
	{
		public InventoryData backpack;
		/// <summary>Version 0.3.0 and earlier: Craft's own warehouse. It is still read (so old saves load) but no longer used: the stock is Lords' (750 of each to start with).</summary>
		public InventoryData warehouse;
		/// <summary>Version 0.3.0 and earlier. The goal now counts what is stored in the current visit, so this is read and ignored.</summary>
		public bool goalReached;
		/// <summary>The stock of the standard village, for a guest or an offline player. Not written for a Lords village: its stock lives in Lords.</summary>
		public Dictionary<string, double> stock;
	}

	/// <summary>
	/// The rules of Craft with no Unity in them: a small backpack, gathering into the backpack, storing into the lord's stock (the
	/// <see cref="SharedStock"/> of the village: the same stock Lords has), and the goal of storing enough of every resource in this visit.
	/// </summary>
	public sealed class CraftSession
	{
		/// <summary>Slots in the backpack; each holds one kind of resource. One slot per kind, so all six kinds can be carried at once.</summary>
		public const int BackpackSlots = 6;
		public const int BackpackStack = 8;
		/// <summary>How much of each resource must be stored in one visit to reach the goal. The stock itself starts at 750 of each, so the goal counts what Craft added.</summary>
		public const int DefaultGoal = 10;
		/// <summary>The most a single gather yields.</summary>
		public const int MaxGather = BackpackStack;

		readonly Dictionary<string, int> _stored = new Dictionary<string, int>();
		bool _mirroredGoal;

		public ItemContainer Backpack { get; } = new ItemContainer(BackpackSlots, BackpackStack);
		/// <summary>The lord's stores. Until a village is loaded this is the standard village's local stock.</summary>
		public SharedStock Stock { get; private set; }
		public int Goal { get; }
		public bool GoalReached { get; internal set; }

		public CraftSession(int goal = DefaultGoal)
		{
			Goal = goal;
			Stock = VillageSource.CreateDefault(null, "", SharedStock.SystemClock()).Stock;
		}

		/// <summary>Uses the stock of the village that was loaded.</summary>
		public void UseStock(SharedStock stock)
		{
			Stock = stock;
		}

		/// <summary>
		/// What one gather of a plot yields: 2, plus one for every two levels, at most <see cref="MaxGather"/>. A level 1 plot gives 2, level 2 gives 3,
		/// level 4 gives 4. (Lords' plots yield 2, 5, 9, 15... an hour; Craft only scales the size of a harvest, not the hourly yield.)
		/// </summary>
		public static int AmountForLevel(int level) => Math.Min(MaxGather, 2 + Math.Max(0, level) / 2);

		/// <summary>Puts gathered resources in the backpack. Returns how many fitted (0 when it is full).</summary>
		public int Gather(string itemId, int amount) => Backpack.AddUpTo(itemId, amount);

		/// <summary>How many of a resource were stored in this visit (by this player, or online by anyone).</summary>
		public int StoredThisVisit(string itemId) => _stored.TryGetValue(itemId, out var n) ? n : 0;

		public int TotalStoredThisVisit
		{
			get
			{
				var total = 0;
				foreach (var id in Items.All) total += StoredThisVisit(id);
				return total;
			}
		}

		/// <summary>
		/// Stores everything in the backpack that fits under the stores' capacity; what does not fit stays in the backpack. Returns how many
		/// items were stored; <paramref name="goalJustReached"/> is true only the first time the goal is met.
		/// </summary>
		public int Deposit(out bool goalJustReached)
		{
			var moved = 0;
			foreach (var id in Items.All)
			{
				var carried = Backpack.Count(id);
				if (carried <= 0) continue;
				var stored = StoreInStock(id, carried);
				if (stored > 0) Backpack.TryRemove(id, stored);
				moved += stored;
			}
			goalJustReached = CheckGoal();
			return moved;
		}

		/// <summary>Stores a number of one resource in the stock (a friend's load, online). Returns how many fitted under the capacity.</summary>
		public int StoreInStock(string itemId, int amount)
		{
			var stored = Stock.Add(itemId, amount);
			if (stored > 0) _stored[itemId] = StoredThisVisit(itemId) + stored;
			return stored;
		}

		/// <summary>True only the first time the goal is met.</summary>
		public bool CheckGoal()
		{
			if (GoalReached || !MeetsGoal()) return false;
			GoalReached = true;
			return true;
		}

		public bool MeetsGoal()
		{
			if (_mirroredGoal) return true;
			foreach (var id in Items.All) if (StoredThisVisit(id) < Goal) return false;
			return true;
		}

		/// <summary>Online client: the host says whether the goal has been reached this visit.</summary>
		public void MirrorGoal(bool reached)
		{
			if (reached) _mirroredGoal = true;
		}

		/// <summary>Online client: the stock as the host reports it.</summary>
		public void MirrorStock(string itemId, int count) => Stock.SetMirrored(itemId, count);

		public CraftSave ToSave()
		{
			var save = new CraftSave { backpack = Backpack.ToData() };
			// Only a local stock is kept here; a Lords village keeps it in Lords.
			if (Stock.Mode == StockMode.Local)
			{
				save.stock = new Dictionary<string, double>();
				foreach (var id in Items.All) save.stock[id] = Stock.Get(id);
			}
			return save;
		}

		/// <summary>The stock saved by the standard village, or null when the save has none (a new or an old save).</summary>
		public static Amounts LocalStockOf(CraftSave save)
		{
			if (save == null || save.stock == null || save.stock.Count == 0) return null;
			var amounts = DefaultVillage.StartAmounts();
			foreach (var pair in save.stock)
				if (Res.IsKnown(pair.Key) && !double.IsNaN(pair.Value) && !double.IsInfinity(pair.Value) && pair.Value >= 0) amounts[pair.Key] = pair.Value;
			return amounts;
		}

		public void Load(CraftSave save)
		{
			if (save == null) return;
			Backpack.Load(save.backpack);
			// save.warehouse and save.goalReached belong to older versions and are ignored.
		}
	}

	/// <summary>Reads and writes the <see cref="CraftSave"/> in the game's save slot.</summary>
	public sealed class CraftSaveStore
	{
		const string Slot = "default";
		const int PayloadVersion = 1;

		readonly SaveStore _store;

		public CraftSaveStore(string rootDirectory = null)
		{
			_store = new SaveStore("craft", rootDirectory);
		}

		/// <summary>The saved game, or null when there is none yet or it cannot be read.</summary>
		public CraftSave Load()
		{
			var result = _store.Load<CraftSave>(Slot, PayloadVersion);
			return result.Ok ? result.Value : null;
		}

		public void Save(CraftSave save)
		{
			_store.Save(Slot, save, PayloadVersion);
		}
	}
}
