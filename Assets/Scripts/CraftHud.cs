using ITKamianets.Engine.UI;
using System.Text;
using ITKamianets.Engine.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using ITKamianets.Engine.Shell;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
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

		/// <summary>Tests set this to build the touch controls on a desktop.</summary>
		public static bool ForceTouch;
		/// <summary>True on a phone (or when forced): texts name the touch buttons, not keys.</summary>
		public static bool TouchMode => TouchButton.IsNeeded || ForceTouch;

		public bool HasTouchControls { get; private set; }

		ShellChrome _chrome;
		GameObject _stockPanel;
		Text _stockText, _stockWarehouse, _stockWorkers, _stockVillage, _loading;
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
			// The shell's bars (top, and the places at the bottom on a phone); the game's own screen lives between them.
			_chrome = ShellChrome.Create(transform);
			_chrome.PlaceSelected += OnPlace;
			var hudObject = new GameObject("Hud", typeof(RectTransform));
			hudObject.transform.SetParent(canvasObject.transform, false);
			var hudRect = (RectTransform)hudObject.transform;
			hudRect.anchorMin = new Vector2(0f, _chrome.BottomFraction);
			hudRect.anchorMax = new Vector2(1f, 1f - _chrome.TopFraction);
			hudRect.offsetMin = Vector2.zero;
			hudRect.offsetMax = Vector2.zero;
			var root = hudObject.transform;
			var touch = TouchMode;
			if (touch) BuildLookArea(root);

			var left = Box("BackpackPanel", root, new Vector2(0.015f, 0.58f), new Vector2(0.25f, 0.985f), Panel);
			_backpack = Label(left.transform, "Text", "", 28, TextAnchor.UpperLeft, new Vector2(0.06f, 0.04f), new Vector2(0.96f, 0.96f), Color.white);

			var right = Box("WarehousePanel", root, new Vector2(0.72f, 0.58f), new Vector2(0.985f, 0.985f), Panel);
			_warehouse = Label(right.transform, "Text", "", 28, TextAnchor.UpperLeft, new Vector2(0.06f, 0.04f), new Vector2(0.96f, 0.96f), Color.white);

			_prompt = Label(root, "Prompt", "", touch ? 34 : 38, TextAnchor.MiddleCenter, new Vector2(touch ? 0.2f : 0.2f, 0.16f), new Vector2(touch ? 0.62f : 0.8f, 0.24f), Color.white);
			var back = Box("ProgressBack", root, new Vector2(0.4f, 0.13f), new Vector2(0.6f, 0.15f), new Color(0f, 0f, 0f, 0.6f));
			_barBack = back.GetComponent<Image>();
			var fill = Box("ProgressFill", back.transform, Vector2.zero, new Vector2(0f, 1f), Accent);
			_barFill = fill.GetComponent<Image>();

			_loading = Label(root, "Loading", "", 56, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.62f), Color.white);
			_toast = Label(root, "Toast", "", 44, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.7f), new Vector2(0.75f, 0.78f), Accent);
			_banner = Label(root, "Banner", "", 80, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.62f), new Color(1f, 0.9f, 0.4f));
			if (!touch) Label(root, "Hint", "WASD move  ·  Shift run  ·  Space jump  ·  hold E gather, press E store  ·  V camera  ·  Esc pause", 22, TextAnchor.LowerLeft, new Vector2(0.015f, 0.01f), new Vector2(0.9f, 0.06f), new Color(1f, 1f, 1f, 0.7f));
			else BuildTouch(root);

			_pause = Box("PauseMenu", root, new Vector2(0.35f, 0.32f), new Vector2(0.65f, 0.68f), new Color(0.06f, 0.05f, 0.09f, 0.9f));
			Label(_pause.transform, "Title", "PAUSED", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.68f), new Vector2(1f, 0.96f), Accent);
			Button(_pause.transform, "Resume", new Vector2(0.2f, 0.4f), new Vector2(0.8f, 0.62f), () => Game.SetPaused(false));
			Button(_pause.transform, "Save, main menu", new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.34f), () => Game.QuitToMenu());
			_pause.SetActive(false);

			_stockPanel = Box("StockPanel", root, new Vector2(0.2f, 0.06f), new Vector2(0.8f, 0.94f), new Color(0.06f, 0.05f, 0.09f, 0.94f));
			Label(_stockPanel.transform, "Title", "STOCK", 56, TextAnchor.MiddleCenter, new Vector2(0f, 0.88f), new Vector2(1f, 0.99f), Accent);
			_stockVillage = Label(_stockPanel.transform, "Village", "", 28, TextAnchor.UpperCenter, new Vector2(0.03f, 0.78f), new Vector2(0.97f, 0.88f), new Color(1f, 0.95f, 0.7f));
			_stockText = Label(_stockPanel.transform, "Backpack", "", 32, TextAnchor.UpperLeft, new Vector2(0.05f, 0.3f), new Vector2(0.48f, 0.77f), Color.white);
			_stockWarehouse = Label(_stockPanel.transform, "Warehouse", "", 32, TextAnchor.UpperLeft, new Vector2(0.52f, 0.3f), new Vector2(0.95f, 0.77f), Color.white);
			_stockWorkers = Label(_stockPanel.transform, "Workers", "", 28, TextAnchor.UpperLeft, new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.29f), Color.white);
			Button(_stockPanel.transform, "Back to the settlement", new Vector2(0.2f, 0.04f), new Vector2(0.8f, 0.16f), () => _chrome.Select("world"));
			_stockPanel.SetActive(false);
		}

		void Update()
		{
			if (Game == null || _backpack == null) return;
			var session = Game.Session;
			if (_chrome != null) _chrome.SetStatus(Game.Net != null ? "Online: " + (Game.Net.Workers.Count + 1) + " workers" : Game.Village != null ? Game.Village.Title : "Loading");
			SetText(_loading, Game.Loading ? (Game.Net != null && Game.Net.IsClient ? "Loading the host's village…" : "Loading your village…") : "");
			if (_stockPanel.activeSelf)
			{
				SetText(_stockText, BackpackText(session));
				SetText(_stockWarehouse, WarehouseText(session));
				SetText(_stockWorkers, WorkersText());
				SetText(_stockVillage, VillageText());
			}

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

		void OnPlace(string id)
		{
			if (Game == null) return;
			_stockPanel.SetActive(id == "stock");
			// Any place but the settlement holds the game (offline); the profile panel is the shell's own.
			Game.SetUiHold(id != "world");
		}

		// The village on screen, and why: "Ann's village  ·  Linked to Lords." or "Standard village  ·  Guest account: ...".
		string VillageText()
		{
			var village = Game.Village;
			if (village == null) return "";
			_text.Clear();
			_text.Append(village.Title);
			var status = village.IsLords && !string.IsNullOrEmpty(village.Stock.Status) ? village.Stock.Status : village.Status;
			if (Game.Net != null && Game.Net.IsClient) status = village.Status;
			if (!string.IsNullOrEmpty(status)) _text.Append("  ·  ").Append(status);
			return _text.ToString();
		}

		string WorkersText()
		{
			if (Game.Net == null) return "";
			var sb = new StringBuilder("WORKERS HERE: You");
			foreach (var pair in Game.Net.Workers) if (pair.Value != null) sb.Append("  ·  ").Append(pair.Value.DisplayName);
			return sb.ToString();
		}

		// ---- touch: a stick on the left, buttons on the right, dragging anywhere else turns the view ----

		void BuildLookArea(Transform parent)
		{
			var area = UiBuilder.Box("LookArea", parent, Vector2.zero, Vector2.one, Color.clear);
			area.AddComponent<TouchLookArea>();
		}

		void BuildTouch(Transform parent)
		{
			HasTouchControls = true;
			var holder = UiBuilder.Box("Touch", parent, Vector2.zero, Vector2.one, Color.clear);
			holder.GetComponent<Image>().raycastTarget = false;
			var t = holder.transform;

			var stickBase = UiBuilder.Box("StickBase", t, new Vector2(0.03f, 0.07f), new Vector2(0.2f, 0.5f), new Color(1f, 1f, 1f, 0.1f));
			stickBase.GetComponent<Image>().raycastTarget = false;
			var knob = UiBuilder.Box("StickKnob", stickBase.transform, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), new Color(1f, 1f, 1f, 0.3f));
			knob.SetActive(false);
			var stick = knob.AddComponent<OnScreenStick>();
			stick.controlPath = EngineTouchControls.Path(EngineTouchControls.Move);
			stick.movementRange = 120f;
			knob.SetActive(true);

			TouchButtonFor(t, "Use", EngineTouchControls.Interact, new Vector2(0.78f, 0.06f), new Vector2(0.97f, 0.34f), 40);
			TouchButtonFor(t, "Jump", EngineTouchControls.Jump, new Vector2(0.82f, 0.38f), new Vector2(0.96f, 0.58f), 32);
			TouchButtonFor(t, "Run", EngineTouchControls.Sprint, new Vector2(0.22f, 0.07f), new Vector2(0.33f, 0.22f), 30);
			TouchButtonFor(t, "View", EngineTouchControls.ToggleView, new Vector2(0.64f, 0.38f), new Vector2(0.76f, 0.52f), 28);
			var bag = UiBuilder.Box("Touch Bag", t, new Vector2(0.64f, 0.06f), new Vector2(0.76f, 0.24f), new Color(1f, 1f, 1f, 0.2f));
			Label(bag.transform, "Text", "Bag", 32, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.95f));
			var bagButton = bag.AddComponent<UnityEngine.UI.Button>();
			bagButton.onClick.AddListener(() => _chrome.Select("stock"));
		}

		void TouchButtonFor(Transform parent, string label, string control, Vector2 min, Vector2 max, int size)
		{
			var box = UiBuilder.Box("Touch " + label, parent, min, max, new Color(1f, 1f, 1f, 0.2f));
			Label(box.transform, "Text", label, size, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0.95f));
			box.SetActive(false);
			var button = box.AddComponent<OnScreenButton>();
			button.controlPath = EngineTouchControls.Path(control);
			box.SetActive(true);
		}

		string BackpackText(CraftSession session)
		{
			var bag = session.Backpack;
			_text.Clear();
			_text.Append("BACKPACK  ").Append(bag.TotalItems).Append(" / ").Append(bag.SlotCount * bag.StackLimit).Append('\n');
			foreach (var id in Items.All) _text.Append(Items.Name(id)).Append("  ").Append(bag.Count(id)).Append('\n');
			return _text.ToString();
		}

		// The lord's stores: what is there now and what the stores hold at most, then the goal of this visit.
		string WarehouseText(CraftSession session)
		{
			_text.Clear();
			var stock = session.Stock;
			_text.Append(stock.IsShared ? "STORES  (shared with Lords)\n" : "STORES\n");
			foreach (var id in Items.All)
				_text.Append(Items.Name(id)).Append("  ").Append(stock.Count(id)).Append(" / ").Append(Mathf.RoundToInt((float)stock.Capacity(id))).Append('\n');
			var done = 0;
			foreach (var id in Items.All) if (session.StoredThisVisit(id) >= session.Goal) done++;
			_text.Append("GOAL  store ").Append(session.Goal).Append(" of each: ").Append(session.GoalReached ? "done" : done + " of " + Items.All.Length);
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
