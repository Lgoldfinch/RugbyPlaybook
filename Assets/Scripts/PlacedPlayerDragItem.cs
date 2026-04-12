using UnityEngine;
using UnityEngine.EventSystems;

public class PlacedPlayerDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    PitchSetup _pitchSetup;
    RectTransform _chipRect;

    public void Init(PitchSetup pitchSetup, RectTransform chipRect)
    {
        _pitchSetup = pitchSetup;
        _chipRect = chipRect;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanInteractWithPlacedChipDrag())
            return;

        if (_pitchSetup.InteractionMode == PitchInteractionMode.DragPlayers)
            _pitchSetup.PushUndoCurrent();

        _pitchSetup.OnPlacedChipDrag(_chipRect, eventData);

        if (_pitchSetup.InteractionMode == PitchInteractionMode.DragPlayers)
            _pitchSetup.NotifyPlacedChipDragStarted(_chipRect);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!CanInteractWithPlacedChipDrag())
            return;

        _pitchSetup.OnPlacedChipDrag(_chipRect, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _pitchSetup?.NotifyPlacedChipDragEnded(_chipRect);

        if (!CanInteractWithPlacedChipDrag())
            return;

        _pitchSetup.OnPlacedChipDrag(_chipRect, eventData);
    }

    bool CanInteractWithPlacedChipDrag()
    {
        if (_pitchSetup == null || !_pitchSetup || _chipRect == null || !_chipRect)
            return false;
        if (_pitchSetup.EraseModeActive)
            return false;
        return true;
    }
}
