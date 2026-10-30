using ITKamianets.Engine.Input;
using UnityEngine;

namespace Temnorid.Craft
{
	/// <summary>
	/// What the player does besides walking: hold E next to a resource to gather it, press E next to the
	/// warehouse to store the load. It only reads the E action and asks <see cref="CraftGame"/> to do the work.
	/// </summary>
	public sealed class PlayerWorker : MonoBehaviour
	{
		public CraftGame Game;
		/// <summary>How close to a resource or the warehouse the player must stand, in metres.</summary>
		public float Reach = 2.2f;

		/// <summary>What the player can do right now, for the screen. Empty when there is nothing.</summary>
		public string Prompt { get; private set; } = "";

		/// <summary>How far the current gather has got, 0 to 1.</summary>
		public float Progress { get; private set; }

		bool _wasDown;

		void Update()
		{
			if (Game == null || Game.Paused)
			{
				Prompt = "";
				Progress = 0f;
				return;
			}

			var position = transform.position;
			var interact = EngineInput.Default.Player.Interact;

			// A press is the frame the key goes down. Worked out here from "is it down", which holds for
			// every device and every way of injecting input, rather than from a per-frame flag.
			var down = interact.IsPressed();
			var pressedNow = down && !_wasDown;
			_wasDown = down;

			ResourceNode node = null;
			var nodeDistance = Reach;
			foreach (var candidate in ResourceNode.All)
			{
				if (!candidate.Available) continue;
				var distance = candidate.DistanceFrom(position);
				if (distance <= nodeDistance)
				{
					nodeDistance = distance;
					node = candidate;
				}
			}

			var warehouse = Warehouse.Instance;
			var warehouseDistance = warehouse != null ? warehouse.DistanceFrom(position) : float.MaxValue;
			var atWarehouse = warehouseDistance <= Reach + 0.8f;

			if (atWarehouse && (node == null || warehouseDistance < nodeDistance))
			{
				Progress = 0f;
				var carried = Game.Session.Backpack.TotalItems;
				Prompt = carried > 0 ? $"Press E to store {carried} items in the warehouse" : "Warehouse: your backpack is empty";
				if (carried > 0 && pressedNow) Game.Deposit();
				return;
			}

			if (node != null)
			{
				if (Game.Session.Backpack.SpaceFor(node.ItemId) <= 0)
				{
					Progress = 0f;
					Prompt = "Backpack full: store your load in the warehouse";
					return;
				}

				Prompt = $"Hold E to gather {node.DisplayName}";
				if (down)
				{
					Progress += Time.deltaTime / node.GatherSeconds;
					if (Progress >= 1f)
					{
						Progress = 0f;
						Game.Gather(node);
					}
				}
				else
				{
					Progress = 0f;
				}
				return;
			}

			Progress = 0f;
			Prompt = "";
		}
	}
}
