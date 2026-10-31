using System.Collections;
using System.IO;
using NUnit.Framework;
using Temnorid.Village;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Craft.Tests
{
	public class SettlementTests
	{
		string _saveFolder;

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "craft-playmode-" + System.Guid.NewGuid());
			CraftGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			CraftGame.SaveRootOverride = null;
			CraftGame.VillageLoader = null;
			Time.timeScale = 1f;
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		[UnityTest]
		public IEnumerator The_settlement_is_the_standard_village_with_a_farm_a_warehouse_and_a_worker_when_nobody_is_signed_in()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;

			var game = Object.FindAnyObjectByType<CraftGame>();
			Assert.IsTrue(game.HasVillage, "the standard village is there in the first frame");
			Assert.IsFalse(game.Loading);
			Assert.IsFalse(game.Village.IsLords);
			Assert.AreEqual(StockMode.Local, game.Village.Stock.Mode);
			Assert.AreEqual(12, game.View.Plots.Count);

			var counts = new System.Collections.Generic.Dictionary<string, int>();
			foreach (var node in ResourceNode.All)
			{
				counts.TryGetValue(node.ItemId, out var n);
				counts[node.ItemId] = n + 1;
			}
			foreach (var id in Items.All) Assert.AreEqual(2, counts.ContainsKey(id) ? counts[id] : 0, "two plots of " + id);

			Assert.IsNotNull(Warehouse.Instance, "a warehouse");
			Assert.AreSame(game.View.StoreBuilding, Warehouse.Instance.transform, "the village's warehouse is the place to store");
			var worker = Object.FindAnyObjectByType<PlayerWorker>();
			Assert.IsNotNull(worker, "a worker");
			Assert.IsNotNull(Camera.main, "a camera");
			Assert.Less((worker.transform.position - game.View.StorePosition).magnitude, 12f, "the worker starts near the stores");
			foreach (var id in Items.All) Assert.AreEqual(750, game.Session.Stock.Count(id), id + " starts at Lords' start stock");
		}

		[UnityTest]
		public IEnumerator A_plots_harvest_grows_with_its_level()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;

			var game = Object.FindAnyObjectByType<CraftGame>();
			foreach (var plot in game.View.Plots)
			{
				var node = plot.Root.GetComponent<ResourceNode>();
				Assert.AreEqual(CraftSession.AmountForLevel(plot.Data.Level), node.AmountPerGather, "plot " + plot.Id);
				Assert.AreEqual(plot.Data.Resource, node.ItemId);
			}
			var low = game.View.Plots[0].Root.GetComponent<ResourceNode>();
			var high = game.View.Plots[1].Root.GetComponent<ResourceNode>();
			Assert.AreNotEqual(low.AmountPerGather, high.AmountPerGather, "levels 2 and 1 give different harvests");
		}

		[UnityTest]
		public IEnumerator Gathering_from_every_plot_and_storing_it_adds_to_the_stock_and_reaches_the_goal()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;

			var game = Object.FindAnyObjectByType<CraftGame>();
			game.SaveEnabled = false;

			// Walk the whole farm: gather from each plot until it is used up, storing whenever the backpack is full.
			foreach (var node in ResourceNode.All.ToArray())
			{
				while (node.Available)
				{
					if (game.Session.Backpack.SpaceFor(node.ItemId) <= 0) game.Deposit();
					game.Gather(node);
				}
			}
			game.Deposit();

			foreach (var id in Items.All)
			{
				Assert.GreaterOrEqual(game.Session.StoredThisVisit(id), CraftSession.DefaultGoal, id + " stored this visit");
				Assert.AreEqual(750 + game.Session.StoredThisVisit(id), game.Session.Stock.Count(id), id + ": the stock went up by what was stored");
			}
			Assert.IsTrue(game.Session.GoalReached, "the goal is reached");
		}

		[UnityTest]
		public IEnumerator The_standard_villages_stock_and_backpack_survive_a_restart()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			var game = Object.FindAnyObjectByType<CraftGame>();
			var plot = ResourceNode.All[0];
			var item = plot.ItemId;
			game.Gather(plot);
			game.Deposit();
			var wood = game.Session.Stock.Count(Items.Wood);
			game.Gather(ResourceNode.All.ToArray()[0]);
			var carried = game.Session.Backpack.TotalItems;
			game.Save();
			var stored = game.Session.Stock.Count(item);

			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			var again = Object.FindAnyObjectByType<CraftGame>();
			Assert.AreEqual(stored, again.Session.Stock.Count(item), "the local stock was kept in Craft's own save");
			Assert.AreEqual(carried, again.Session.Backpack.TotalItems, "and the backpack");
			Assert.GreaterOrEqual(wood, 750);
		}
	}
}
