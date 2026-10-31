using System.Collections.Generic;
using System.IO;
using ITKamianets.Engine.Inventory;
using NUnit.Framework;
using Temnorid.Village;

namespace Temnorid.Craft.Tests
{
	public class CraftSessionTests
	{
		[Test]
		public void The_backpack_holds_less_than_the_goal_so_one_trip_is_not_enough()
		{
			var session = new CraftSession();
			var capacity = CraftSession.BackpackSlots * CraftSession.BackpackStack;
			Assert.Less(capacity, CraftSession.DefaultGoal * Items.All.Length);

			Assert.AreEqual(8, session.Gather(Items.Wood, 8));
			Assert.AreEqual(40, session.Backpack.SpaceFor(Items.Wood), "five empty slots are left for more wood");
		}

		[Test]
		public void A_full_backpack_cannot_gather_more()
		{
			var session = new CraftSession();
			foreach (var id in Items.All) session.Gather(id, 8);
			Assert.AreEqual(48, session.Backpack.TotalItems);
			Assert.AreEqual(0, session.Gather(Items.Wood, 2));
			Assert.AreEqual(0, session.Gather(Items.Clay, 2));
		}

		[Test]
		public void The_six_resources_match_the_ids_of_Lords_and_of_the_village_package()
		{
			CollectionAssert.AreEqual(new[] { "wood", "stone", "iron", "water", "grain", "clay" }, Items.All);
			CollectionAssert.AreEqual(Res.All, Items.All, "Craft and the village package agree on ids and order");
			CollectionAssert.AreEqual(new[] { "Wood", "Stone", "Iron", "Water", "Grain", "Clay" }, System.Array.ConvertAll(Items.All, Items.Name));
			Assert.GreaterOrEqual(CraftSession.BackpackSlots, Items.All.Length, "one backpack slot per kind");
		}

		[Test]
		public void A_new_session_has_the_standard_village_stock_of_750_of_each()
		{
			var session = new CraftSession();
			foreach (var id in Items.All) Assert.AreEqual(750, session.Stock.Count(id), id);
			Assert.AreEqual(StockMode.Local, session.Stock.Mode);
			Assert.IsFalse(session.Stock.IsShared);
		}

		[Test]
		public void Depositing_empties_the_backpack_into_the_stock()
		{
			var session = new CraftSession();
			session.Gather(Items.Wood, 5);
			session.Gather(Items.Iron, 3);

			var moved = session.Deposit(out var goalReached);
			Assert.AreEqual(8, moved);
			Assert.IsFalse(goalReached);
			Assert.IsTrue(session.Backpack.IsEmpty);
			Assert.AreEqual(755, session.Stock.Count(Items.Wood));
			Assert.AreEqual(753, session.Stock.Count(Items.Iron));
			Assert.AreEqual(750, session.Stock.Count(Items.Stone), "untouched");
			Assert.AreEqual(5, session.StoredThisVisit(Items.Wood));
			Assert.AreEqual(8, session.TotalStoredThisVisit);
		}

		[Test]
		public void What_does_not_fit_under_the_capacity_stays_in_the_backpack()
		{
			var session = new CraftSession();
			// The standard village's well (level 1) holds 1200 water: fill it up, then carry 8 more.
			Assert.AreEqual(1200, session.Stock.Capacity(Items.Water));
			Assert.AreEqual(450, session.Stock.Add(Items.Water, 450));
			session.Gather(Items.Water, 8);
			session.Gather(Items.Wood, 2);
			var moved = session.Deposit(out _);
			Assert.AreEqual(2, moved, "only the wood fits");
			Assert.AreEqual(8, session.Backpack.Count(Items.Water), "the excess stays in the backpack");
			Assert.AreEqual(1200, session.Stock.Count(Items.Water));
			Assert.AreEqual(0, session.StoredThisVisit(Items.Water), "nothing of the water was stored by depositing");
		}

		[Test]
		public void The_goal_counts_what_was_stored_this_visit_and_is_reached_once()
		{
			var session = new CraftSession(10);
			for (var trip = 0; trip < 2; trip++)
			{
				foreach (var id in Items.All) session.Gather(id, 5);
				var moved = session.Deposit(out var reached);
				Assert.AreEqual(30, moved);
				Assert.AreEqual(trip == 1, reached, "only the second trip completes the goal");
			}
			Assert.IsTrue(session.GoalReached);
			foreach (var id in Items.All) Assert.AreEqual(760, session.Stock.Count(id), "750 in the stock to start with, 10 stored");

			session.Gather(Items.Wood, 4);
			session.Deposit(out var again);
			Assert.IsFalse(again, "the goal is announced only once");
			Assert.IsTrue(session.GoalReached, "and stays reached");
		}

		[Test]
		public void The_stock_already_being_full_of_750_does_not_reach_the_goal_by_itself()
		{
			var session = new CraftSession(10);
			Assert.IsFalse(session.MeetsGoal());
			Assert.IsFalse(session.CheckGoal());
		}

		[Test]
		public void The_goal_needs_all_six_resources()
		{
			var session = new CraftSession(10);
			session.Gather(Items.Wood, 8);
			session.Deposit(out _);
			session.Gather(Items.Wood, 8);
			session.Gather(Items.Stone, 8);
			session.Deposit(out var reached);
			Assert.IsFalse(reached, "no iron, water, grain or clay stored yet");
			Assert.IsFalse(session.MeetsGoal());
		}

		[Test]
		public void A_plot_yields_more_per_gather_as_its_level_grows_up_to_the_stack_size()
		{
			Assert.AreEqual(2, CraftSession.AmountForLevel(0));
			Assert.AreEqual(2, CraftSession.AmountForLevel(1));
			Assert.AreEqual(3, CraftSession.AmountForLevel(2));
			Assert.AreEqual(4, CraftSession.AmountForLevel(4));
			Assert.AreEqual(7, CraftSession.AmountForLevel(10));
			Assert.AreEqual(CraftSession.MaxGather, CraftSession.AmountForLevel(100));
			Assert.AreEqual(2, CraftSession.AmountForLevel(-3));
		}

		[Test]
		public void An_online_guest_mirrors_the_hosts_stock_and_goal_and_cannot_store_itself()
		{
			var session = new CraftSession(10);
			session.UseStock(SharedStock.ForMirror(Amounts.Filled(2400)));
			session.MirrorStock(Items.Wood, 812);
			Assert.AreEqual(812, session.Stock.Count(Items.Wood));
			Assert.AreEqual(2400, session.Stock.Capacity(Items.Wood));
			Assert.AreEqual(0, session.StoreInStock(Items.Wood, 5), "only the host changes the stock");
			Assert.IsFalse(session.MeetsGoal());
			session.MirrorGoal(true);
			Assert.IsTrue(session.MeetsGoal());
		}

		[Test]
		public void An_old_three_resource_save_loads_and_its_warehouse_and_goal_are_ignored()
		{
			var folder = Path.Combine(Path.GetTempPath(), "craft-oldsave-" + System.Guid.NewGuid());
			try
			{
				// Written the way version 0.2.0 did: a 3-slot backpack, 12-slot warehouse, three kinds.
				var old = new CraftSave
				{
					backpack = new InventoryData
					{
						slots = 3, stackLimit = 8,
						items = new List<ItemEntry>
						{
							new ItemEntry { itemId = "wood", quantity = 8 },
							new ItemEntry { itemId = "iron", quantity = 5 },
						},
					},
					warehouse = new InventoryData
					{
						slots = 12, stackLimit = 999,
						items = new List<ItemEntry>
						{
							new ItemEntry { itemId = "wood", quantity = 12 },
							new ItemEntry { itemId = "stone", quantity = 11 },
						},
					},
					goalReached = true,
				};
				new CraftSaveStore(folder).Save(old);

				var loaded = new CraftSaveStore(folder).Load();
				var session = new CraftSession();
				session.Load(loaded);
				Assert.AreEqual(8, session.Backpack.Count(Items.Wood));
				Assert.AreEqual(5, session.Backpack.Count(Items.Iron));
				Assert.IsFalse(session.GoalReached, "the goal belongs to the visit now");
				Assert.IsNull(CraftSession.LocalStockOf(loaded), "an old save has no stock: the village starts at 750");
				foreach (var id in Items.All) Assert.AreEqual(750, session.Stock.Count(id), id);
			}
			finally
			{
				if (Directory.Exists(folder)) Directory.Delete(folder, true);
			}
		}

		[Test]
		public void The_game_survives_a_restart_with_its_local_stock_and_a_damaged_save_starts_fresh()
		{
			var folder = Path.Combine(Path.GetTempPath(), "craft-test-" + System.Guid.NewGuid());
			try
			{
				var store = new CraftSaveStore(folder);
				Assert.IsNull(store.Load(), "nothing saved yet");

				var session = new CraftSession();
				session.Gather(Items.Wood, 6);
				session.Gather(Items.Stone, 2);
				session.Deposit(out _);
				session.Gather(Items.Iron, 4);
				var save = session.ToSave();
				Assert.IsNotNull(save.stock, "the standard village keeps its stock in Craft's own save");
				store.Save(save);

				var loaded = new CraftSaveStore(folder).Load();
				var restored = new CraftSession();
				restored.Load(loaded);
				var local = CraftSession.LocalStockOf(loaded);
				restored.UseStock(SharedStock.ForLocal(DefaultVillage.Create(0), local));
				Assert.AreEqual(756, restored.Stock.Count(Items.Wood));
				Assert.AreEqual(752, restored.Stock.Count(Items.Stone));
				Assert.AreEqual(750, restored.Stock.Count(Items.Clay));
				Assert.AreEqual(4, restored.Backpack.Count(Items.Iron), "the backpack is saved too");

				File.WriteAllText(Path.Combine(folder, "craft", "default.json"), "not json at all");
				Assert.IsNull(new CraftSaveStore(folder).Load(), "damaged file: start fresh");
				Assert.AreEqual(1, Directory.GetFiles(Path.Combine(folder, "craft"), "default.corrupt-*.json").Length, "the damaged file is kept");
			}
			finally
			{
				if (Directory.Exists(folder)) Directory.Delete(folder, true);
			}
		}

		[Test]
		public void A_lords_stock_is_not_kept_in_the_craft_save_and_a_bad_local_stock_is_ignored()
		{
			var progress = DefaultVillage.Create(1);
			var store = new InMemoryProgressStore(Newtonsoft.Json.Linq.JObject.FromObject(progress));
			var document = store.LoadAsync().GetAwaiter().GetResult();
			var session = new CraftSession();
			session.UseStock(SharedStock.ForLords(document.Progress, store));
			Assert.IsNull(session.ToSave().stock, "the stock lives in Lords");

			var damaged = new CraftSave { stock = new Dictionary<string, double> { { "wood", double.NaN }, { "stone", -5 }, { "gold", 9 }, { "iron", 100 } } };
			var amounts = CraftSession.LocalStockOf(damaged);
			Assert.AreEqual(750, amounts["wood"], "NaN is ignored");
			Assert.AreEqual(750, amounts["stone"], "a negative amount is ignored");
			Assert.AreEqual(100, amounts["iron"]);
		}
	}
}
