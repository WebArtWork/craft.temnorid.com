using System.Collections;
using System.IO;
using NUnit.Framework;
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
			Time.timeScale = 1f;
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		[UnityTest]
		public IEnumerator The_settlement_has_every_resource_a_warehouse_and_a_worker()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;

			var counts = new System.Collections.Generic.Dictionary<string, int>();
			foreach (var node in ResourceNode.All)
			{
				counts.TryGetValue(node.ItemId, out var n);
				counts[node.ItemId] = n + 1;
			}
			foreach (var id in Items.All) Assert.GreaterOrEqual(counts.ContainsKey(id) ? counts[id] : 0, 3, "enough " + id + " nodes");

			Assert.IsNotNull(Warehouse.Instance, "a warehouse");
			Assert.IsNotNull(Object.FindAnyObjectByType<PlayerWorker>(), "a worker");
			Assert.IsNotNull(Camera.main, "a camera");
		}

		[UnityTest]
		public IEnumerator Gathering_from_every_node_and_storing_it_reaches_the_goal()
		{
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;

			var game = Object.FindAnyObjectByType<CraftGame>();
			game.SaveEnabled = false;

			// Walk the whole settlement: gather from each node until it is used up, storing whenever the backpack is full.
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
				Assert.GreaterOrEqual(game.Session.Warehouse.Count(id), CraftSession.DefaultGoal, id + " stored");
			Assert.IsTrue(game.Session.GoalReached, "the goal is reached");
		}
	}
}
