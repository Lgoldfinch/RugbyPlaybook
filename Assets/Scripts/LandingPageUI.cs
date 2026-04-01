using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

public class LandingPageUI : MonoBehaviour
{
    const string RugbyPitchSceneName = "RugbyPitch";

    [SerializeField] GameObject playListRowPrefab;

    static readonly Color BgDark = new(0.12f, 0.14f, 0.18f, 1f);
    static readonly Color Panel = new(0.18f, 0.2f, 0.26f, 1f);
    static readonly Color Accent = new(0.2f, 0.55f, 0.35f, 1f);
    static readonly Color Danger = new(0.65f, 0.22f, 0.2f, 1f);
    static readonly Color TextPrimary = new(0.95f, 0.95f, 0.95f, 1f);
    static readonly Color TextMuted = new(0.65f, 0.68f, 0.72f, 1f);

    readonly PlaybookStorage _storage = new();
    List<Play> _plays = new();
    Font _font;

    RectTransform _scrollContent;
    GameObject _emptyLabelGo;
    GameObject _rowTemplate;

    GameObject _listPanel;

    GameObject _createModal;
    InputField _createNameField;

    GameObject _deleteModal;
    Text _deleteMessage;
    Play _pendingDelete;

    void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null)
            _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        EnsureEventSystem();
        BuildUi();
        _plays = _storage.Load();
    }

    void Start()
    {
        RefreshList();
    }

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var safe = new GameObject("SafeArea");
        safe.transform.SetParent(canvasGo.transform, false);
        var safeRect = safe.AddComponent<RectTransform>();
        safeRect.anchorMin = Vector2.zero;
        safeRect.anchorMax = Vector2.one;
        safeRect.offsetMin = Vector2.zero;
        safeRect.offsetMax = Vector2.zero;
        safe.AddComponent<SafeAreaFitter>();

        var bg = safe.AddComponent<Image>();
        bg.color = BgDark;
        bg.raycastTarget = false;

        BuildListPanel(safe.transform);
        BuildCreateModal(safe.transform);
        BuildDeleteModal(safe.transform);

        _rowTemplate = playListRowPrefab != null ? playListRowPrefab : CreateRowTemplate();
        _rowTemplate.SetActive(false);
        _rowTemplate.transform.SetParent(safe.transform, false);
    }

    void BuildListPanel(Transform parent)
    {
        _listPanel = new GameObject("ListPanel");
        _listPanel.transform.SetParent(parent, false);
        var root = _listPanel.AddComponent<RectTransform>();
        StretchFull(root);

        var header = new GameObject("Header");
        header.transform.SetParent(root, false);
        var headerRt = header.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0, 1);
        headerRt.anchorMax = new Vector2(1, 1);
        headerRt.pivot = new Vector2(0.5f, 1);
        headerRt.sizeDelta = new Vector2(0, 140);
        headerRt.anchoredPosition = new Vector2(0, 0);
        var headerBg = header.AddComponent<Image>();
        headerBg.color = Panel;
        var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
        headerLayout.padding = new RectOffset(32, 32, 40, 24);
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childForceExpandHeight = true;
        headerLayout.spacing = 16;

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(header.transform, false);
        var titleText = titleGo.AddComponent<Text>();
        titleText.font = _font;
        titleText.fontSize = 42;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = TextPrimary;
        titleText.text = "Playbook";
        titleText.alignment = TextAnchor.MiddleLeft;
        var titleLe = titleGo.AddComponent<LayoutElement>();
        titleLe.flexibleWidth = 1f;
        titleLe.minHeight = 72;

        var newBtnGo = CreateButton(header.transform, "New play", Accent, 280, 72, OnNewPlayClicked);
        var newLe = newBtnGo.GetComponent<LayoutElement>();
        if (newLe == null)
            newLe = newBtnGo.AddComponent<LayoutElement>();
        newLe.preferredWidth = 280;
        newLe.flexibleWidth = 0;

        var scrollGo = new GameObject("Scroll");
        scrollGo.transform.SetParent(root, false);
        var scrollRt = scrollGo.AddComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0, 0);
        scrollRt.anchorMax = new Vector2(1, 1);
        scrollRt.offsetMin = new Vector2(24, 24);
        scrollRt.offsetMax = new Vector2(-24, -156);

        var scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.color = new Color(0.1f, 0.11f, 0.14f, 0.6f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRt = viewport.AddComponent<RectTransform>();
        StretchFull(vpRt);
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = Vector2.zero;
        var mask = viewport.AddComponent<RectMask2D>();
        mask.padding = Vector4.zero;
        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1, 1, 1, 0.02f);

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        _scrollContent = content.AddComponent<RectTransform>();
        _scrollContent.anchorMin = new Vector2(0, 1);
        _scrollContent.anchorMax = new Vector2(1, 1);
        _scrollContent.pivot = new Vector2(0.5f, 1);
        _scrollContent.anchoredPosition = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(0, 0, 8, 8);
        vlg.spacing = 12;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = _scrollContent;
        scroll.viewport = vpRt;

        _emptyLabelGo = new GameObject("EmptyState");
        _emptyLabelGo.transform.SetParent(root, false);
        var emptyRt = _emptyLabelGo.AddComponent<RectTransform>();
        emptyRt.anchorMin = new Vector2(0, 0);
        emptyRt.anchorMax = new Vector2(1, 1);
        emptyRt.offsetMin = new Vector2(48, 200);
        emptyRt.offsetMax = new Vector2(-48, -200);
        var emptyText = _emptyLabelGo.AddComponent<Text>();
        emptyText.font = _font;
        emptyText.fontSize = 32;
        emptyText.color = TextMuted;
        emptyText.alignment = TextAnchor.MiddleCenter;
        emptyText.text = "No plays yet.\nTap \"New play\" to create one.";
    }

    void BuildCreateModal(Transform parent)
    {
        _createModal = CreateModalBackdrop(parent, "CreateModal");
        var panel = _createModal.transform.GetChild(0);

        var title = panel.Find("Title").GetComponent<Text>();
        title.text = "New play";

        var fieldGo = new GameObject("NameField");
        fieldGo.transform.SetParent(panel, false);
        var fieldRt = fieldGo.AddComponent<RectTransform>();
        fieldRt.sizeDelta = new Vector2(0, 88);
        var fieldLayout = fieldGo.AddComponent<LayoutElement>();
        fieldLayout.minHeight = 88;
        fieldLayout.preferredHeight = 88;
        fieldLayout.flexibleWidth = 1f;
        var fieldImg = fieldGo.AddComponent<Image>();
        fieldImg.color = new Color(0.08f, 0.09f, 0.11f, 1f);
        _createNameField = fieldGo.AddComponent<InputField>();
        var fieldText = CreateChildText(fieldGo.transform, "Text", 28, TextAnchor.MiddleLeft, TextPrimary);
        fieldText.rectTransform.offsetMin = new Vector2(20, 8);
        fieldText.rectTransform.offsetMax = new Vector2(-20, -8);
        _createNameField.textComponent = fieldText;
        _createNameField.lineType = InputField.LineType.SingleLine;
        var placeholder = CreateChildText(fieldGo.transform, "Placeholder", 28, TextAnchor.MiddleLeft, TextMuted);
        placeholder.rectTransform.offsetMin = new Vector2(20, 8);
        placeholder.rectTransform.offsetMax = new Vector2(-20, -8);
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.text = "Play name";
        placeholder.raycastTarget = false;
        _createNameField.placeholder = placeholder;

        var row = new GameObject("Buttons");
        row.transform.SetParent(panel, false);
        var rowRt = row.AddComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(0, 88);
        var rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = 88;
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = false;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        CreateButton(row.transform, "Cancel", new Color(0.28f, 0.3f, 0.36f, 1f), 220, 72, CloseCreateModal);
        CreateButton(row.transform, "Create", Accent, 220, 72, OnCreateConfirmed);

        _createModal.SetActive(false);
    }

    void BuildDeleteModal(Transform parent)
    {
        _deleteModal = CreateModalBackdrop(parent, "DeleteModal");
        var panel = _deleteModal.transform.GetChild(0);
        Destroy(panel.Find("Title").gameObject);

        var msgGo = new GameObject("Message");
        msgGo.transform.SetParent(panel, false);
        var msgRt = msgGo.AddComponent<RectTransform>();
        msgRt.sizeDelta = new Vector2(0, 120);
        var msgLe = msgGo.AddComponent<LayoutElement>();
        msgLe.minHeight = 100;
        msgLe.flexibleWidth = 1f;
        _deleteMessage = msgGo.AddComponent<Text>();
        _deleteMessage.font = _font;
        _deleteMessage.fontSize = 30;
        _deleteMessage.color = TextPrimary;
        _deleteMessage.alignment = TextAnchor.MiddleCenter;
        _deleteMessage.text = "Delete this play?";

        var row = new GameObject("Buttons");
        row.transform.SetParent(panel, false);
        var rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = 88;
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = false;
        h.childControlHeight = true;

        CreateButton(row.transform, "Cancel", new Color(0.28f, 0.3f, 0.36f, 1f), 220, 72, CloseDeleteModal);
        CreateButton(row.transform, "Delete", Danger, 220, 72, OnDeleteConfirmed);

        _deleteModal.SetActive(false);
    }

    GameObject CreateModalBackdrop(Transform parent, string name)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        var rootRt = root.AddComponent<RectTransform>();
        StretchFull(rootRt);
        var dim = root.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.55f);
        var dimBtn = root.AddComponent<Button>();
        dimBtn.targetGraphic = dim;
        dimBtn.onClick.AddListener(() =>
        {
            if (name == "CreateModal")
                CloseCreateModal();
            else
                CloseDeleteModal();
        });

        var panel = new GameObject("Panel");
        panel.transform.SetParent(root.transform, false);
        var panelRt = panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(720, 420);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = Panel;
        var v = panel.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(40, 40, 36, 36);
        v.spacing = 24;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panel.transform, false);
        var titleText = titleGo.AddComponent<Text>();
        titleText.font = _font;
        titleText.fontSize = 34;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = TextPrimary;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.text = "Title";
        var titleLe = titleGo.AddComponent<LayoutElement>();
        titleLe.minHeight = 48;

        return root;
    }

    GameObject CreateRowTemplate()
    {
        var row = new GameObject("PlayListRowTemplate");
        row.layer = 5;
        var rt = row.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 88);
        var rowImg = row.AddComponent<Image>();
        rowImg.color = new Color(0.22f, 0.24f, 0.3f, 1f);
        var rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = 88;
        rowLe.preferredHeight = 88;
        rowLe.flexibleWidth = 1f;
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(8, 8, 8, 8);
        h.spacing = 8;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        var open = new GameObject("Open");
        open.transform.SetParent(row.transform, false);
        var openRt = open.AddComponent<RectTransform>();
        var openImg = open.AddComponent<Image>();
        openImg.color = new Color(0.28f, 0.3f, 0.38f, 1f);
        var openBtn = open.AddComponent<Button>();
        openBtn.targetGraphic = openImg;
        var openLe = open.AddComponent<LayoutElement>();
        openLe.flexibleWidth = 1f;
        openLe.minWidth = 100;
        var openTextGo = new GameObject("Text");
        openTextGo.transform.SetParent(open.transform, false);
        var openTextRt = openTextGo.AddComponent<RectTransform>();
        StretchFull(openTextRt);
        openTextRt.offsetMin = new Vector2(20, 0);
        openTextRt.offsetMax = new Vector2(-12, 0);
        var openText = openTextGo.AddComponent<Text>();
        openText.font = _font;
        openText.fontSize = 30;
        openText.color = TextPrimary;
        openText.alignment = TextAnchor.MiddleLeft;

        var del = new GameObject("Delete");
        del.transform.SetParent(row.transform, false);
        del.AddComponent<Image>().color = new Color(0.4f, 0.22f, 0.22f, 1f);
        var delBtn = del.AddComponent<Button>();
        delBtn.targetGraphic = del.GetComponent<Image>();
        var delLe = del.AddComponent<LayoutElement>();
        delLe.preferredWidth = 96;
        delLe.flexibleWidth = 0;
        var delTextGo = new GameObject("Text");
        delTextGo.transform.SetParent(del.transform, false);
        var delTextRt = delTextGo.AddComponent<RectTransform>();
        StretchFull(delTextRt);
        var delText = delTextGo.AddComponent<Text>();
        delText.font = _font;
        delText.fontSize = 26;
        delText.color = TextPrimary;
        delText.alignment = TextAnchor.MiddleCenter;
        delText.text = "Del";

        row.AddComponent<PlayListRowView>();
        return row;
    }

    static Text CreateChildText(Transform parent, string name, int size, TextAnchor align, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        StretchFull(rt);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.color = c;
        t.alignment = align;
        t.text = string.Empty;
        return t;
    }

    GameObject CreateButton(Transform parent, string label, Color bg, float width, float height, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Button");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
        le.minHeight = height;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        StretchFull(textRt);
        var text = textGo.AddComponent<Text>();
        text.font = _font;
        text.fontSize = 28;
        text.color = TextPrimary;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = label;
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void RefreshList()
    {
        foreach (Transform c in _scrollContent)
            Destroy(c.gameObject);

        _emptyLabelGo.SetActive(_plays.Count == 0);

        foreach (var play in _plays)
        {
            var go = Instantiate(_rowTemplate, _scrollContent);
            go.SetActive(true);
            var view = go.GetComponent<PlayListRowView>();
            view.Bind(play, OnOpenPlay, OnDeleteRequested);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
    }

    void OnOpenPlay(Play play)
    {
        if (play == null)
            return;
        PlayNavContext.CurrentPlay = play;
        SceneManager.LoadScene(RugbyPitchSceneName);
    }

    void OnNewPlayClicked()
    {
        _createNameField.text = string.Empty;
        _createModal.SetActive(true);
        _createNameField.Select();
        _createNameField.ActivateInputField();
    }

    void CloseCreateModal()
    {
        _createModal.SetActive(false);
    }

    void OnCreateConfirmed()
    {
        var name = _createNameField.text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return;

        _plays.Add(Play.Create(name));
        _storage.Save(_plays);
        CloseCreateModal();
        RefreshList();
    }

    void OnDeleteRequested(Play play)
    {
        _pendingDelete = play;
        _deleteMessage.text = $"Delete \"{play.name}\"?";
        _deleteModal.SetActive(true);
    }

    void CloseDeleteModal()
    {
        _pendingDelete = null;
        _deleteModal.SetActive(false);
    }

    void OnDeleteConfirmed()
    {
        if (_pendingDelete == null)
        {
            CloseDeleteModal();
            return;
        }

        _plays.RemoveAll(p => p.id == _pendingDelete.id);
        _storage.Save(_plays);

        CloseDeleteModal();
        RefreshList();
    }
}
