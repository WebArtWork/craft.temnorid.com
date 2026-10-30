using UnityEngine;

namespace Temnorid.Craft
{
	/// <summary>The warehouse building: the place to store what the backpack carries.</summary>
	public sealed class Warehouse : MonoBehaviour
	{
		public static Warehouse Instance { get; private set; }

		Collider _collider;

		void Awake()
		{
			Instance = this;
			_collider = GetComponent<Collider>();
		}

		void OnDestroy()
		{
			if (Instance == this) Instance = null;
		}

		/// <summary>Distance on the ground from a point to the building's walls, 0 when touching.</summary>
		public float DistanceFrom(Vector3 point)
		{
			var closest = _collider != null ? _collider.ClosestPoint(point) : transform.position;
			closest.y = point.y;
			return Vector3.Distance(point, closest);
		}
	}
}
