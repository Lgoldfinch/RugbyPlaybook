using System;
using UnityEngine;
using UnityEngine.UI;

public class PlayListRowView : MonoBehaviour
{
    [SerializeField] Button openButton;
    [SerializeField] Button deleteButton;
    [SerializeField] Text titleText;

    Play _play;
    Action<Play> _onOpen;
    Action<Play> _onDeleteRequested;

    void Awake()
    {
        if (openButton == null)
            openButton = transform.Find("Open")?.GetComponent<Button>();
        if (deleteButton == null)
            deleteButton = transform.Find("Delete")?.GetComponent<Button>();
        if (titleText == null && openButton != null)
            titleText = openButton.GetComponentInChildren<Text>();

        if (openButton != null)
            openButton.onClick.AddListener(OnOpenClicked);
        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteClicked);
    }

    void OnOpenClicked() => _onOpen?.Invoke(_play);

    void OnDeleteClicked() => _onDeleteRequested?.Invoke(_play);

    public void Bind(Play play, Action<Play> onOpen, Action<Play> onDeleteRequested)
    {
        _play = play;
        _onOpen = onOpen;
        _onDeleteRequested = onDeleteRequested;
        if (titleText != null)
            titleText.text = play != null ? play.name : string.Empty;
    }
}
