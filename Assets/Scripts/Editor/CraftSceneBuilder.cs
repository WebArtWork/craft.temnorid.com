using ITKamianets.Engine.Graybox;
using ITKamianets.Engine.Graybox.EditorTools;
using ITKamianets.Engine.Shell.Editor;
using System;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.CharacterView;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Temnorid.Craft.EditorTools
{
	/// <summary>
	/// Builds the settlement from Unity primitives only: the ground, a forest, a rock field, an iron field,
	/// the warehouse, the worker and the camera. Run it from the menu (Temnorid > Craft > Build Scene) or the
	/// command line (<c>-executeMethod Temnorid.Craft.EditorTools.CraftSceneBuilder.Build</c>); it rebuilds
	/// <c>Assets/Scenes/Main.unity</c> from scratch each time, so do not edit that scene by hand.
	/// </summary>
	public static class CraftSceneBuilder
	{
		const string ScenePath = "Assets/Scenes/Main.unity";
		public const string Version = "0.2.0";
		const string MaterialDir = "Assets/Materials";

		[MenuItem("Temnorid/Craft/Build Scene")]
		public static void Build()
		{
			ShellSceneKit.ApplyPlatformSettings("Temnorid", "Temnorid Craft", "com.itkamianets.craft", Version);
			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.itkamianets.craft");
			PlayerSettings.runInBackground = true;
			PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;

			var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
			RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.74f);

			var world = new GameObject("World").transform;
			BuildGround(world);
			BuildForest(world);
			BuildRockField(world);
			BuildIronField(world);
			BuildWarehouse(world);

			var player = BuildPlayer();
			var camera = BuildCameraAndLight(player.GetComponent<CharacterMotor>());
			var cameraName = camera.name;

			var gameObject = new GameObject("Game");
			var game = gameObject.AddComponent<CraftGame>();
			var worker = player.GetComponent<PlayerWorker>();
			worker.Game = game;
			var hud = gameObject.AddComponent<CraftHud>();
			hud.Game = game;
			hud.Worker = worker;

			EditorSceneManager.SaveScene(scene, ScenePath);
			ShellSceneKit.BuildBoot();
			ShellSceneKit.BuildMenu();
			ShellSceneKit.SetBuildOrder(ScenePath);
			AssetDatabase.SaveAssets();
			Debug.Log("[Craft] Scene built: " + ScenePath + " (camera " + cameraName + ")");
		}

		// ---- the world ----

		static void BuildGround(Transform world)
		{
			var grass = Mat("Grass", new Color(0.33f, 0.55f, 0.28f));
			var dirt = Mat("Dirt", new Color(0.58f, 0.44f, 0.3f));
			Prim(PrimitiveType.Cube, "Ground", world, new Vector3(0f, -0.5f, 0f), new Vector3(240f, 1f, 240f), grass, true);
			Prim(PrimitiveType.Cube, "PathToWarehouse", world, new Vector3(0f, 0.02f, 8f), new Vector3(3.5f, 0.05f, 12f), dirt);
		}

		static void BuildForest(Transform world)
		{
			var root = new GameObject("Forest").transform;
			root.SetParent(world, false);
			var trunk = Mat("Trunk", new Color(0.4f, 0.27f, 0.16f));
			var leaves = Mat("Leaves", new Color(0.15f, 0.42f, 0.18f));
			var spots = new[] { new Vector3(-30f, 0f, 0f), new Vector3(-26f, 0f, 6f), new Vector3(-24f, 0f, -5f), new Vector3(-20f, 0f, 3f), new Vector3(-31f, 0f, 8f) };
			for (var i = 0; i < spots.Length; i++)
			{
				var node = NewNode(root, "Tree_" + i, spots[i], Items.Wood, "resources/wood-node", 3, 1.2f);
				var capsule = node.gameObject.AddComponent<CapsuleCollider>();
				capsule.radius = 0.9f;
				capsule.height = 4.2f;
				capsule.center = new Vector3(0f, 2.1f, 0f);
				Prim(PrimitiveType.Cylinder, "Trunk", node.Visual, new Vector3(0f, 1.5f, 0f), new Vector3(0.6f, 1.5f, 0.6f), trunk);
				Prim(PrimitiveType.Sphere, "Crown", node.Visual, new Vector3(0f, 3.7f, 0f), new Vector3(3.2f, 3f, 3.2f), leaves);
			}
			Sign(world, "FOREST\nWood", new Vector3(-16f, 0f, -2f));
		}

		static void BuildRockField(Transform world)
		{
			var root = new GameObject("RockField").transform;
			root.SetParent(world, false);
			var rock = Mat("Rock", new Color(0.55f, 0.55f, 0.58f));
			var spots = new[] { new Vector3(24f, 0f, -6f), new Vector3(28f, 0f, 0f), new Vector3(24f, 0f, 5f), new Vector3(31f, 0f, -4f), new Vector3(30f, 0f, 6f) };
			for (var i = 0; i < spots.Length; i++)
			{
				var node = NewNode(root, "Rock_" + i, spots[i], Items.Stone, "resources/stone-node", 3, 1.2f);
				var sphere = node.gameObject.AddComponent<SphereCollider>();
				sphere.radius = 1.4f;
				sphere.center = new Vector3(0f, 0.8f, 0f);
				Prim(PrimitiveType.Sphere, "Boulder", node.Visual, new Vector3(0f, 0.8f, 0f), new Vector3(2.8f, 1.8f, 2.4f), rock);
				Prim(PrimitiveType.Sphere, "Small", node.Visual, new Vector3(1.2f, 0.4f, -0.8f), new Vector3(1.2f, 0.8f, 1f), rock);
			}
			Sign(world, "ROCKS\nStone", new Vector3(18f, 0f, -2f));
		}

		static void BuildIronField(Transform world)
		{
			var root = new GameObject("IronField").transform;
			root.SetParent(world, false);
			var ore = Mat("IronOre", new Color(0.3f, 0.34f, 0.44f));
			var vein = Mat("IronVein", new Color(0.85f, 0.45f, 0.2f));
			var spots = new[] { new Vector3(-6f, 0f, -26f), new Vector3(-1f, 0f, -30f), new Vector3(4f, 0f, -26f), new Vector3(9f, 0f, -30f), new Vector3(-10f, 0f, -31f) };
			for (var i = 0; i < spots.Length; i++)
			{
				var node = NewNode(root, "Iron_" + i, spots[i], Items.Iron, "resources/iron-node", 3, 1.6f);
				var box = node.gameObject.AddComponent<BoxCollider>();
				box.size = new Vector3(2.2f, 1.8f, 2.2f);
				box.center = new Vector3(0f, 0.9f, 0f);
				var block = Prim(PrimitiveType.Cube, "Ore", node.Visual, new Vector3(0f, 0.9f, 0f), new Vector3(2f, 1.8f, 2f), ore);
				block.transform.localRotation = Quaternion.Euler(0f, 20f * i, 0f);
				Prim(PrimitiveType.Cube, "Vein", node.Visual, new Vector3(0.5f, 1.2f, 0.3f), new Vector3(0.5f, 0.8f, 0.5f), vein);
			}
			Sign(world, "IRON\nOre", new Vector3(0f, 0f, -20f));
		}

		static void BuildWarehouse(Transform world)
		{
			var wall = Mat("Wall", new Color(0.86f, 0.78f, 0.62f));
			var roof = Mat("Roof", new Color(0.55f, 0.22f, 0.18f));
			var door = Mat("Door", new Color(0.25f, 0.16f, 0.1f));

			var building = new GameObject("Warehouse");
			building.transform.SetParent(world, false);
			building.transform.position = new Vector3(0f, 0f, 18f);
			building.AddComponent<Warehouse>();
			building.AddComponent<AssetIdTag>().Id = "buildings/warehouse";
			var box = building.AddComponent<BoxCollider>();
			box.size = new Vector3(8f, 4f, 6f);
			box.center = new Vector3(0f, 2f, 0f);

			var visual = new GameObject("Visual").transform;
			visual.SetParent(building.transform, false);
			Prim(PrimitiveType.Cube, "Walls", visual, new Vector3(0f, 2f, 0f), new Vector3(8f, 4f, 6f), wall);
			var left = Prim(PrimitiveType.Cube, "RoofLeft", visual, new Vector3(0f, 4.7f, -1.5f), new Vector3(8.6f, 0.3f, 3.6f), roof);
			left.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
			var right = Prim(PrimitiveType.Cube, "RoofRight", visual, new Vector3(0f, 4.7f, 1.5f), new Vector3(8.6f, 0.3f, 3.6f), roof);
			right.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
			Prim(PrimitiveType.Cube, "Door", visual, new Vector3(0f, 1.3f, -3.05f), new Vector3(1.8f, 2.6f, 0.15f), door);

			Sign(visual, "WAREHOUSE", new Vector3(0f, 3.3f, -3.3f), true);
		}

		// ---- the worker ----

		static GameObject BuildPlayer()
		{
			var blue = Mat("WorkerBlue", new Color(0.2f, 0.45f, 0.9f));
			var skin = Mat("Skin", new Color(0.88f, 0.7f, 0.55f));
			var pack = Mat("Pack", new Color(0.5f, 0.32f, 0.16f));

			var player = new GameObject("Player");
			player.transform.position = new Vector3(0f, 0.1f, 0f);
			player.AddComponent<CharacterController>();
			player.AddComponent<CharacterMotor>();
			player.AddComponent<CharacterViewControls>();
			player.AddComponent<PlayerWorker>();
			player.AddComponent<AssetIdTag>().Id = "characters/worker";

			var body = new GameObject("Body").transform;
			body.SetParent(player.transform, false);
			Prim(PrimitiveType.Capsule, "Torso", body, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), blue);
			Prim(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 1.62f, 0.02f), new Vector3(0.38f, 0.38f, 0.38f), skin);
			Prim(PrimitiveType.Cube, "Backpack", body, new Vector3(0f, 1.1f, -0.36f), new Vector3(0.5f, 0.65f, 0.28f), pack);
			return player;
		}

		static Camera BuildCameraAndLight(CharacterMotor target)
		{
			var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
			var camera = cameraObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = new Color(0.56f, 0.72f, 0.9f);
			camera.fieldOfView = 65f;
			camera.nearClipPlane = 0.1f;
			camera.farClipPlane = 500f;
			cameraObject.AddComponent<AudioListener>();
			var view = cameraObject.AddComponent<CharacterViewCamera>();

			var serialized = new SerializedObject(view);
			serialized.FindProperty("target").objectReferenceValue = target;
			serialized.FindProperty("settings.StartView").enumValueIndex = (int)CameraView.ThirdPerson;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			var lightObject = new GameObject("Sun");
			lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
			var light = lightObject.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1.15f;
			light.shadows = LightShadows.Soft;
			return camera;
		}

		// ---- helpers ----

		static ResourceNode NewNode(Transform parent, string name, Vector3 position, string itemId, string assetId, int harvests, float gatherSeconds)
		{
			var root = new GameObject(name);
			root.transform.SetParent(parent, false);
			root.transform.position = position;
			root.AddComponent<AssetIdTag>().Id = assetId;
			var node = root.AddComponent<ResourceNode>();
			node.ItemId = itemId;
			node.Harvests = harvests;
			node.GatherSeconds = gatherSeconds;
			var visual = new GameObject("Visual").transform;
			visual.SetParent(root.transform, false);
			node.Visual = visual;
			return node;
		}

		/// <summary>A signpost, or just text on a wall when <paramref name="onWall"/>, readable from the settlement's centre.</summary>
		static void Sign(Transform parent, string text, Vector3 position, bool onWall = false)
		{
			var wood = Mat("SignWood", new Color(0.45f, 0.3f, 0.18f));
			var root = new GameObject("Sign").transform;
			root.SetParent(parent, false);
			root.localPosition = position;

			var textParent = root;
			if (!onWall)
			{
				var toward = new Vector3(0f, 0f, 0f) - position;
				toward.y = 0f;
				root.rotation = Quaternion.LookRotation(-toward.normalized);
				Prim(PrimitiveType.Cylinder, "Post", root, new Vector3(0f, 1.1f, 0f), new Vector3(0.15f, 1.1f, 0.15f), wood);
				Prim(PrimitiveType.Cube, "Board", root, new Vector3(0f, 2.3f, 0f), new Vector3(2.6f, 1.2f, 0.12f), wood);
				textParent = root;
			}

			// A TextMesh reads correctly for a viewer looking along its +Z. The signpost's +Z points away from
			// the settlement's centre, so the text sits on the near side of the board (-Z).
			var textObject = new GameObject("Text");
			textObject.transform.SetParent(textParent, false);
			textObject.transform.localPosition = new Vector3(0f, onWall ? 0f : 2.3f, onWall ? 0f : -0.08f);
			textObject.transform.localRotation = Quaternion.identity;
			var mesh = textObject.AddComponent<TextMesh>();
			mesh.text = text;
			mesh.anchor = TextAnchor.MiddleCenter;
			mesh.alignment = TextAlignment.Center;
			mesh.characterSize = 0.16f;
			mesh.fontSize = 48;
			mesh.color = Color.white;
			var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			mesh.font = font;
			textObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
		}

		// Shared with the other games: 3d-unity-graybox.
		static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider = false) => GrayboxBuilder.Prim(type, name, parent, position, scale, material, keepCollider);

		static Material Mat(string name, Color colour) => GrayboxBuilder.Mat(name, colour, MaterialDir, 0.15f);
	}
}
