using ITKamianets.Engine.Net;
using ITKamianets.Engine.UI;
using UnityEngine;

namespace Temnorid.Craft
{
	/// <summary>
	/// Another player's worker, shown on this machine from the poses its owner publishes. It is only a picture: no collider,
	/// nothing in the world depends on it. The pose carries the owner's backpack load (A) and whether they are gathering (State 1).
	/// </summary>
	public sealed class RemoteWorker : MonoBehaviour
	{
		public ulong Owner;
		public string DisplayName = "Worker";
		public bool Gathering { get; private set; }
		public float Carrying { get; private set; }

		RemoteBody _body;

		public static RemoteWorker Create(ulong owner, string displayName, Color tint, Material template)
		{
			var go = new GameObject("Worker_" + displayName);
			var worker = go.AddComponent<RemoteWorker>();
			worker.Owner = owner;
			worker.DisplayName = displayName;
			worker.Build(tint, template);
			return worker;
		}

		void Build(Color tint, Material template)
		{
			_body = gameObject.AddComponent<RemoteBody>();
			var visual = new GameObject("Body").transform;
			visual.SetParent(transform, false);
			var cloth = Tinted(template, tint);
			var skin = Tinted(template, new Color(0.88f, 0.7f, 0.55f));
			var pack = Tinted(template, new Color(0.5f, 0.32f, 0.16f));
			Part(visual, PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), cloth);
			Part(visual, PrimitiveType.Sphere, new Vector3(0f, 1.62f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), skin);
			Part(visual, PrimitiveType.Cube, new Vector3(0f, 1.1f, -0.36f), new Vector3(0.5f, 0.65f, 0.28f), pack);

			var font = new UiBuilder().Font;
			var label = new GameObject("Name");
			label.transform.SetParent(transform, false);
			label.transform.localPosition = new Vector3(0f, 2.3f, 0f);
			var text = label.AddComponent<TextMesh>();
			text.text = DisplayName;
			text.font = font;
			text.fontSize = 48;
			text.characterSize = 0.06f;
			text.anchor = TextAnchor.MiddleCenter;
			text.color = tint;
			if (font != null) label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
			label.AddComponent<FaceCamera>();
		}

		static Material Tinted(Material template, Color colour)
		{
			var material = template != null ? new Material(template) : new Material(Shader.Find("Sprites/Default"));
			material.color = colour;
			if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
			return material;
		}

		static void Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
		{
			var part = GameObject.CreatePrimitive(type);
			Destroy(part.GetComponent<Collider>()); // never in the way of anybody
			part.transform.SetParent(parent, false);
			part.transform.localPosition = position;
			part.transform.localScale = scale;
			part.GetComponent<Renderer>().sharedMaterial = material;
		}

		/// <summary>Shows the owner's latest pose.</summary>
		public void Show(NetPose pose)
		{
			_body.Apply(pose);
			Gathering = pose.State == 1;
			Carrying = pose.A;
		}

		sealed class FaceCamera : MonoBehaviour
		{
			void LateUpdate()
			{
				var camera = Camera.main;
				if (camera != null) transform.rotation = camera.transform.rotation;
			}
		}
	}
}
