using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum PitchInteractionMode
{
    DragPlayers,
    DrawLines,
}

/// <summary>
/// Spawns the rugby pitch prefab and a screen-space Back control to return to the home scene.
/// </summary>
public class PitchSetup : MonoBehaviour
{
    const int PlayerCount = 15;
    const float PlayMoveSpeed = 320f;
    static readonly Vector2 PlayerChipSize = new Vector2(58, 58);
    static readonly Color PlayerChipColor = new Color(0.14f, 0.32f, 0.62f, 1f);
    static readonly Color DefendingPlayerChipColor = new Color(0.62f, 0.16f, 0.16f, 1f);

    [SerializeField] GameObject rugbyPitchPrefab;
    [SerializeField] string homeSceneName = "HomePage";

    public string HomeSceneName => homeSceneName;

    public PitchInteractionMode InteractionMode => _interactionMode;

    public bool EraseModeActive => _eraseModeActive;

    Font _font;
    RectTransform _pitchHudArea;
    RectTransform _dragLayer;
    readonly Dictionary<int, GameObject> _trayItems = new();
    readonly Dictionary<int, RectTransform> _placedPlayers = new();
    readonly Dictionary<int, Vector2> _playerStartPitchLocal = new();
    readonly Dictionary<int, RectTransform> _playerLines = new();
    readonly Dictionary<int, List<Vector2>> _playerLinePaths = new();
    RectTransform _linesRoot;
    PlacedPlayerLineDrawer _activeLineDrawer;
    RectTransform _dragPreview;
    GameObject _resetConfirmDialog;
    PitchInteractionMode _interactionMode = PitchInteractionMode.DragPlayers;
    Text _interactionModeButtonLabel;
    Button _playButton;
    Coroutine _playRoutine;
    bool _eraseModeActive;
    Button _eraseButton;
    Text _eraseButtonLabel;

    const int MaxUndoDepth = 50;
    readonly List<PitchEditorSnapshot> _undo = new();
    readonly List<PitchEditorSnapshot> _redo = new();
    Button _undoButton;
    Button _redoButton;

    RectTransform _activePlacedChipDrag;

    void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Font.CreateDynamicFontFromOSFont("Arial", 16);

        EnsureEventSystem();
        BuildHud();
    }

    void Start()
    {
        if (rugbyPitchPrefab == null)
        {
            Debug.LogError($"{nameof(PitchSetup)}: assign Rugby Pitch Prefab on {gameObject.name}.", this);
            return;
        }

        Instantiate(rugbyPitchPrefab, transform);
    }

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    void BuildHud()
    {
        var canvasGo = new GameObject("PitchHUD");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var root = canvasGo.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        var attackTray = AddSideTray(canvasGo.transform, "Attacking", PlayerChipColor, 0, -112f);
        var defendTray = AddSideTray(canvasGo.transform, "Defending", DefendingPlayerChipColor, PlayerCount, attackTray.nextStackTopY);

        var rightArea = new GameObject("PitchHudArea");
        rightArea.transform.SetParent(canvasGo.transform, false);
        _pitchHudArea = rightArea.AddComponent<RectTransform>();
        _pitchHudArea.anchorMin = new Vector2(0, 0);
        _pitchHudArea.anchorMax = new Vector2(1, 1);
        const float modeStripWidth = 128f;
        _pitchHudArea.offsetMin = new Vector2(260, 20);
        _pitchHudArea.offsetMax = new Vector2(-(20f + modeStripWidth), -20);

        var linesRootGo = new GameObject("LinesRoot");
        linesRootGo.transform.SetParent(_pitchHudArea.transform, false);
        _linesRoot = linesRootGo.AddComponent<RectTransform>();
        _linesRoot.anchorMin = Vector2.zero;
        _linesRoot.anchorMax = Vector2.one;
        _linesRoot.offsetMin = Vector2.zero;
        _linesRoot.offsetMax = Vector2.zero;
        linesRootGo.transform.SetAsFirstSibling();

        var dragLayerGo = new GameObject("DragLayer");
        dragLayerGo.transform.SetParent(canvasGo.transform, false);
        _dragLayer = dragLayerGo.AddComponent<RectTransform>();
        _dragLayer.anchorMin = Vector2.zero;
        _dragLayer.anchorMax = Vector2.one;
        _dragLayer.offsetMin = Vector2.zero;
        _dragLayer.offsetMax = Vector2.zero;

        var btnGo = new GameObject("BackButton");
        btnGo.transform.SetParent(canvasGo.transform, false);
        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(24, -24);
        rt.sizeDelta = new Vector2(200, 72);

        var img = btnGo.AddComponent<Image>();
        img.color = new Color(0.28f, 0.3f, 0.36f, 0.95f);

        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(GoHome);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<Text>();
        text.font = _font;
        text.fontSize = 30;
        text.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        text.alignment = TextAnchor.MiddleCenter;
        text.text = "Back";
        text.raycastTarget = false;

        const float undoRedoTopY = -24f;
        const float undoRedoHeight = 72f;
        var undoBtnGo = new GameObject("UndoButton");
        undoBtnGo.transform.SetParent(canvasGo.transform, false);
        var undoRt = undoBtnGo.AddComponent<RectTransform>();
        undoRt.anchorMin = new Vector2(0, 1);
        undoRt.anchorMax = new Vector2(0, 1);
        undoRt.pivot = new Vector2(0, 1);
        undoRt.anchoredPosition = new Vector2(24f + 200f + 12f, undoRedoTopY);
        undoRt.sizeDelta = new Vector2(96, undoRedoHeight);
        var undoImg = undoBtnGo.AddComponent<Image>();
        undoImg.color = new Color(0.28f, 0.3f, 0.36f, 0.95f);
        _undoButton = undoBtnGo.AddComponent<Button>();
        _undoButton.targetGraphic = undoImg;
        _undoButton.onClick.AddListener(PerformUndo);
        var undoTextGo = new GameObject("Text");
        undoTextGo.transform.SetParent(undoBtnGo.transform, false);
        var undoTextRt = undoTextGo.AddComponent<RectTransform>();
        undoTextRt.anchorMin = Vector2.zero;
        undoTextRt.anchorMax = Vector2.one;
        undoTextRt.offsetMin = Vector2.zero;
        undoTextRt.offsetMax = Vector2.zero;
        var undoText = undoTextGo.AddComponent<Text>();
        undoText.font = _font;
        undoText.fontSize = 22;
        undoText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        undoText.alignment = TextAnchor.MiddleCenter;
        undoText.text = "Undo";
        undoText.raycastTarget = false;

        var redoBtnGo = new GameObject("RedoButton");
        redoBtnGo.transform.SetParent(canvasGo.transform, false);
        var redoRt = redoBtnGo.AddComponent<RectTransform>();
        redoRt.anchorMin = new Vector2(0, 1);
        redoRt.anchorMax = new Vector2(0, 1);
        redoRt.pivot = new Vector2(0, 1);
        redoRt.anchoredPosition = new Vector2(24f + 200f + 12f + 96f + 12f, undoRedoTopY);
        redoRt.sizeDelta = new Vector2(96, undoRedoHeight);
        var redoImg = redoBtnGo.AddComponent<Image>();
        redoImg.color = new Color(0.28f, 0.3f, 0.36f, 0.95f);
        _redoButton = redoBtnGo.AddComponent<Button>();
        _redoButton.targetGraphic = redoImg;
        _redoButton.onClick.AddListener(PerformRedo);
        var redoTextGo = new GameObject("Text");
        redoTextGo.transform.SetParent(redoBtnGo.transform, false);
        var redoTextRt = redoTextGo.AddComponent<RectTransform>();
        redoTextRt.anchorMin = Vector2.zero;
        redoTextRt.anchorMax = Vector2.one;
        redoTextRt.offsetMin = Vector2.zero;
        redoTextRt.offsetMax = Vector2.zero;
        var redoText = redoTextGo.AddComponent<Text>();
        redoText.font = _font;
        redoText.fontSize = 22;
        redoText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        redoText.alignment = TextAnchor.MiddleCenter;
        redoText.text = "Redo";
        redoText.raycastTarget = false;

        RefreshUndoRedoButtons();

        var resetBtnGo = new GameObject("ResetPlayersButton");
        resetBtnGo.transform.SetParent(canvasGo.transform, false);
        var resetRt = resetBtnGo.AddComponent<RectTransform>();
        resetRt.anchorMin = new Vector2(0, 1);
        resetRt.anchorMax = new Vector2(0, 1);
        resetRt.pivot = new Vector2(0, 1);
        resetRt.anchoredPosition = new Vector2(24, defendTray.nextStackTopY);
        resetRt.sizeDelta = new Vector2(Mathf.Max(attackTray.trayWidth, defendTray.trayWidth), 56);

        var resetImg = resetBtnGo.AddComponent<Image>();
        resetImg.color = new Color(0.28f, 0.3f, 0.36f, 0.95f);

        var resetBtn = resetBtnGo.AddComponent<Button>();
        resetBtn.targetGraphic = resetImg;
        resetBtn.onClick.AddListener(ShowResetConfirmDialog);

        var resetTextGo = new GameObject("Text");
        resetTextGo.transform.SetParent(resetBtnGo.transform, false);
        var resetTextRt = resetTextGo.AddComponent<RectTransform>();
        resetTextRt.anchorMin = Vector2.zero;
        resetTextRt.anchorMax = Vector2.one;
        resetTextRt.offsetMin = Vector2.zero;
        resetTextRt.offsetMax = Vector2.zero;
        var resetText = resetTextGo.AddComponent<Text>();
        resetText.font = _font;
        resetText.fontSize = 26;
        resetText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        resetText.alignment = TextAnchor.MiddleCenter;
        resetText.text = "Reset";
        resetText.raycastTarget = false;

        var modeStripGo = new GameObject("InteractionModeStrip");
        modeStripGo.transform.SetParent(canvasGo.transform, false);
        var modeStripRt = modeStripGo.AddComponent<RectTransform>();
        modeStripRt.anchorMin = new Vector2(1f, 0f);
        modeStripRt.anchorMax = new Vector2(1f, 1f);
        modeStripRt.pivot = new Vector2(1f, 0.5f);
        modeStripRt.offsetMin = new Vector2(-modeStripWidth, 20f);
        modeStripRt.offsetMax = new Vector2(-20f, -20f);
        var modeStripBg = modeStripGo.AddComponent<Image>();
        modeStripBg.color = new Color(0.1f, 0.11f, 0.14f, 0.88f);
        modeStripBg.raycastTarget = true;

        var modeBtnGo = new GameObject("InteractionModeButton");
        modeBtnGo.transform.SetParent(modeStripGo.transform, false);
        var modeBtnRt = modeBtnGo.AddComponent<RectTransform>();
        modeBtnRt.anchorMin = new Vector2(0f, 1f);
        modeBtnRt.anchorMax = new Vector2(1f, 1f);
        modeBtnRt.pivot = new Vector2(0.5f, 1f);
        modeBtnRt.anchoredPosition = new Vector2(0f, -12f);
        modeBtnRt.sizeDelta = new Vector2(-16f, 120f);
        var modeBtnImg = modeBtnGo.AddComponent<Image>();
        modeBtnImg.color = new Color(0.28f, 0.3f, 0.36f, 0.95f);
        var modeBtn = modeBtnGo.AddComponent<Button>();
        modeBtn.targetGraphic = modeBtnImg;
        modeBtn.onClick.AddListener(CycleInteractionMode);
        var modeTextGo = new GameObject("Text");
        modeTextGo.transform.SetParent(modeBtnGo.transform, false);
        var modeTextRt = modeTextGo.AddComponent<RectTransform>();
        modeTextRt.anchorMin = Vector2.zero;
        modeTextRt.anchorMax = Vector2.one;
        modeTextRt.offsetMin = new Vector2(6f, 6f);
        modeTextRt.offsetMax = new Vector2(-6f, -6f);
        _interactionModeButtonLabel = modeTextGo.AddComponent<Text>();
        _interactionModeButtonLabel.font = _font;
        _interactionModeButtonLabel.fontSize = 22;
        _interactionModeButtonLabel.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        _interactionModeButtonLabel.alignment = TextAnchor.MiddleCenter;
        _interactionModeButtonLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        _interactionModeButtonLabel.verticalOverflow = VerticalWrapMode.Truncate;
        _interactionModeButtonLabel.raycastTarget = false;
        RefreshInteractionModeButtonLabel();

        var eraseBtnGo = new GameObject("EraseButton");
        eraseBtnGo.transform.SetParent(modeStripGo.transform, false);
        var eraseBtnRt = eraseBtnGo.AddComponent<RectTransform>();
        eraseBtnRt.anchorMin = new Vector2(0f, 0f);
        eraseBtnRt.anchorMax = new Vector2(1f, 0f);
        eraseBtnRt.pivot = new Vector2(0.5f, 0f);
        eraseBtnRt.anchoredPosition = new Vector2(0f, 12f);
        eraseBtnRt.sizeDelta = new Vector2(-16f, 52f);
        var eraseImg = eraseBtnGo.AddComponent<Image>();
        eraseImg.color = new Color(0.28f, 0.3f, 0.36f, 0.95f);
        _eraseButton = eraseBtnGo.AddComponent<Button>();
        _eraseButton.targetGraphic = eraseImg;
        _eraseButton.onClick.AddListener(OnEraseToggle);
        var eraseTextGo = new GameObject("Text");
        eraseTextGo.transform.SetParent(eraseBtnGo.transform, false);
        var eraseTextRt = eraseTextGo.AddComponent<RectTransform>();
        eraseTextRt.anchorMin = Vector2.zero;
        eraseTextRt.anchorMax = Vector2.one;
        eraseTextRt.offsetMin = Vector2.zero;
        eraseTextRt.offsetMax = Vector2.zero;
        _eraseButtonLabel = eraseTextGo.AddComponent<Text>();
        _eraseButtonLabel.font = _font;
        _eraseButtonLabel.fontSize = 20;
        _eraseButtonLabel.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        _eraseButtonLabel.alignment = TextAnchor.MiddleCenter;
        _eraseButtonLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        _eraseButtonLabel.verticalOverflow = VerticalWrapMode.Truncate;
        _eraseButtonLabel.raycastTarget = false;
        RefreshEraseButtonVisual();

        var playBtnGo = new GameObject("PlayButton");
        playBtnGo.transform.SetParent(modeStripGo.transform, false);
        var playBtnRt = playBtnGo.AddComponent<RectTransform>();
        playBtnRt.anchorMin = new Vector2(0f, 0f);
        playBtnRt.anchorMax = new Vector2(1f, 0f);
        playBtnRt.pivot = new Vector2(0.5f, 0f);
        playBtnRt.anchoredPosition = new Vector2(0f, 12f + 52f + 8f);
        playBtnRt.sizeDelta = new Vector2(-16f, 52f);
        var playImg = playBtnGo.AddComponent<Image>();
        playImg.color = new Color(0.28f, 0.3f, 0.36f, 0.95f);
        var playBtn = playBtnGo.AddComponent<Button>();
        playBtn.targetGraphic = playImg;
        playBtn.onClick.AddListener(OnPlayPressed);
        _playButton = playBtn;
        var playTextGo = new GameObject("Text");
        playTextGo.transform.SetParent(playBtnGo.transform, false);
        var playTextRt = playTextGo.AddComponent<RectTransform>();
        playTextRt.anchorMin = Vector2.zero;
        playTextRt.anchorMax = Vector2.one;
        playTextRt.offsetMin = Vector2.zero;
        playTextRt.offsetMax = Vector2.zero;
        var playText = playTextGo.AddComponent<Text>();
        playText.font = _font;
        playText.fontSize = 22;
        playText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        playText.alignment = TextAnchor.MiddleCenter;
        playText.text = "Play";
        playText.raycastTarget = false;

        const float stripBtnHeight = 52f;
        const float stripBtnGap = 8f;
        const float stripBottomPad = 12f;
        var resetLayoutBtnGo = new GameObject("ResetLayoutButton");
        resetLayoutBtnGo.transform.SetParent(modeStripGo.transform, false);
        var resetLayoutBtnRt = resetLayoutBtnGo.AddComponent<RectTransform>();
        resetLayoutBtnRt.anchorMin = new Vector2(0f, 0f);
        resetLayoutBtnRt.anchorMax = new Vector2(1f, 0f);
        resetLayoutBtnRt.pivot = new Vector2(0.5f, 0f);
        resetLayoutBtnRt.anchoredPosition = new Vector2(0f, stripBottomPad + 2f * (stripBtnHeight + stripBtnGap));
        resetLayoutBtnRt.sizeDelta = new Vector2(-16f, stripBtnHeight);
        var resetLayoutImg = resetLayoutBtnGo.AddComponent<Image>();
        resetLayoutImg.color = new Color(0.28f, 0.3f, 0.36f, 0.95f);
        var resetLayoutBtn = resetLayoutBtnGo.AddComponent<Button>();
        resetLayoutBtn.targetGraphic = resetLayoutImg;
        resetLayoutBtn.onClick.AddListener(OnResetPlayersToStartingPositions);
        var resetLayoutTextGo = new GameObject("Text");
        resetLayoutTextGo.transform.SetParent(resetLayoutBtnGo.transform, false);
        var resetLayoutTextRt = resetLayoutTextGo.AddComponent<RectTransform>();
        resetLayoutTextRt.anchorMin = Vector2.zero;
        resetLayoutTextRt.anchorMax = Vector2.one;
        resetLayoutTextRt.offsetMin = new Vector2(4f, 2f);
        resetLayoutTextRt.offsetMax = new Vector2(-4f, -2f);
        var resetLayoutText = resetLayoutTextGo.AddComponent<Text>();
        resetLayoutText.font = _font;
        resetLayoutText.fontSize = 17;
        resetLayoutText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        resetLayoutText.alignment = TextAnchor.MiddleCenter;
        resetLayoutText.horizontalOverflow = HorizontalWrapMode.Wrap;
        resetLayoutText.verticalOverflow = VerticalWrapMode.Truncate;
        resetLayoutText.text = "Reset\npositions";
        resetLayoutText.raycastTarget = false;

        var dialogRoot = new GameObject("ResetConfirmDialog");
        dialogRoot.transform.SetParent(canvasGo.transform, false);
        _resetConfirmDialog = dialogRoot;
        dialogRoot.SetActive(false);

        var dlgRt = dialogRoot.AddComponent<RectTransform>();
        dlgRt.anchorMin = Vector2.zero;
        dlgRt.anchorMax = Vector2.one;
        dlgRt.offsetMin = Vector2.zero;
        dlgRt.offsetMax = Vector2.zero;
        var dlgBg = dialogRoot.AddComponent<Image>();
        dlgBg.color = new Color(0f, 0f, 0f, 0.55f);
        dlgBg.raycastTarget = true;
        var dlgDismiss = dialogRoot.AddComponent<Button>();
        dlgDismiss.targetGraphic = dlgBg;
        dlgDismiss.transition = Selectable.Transition.None;
        dlgDismiss.onClick.AddListener(HideResetConfirmDialog);

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(dialogRoot.transform, false);
        var panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(560, 260);
        panelRt.anchoredPosition = Vector2.zero;
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.16f, 0.17f, 0.2f, 1f);
        panelImg.raycastTarget = true;

        var msgGo = new GameObject("Message");
        msgGo.transform.SetParent(panelGo.transform, false);
        var msgRt = msgGo.AddComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0.06f, 0.36f);
        msgRt.anchorMax = new Vector2(0.94f, 0.92f);
        msgRt.offsetMin = Vector2.zero;
        msgRt.offsetMax = Vector2.zero;
        var msgText = msgGo.AddComponent<Text>();
        msgText.font = _font;
        msgText.fontSize = 28;
        msgText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        msgText.alignment = TextAnchor.MiddleCenter;
        msgText.text = "Remove all players from the pitch?";
        msgText.horizontalOverflow = HorizontalWrapMode.Wrap;
        msgText.verticalOverflow = VerticalWrapMode.Truncate;
        msgText.raycastTarget = false;

        var cancelGo = new GameObject("CancelButton");
        cancelGo.transform.SetParent(panelGo.transform, false);
        var cancelRt = cancelGo.AddComponent<RectTransform>();
        cancelRt.anchorMin = new Vector2(0.5f, 0.5f);
        cancelRt.anchorMax = new Vector2(0.5f, 0.5f);
        cancelRt.pivot = new Vector2(0.5f, 0.5f);
        cancelRt.sizeDelta = new Vector2(220, 52);
        cancelRt.anchoredPosition = new Vector2(-125, -72);
        var cancelImg = cancelGo.AddComponent<Image>();
        cancelImg.color = new Color(0.28f, 0.3f, 0.36f, 0.95f);
        var cancelBtn = cancelGo.AddComponent<Button>();
        cancelBtn.targetGraphic = cancelImg;
        cancelBtn.onClick.AddListener(HideResetConfirmDialog);
        var cancelTextGo = new GameObject("Text");
        cancelTextGo.transform.SetParent(cancelGo.transform, false);
        var cancelTextRt = cancelTextGo.AddComponent<RectTransform>();
        cancelTextRt.anchorMin = Vector2.zero;
        cancelTextRt.anchorMax = Vector2.one;
        cancelTextRt.offsetMin = Vector2.zero;
        cancelTextRt.offsetMax = Vector2.zero;
        var cancelText = cancelTextGo.AddComponent<Text>();
        cancelText.font = _font;
        cancelText.fontSize = 24;
        cancelText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        cancelText.alignment = TextAnchor.MiddleCenter;
        cancelText.text = "Cancel";
        cancelText.raycastTarget = false;

        var confirmGo = new GameObject("ConfirmResetButton");
        confirmGo.transform.SetParent(panelGo.transform, false);
        var confirmRt = confirmGo.AddComponent<RectTransform>();
        confirmRt.anchorMin = new Vector2(0.5f, 0.5f);
        confirmRt.anchorMax = new Vector2(0.5f, 0.5f);
        confirmRt.pivot = new Vector2(0.5f, 0.5f);
        confirmRt.sizeDelta = new Vector2(220, 52);
        confirmRt.anchoredPosition = new Vector2(125, -72);
        var confirmImg = confirmGo.AddComponent<Image>();
        confirmImg.color = new Color(0.28f, 0.3f, 0.36f, 0.95f);
        var confirmBtn = confirmGo.AddComponent<Button>();
        confirmBtn.targetGraphic = confirmImg;
        confirmBtn.onClick.AddListener(OnResetConfirmed);
        var confirmTextGo = new GameObject("Text");
        confirmTextGo.transform.SetParent(confirmGo.transform, false);
        var confirmTextRt = confirmTextGo.AddComponent<RectTransform>();
        confirmTextRt.anchorMin = Vector2.zero;
        confirmTextRt.anchorMax = Vector2.one;
        confirmTextRt.offsetMin = Vector2.zero;
        confirmTextRt.offsetMax = Vector2.zero;
        var confirmText = confirmTextGo.AddComponent<Text>();
        confirmText.font = _font;
        confirmText.fontSize = 24;
        confirmText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        confirmText.alignment = TextAnchor.MiddleCenter;
        confirmText.text = "Reset";
        confirmText.raycastTarget = false;
    }

    (float nextStackTopY, float trayWidth) AddSideTray(Transform canvasParent, string title, Color slotColor, int playerIdOffset, float trayTopAnchorY)
    {
        const float trayStackGap = 12f;
        const float trayTitleTopInset = 10f;
        const float trayTitleTextHeight = 28f;
        const float trayTitleGapBeforeGrid = 8f;
        var trayTitleBlock = trayTitleTopInset + trayTitleTextHeight + trayTitleGapBeforeGrid;
        const float trayContentMargin = 20f;

        var trayBox = new GameObject($"{title}Tray");
        trayBox.transform.SetParent(canvasParent, false);
        var trayBoxRt = trayBox.AddComponent<RectTransform>();
        trayBoxRt.anchorMin = new Vector2(0, 1);
        trayBoxRt.anchorMax = new Vector2(0, 1);
        trayBoxRt.pivot = new Vector2(0, 1);
        trayBoxRt.anchoredPosition = new Vector2(24f, trayTopAnchorY);
        var trayBg = trayBox.AddComponent<Image>();
        trayBg.color = new Color(0.1f, 0.11f, 0.14f, 0.88f);

        var trayTitleGo = new GameObject("TrayTitle");
        trayTitleGo.transform.SetParent(trayBox.transform, false);
        var trayTitleRt = trayTitleGo.AddComponent<RectTransform>();
        trayTitleRt.anchorMin = new Vector2(0f, 1f);
        trayTitleRt.anchorMax = new Vector2(1f, 1f);
        trayTitleRt.pivot = new Vector2(0.5f, 1f);
        trayTitleRt.anchoredPosition = new Vector2(0f, -trayTitleTopInset);
        trayTitleRt.sizeDelta = new Vector2(-20f, trayTitleTextHeight);
        var trayTitleText = trayTitleGo.AddComponent<Text>();
        trayTitleText.font = _font;
        trayTitleText.fontSize = 22;
        trayTitleText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        trayTitleText.alignment = TextAnchor.MiddleCenter;
        trayTitleText.text = title;
        trayTitleText.raycastTarget = false;

        var trayContent = new GameObject("TrayContent");
        trayContent.transform.SetParent(trayBox.transform, false);
        var trayContentRt = trayContent.AddComponent<RectTransform>();
        trayContentRt.anchorMin = Vector2.zero;
        trayContentRt.anchorMax = Vector2.one;
        trayContentRt.offsetMin = new Vector2(10, 10);
        trayContentRt.offsetMax = new Vector2(-10, -(10f + trayTitleBlock));
        var trayGrid = trayContent.AddComponent<GridLayoutGroup>();
        trayGrid.cellSize = new Vector2(58, 58);
        trayGrid.spacing = new Vector2(8, 8);
        trayGrid.padding = new RectOffset(6, 6, 6, 6);
        trayGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        trayGrid.constraintCount = 3;
        trayGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        trayGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        trayGrid.childAlignment = TextAnchor.UpperCenter;

        int trayRows = (PlayerCount + trayGrid.constraintCount - 1) / trayGrid.constraintCount;
        float gridHeight = trayGrid.padding.vertical + trayRows * trayGrid.cellSize.y
            + Mathf.Max(0, trayRows - 1) * trayGrid.spacing.y;
        float gridWidth = trayGrid.padding.horizontal + trayGrid.constraintCount * trayGrid.cellSize.x
            + Mathf.Max(0, trayGrid.constraintCount - 1) * trayGrid.spacing.x;
        trayBoxRt.sizeDelta = new Vector2(gridWidth + trayContentMargin, gridHeight + trayContentMargin + trayTitleBlock);

        for (var d = 1; d <= PlayerCount; d++)
        {
            var playerId = d + playerIdOffset;
            CreatePlayerTrayButton(trayContent.transform, d, playerId, slotColor);
        }

        var nextTop = trayTopAnchorY - trayBoxRt.sizeDelta.y - trayStackGap;
        return (nextTop, trayBoxRt.sizeDelta.x);
    }

    void CreatePlayerTrayButton(Transform parent, int displayNumber, int playerId, Color traySlotColor)
    {
        var go = new GameObject($"Player{playerId}TrayItem");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = PlayerChipSize;

        var img = go.AddComponent<Image>();
        img.color = traySlotColor;
        img.raycastTarget = true;

        var drag = go.AddComponent<PlayerTrayDragItem>();
        drag.Init(this, playerId);

        var trayGroup = go.AddComponent<CanvasGroup>();
        trayGroup.alpha = 1f;
        trayGroup.interactable = true;
        trayGroup.blocksRaycasts = true;

        _trayItems[playerId] = go;

        var labelGo = new GameObject("Text");
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        var label = labelGo.AddComponent<Text>();
        label.font = _font;
        label.fontSize = 24;
        label.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        label.alignment = TextAnchor.MiddleCenter;
        label.text = displayNumber.ToString();
        label.raycastTarget = false;
    }

    static string ChipLabelForPlayerId(int playerId) =>
        playerId > PlayerCount ? (playerId - PlayerCount).ToString() : playerId.ToString();

    static Color ChipPreviewTint(Color opaque) =>
        new Color(opaque.r, opaque.g, opaque.b, 0.65f);

    Color PlacedChipColorForPlayerId(int playerId) =>
        playerId > PlayerCount ? DefendingPlayerChipColor : PlayerChipColor;

    public void OnTrayBeginDrag(int playerNumber, PointerEventData eventData)
    {
        if (_eraseModeActive)
            return;

        if (_interactionMode != PitchInteractionMode.DragPlayers)
            return;

        if (_dragPreview != null)
            Destroy(_dragPreview.gameObject);

        var previewColor = ChipPreviewTint(PlacedChipColorForPlayerId(playerNumber));
        _dragPreview = CreatePlayerChip(ChipLabelForPlayerId(playerNumber), previewColor, _dragLayer, raycastTarget: false);
        UpdateDragPreviewPosition(eventData);
    }

    public void OnTrayDrag(PointerEventData eventData)
    {
        if (_eraseModeActive)
            return;

        if (_interactionMode != PitchInteractionMode.DragPlayers)
            return;

        UpdateDragPreviewPosition(eventData);
    }

    public void OnTrayEndDrag(int playerNumber, PointerEventData eventData)
    {
        if (_dragPreview != null)
            Destroy(_dragPreview.gameObject);
        _dragPreview = null;

        if (_eraseModeActive)
            return;

        if (_interactionMode != PitchInteractionMode.DragPlayers)
            return;

        if (_pitchHudArea == null)
            return;

        if (!RectTransformUtility.RectangleContainsScreenPoint(_pitchHudArea, eventData.position, null))
            return;

        PushUndoCurrent();
        PlacePlayerChipCore(playerNumber, eventData.position);
    }

    void UpdateDragPreviewPosition(PointerEventData eventData)
    {
        if (_dragPreview == null || _dragLayer == null)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragLayer, eventData.position, null, out var localPoint))
        {
            _dragPreview.anchorMin = new Vector2(0.5f, 0.5f);
            _dragPreview.anchorMax = new Vector2(0.5f, 0.5f);
            _dragPreview.anchoredPosition = localPoint;
        }
    }

    void PlacePlayerChipCore(int playerNumber, Vector2 screenPosition)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_pitchHudArea, screenPosition, null, out var localPoint))
            return;

        if (_placedPlayers.TryGetValue(playerNumber, out var existing) && existing != null)
        {
            existing.anchorMin = new Vector2(0.5f, 0.5f);
            existing.anchorMax = new Vector2(0.5f, 0.5f);
            existing.anchoredPosition = localPoint;
            _playerStartPitchLocal[playerNumber] = localPoint;
            SetTraySlotForPlayerOnPitch(playerNumber, true);
            return;
        }

        SpawnPlacedPlayerChip(playerNumber, localPoint);
        _playerStartPitchLocal[playerNumber] = localPoint;
        SetTraySlotForPlayerOnPitch(playerNumber, true);
    }

    RectTransform SpawnPlacedPlayerChip(int playerNumber, Vector2 localPoint)
    {
        var chipColor = PlacedChipColorForPlayerId(playerNumber);
        var chip = CreatePlayerChip(ChipLabelForPlayerId(playerNumber), chipColor, _pitchHudArea);
        chip.gameObject.name = $"Player{playerNumber}Chip";
        chip.anchorMin = new Vector2(0.5f, 0.5f);
        chip.anchorMax = new Vector2(0.5f, 0.5f);
        chip.anchoredPosition = localPoint;

        var drag = chip.gameObject.AddComponent<PlacedPlayerDragItem>();
        drag.Init(this, chip);

        var lineDrawer = chip.gameObject.AddComponent<PlacedPlayerLineDrawer>();
        lineDrawer.Init(this, chip, playerNumber);

        var eraseTap = chip.gameObject.AddComponent<PitchChipEraseTap>();
        eraseTap.Init(this, playerNumber);

        _placedPlayers[playerNumber] = chip;
        return chip;
    }

    void SetTraySlotForPlayerOnPitch(int playerNumber, bool onPitch)
    {
        if (!_trayItems.TryGetValue(playerNumber, out var trayGo) || trayGo == null)
            return;

        var cg = trayGo.GetComponent<CanvasGroup>();
        if (cg == null)
            return;

        if (onPitch)
        {
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
        }
        else
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    public void OnPlacedChipDrag(RectTransform chip, PointerEventData eventData)
    {
        if (!chip || !_pitchHudArea)
            return;

        if (_eraseModeActive)
            return;

        if (_interactionMode != PitchInteractionMode.DragPlayers)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_pitchHudArea, eventData.position, null, out var localPoint))
            return;

        var halfW = chip.sizeDelta.x * 0.5f;
        var halfH = chip.sizeDelta.y * 0.5f;
        var rect = _pitchHudArea.rect;
        localPoint.x = Mathf.Clamp(localPoint.x, rect.xMin + halfW, rect.xMax - halfW);
        localPoint.y = Mathf.Clamp(localPoint.y, rect.yMin + halfH, rect.yMax - halfH);

        chip.anchorMin = new Vector2(0.5f, 0.5f);
        chip.anchorMax = new Vector2(0.5f, 0.5f);
        chip.anchoredPosition = localPoint;
    }

    RectTransform CreatePlayerChip(string label, Color bgColor, Transform parent, bool raycastTarget = true)
    {
        var chip = new GameObject("PlayerChip");
        chip.transform.SetParent(parent, false);

        var rt = chip.AddComponent<RectTransform>();
        rt.sizeDelta = PlayerChipSize;

        var img = chip.AddComponent<Image>();
        img.color = bgColor;
        img.raycastTarget = raycastTarget;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(chip.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var text = textGo.AddComponent<Text>();
        text.font = _font;
        text.fontSize = 24;
        text.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        text.alignment = TextAnchor.MiddleCenter;
        text.text = label;
        text.raycastTarget = false;

        return rt;
    }

    static string InteractionModeLabel(PitchInteractionMode mode) =>
        mode switch
        {
            PitchInteractionMode.DragPlayers => "Drag players",
            PitchInteractionMode.DrawLines => "Draw lines",
            _ => mode.ToString(),
        };

    void RefreshInteractionModeButtonLabel()
    {
        if (_interactionModeButtonLabel != null)
            _interactionModeButtonLabel.text = InteractionModeLabel(_interactionMode);
    }

    void CycleInteractionMode()
    {
        ClearEraseMode();
        _activeLineDrawer?.CancelRubberBand();
        var modes = System.Enum.GetValues(typeof(PitchInteractionMode));
        _interactionMode = (PitchInteractionMode)(((int)_interactionMode + 1) % modes.Length);
        RefreshInteractionModeButtonLabel();
    }

    void ShowResetConfirmDialog()
    {
        if (_resetConfirmDialog != null)
            _resetConfirmDialog.SetActive(true);
    }

    void HideResetConfirmDialog()
    {
        if (_resetConfirmDialog != null)
            _resetConfirmDialog.SetActive(false);
    }

    void OnResetConfirmed()
    {
        PushUndoCurrent();
        ResetPlacedPlayers();
        HideResetConfirmDialog();
    }

    void OnResetPlayersToStartingPositions()
    {
        if (_dragPreview != null)
        {
            Destroy(_dragPreview.gameObject);
            _dragPreview = null;
        }

        _activeLineDrawer?.CancelRubberBand();

        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        if (_playButton != null)
            _playButton.interactable = true;

        ClearEraseMode();

        if (_pitchHudArea == null)
            return;

        PushUndoCurrent();

        var rect = _pitchHudArea.rect;
        foreach (var kv in _placedPlayers)
        {
            var chip = kv.Value;
            if (chip == null || !_playerStartPitchLocal.TryGetValue(kv.Key, out var home))
                continue;

            var halfW = chip.sizeDelta.x * 0.5f;
            var halfH = chip.sizeDelta.y * 0.5f;
            home.x = Mathf.Clamp(home.x, rect.xMin + halfW, rect.xMax - halfW);
            home.y = Mathf.Clamp(home.y, rect.yMin + halfH, rect.yMax - halfH);
            chip.anchorMin = new Vector2(0.5f, 0.5f);
            chip.anchorMax = new Vector2(0.5f, 0.5f);
            chip.anchoredPosition = home;
        }
    }

    void ResetPlacedPlayers()
    {
        if (_dragPreview != null)
        {
            Destroy(_dragPreview.gameObject);
            _dragPreview = null;
        }

        _activeLineDrawer?.CancelRubberBand();

        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        if (_playButton != null)
            _playButton.interactable = true;

        ClearEraseMode();

        EndActivePlacedChipDragIfNeeded();

        foreach (var line in _playerLines.Values)
        {
            if (line != null)
                Destroy(line.gameObject);
        }

        _playerLines.Clear();
        _playerLinePaths.Clear();

        foreach (var chip in _placedPlayers.Values)
        {
            if (chip != null)
                Destroy(chip.gameObject);
        }

        _placedPlayers.Clear();
        _playerStartPitchLocal.Clear();

        for (var n = 1; n <= PlayerCount * 2; n++)
            SetTraySlotForPlayerOnPitch(n, false);
    }

    internal void SetActiveLineDrawer(PlacedPlayerLineDrawer drawer)
    {
        _activeLineDrawer = drawer;
    }

    internal void ClearActiveLineDrawer(PlacedPlayerLineDrawer drawer)
    {
        if (_activeLineDrawer == drawer)
            _activeLineDrawer = null;
    }

    internal void RemoveCommittedLineForPlayer(int playerNumber)
    {
        _playerLinePaths.Remove(playerNumber);
        if (_playerLines.TryGetValue(playerNumber, out var line) && line != null)
        {
            Destroy(line.gameObject);
            _playerLines.Remove(playerNumber);
        }
    }

    internal RectTransform CreatePlayerLineRoot()
    {
        var go = new GameObject("PlayerLine");
        go.transform.SetParent(_linesRoot, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    internal void RebuildPlayerPolyline(RectTransform lineRoot, IReadOnlyList<Vector2> anchors, Vector2 trailEnd, bool drawTrail, int lineOwnerPlayerNumber)
    {
        if (lineRoot == null)
            return;

        for (int i = lineRoot.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(lineRoot.GetChild(i).gameObject);

        if (anchors == null || anchors.Count < 1)
            return;

        for (int i = 0; i < anchors.Count - 1; i++)
            AddLineSegmentChild(lineRoot, anchors[i], anchors[i + 1], lineOwnerPlayerNumber);

        if (drawTrail && anchors.Count > 0)
        {
            var from = anchors[anchors.Count - 1];
            if ((trailEnd - from).sqrMagnitude > 0.0001f)
                AddLineSegmentChild(lineRoot, from, trailEnd, lineOwnerPlayerNumber);
        }
    }

    void AddLineSegmentChild(Transform parent, Vector2 startLocal, Vector2 endLocal, int lineOwnerPlayerNumber)
    {
        var go = new GameObject("Segment");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        var img = go.AddComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = _eraseModeActive;
        // Thin visuals stay 2px tall; padding widens hit-testing without changing layout bounds.
        img.raycastPadding = new Vector4(0f, 14f, 0f, 14f);
        LayoutLineSegment(rt, startLocal, endLocal);
        var segTap = go.AddComponent<PitchLineSegmentEraseTap>();
        segTap.Init(this, lineOwnerPlayerNumber);
    }

    internal void LayoutLineSegment(RectTransform lineRt, Vector2 startLocal, Vector2 endLocal)
    {
        if (lineRt == null)
            return;

        var delta = endLocal - startLocal;
        var len = delta.magnitude;
        if (len < 0.001f)
            len = 0.001f;

        var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        lineRt.anchorMin = new Vector2(0.5f, 0.5f);
        lineRt.anchorMax = new Vector2(0.5f, 0.5f);
        lineRt.pivot = new Vector2(0f, 0.5f);
        lineRt.anchoredPosition = startLocal;
        lineRt.localRotation = Quaternion.Euler(0f, 0f, angle);
        lineRt.sizeDelta = new Vector2(len, 2f);
    }

    internal Vector2 ScreenPointToClampedPitchLocal(Vector2 screenPosition)
    {
        if (_pitchHudArea == null)
            return Vector2.zero;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_pitchHudArea, screenPosition, null, out var local))
            return Vector2.zero;

        const float margin = 1f;
        var r = _pitchHudArea.rect;
        local.x = Mathf.Clamp(local.x, r.xMin + margin, r.xMax - margin);
        local.y = Mathf.Clamp(local.y, r.yMin + margin, r.yMax - margin);
        return local;
    }

    internal void CommitPlayerLine(int playerNumber, RectTransform lineRt, IReadOnlyList<Vector2> pathPoints)
    {
        if (lineRt == null)
            return;

        RemoveCommittedLineForPlayer(playerNumber);
        _playerLines[playerNumber] = lineRt;

        if (pathPoints != null && pathPoints.Count >= 2)
            _playerLinePaths[playerNumber] = new List<Vector2>(pathPoints);

        SetLineSegmentsRaycastForErase(_eraseModeActive);
    }

    void OnEraseToggle()
    {
        _eraseModeActive = !_eraseModeActive;
        RefreshEraseButtonVisual();
        SetLineSegmentsRaycastForErase(_eraseModeActive);
        RefreshLinesRootSiblingOrder();
    }

    void ClearEraseMode()
    {
        if (!_eraseModeActive)
            return;

        _eraseModeActive = false;
        RefreshEraseButtonVisual();
        SetLineSegmentsRaycastForErase(false);
        RefreshLinesRootSiblingOrder();
    }

    void RefreshLinesRootSiblingOrder()
    {
        if (_linesRoot == null)
            return;

        if (_eraseModeActive)
            _linesRoot.SetAsLastSibling();
        else
            _linesRoot.SetAsFirstSibling();
    }

    void RefreshEraseButtonVisual()
    {
        if (_eraseButtonLabel != null)
            _eraseButtonLabel.text = _eraseModeActive ? "Erase on" : "Erase";

        if (_eraseButton != null)
        {
            var img = _eraseButton.GetComponent<Image>();
            if (img != null)
                img.color = _eraseModeActive
                    ? new Color(0.38f, 0.42f, 0.55f, 0.98f)
                    : new Color(0.28f, 0.3f, 0.36f, 0.95f);
        }
    }

    void SetLineSegmentsRaycastForErase(bool raycast)
    {
        foreach (var lineRoot in _playerLines.Values)
        {
            if (lineRoot == null)
                continue;

            for (var i = 0; i < lineRoot.childCount; i++)
            {
                var img = lineRoot.GetChild(i).GetComponent<Image>();
                if (img != null)
                    img.raycastTarget = raycast;
            }
        }
    }

    public void TryDeletePlacedPlayer(int playerNumber)
    {
        if (!_eraseModeActive)
            return;

        if (!_placedPlayers.TryGetValue(playerNumber, out var chip) || chip == null)
            return;

        PushUndoCurrent();

        Destroy(chip.gameObject);
        _placedPlayers.Remove(playerNumber);
        _playerStartPitchLocal.Remove(playerNumber);
        RemoveCommittedLineForPlayer(playerNumber);
        SetTraySlotForPlayerOnPitch(playerNumber, false);
    }

    public void TryDeleteLineOnly(int playerNumber)
    {
        if (!_eraseModeActive)
            return;

        PushUndoCurrent();

        RemoveCommittedLineForPlayer(playerNumber);
    }

    void OnPlayPressed()
    {
        if (_playRoutine != null)
            return;

        ClearEraseMode();

        _playRoutine = StartCoroutine(RunPlayAnimations());
    }

    IEnumerator RunPlayAnimations()
    {
        if (_playButton != null)
            _playButton.interactable = false;

        var pending = 0;
        foreach (var kv in _placedPlayers)
        {
            var chip = kv.Value;
            if (chip == null)
                continue;

            if (!_playerLinePaths.TryGetValue(kv.Key, out var path) || path == null || path.Count < 2)
                continue;

            pending++;
            var pathCopy = new List<Vector2>(path);
            StartCoroutine(AnimateChipAlongPath(chip, pathCopy, () => pending--));
        }

        while (pending > 0)
            yield return null;

        if (_playButton != null)
            _playButton.interactable = true;

        _playRoutine = null;
    }

    IEnumerator AnimateChipAlongPath(RectTransform chip, List<Vector2> path, System.Action onComplete)
    {
        try
        {
            if (chip == null || path == null || path.Count < 2)
                yield break;

            var len = PolylineLength(path);
            if (len < 0.001f)
                yield break;

            var duration = len / PlayMoveSpeed;
            var u = 0f;
            while (u < duration && chip != null)
            {
                u += Time.deltaTime;
                chip.anchoredPosition = GetPointAlongPolyline(path, Mathf.Clamp01(u / duration));
                yield return null;
            }

            if (chip != null)
                chip.anchoredPosition = path[path.Count - 1];
        }
        finally
        {
            onComplete?.Invoke();
        }
    }

    static float PolylineLength(IReadOnlyList<Vector2> path)
    {
        if (path == null || path.Count < 2)
            return 0f;

        var t = 0f;
        for (var i = 0; i < path.Count - 1; i++)
            t += Vector2.Distance(path[i], path[i + 1]);

        return t;
    }

    static Vector2 GetPointAlongPolyline(IReadOnlyList<Vector2> path, float t)
    {
        if (path == null || path.Count == 0)
            return Vector2.zero;

        if (path.Count == 1)
            return path[0];

        var total = PolylineLength(path);
        if (total < 0.0001f)
            return path[0];

        var dist = t * total;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var seg = Vector2.Distance(path[i], path[i + 1]);
            if (dist <= seg)
                return Vector2.Lerp(path[i], path[i + 1], seg > 0.0001f ? dist / seg : 0f);

            dist -= seg;
        }

        return path[path.Count - 1];
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return;

        var mod = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed
            || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed;
        if (!mod || !kb.zKey.wasPressedThisFrame)
            return;

        if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
            PerformRedo();
        else
            PerformUndo();
    }

    public void PushUndoCurrent()
    {
        CancelTransientForUndo();

        _undo.Add(CaptureSnapshot());
        if (_undo.Count > MaxUndoDepth)
            _undo.RemoveAt(0);

        _redo.Clear();
        RefreshUndoRedoButtons();
    }

    void PerformUndo()
    {
        if (_undo.Count == 0)
            return;

        CancelTransientForUndo();

        _redo.Add(CaptureSnapshot());
        var snap = _undo[_undo.Count - 1];
        _undo.RemoveAt(_undo.Count - 1);
        ApplySnapshot(snap);
        RefreshUndoRedoButtons();
    }

    void PerformRedo()
    {
        if (_redo.Count == 0)
            return;

        CancelTransientForUndo();

        _undo.Add(CaptureSnapshot());
        var snap = _redo[_redo.Count - 1];
        _redo.RemoveAt(_redo.Count - 1);
        ApplySnapshot(snap);
        RefreshUndoRedoButtons();
    }

    void CancelTransientForUndo()
    {
        if (_dragPreview != null)
        {
            Destroy(_dragPreview.gameObject);
            _dragPreview = null;
        }

        _activeLineDrawer?.CancelRubberBand();
    }

    void RefreshUndoRedoButtons()
    {
        if (_undoButton != null)
            _undoButton.interactable = _undo.Count > 0;

        if (_redoButton != null)
            _redoButton.interactable = _redo.Count > 0;
    }

    PitchEditorSnapshot CaptureSnapshot()
    {
        var s = new PitchEditorSnapshot();
        foreach (var kv in _placedPlayers)
        {
            if (kv.Value == null)
                continue;

            s.Placed[kv.Key] = kv.Value.anchoredPosition;
            if (_playerStartPitchLocal.TryGetValue(kv.Key, out var home))
                s.Start[kv.Key] = home;
            else
                s.Start[kv.Key] = kv.Value.anchoredPosition;
        }

        foreach (var kv in _playerLinePaths)
        {
            if (kv.Value != null && kv.Value.Count >= 2)
                s.Lines[kv.Key] = new List<Vector2>(kv.Value);
        }

        return s;
    }

    void ApplySnapshot(PitchEditorSnapshot snap)
    {
        if (snap == null || _pitchHudArea == null)
            return;

        EndActivePlacedChipDragIfNeeded();

        foreach (var line in _playerLines.Values)
        {
            if (line != null)
                Destroy(line.gameObject);
        }

        _playerLines.Clear();
        _playerLinePaths.Clear();

        var removeIds = new List<int>();
        foreach (var kv in _placedPlayers)
        {
            if (!snap.Placed.ContainsKey(kv.Key))
                removeIds.Add(kv.Key);
        }

        foreach (var id in removeIds)
        {
            if (_placedPlayers.TryGetValue(id, out var chip) && chip != null)
                Destroy(chip.gameObject);

            _placedPlayers.Remove(id);
            _playerStartPitchLocal.Remove(id);
            SetTraySlotForPlayerOnPitch(id, false);
        }

        foreach (var kv in snap.Placed)
        {
            var id = kv.Key;
            var pos = kv.Value;
            if (!_placedPlayers.TryGetValue(id, out var chip) || chip == null)
                chip = SpawnPlacedPlayerChip(id, pos);
            else
            {
                chip.anchorMin = new Vector2(0.5f, 0.5f);
                chip.anchorMax = new Vector2(0.5f, 0.5f);
                chip.anchoredPosition = pos;
            }

            if (snap.Start.TryGetValue(id, out var home))
                _playerStartPitchLocal[id] = home;
            else
                _playerStartPitchLocal[id] = pos;

            SetTraySlotForPlayerOnPitch(id, true);
        }

        foreach (var kv in snap.Lines)
        {
            if (kv.Value == null || kv.Value.Count < 2)
                continue;

            if (!snap.Placed.ContainsKey(kv.Key))
                continue;

            RestoreCommittedLine(kv.Key, kv.Value);
        }

        SetLineSegmentsRaycastForErase(_eraseModeActive);
        RefreshLinesRootSiblingOrder();
    }

    internal void NotifyPlacedChipDragStarted(RectTransform chip)
    {
        if (chip != null && chip)
            _activePlacedChipDrag = chip;
    }

    internal void NotifyPlacedChipDragEnded(RectTransform chip)
    {
        if (_activePlacedChipDrag == null)
            return;

        if (chip == null || !chip)
        {
            _activePlacedChipDrag = null;
            return;
        }

        if (_activePlacedChipDrag == chip)
            _activePlacedChipDrag = null;
    }

    void EndActivePlacedChipDragIfNeeded()
    {
        var chip = _activePlacedChipDrag;
        if (chip == null || !chip)
        {
            _activePlacedChipDrag = null;
            return;
        }

        var es = EventSystem.current;
        if (es == null)
        {
            _activePlacedChipDrag = null;
            return;
        }

        var ped = new PointerEventData(es)
        {
            pointerId = -1,
            position = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Input.mousePosition,
        };

        ExecuteEvents.Execute(chip.gameObject, ped, ExecuteEvents.endDragHandler);

        if (_activePlacedChipDrag == chip)
            _activePlacedChipDrag = null;
    }

    void RestoreCommittedLine(int playerNumber, IReadOnlyList<Vector2> path)
    {
        if (path == null || path.Count < 2 || _linesRoot == null)
            return;

        var lineRoot = CreatePlayerLineRoot();
        var end = path[path.Count - 1];
        RebuildPlayerPolyline(lineRoot, path, end, drawTrail: false, playerNumber);
        _playerLines[playerNumber] = lineRoot;
        _playerLinePaths[playerNumber] = new List<Vector2>(path);
    }

    sealed class PitchEditorSnapshot
    {
        public readonly Dictionary<int, Vector2> Placed = new();
        public readonly Dictionary<int, Vector2> Start = new();
        public readonly Dictionary<int, List<Vector2>> Lines = new();
    }

    void GoHome()
    {
        if (string.IsNullOrEmpty(homeSceneName))
        {
            Debug.LogError($"{nameof(PitchSetup)}: home scene name is empty.", this);
            return;
        }

        _activePlacedChipDrag = null;
        PlayNavContext.CurrentPlay = null;
        SceneManager.LoadScene(homeSceneName);
    }
}
