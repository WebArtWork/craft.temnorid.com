using System.IO;
using NUnit.Framework;

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
			Assert.AreEqual(16, session.Backpack.SpaceFor(Items.Wood), "two empty slots are left for more wood");
		}

		[Test]
		public void A_full_backpack_cannot_gather_more()
		{
			var session = new CraftSession();
			session.Gather(Items.Wood, 8);
			session.Gather(Items.Stone, 8);
			session.Gather(Items.Iron, 8);
			Assert.AreEqual(24, session.Backpack.TotalItems);
			Assert.AreEqual(0, session.Gather(Items.Wood, 2));
			Assert.AreEqual(0, session.Gather(Items.Iron, 2));
		}

		[Test]
		public void Depositing_empties_the_backpack_into_the_warehouse()
		{
			var session = new CraftSession();
			session.Gather(Items.Wood, 5);
			session.Gather(Items.Iron, 3);

			var moved = session.Deposit(out var goalReached);
			Assert.AreEqual(8, moved);
			Assert.IsFalse(goalReached);
			Assert.IsTrue(session.Backpack.IsEmpty);
			Assert.AreEqual(5, session.Warehouse.Count(Items.Wood));
			Assert.AreEqual(3, session.Warehouse.Count(Items.Iron));
		}

		[Test]
		public void The_goal_is_reached_once_when_every_resource_is_stored()
		{
			var session = new CraftSession(10);
			for (var trip = 0; trip < 2; trip++)
			{
				foreach (var id in Items.All) session.Gather(id, 5);
				var moved = session.Deposit(out var reached);
				Assert.AreEqual(15, moved);
				Assert.AreEqual(trip == 1, reached, "only the second trip completes the goal");
			}
			Assert.IsTrue(session.GoalReached);

			session.Gather(Items.Wood, 4);
			session.Deposit(out var again);
			Assert.IsFalse(again, "the goal is announced only once");
			Assert.IsTrue(session.GoalReached, "and stays reached");
		}

		[Test]
		public void The_goal_needs_all_three_resources()
		{
			var session = new CraftSession(10);
			session.Gather(Items.Wood, 8);
			session.Deposit(out _);
			session.Gather(Items.Wood, 8);
			session.Gather(Items.Stone, 8);
			session.Deposit(out var reached);
			Assert.IsFalse(reached, "no iron stored yet");
			Assert.IsFalse(session.MeetsGoal());
		}

		[Test]
		public void The_game_survives_a_restart_and_a_damaged_save_starts_fresh()
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
				store.Save(session.ToSave());

				var restored = new CraftSession();
				restored.Load(new CraftSaveStore(folder).Load());
				Assert.AreEqual(6, restored.Warehouse.Count(Items.Wood));
				Assert.AreEqual(2, restored.Warehouse.Count(Items.Stone));
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
	}
}
