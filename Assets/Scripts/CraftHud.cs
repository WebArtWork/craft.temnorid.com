using ITKamianets.Engine.UI;
using System.Text;
using ITKamianets.Engine.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Temnorid.Craft
{
	/// <summary>
	/// The screen: what the backpack and the warehouse hold, what the player can do right now, short
	/// messages, the goal banner and the pause menu. Plain uGUI built in code; it only shows what
	/// <see cref="CraftGame"/> and <see cref="PlayerWorker"/> say.
	/// </summary>
	public sealed class CraftHud : MonoBehaviour
	{
		public CraftGame Game;
		public PlayerWorker Worker;

		static readonly Color Panel = new Color(0.06f, 0.05f, 0.09f, 0.72f);
		static readonly Color Accent = new Color(0.55f, 0.85f, 0.4f);

		UiBuilder _ui;
		Text _backpack, _warehouse, _prompt, _toast, _banner;
		Image _barBack, _barFill;
		GameObject _pause;
		readonly StringBuilder _text = new StringBuilder();

		void Start()
		{
			_ui = new UiBuilder
			{
				ButtonHighlight = new Color(0.85f, 1f, 0.7f),
				ButtonSelected = new Color(0.7f, 0.95f, 0.5f),
				ButtonPressed = new Color(0.5f, 0.75f, 0.35f),
			};
			EnsureEventSystem();

			var canvasObject = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			canvasObject.transform.SetParent(transform, false);
			canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
			var scaler = canvasObject.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 0.5f;
			var root = canvasObject.transform;

			var left = Box("BackpackPanel", root, new Vector2(0.015f, 0.78f), new Vector2(0.24f, 0.985f), Panel);
			_backpack = Label(left.transform, "Text", "", 30, TextAnchor.UpperLeft, new Vector2(0.06f, 0.04f), new Vector2(0.96f, 0.96f), Color.white);

			var right = Box("WarehousePanel", root, new Vector2(0.72f, 0.78f), new Vector2(0.985f, 0.985f), Panel);
			_warehouse = Label(right.transform, "Text", "", 30, TextAnchor.UpperLeft, new Vector2(0.06f, 0.04f), new Vector2(0.96f, 0.96f), Color.white);

			_prompt = Label(root, "Prompt", "", 38, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.16f), new Vector2(0.8f, 0.24f), Color.white);
			var back = Box("ProgressBack", root, new Vector2(0.4f, 0.13f), new Vector2(0.6f, 0.15f), new Color(0f, 0f, 0f, 0.6f));
			_barBack = back.GetComponent<Image>();
			var fill = Box("ProgressFill", back.transform, Vector2.zero, new Vector2(0f, 1f), Accent);
			_barFill = fill.GetComponent<Image>();

			_toast = Label(root, "Toast", "", 44, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.7f), new Vector2(0.75f, 0.78f), Accent);
			_banner = Label(root, "Banner", "", 80, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.62f), new Color(1f, 0.9f, 0.4f));
			Label(root, "Hint", "WASD move  ·  Shift run  ·  Space jump  ·  hold E gather, press E store  ·  V camera  ·  Esc pause", 22, TextAnchor.LowerLeft, new Vector2(0.015f, 0.01f), new Vector2(0.9f, 0.06f), new Color(1f, 1f, 1f, 0.7f));

			_pause = Box("PauseMenu", root, new Vector2(0.35f, 0.32f), new Vector2(0.65f, 0.68f), new Color(0.06f, 0.05f, 0.09f, 0.9f));
			Label(_pause.transform, "Title", "PAUSED", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.68f), new Vector2(1f, 0.96f), Accent);
			Button(_pause.transform, "Resume", new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.62f), () => Game.SetPaused(false));
			Button(_pause.transform, "Save and quit", new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.34f), () => Game.SaveAndQuit());
			_pause.SetActive(false);
		}

		void Update()
		{
			if (Game == null || _backpack == null) return;
			var session = Game.Session;

			SetText(_backpack, BackpackText(session));
			SetText(_warehouse, WarehouseText(session));
			SetText(_prompt, Worker != null ? Worker.Prompt : "");

			var progress = Worker != null ? Worker.Progress : 0f;
			_barBack.gameObject.SetActive(progress > 0.001f);
			_barFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);

			SetText(_toast, Time.unscaledTime < Game.ToastUntil ? Game.ToastText : "");
			SetText(_banner, Time.unscaledTime < Game.BannerUntil ? "GOAL REACHED!\nKeep gathering if you like." : "");

			if (_pause.activeSelf != Game.Paused)
			{
				_pause.SetActive(Game.Paused);
				if (Game.Paused && EventSystem.current != null)
					EventSystem.current.SetSelectedGameObject(_pause.GetComponentInChildren<UnityEngine.UI.Button>().gameObject);
			}
		}

		string BackpackText(CraftSession session)
		{
			var bag = session.Backpack;
			_text.Clear();
			_text.Append("BACKPACK  ").Append(bag.TotalItems).Append(" / ").Append(bag.SlotCount * bag.StackLimit).Append('\n');
			foreach (var id in Items.All) _text.Append(Items.Name(id)).Append("  ").Append(bag.Count(id)).Append('\n');
			return _text.ToString();
		}

		string WarehouseText(CraftSession session)
		{
			_text.Clear();
			_text.Append("WAREHOUSE  (goal ").Append(session.Goal).Append(")\n");
			foreach (var id in Items.All)
			{
				var stored = session.Warehouse.Count(id);
				_text.Append(Items.Name(id)).Append("  ").Append(stored).Append(" / ").Append(session.Goal);
				if (stored >= session.Goal) _text.Append("  done");
				_text.Append('\n');
			}
			return _text.ToString();
		}

		static void SetText(Text label, string value)
		{
			if (label.text != value) label.text = value;
		}

		// ---- building blocks ----

		// Shared with the other games: 3d-unity-ui.
		void EnsureEventSystem() => UiBuilder.EnsureEventSystem(transform);

		static GameObject Box(string name, Transform parent, Vector2 min, Vector2 max, Color colour) => UiBuilder.Box(name, parent, min, max, colour);

		Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Vector2 min, Vector2 max, Color colour) => _ui.Label(name, parent, text, size, anchor, min, max, colour);

		void Button(Transform parent, string text, Vector2 min, Vector2 max, System.Action onClick) => _ui.Button(text, parent, min, max, onClick);

	}
}
