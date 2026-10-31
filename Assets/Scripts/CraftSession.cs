using System;
using ITKamianets.Engine.Inventory;
using ITKamianets.Engine.Save;

namespace Temnorid.Craft
{
	/// <summary>The kinds of resource in the settlement. Ids are what is stored in the inventories and the save.</summary>
	public static class Items
	{
		public const string Wood = "wood";
		public const string Stone = "stone";
		public const string Iron = "iron";

		public static readonly string[] All = { Wood, Stone, Iron };

		public static string Name(string itemId)
		{
			switch (itemId)
			{
				case Wood: return "Wood";
				case Stone: return "Stone";
				case Iron: return "Iron";
				default: return itemId;
			}
		}
	}

	/// <summary>What is kept between sessions.</summary>
	[Serializable]
	public sealed class CraftSave
	{
		public InventoryData backpack;
		public InventoryData warehouse;
		public bool goalReached;
	}

	/// <summary>
	/// The rules of Craft with no Unity in them: a small backpack, a big warehouse, gathering into the
	/// backpack, storing into the warehouse, and the goal of having enough of every resource stored.
	/// </summary>
	public sealed class CraftSession
	{
		/// <summary>Slots in the backpack; each holds one kind of resource.</summary>
		public const int BackpackSlots = 3;
		public const int BackpackStack = 8;
		public const int WarehouseSlots = 12;
		public const int WarehouseStack = 999;
		/// <summary>How much of each resource must be stored to reach the goal.</summary>
		public const int DefaultGoal = 10;

		public ItemContainer Backpack { get; } = new ItemContainer(BackpackSlots, BackpackStack);
		public ItemContainer Warehouse { get; } = new ItemContainer(WarehouseSlots, WarehouseStack);
		public int Goal { get; }
		public bool GoalReached { get; internal set; }

		public CraftSession(int goal = DefaultGoal)
		{
			Goal = goal;
		}

		/// <summary>Puts gathered resources in the backpack. Returns how many fitted (0 when it is full).</summary>
		public int Gather(string itemId, int amount) => Backpack.AddUpTo(itemId, amount);

		/// <summary>
		/// Stores everything in the backpack that fits in the warehouse. Returns how many items were stored;
		/// <paramref name="goalJustReached"/> is true only the first time the goal is met.
		/// </summary>
		public int Deposit(out bool goalJustReached)
		{
			var moved = ItemContainer.TransferAll(Backpack, Warehouse);
			goalJustReached = CheckGoal();
			return moved;
		}

		/// <summary>Stores a number of one resource in the warehouse (a friend's load, online). Returns how many fitted.</summary>
		public int StoreInWarehouse(string itemId, int amount) => Warehouse.AddUpTo(itemId, amount);

		/// <summary>True only the first time the goal is met.</summary>
		public bool CheckGoal()
		{
			if (GoalReached || !MeetsGoal()) return false;
			GoalReached = true;
			return true;
		}

		/// <summary>Online client: makes the warehouse show what the host reports for one resource.</summary>
		public void MirrorWarehouse(string itemId, int count)
		{
			var have = Warehouse.Count(itemId);
			if (count > have) Warehouse.AddUpTo(itemId, count - have);
			else if (count < have) Warehouse.TryRemove(itemId, have - count);
		}

		public bool MeetsGoal()
		{
			foreach (var id in Items.All) if (Warehouse.Count(id) < Goal) return false;
			return true;
		}

		public CraftSave ToSave() => new CraftSave { backpack = Backpack.ToData(), warehouse = Warehouse.ToData(), goalReached = GoalReached };

		public void Load(CraftSave save)
		{
			if (save == null) return;
			Backpack.Load(save.backpack);
			Warehouse.Load(save.warehouse);
			GoalReached = save.goalReached;
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
