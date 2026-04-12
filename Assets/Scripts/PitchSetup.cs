using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Spawns the rugby pitch prefab and a screen-space Back control to return to the home scene.
/// </summary>
public class PitchSetup : MonoBehaviour
{
    const int PlayerCount = 15;
    static readonly Vector2 PlayerChipSize = new Vector2(58, 58);
    static readonly Color PlayerChipColor = new Color(0.14f, 0.32f, 0.62f, 1f);

    [SerializeField] GameObject rugbyPitchPrefab;
    [SerializeField] string homeSceneName = "HomePage";

    public string HomeSceneName => homeSceneName;

    Font _font;
    RectTransform _pitchHudArea;
    RectTransform _dragLayer;
    readonly Dictionary<int, RectTransform> _placedPlayers = new();
    RectTransform _dragPreview;

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

        var trayBox = new GameObject("PlayerTrayBox");
        trayBox.transform.SetParent(canvasGo.transform, false);
        var trayBoxRt = trayBox.AddComponent<RectTransform>();
        trayBoxRt.anchorMin = new Vector2(0, 1);
        trayBoxRt.anchorMax = new Vector2(0, 1);
        trayBoxRt.pivot = new Vector2(0, 1);
        trayBoxRt.anchoredPosition = new Vector2(24, -112);
        var trayBg = trayBox.AddComponent<Image>();
        trayBg.color = new Color(0.1f, 0.11f, 0.14f, 0.88f);

        var trayContent = new GameObject("TrayContent");
        trayContent.transform.SetParent(trayBox.transform, false);
        var trayContentRt = trayContent.AddComponent<RectTransform>();
        trayContentRt.anchorMin = Vector2.zero;
        trayContentRt.anchorMax = Vector2.one;
        trayContentRt.offsetMin = new Vector2(10, 10);
        trayContentRt.offsetMax = new Vector2(-10, -10);
        var trayGrid = trayContent.AddComponent<GridLayoutGroup>();
        trayGrid.cellSize = new Vector2(58, 58);
        trayGrid.spacing = new Vector2(8, 8);
        trayGrid.padding = new RectOffset(6, 6, 6, 6);
        trayGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        trayGrid.constraintCount = 3;
        trayGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        trayGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        trayGrid.childAlignment = TextAnchor.UpperCenter;

        const float trayContentMargin = 20f;
        int trayRows = (PlayerCount + trayGrid.constraintCount - 1) / trayGrid.constraintCount;
        float gridHeight = trayGrid.padding.vertical + trayRows * trayGrid.cellSize.y
            + Mathf.Max(0, trayRows - 1) * trayGrid.spacing.y;
        float gridWidth = trayGrid.padding.horizontal + trayGrid.constraintCount * trayGrid.cellSize.x
            + Mathf.Max(0, trayGrid.constraintCount - 1) * trayGrid.spacing.x;
        trayBoxRt.sizeDelta = new Vector2(gridWidth + trayContentMargin, gridHeight + trayContentMargin);

        var rightArea = new GameObject("PitchHudArea");
        rightArea.transform.SetParent(canvasGo.transform, false);
        _pitchHudArea = rightArea.AddComponent<RectTransform>();
        _pitchHudArea.anchorMin = new Vector2(0, 0);
        _pitchHudArea.anchorMax = new Vector2(1, 1);
        _pitchHudArea.offsetMin = new Vector2(260, 20);
        _pitchHudArea.offsetMax = new Vector2(-20, -20);

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

        for (int number = 1; number <= PlayerCount; number++)
        {
            CreatePlayerTrayButton(trayContent.transform, number);
        }
    }

    void CreatePlayerTrayButton(Transform parent, int playerNumber)
    {
        var go = new GameObject($"Player{playerNumber}TrayItem");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = PlayerChipSize;

        var img = go.AddComponent<Image>();
        img.color = PlayerChipColor;
        img.raycastTarget = true;

        var drag = go.AddComponent<PlayerTrayDragItem>();
        drag.Init(this, playerNumber);

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
        label.text = playerNumber.ToString();
        label.raycastTarget = false;
    }

    public void OnTrayBeginDrag(int playerNumber, PointerEventData eventData)
    {
        if (_dragPreview != null)
            Destroy(_dragPreview.gameObject);

        _dragPreview = CreatePlayerChip(playerNumber.ToString(), new Color(0.14f, 0.32f, 0.62f, 0.65f), _dragLayer, raycastTarget: false);
        UpdateDragPreviewPosition(eventData);
    }

    public void OnTrayDrag(PointerEventData eventData)
    {
        UpdateDragPreviewPosition(eventData);
    }

    public void OnTrayEndDrag(int playerNumber, PointerEventData eventData)
    {
        if (_dragPreview != null)
            Destroy(_dragPreview.gameObject);
        _dragPreview = null;

        if (_pitchHudArea == null)
            return;

        if (!RectTransformUtility.RectangleContainsScreenPoint(_pitchHudArea, eventData.position, null))
            return;

        PlacePlayerChip(playerNumber, eventData.position);
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

    void PlacePlayerChip(int playerNumber, Vector2 screenPosition)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_pitchHudArea, screenPosition, null, out var localPoint))
            return;

        if (_placedPlayers.TryGetValue(playerNumber, out var existing) && existing != null)
        {
            existing.anchorMin = new Vector2(0.5f, 0.5f);
            existing.anchorMax = new Vector2(0.5f, 0.5f);
            existing.anchoredPosition = localPoint;
            return;
        }

        var chip = CreatePlayerChip(playerNumber.ToString(), PlayerChipColor, _pitchHudArea);
        chip.gameObject.name = $"Player{playerNumber}Chip";
        chip.anchorMin = new Vector2(0.5f, 0.5f);
        chip.anchorMax = new Vector2(0.5f, 0.5f);
        chip.anchoredPosition = localPoint;

        var drag = chip.gameObject.AddComponent<PlacedPlayerDragItem>();
        drag.Init(this, chip);

        _placedPlayers[playerNumber] = chip;
    }

    public void OnPlacedChipDrag(RectTransform chip, PointerEventData eventData)
    {
        if (chip == null || _pitchHudArea == null)
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

    void GoHome()
    {
        if (string.IsNullOrEmpty(homeSceneName))
        {
            Debug.LogError($"{nameof(PitchSetup)}: home scene name is empty.", this);
            return;
        }

        PlayNavContext.CurrentPlay = null;
        SceneManager.LoadScene(homeSceneName);
    }
}
