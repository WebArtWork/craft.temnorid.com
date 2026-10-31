using System;
using System.Collections;
using System.IO;
using ITKamianets.Engine.Input;
using ITKamianets.Engine.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Temnorid.Craft.Tests
{
	/// <summary>
	/// The real settlement with this machine as the host and a second player (a detached session in the same process) as the guest:
	/// the guest's worker is shown on the host from its pose, the host's world reaches the guest as entities, and a gather and a
	/// store requested by the guest are applied by the host and answered. Also the touch controls.
	/// </summary>
	public class OnlineCraftTests
	{
		NetSession _guest;
		string _saveFolder;

		static IEnumerator Until(Func<bool> condition, string what, float seconds = 10f)
		{
			var end = Time.realtimeSinceStartup + seconds;
			while (!condition())
			{
				if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
				yield return null;
			}
		}

		[SetUp]
		public void SetUp()
		{
			_saveFolder = Path.Combine(Path.GetTempPath(), "craft-online-" + Guid.NewGuid());
			CraftGame.SaveRootOverride = _saveFolder;
		}

		[TearDown]
		public void TearDown()
		{
			Time.timeScale = 1f;
			CraftHud.ForceTouch = false;
			CraftGame.SaveRootOverride = null;
			if (_guest != null) { _guest.Leave(); UnityEngine.Object.Destroy(_guest.gameObject); }
			NetSession.Instance.Leave();
			if (Directory.Exists(_saveFolder)) Directory.Delete(_saveFolder, true);
		}

		[UnityTest]
		[Timeout(120000)]
		public IEnumerator A_guest_gathers_and_stores_in_the_hosts_settlement_and_the_host_stays_the_authority()
		{
			var host = NetSession.Instance;
			host.Leave();
			host.GameId = "craft";
			host.GameVersion = Application.version;
			host.LocalName = "Hosty";
			host.MaxPlayers = 4;
			Assert.IsTrue(host.HostDirect(7804), host.LastError);

			_guest = NetSession.CreateDetached();
			_guest.GameId = "craft";
			_guest.GameVersion = Application.version;
			_guest.LocalName = "Guesty";
			_guest.LocalColour = 3;
			Assert.IsTrue(_guest.JoinDirect("127.0.0.1:7804"), _guest.LastError);
			yield return Until(() => host.Players.Count == 2 && _guest.Players.Count == 2 && !_guest.Connecting, "the guest to join");

			string got = null;
			string message = null;
			_guest.On(CraftNet.GotEvent, (who, text) => got = text);
			_guest.On(CraftNet.MessageEvent, (who, text) => message = text);

			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;

			var game = UnityEngine.Object.FindAnyObjectByType<CraftGame>();
			game.SaveEnabled = false;
			Assert.IsNotNull(game.Net, "an online settlement has the link to other players");
			Assert.IsTrue(game.Net.IsHost);
			Assert.IsTrue(game.Net.Nodes.Count >= 30);

			// The guest says where its worker is: the host shows it. It stands at the first tree.
			var tree = game.Net.Nodes[0];
			foreach (var node in game.Net.Nodes) if (node.ItemId == Items.Wood) { tree = node; break; }
			var target = tree.transform.position + new Vector3(2f, 0f, 0f);
			for (var i = 0; i < 6; i++)
			{
				_guest.PublishPose(new NetPose { Position = target, Yaw = 30f, Speed = 2f, State = 0, A = 5f });
				yield return new WaitForSeconds(0.1f);
			}
			yield return Until(() => game.Net.Workers.Count == 1, "the guest's worker to appear");
			RemoteWorker shown = null;
			foreach (var worker in game.Net.Workers.Values) shown = worker;
			Assert.AreEqual("Guesty", shown.DisplayName);
			yield return Until(() => (shown.transform.position - target).magnitude < 0.5f, "the guest's worker to be shown where it is");
			Assert.AreEqual(5f, shown.Carrying, 0.01f);

			// The host's world reaches the guest: every node and the warehouse counts.
			yield return Until(() => _guest.Entities.Count >= Items.All.Length + game.Net.Nodes.Count, "the world's entities");
			Assert.AreEqual(3, _guest.Entities[tree.NetId].Value, 0.01f, "three harvests left");
			Assert.AreEqual(CraftNet.KindStore, _guest.Entities[1].Kind);

			// A gather by the guest is a request: the host takes the harvest, tells the guest, and the node's new state reaches the guest.
			_guest.Send(CraftNet.GatherEvent, tree.NetId.ToString(), true);
			yield return Until(() => got != null, "the host to hand over the harvest");
			Assert.AreEqual("wood|2", got);
			Assert.AreEqual(2, tree.Left, "the host's node was used once");
			yield return Until(() => _guest.Entities.TryGetValue(tree.NetId, out var e) && e.Value < 2.5f, "the guest to see the node's new state");
			Assert.AreEqual(0, game.Session.Backpack.TotalItems, "the host's own backpack is untouched");

			// A gather from too far away is refused.
			var far = game.Net.Nodes[0];
			var farLeft = far.Left;
			_guest.Send(CraftNet.GatherEvent, far.NetId.ToString(), true);
			yield return new WaitForSeconds(0.5f);
			Assert.AreEqual(farLeft, far.Left, "a node the guest is not standing at stays as it was");

			// A store by the guest: it has to be at the warehouse; then the host stores it and tells everybody the new count.
			_guest.Send(CraftNet.DepositEvent, "wood:4", true);
			yield return new WaitForSeconds(0.4f);
			Assert.AreEqual(0, game.Session.Warehouse.Count(Items.Wood), "not at the warehouse: refused");
			var warehouse = Warehouse.Instance.transform.position + new Vector3(0f, 0f, -4f);
			for (var i = 0; i < 6; i++)
			{
				_guest.PublishPose(new NetPose { Position = warehouse, State = 0 });
				yield return new WaitForSeconds(0.1f);
			}
			_guest.Send(CraftNet.DepositEvent, "wood:4,gold:9", true);
			yield return new WaitForSeconds(0.4f);
			Assert.AreEqual(0, game.Session.Warehouse.Count(Items.Wood), "an unknown resource makes the whole load invalid");
			_guest.Send(CraftNet.DepositEvent, "wood:4", true);
			yield return Until(() => game.Session.Warehouse.Count(Items.Wood) == 4, "the host to store the guest's load");
			yield return Until(() => message != null && message.StartsWith("Stored 4"), "the guest to be told");
			yield return Until(() => _guest.Entities.TryGetValue(1, out var e) && e.State == 4, "the guest to see the warehouse count");

			// The new kinds work the same way: gather clay (a node the host and the guest both name by its id), then store six kinds.
			ResourceNode pit = null;
			foreach (var node in game.Net.Nodes) if (node.ItemId == Items.Clay) { pit = node; break; }
			Assert.IsNotNull(pit, "a clay node");
			var clayIndex = Array.IndexOf(Items.All, Items.Clay);
			Assert.AreEqual(CraftNet.KindNodeBase + clayIndex, _guest.Entities[pit.NetId].Kind, "the entity names the kind");
			Assert.AreEqual(CraftNet.StoreBase + clayIndex, 6, "clay is the sixth warehouse entity");
			Assert.AreEqual(CraftNet.KindStore, _guest.Entities[6].Kind);
			var pitSpot = pit.transform.position + new Vector3(2f, 0f, 0f);
			for (var i = 0; i < 6; i++)
			{
				_guest.PublishPose(new NetPose { Position = pitSpot, State = 0 });
				yield return new WaitForSeconds(0.1f);
			}
			got = null;
			_guest.Send(CraftNet.GatherEvent, pit.NetId.ToString(), true);
			yield return Until(() => got != null, "the host to hand over the clay");
			Assert.AreEqual("clay|2", got);
			Assert.AreEqual(2, pit.Left);
			for (var i = 0; i < 6; i++)
			{
				_guest.PublishPose(new NetPose { Position = warehouse, State = 0 });
				yield return new WaitForSeconds(0.1f);
			}
			_guest.Send(CraftNet.DepositEvent, "wood:1,stone:1,iron:1,water:1,grain:1,clay:2", true);
			yield return Until(() => game.Session.Warehouse.Count(Items.Clay) == 2, "the host to store clay");
			Assert.AreEqual(1, game.Session.Warehouse.Count(Items.Water));
			yield return Until(() => _guest.Entities.TryGetValue(6, out var e) && e.State == 2, "the guest to see the clay count");
		}

		[UnityTest]
		[Timeout(60000)]
		public IEnumerator A_phone_gets_a_stick_a_look_area_and_buttons()
		{
			CraftHud.ForceTouch = true;
			yield return SceneManager.LoadSceneAsync("Main");
			yield return null;
			yield return null;
			UnityEngine.Object.FindAnyObjectByType<CraftGame>().SaveEnabled = false;

			var hud = UnityEngine.Object.FindAnyObjectByType<CraftHud>();
			Assert.IsTrue(hud.HasTouchControls);
			Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<OnScreenStick>().Length, "one move stick");
			Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<TouchLookArea>().Length, "one look area");
			Assert.AreEqual(4, UnityEngine.Object.FindObjectsByType<OnScreenButton>().Length, "use, jump, run, view");

			var found = false;
			foreach (var device in InputSystem.devices) if (device is EngineTouchControls) found = true;
			Assert.IsTrue(found, "the touch controls feed the engine's virtual touch device");
		}

	}
}
