using ITKamianets.Engine.Graybox;
using ITKamianets.Engine.Graybox.EditorTools;
using ITKamianets.Engine.Shell.Editor;
using System;
using Temnorid.Village;
using ITKamianets.Engine.Character;
using ITKamianets.Engine.CharacterView;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Temnorid.Craft.EditorTools
{
	/// <summary>
	/// Builds the scene from Unity primitives only: a safety ground, the <see cref="VillageView"/> (the village and its farm are drawn at
	/// run time from the player's Lords save or the standard village), the worker, the camera and the game objects. Run it from the menu (Temnorid > Craft > Build Scene) or the
	/// command line (<c>-executeMethod Temnorid.Craft.EditorTools.CraftSceneBuilder.Build</c>); it rebuilds
	/// <c>Assets/Scenes/Main.unity</c> from scratch each time, so do not edit that scene by hand.
	/// </summary>
	public static class CraftSceneBuilder
	{
		const string ScenePath = "Assets/Scenes/Main.unity";
		public const string Version = "0.4.0";
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
			BuildVillage();

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

		// A wide ground a little below the village's own, so the worker never falls while the village is still loading and the land past the
		// farm is grass too. The village draws its own ground on top of it.
		static void BuildGround(Transform world)
		{
			var grass = Mat("Grass", new Color(0.33f, 0.55f, 0.28f));
			Prim(PrimitiveType.Cube, "SafetyGround", world, new Vector3(0f, -0.8f, 0f), new Vector3(600f, 1f, 600f), grass, true);
		}

		/// <summary>The village view: its materials are set here so the shaders are part of the build; the village itself is drawn when the game starts.</summary>
		static void BuildVillage()
		{
			var view = new GameObject("Village").AddComponent<VillageView>();
			view.LitTemplate = Mat("VillageLit", Color.white);
			view.TextMaterial = GrayboxBuilder.WorldTextMaterial(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), MaterialDir);
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

		// Shared with the other games: 3d-unity-graybox.
		static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool keepCollider = false) => GrayboxBuilder.Prim(type, name, parent, position, scale, material, keepCollider);

		static Material Mat(string name, Color colour) => GrayboxBuilder.Mat(name, colour, MaterialDir, 0.15f);
	}
}
