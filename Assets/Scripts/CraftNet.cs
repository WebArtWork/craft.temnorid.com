using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ITKamianets.Engine.Net;
using ITKamianets.Engine.Shell;
using UnityEngine;

namespace Temnorid.Craft
{
	/// <summary>
	/// Makes the settlement a shared sandbox for up to four workers. The host owns the world (the resource nodes and the
	/// warehouse) and applies what the others ask for after checking it; each person's backpack stays with that person.
	/// <list type="bullet">
	/// <item>Each worker is simulated by its owner and published as a pose (A is the load carried, State 1 while gathering);
	/// the others show it with <see cref="RemoteWorker"/>.</item>
	/// <item>The host publishes entities about ten times a second: ids <see cref="NodeBase"/> and up are the resource nodes
	/// (the position in the name-sorted list of the scene's nodes, so the same on every machine; Kind <see cref="KindNodeBase"/>
	/// plus the item index, Value the harvests left), ids 1 to 6 (<see cref="StoreBase"/> plus the item index) are the warehouse's count of each resource (State).
	/// Value of those is 1 once the goal has been reached.</item>
	/// <item>A guest gathers by sending <c>cr.gather</c> with the node id; the host checks that the node has something left, that the
	/// guest stands near it and is not faster than the gathering allows, takes the harvest and answers with <c>cr.got</c>
	/// (item and amount) to that guest only, which puts it in its own backpack. A guest stores its load with <c>cr.dep</c>; the
	/// host checks the load looks like a backpack and the guest stands near the warehouse, stores it, and answers with
	/// <c>cr.msg</c> (what happened) and <c>cr.back</c> for anything the warehouse could not take.</item>
	/// </list>
	/// The host is the only writer of the world, so a late or repeated request can never make two machines disagree: the
	/// worst a guest sees is a node that was already used up.
	/// </summary>
	public sealed class CraftNet : MonoBehaviour
	{
		public const int NodeBase = 100;
		public const int KindNodeBase = 100;
		public const int StoreBase = 1;
		public const int KindStore = 200;

		public const string GatherEvent = "cr.gather";
		public const string DepositEvent = "cr.dep";
		public const string GotEvent = "cr.got";
		public const string BackEvent = "cr.back";
		public const string MessageEvent = "cr.msg";

		const float PoseEvery = 1f / 15f;
		const float EntitiesEvery = 0.1f;
		/// <summary>How far from a node or the warehouse a guest may be when it asks (its pose is a little old).</summary>
		const float ReachSlack = 3f;

		CraftGame _game;
		NetSession _net;
		PlayerWorker _worker;
		bool _leaving;
		float _nextPose;
		float _nextEntities;
		Vector3 _lastPos;
		float _lastPosAt;
		Material _template;
		readonly List<ResourceNode> _nodes = new List<ResourceNode>();
		readonly List<NetEntity> _entities = new List<NetEntity>();
		readonly Dictionary<ulong, RemoteWorker> _workers = new Dictionary<ulong, RemoteWorker>();
		readonly Dictionary<long, float> _lastGather = new Dictionary<long, float>();
		readonly StringBuilder _text = new StringBuilder();

		public bool IsHost => _net != null && _net.IsHost;
		public bool IsClient => _net != null && _net.IsClient;
		public IReadOnlyDictionary<ulong, RemoteWorker> Workers => _workers;
		public IReadOnlyList<ResourceNode> Nodes => _nodes;

		public CraftNet Init(CraftGame game)
		{
			_game = game;
			_net = NetSession.Instance;
			_net.On(GatherEvent, OnGather);
			_net.On(DepositEvent, OnDeposit);
			_net.On(GotEvent, OnGot);
			_net.On(BackEvent, OnBack);
			_net.On(MessageEvent, OnMessage);
			_net.Disconnected += OnDisconnected;
			_net.PlayerLeft += OnPlayerLeft;
			_net.EntityUpdated += OnEntity;
			return this;
		}

		void OnDestroy()
		{
			if (_net == null) return;
			_net.Off(GatherEvent, OnGather);
			_net.Off(DepositEvent, OnDeposit);
			_net.Off(GotEvent, OnGot);
			_net.Off(BackEvent, OnBack);
			_net.Off(MessageEvent, OnMessage);
			_net.Disconnected -= OnDisconnected;
			_net.PlayerLeft -= OnPlayerLeft;
			_net.EntityUpdated -= OnEntity;
		}

		void Start()
		{
			// The nodes exist now (they enable before any Start). Every machine sorts them the same way, so an index names a node.
			_nodes.AddRange(ResourceNode.All);
			_nodes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
			for (var i = 0; i < _nodes.Count; i++)
			{
				_nodes[i].NetId = NodeBase + i;
				if (_net.IsClient) _nodes[i].Remote = true;
			}

			// Workers start side by side, in the order of the roster (the host first).
			_worker = FindAnyObjectByType<PlayerWorker>();
			if (_worker != null)
			{
				var players = _net.Players;
				var slot = 0;
				for (var i = 0; i < players.Count; i++) if (players[i].Id == _net.LocalId) slot = i;
				var spot = _worker.transform.position + Vector3.right * ((slot - (players.Count - 1) * 0.5f) * 1.6f);
				var controller = _worker.GetComponent<CharacterController>();
				if (controller != null) controller.enabled = false;
				_worker.transform.position = spot;
				if (controller != null) controller.enabled = true;
				_lastPos = spot;
				_lastPosAt = Time.unscaledTime;
				var renderer = _worker.GetComponentInChildren<Renderer>();
				_template = renderer != null ? renderer.sharedMaterial : null;
			}

			if (_net.IsClient) foreach (var entity in new List<NetEntity>(_net.Entities.Values)) OnEntity(entity);
		}

		// ---- every machine ----

		void Update()
		{
			if (_net == null || !_net.IsOnline) return;
			ShowWorkers();

			if (Time.unscaledTime >= _nextPose)
			{
				_nextPose = Time.unscaledTime + PoseEvery;
				if (_worker != null) _net.PublishPose(MyPose());
			}

			if (_net.IsHost && Time.unscaledTime >= _nextEntities)
			{
				_nextEntities = Time.unscaledTime + EntitiesEvery;
				Publish();
			}
		}

		NetPose MyPose()
		{
			var position = _worker.transform.position;
			var dt = Mathf.Max(0.001f, Time.unscaledTime - _lastPosAt);
			var flat = position - _lastPos;
			flat.y = 0f;
			var speed = flat.magnitude / dt;
			_lastPos = position;
			_lastPosAt = Time.unscaledTime;
			return new NetPose
			{
				Position = position,
				Yaw = _worker.transform.eulerAngles.y,
				Speed = speed,
				State = _worker.Progress > 0.001f ? 1 : 0,
				A = _game.Session.Backpack.TotalItems,
				B = _worker.Progress,
			};
		}

		void ShowWorkers()
		{
			foreach (var player in _net.Players)
			{
				if (player.Id == _net.LocalId) continue;
				if (!_workers.TryGetValue(player.Id, out var worker) || worker == null)
				{
					worker = RemoteWorker.Create(player.Id, player.Name, ShellConfig.Current.ColourOf(player.Colour), _template);
					_workers[player.Id] = worker;
				}
				if (_net.TryGetPose(player.Id, out var pose)) worker.Show(pose);
			}
		}

		void OnDisconnected(string reason)
		{
			// The host left or the connection dropped: nothing to share any more.
			if (_leaving) return;
			_leaving = true;
			_game.QuitToMenu();
		}

		void OnPlayerLeft(NetPlayer player)
		{
			_game.ShowToast(player.Name + " left the settlement.");
			if (_workers.TryGetValue(player.Id, out var worker) && worker != null) Destroy(worker.gameObject);
			_workers.Remove(player.Id);
		}

		// ---- host: publishes the world ----

		void Publish()
		{
			_entities.Clear();
			for (var i = 0; i < Items.All.Length; i++)
			{
				_entities.Add(new NetEntity
				{
					Id = StoreBase + i,
					Kind = KindStore,
					State = _game.Session.Warehouse.Count(Items.All[i]),
					Value = _game.Session.GoalReached ? 1f : 0f,
				});
			}
			for (var i = 0; i < _nodes.Count; i++)
			{
				var node = _nodes[i];
				_entities.Add(new NetEntity
				{
					Id = node.NetId,
					Kind = KindNodeBase + System.Array.IndexOf(Items.All, node.ItemId),
					Position = node.transform.position,
					Value = node.Left,
				});
			}
			_net.PublishEntities(_entities);
		}

		// ---- a guest asks ----

		/// <summary>A guest has finished gathering from a node: the host is asked to take the harvest. True when the request was sent.</summary>
		public bool RequestGather(ResourceNode node)
		{
			if (!_net.IsClient || node == null || node.NetId < NodeBase) return false;
			_net.Send(GatherEvent, node.NetId.ToString(CultureInfo.InvariantCulture), true);
			return true;
		}

		/// <summary>A guest stores its backpack: what it carries goes to the host with the request and leaves the backpack.</summary>
		public bool RequestDeposit()
		{
			if (!_net.IsClient) return false;
			var bag = _game.Session.Backpack;
			if (bag.IsEmpty) return false;
			_text.Clear();
			foreach (var id in Items.All)
			{
				var count = bag.Count(id);
				if (count <= 0) continue;
				if (_text.Length > 0) _text.Append(',');
				_text.Append(id).Append(':').Append(count.ToString(CultureInfo.InvariantCulture));
				bag.TryRemove(id, count);
			}
			_net.Send(DepositEvent, _text.ToString(), true);
			_game.ShowToast("Storing...");
			return true;
		}

		// ---- the host checks and applies ----

		void OnGather(ulong sender, string payload)
		{
			if (!_net.IsHost || _net.Find(sender) == null) return;
			if (!int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) return;
			var index = id - NodeBase;
			if (index < 0 || index >= _nodes.Count) return;
			var node = _nodes[index];
			if (!node.Available)
			{
				Tell(sender, node.DisplayName + " was already used up.");
				return;
			}
			if (!Near(sender, node.DistanceFrom)) return;

			// Not faster than the gathering allows (with some room for the network's rhythm).
			var key = unchecked((long)sender * 100003L + id);
			if (_lastGather.TryGetValue(key, out var last) && Time.time - last < node.GatherSeconds * 0.6f) return;
			_lastGather[key] = Time.time;

			node.Harvest();
			_net.SendTo(sender, GotEvent, node.ItemId + "|" + node.AmountPerGather.ToString(CultureInfo.InvariantCulture));
		}

		void OnDeposit(ulong sender, string payload)
		{
			if (!_net.IsHost || _net.Find(sender) == null) return;
			if (Warehouse.Instance == null || !Near(sender, Warehouse.Instance.DistanceFrom)) return;
			if (!TryParseLoad(payload, out var load)) return;

			var stored = 0;
			_text.Clear();
			foreach (var pair in load)
			{
				var fitted = _game.Session.StoreInWarehouse(pair.Key, pair.Value);
				stored += fitted;
				if (fitted < pair.Value)
				{
					if (_text.Length > 0) _text.Append(',');
					_text.Append(pair.Key).Append(':').Append((pair.Value - fitted).ToString(CultureInfo.InvariantCulture));
				}
			}
			if (_text.Length > 0) _net.SendTo(sender, BackEvent, _text.ToString());
			if (stored > 0)
			{
				Tell(sender, $"Stored {stored} items");
				_game.Save();
			}
			if (_game.Session.CheckGoal()) _game.GoalReachedNow();
		}

		/// <summary>A load looks like a backpack: known resources, positive amounts, no more than the backpack can hold.</summary>
		static bool TryParseLoad(string payload, out Dictionary<string, int> load)
		{
			load = new Dictionary<string, int>();
			if (string.IsNullOrEmpty(payload) || payload.Length > 200) return false;
			var total = 0;
			foreach (var part in payload.Split(','))
			{
				var pieces = part.Split(':');
				if (pieces.Length != 2 || System.Array.IndexOf(Items.All, pieces[0]) < 0) return false;
				if (!int.TryParse(pieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount <= 0) return false;
				if (load.ContainsKey(pieces[0])) return false;
				load[pieces[0]] = amount;
				total += amount;
			}
			return load.Count > 0 && total <= CraftSession.BackpackSlots * CraftSession.BackpackStack;
		}

		bool Near(ulong player, System.Func<Vector3, float> distance)
		{
			if (!_net.TryGetPose(player, out var pose)) return false;
			var reach = _worker != null ? _worker.Reach : 2.2f;
			return distance(pose.Position) <= reach + 0.8f + ReachSlack;
		}

		/// <summary>Host: tells one player something (their request's result).</summary>
		void Tell(ulong player, string text)
		{
			if (_net.IsHost) _net.SendTo(player, MessageEvent, text);
		}

		// ---- a guest is told ----

		void OnGot(ulong sender, string payload)
		{
			if (!_net.IsClient) return;
			var parts = payload.Split('|');
			if (parts.Length != 2 || System.Array.IndexOf(Items.All, parts[0]) < 0) return;
			if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount < 1 || amount > 20) return;
			var added = _game.Session.Gather(parts[0], amount);
			_game.ShowToast(added > 0 ? $"+{added} {Items.Name(parts[0])}" : "Backpack full");
		}

		void OnBack(ulong sender, string payload)
		{
			if (!_net.IsClient || !TryParseLoad(payload, out var load)) return;
			foreach (var pair in load) _game.Session.Gather(pair.Key, pair.Value);
			_game.ShowToast("The warehouse is full: you keep the rest.");
		}

		void OnMessage(ulong sender, string text)
		{
			if (_net.IsClient) _game.ShowToast(text);
		}

		void OnEntity(NetEntity entity)
		{
			if (_game == null || !_net.IsClient) return;
			if (entity.Kind == KindStore)
			{
				var index = entity.Id - StoreBase;
				if (index >= 0 && index < Items.All.Length) _game.MirrorWarehouse(Items.All[index], entity.State, entity.Value > 0.5f);
				return;
			}
			var node = entity.Id - NodeBase;
			if (node >= 0 && node < _nodes.Count) _nodes[node].SetLeft(Mathf.RoundToInt(entity.Value));
		}
	}
}
