using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Spawns the rugby pitch prefab and a screen-space Back control to return to the home scene.
/// </summary>
public class PitchSetup : MonoBehaviour
{
    [SerializeField] GameObject rugbyPitchPrefab;
    [SerializeField] string homeSceneName = "HomePage";

    public string HomeSceneName => homeSceneName;

    Font _font;

    void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Font.CreateDynamicFontFromOSFont("Arial", 16);

        EnsureEventSystem();
        BuildBackButton();
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

    void BuildBackButton()
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

        var btnGo = new GameObject("BackButton");
        btnGo.transform.SetParent(canvasGo.transform, false);
        var rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(32, -32);
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
