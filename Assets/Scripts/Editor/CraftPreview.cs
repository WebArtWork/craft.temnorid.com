using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Temnorid.Craft.EditorTools
{
	/// <summary>
	/// Renders a few pictures of the settlement to <c>Builds/Preview</c>, to check the level without playing.
	/// Command line (needs a graphics device, so no <c>-nographics</c>):
	/// <c>-executeMethod Temnorid.Craft.EditorTools.CraftPreview.Capture</c>.
	/// </summary>
	public static class CraftPreview
	{
		[MenuItem("Temnorid/Craft/Capture Preview Pictures")]
		public static void Capture()
		{
			EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
			var folder = "Builds/Preview";
			Directory.CreateDirectory(folder);

			Shoot(folder + "/start.png", new Vector3(0f, 2.6f, -4.5f), new Vector3(0f, 1.8f, 12f));
			Shoot(folder + "/aerial.png", new Vector3(0f, 55f, -48f), new Vector3(0f, 0f, -2f));
			Shoot(folder + "/forest.png", new Vector3(-10f, 3f, 1f), new Vector3(-27f, 2f, 3f));
			Shoot(folder + "/warehouse.png", new Vector3(0f, 3f, 6f), new Vector3(0f, 2.5f, 18f));
			Debug.Log("[Craft] Preview pictures written to " + folder);
		}

		static void Shoot(string path, Vector3 position, Vector3 lookAt)
		{
			var camera = Camera.main;
			camera.transform.position = position;
			camera.transform.LookAt(lookAt);

			var texture = new RenderTexture(1280, 720, 24);
			camera.targetTexture = texture;
			camera.Render();
			RenderTexture.active = texture;
			var picture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
			picture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
			picture.Apply();
			File.WriteAllBytes(path, picture.EncodeToPNG());

			camera.targetTexture = null;
			RenderTexture.active = null;
			Object.DestroyImmediate(texture);
			Object.DestroyImmediate(picture);
		}
	}
}
