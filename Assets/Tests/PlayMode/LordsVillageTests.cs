using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Temnorid.Village;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Craft.Tests
{
	/// <summary>
	/// The world is the player's Lords village when they are signed in with Google and have a Lords save, and the stock is the shared stock:
	/// everything here runs against a fake save in memory, never the real Firestore.
	/// </summary>
	public class LordsVillageTests
	{
		sealed class Account : IAccountView
		{
			public bool IsSignedIn { get; set; } = true;
			public bool IsGuest { get; set; }
		}

		string _saveFolder;

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "craft-lords-" + Guid.NewGuid());
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

		static IEnumerator Until(Func<bool> condition, string what, float seconds = 10f)
		{
			var end = Time.realtimeSinceStartup + seconds;
			while (!condition())
			{
				if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
				yield return null;
			}
		}

		/// <summary>A Lords save unlike the standard village: another lord, a wall, a higher granary, and 1000 wood saved just now.</summary>
		static JObject LordsDoc()
		{
			var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var progress = DefaultVillage.Create(now);
			progress.Lord = new LordInfo { Name = "Ann" };
			progress.Village.Buildings["-1"] = new VillageBuilding { Type = "wall", Level = 2 };
			progress.Village.Buildings["4"].Level = 4;
			progress.Stock.Amounts["wood"] = 1000;
			var doc = JObject.FromObject(progress);
			doc["hero"] = JObject.Parse("{\"name\":\"Hero\"}");
			doc["updatedAt"] = "2026-10-31T10:00:00Z";
			return doc;
		}

		static Func<Amounts, Task<VillageSession>> LoaderFor(IProgressStore store, bool guest = false) =>
			local => new VillageSource(store, new Account { IsGuest = guest }).LoadAsync(local);

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator A_google_player_sees_their_own_village_while_it_loads_and_the_stock_is_shared_with_Lords()
		{
			var store = new InMemoryProgressStore(LordsDoc());
			var original = (JObject)store.Data.DeepClone();
			var pending = new TaskCompletionSource<VillageSession>();
			CraftGame.VillageLoader = local => pending.Task;

			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
			var game = UnityEngine.Object.FindAnyObjectByType<CraftGame>();
			game.SaveEnabled = false;
			Assert.IsTrue(game.Loading, "waiting for the village");
			Assert.IsFalse(game.HasVillage);
			Assert.AreEqual(0, ResourceNode.All.Count, "no world yet");

			// The save arrives.
			var loaded = LoaderFor(store)(null);
			yield return Until(() => loaded.IsCompleted, "the fake save to load");
			pending.SetResult(loaded.Result);
			yield return Until(() => game.HasVillage, "the village to be drawn");
			Assert.IsFalse(game.Loading);
			Assert.IsTrue(game.Village.IsLords);
			Assert.AreEqual("Ann's village", game.Village.Title);
			Assert.AreEqual(StockMode.Lords, game.Village.Stock.Mode);
			Assert.AreEqual(12, ResourceNode.All.Count);
			Assert.IsNotNull(game.View.Root.Find("Wall"), "the wall of the save is drawn");
			StringAssert.Contains("Level 4", game.View.BuildingBySlot(4).Find("Label").GetComponent<TextMesh>().text, "the granary shows its saved level");
			Assert.AreEqual(1000, game.Session.Stock.Count(Items.Wood), 5, "the stock is Lords' stock, not 750");
			Assert.AreEqual(3500, game.Session.Stock.Capacity(Items.Grain), "a level 4 granary: 800 * 1.45^4 = 3536, rounded to hundreds");

			// Gathering in Craft raises the Lords stock.
			var tree = ResourceNode.All.First(n => n.ItemId == Items.Wood);
			var before = game.Session.Stock.Count(Items.Wood);
			game.Gather(tree);
			game.Deposit();
			Assert.AreEqual(0, game.Session.Backpack.TotalItems, "everything fitted");
			Assert.AreEqual(before + tree.AmountPerGather, game.Session.Stock.Count(Items.Wood), 1, "stored in the shared stock");
			Assert.IsTrue(game.Village.Stock.Dirty);

			// Leaving the game writes it: only the stock field of the save.
			var writes = store.Writes.Count;
			game.Leave();
			yield return Until(() => store.Writes.Count > writes && store.Writes[store.Writes.Count - 1].Applied, "the stock to be written to Lords");
			CollectionAssert.AreEquivalent(new[] { "amounts", "at" }, store.Writes[store.Writes.Count - 1].Stock.Properties().Select(p => p.Name), "the body is {amounts, at}");
			Assert.AreEqual(before + tree.AmountPerGather, (double)store.Data["stock"]["amounts"]["wood"], 1.0, "Lords' wood went up by what Craft stored");
			foreach (var property in original.Properties().Where(p => p.Name != "stock"))
				Assert.IsTrue(JToken.DeepEquals(property.Value, store.Data[property.Name]), property.Name + " was not touched");
			Assert.AreEqual(original.Properties().Count(), store.Data.Properties().Count(), "no field was added");
			Assert.IsFalse(game.Village.Stock.Dirty);
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator A_guest_account_sees_the_standard_village_and_nothing_is_read_or_written()
		{
			var store = new InMemoryProgressStore(LordsDoc());
			CraftGame.VillageLoader = LoaderFor(store, true);

			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
			var game = UnityEngine.Object.FindAnyObjectByType<CraftGame>();
			game.SaveEnabled = false;
			yield return Until(() => game.HasVillage, "the village");
			Assert.IsFalse(game.Village.IsLords);
			Assert.AreEqual("Standard village", game.Village.Title);
			StringAssert.Contains("Guest", game.Village.Status);
			Assert.AreEqual(750, game.Session.Stock.Count(Items.Wood));

			var tree = ResourceNode.All.First(n => n.ItemId == Items.Wood);
			game.Gather(tree);
			game.Deposit();
			game.Leave();
			yield return new WaitForSeconds(0.3f);
			Assert.AreEqual(0, store.Loads, "a guest's save is not even read");
			Assert.AreEqual(0, store.Writes.Count);
			Assert.Greater(game.Session.Stock.Count(Items.Wood), 750, "the local stock still went up");
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator Offline_the_standard_village_is_shown_with_the_reason_and_the_game_goes_on()
		{
			var store = new InMemoryProgressStore(LordsDoc()) { Offline = true };
			CraftGame.VillageLoader = LoaderFor(store);

			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			var game = UnityEngine.Object.FindAnyObjectByType<CraftGame>();
			game.SaveEnabled = false;
			yield return Until(() => game.HasVillage, "the village");
			Assert.IsFalse(game.Village.IsLords);
			StringAssert.Contains("Offline", game.Village.Status);
			Assert.AreEqual(12, ResourceNode.All.Count);
			Assert.AreEqual(0, store.Writes.Count);
		}
	}
}
