using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform _rect;
    Rect _lastSafeArea;
    Vector2 _lastScreen;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        Apply();
    }

    void Update()
    {
        if (_lastSafeArea != Screen.safeArea ||
            _lastScreen.x != Screen.width ||
            _lastScreen.y != Screen.height)
            Apply();
    }

    void Apply()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();

        var sa = Screen.safeArea;
        _lastSafeArea = sa;
        _lastScreen = new Vector2(Screen.width, Screen.height);

        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        var anchorMin = sa.position;
        var anchorMax = sa.position + sa.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        _rect.anchorMin = anchorMin;
        _rect.anchorMax = anchorMax;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }
}
