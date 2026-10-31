using System.Collections.Generic;
using UnityEngine;

namespace Temnorid.Craft
{
	/// <summary>
	/// A tree, a rock or an iron vein. The player gathers from it a few times; then it is used up
	/// and grows back after a while.
	/// </summary>
	public sealed class ResourceNode : MonoBehaviour
	{
		public static readonly List<ResourceNode> All = new List<ResourceNode>();

		public string ItemId = Items.Wood;
		public int AmountPerGather = 2;
		public float GatherSeconds = 1.2f;
		public int Harvests = 3;
		public float RespawnSeconds = 25f;
		/// <summary>The shapes that draw the node; hidden while it is used up and shrunk as it is gathered.</summary>
		public Transform Visual;

		/// <summary>Online: the same on every machine (the node's place in the sorted list); 0 offline.</summary>
		public int NetId;
		/// <summary>Online client: the host decides this node's state; it does not grow back by itself here.</summary>
		public bool Remote;

		int _left;
		float _respawnAt;
		Collider _collider;

		public bool Available => _left > 0;
		public int Left => _left;
		public string DisplayName => Items.Name(ItemId);

		void Awake()
		{
			_left = Harvests;
			_collider = GetComponent<Collider>();
		}

		void OnEnable() => All.Add(this);
		void OnDisable() => All.Remove(this);

		void Update()
		{
			if (!Remote && _left == 0 && Time.time >= _respawnAt)
			{
				_left = Harvests;
				SetStanding(true);
			}
		}

		/// <summary>Distance on the ground from a point to the node's body, 0 when touching.</summary>
		public float DistanceFrom(Vector3 point)
		{
			var closest = _collider != null && _collider.enabled ? _collider.ClosestPoint(point) : transform.position;
			closest.y = point.y;
			return Vector3.Distance(point, closest);
		}

		/// <summary>Client: shows the state the host reports.</summary>
		public void SetLeft(int left)
		{
			left = Mathf.Clamp(left, 0, Harvests);
			if (left == _left) return;
			var wasUsedUp = _left == 0;
			_left = left;
			if (left == 0) SetStanding(false);
			else if (wasUsedUp) SetStanding(true);
			else ShowSize();
		}

		/// <summary>One gather is taken from the node.</summary>
		public void Harvest()
		{
			if (_left <= 0) return;
			_left--;
			if (_left == 0)
			{
				_respawnAt = Time.time + RespawnSeconds;
				SetStanding(false);
			}
			else
			{
				ShowSize();
			}
		}

		void SetStanding(bool standing)
		{
			if (Visual != null) Visual.gameObject.SetActive(standing);
			if (_collider != null) _collider.enabled = standing;
			if (standing) ShowSize();
		}

		void ShowSize()
		{
			if (Visual != null) Visual.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, (float)_left / Mathf.Max(1, Harvests));
		}
	}
}
