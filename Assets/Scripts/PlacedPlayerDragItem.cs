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
        if (_pitchSetup != null && _pitchSetup.EraseModeActive)
            return;

        _pitchSetup?.OnPlacedChipDrag(_chipRect, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_pitchSetup != null && _pitchSetup.EraseModeActive)
            return;

        _pitchSetup?.OnPlacedChipDrag(_chipRect, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_pitchSetup != null && _pitchSetup.EraseModeActive)
            return;

        _pitchSetup?.OnPlacedChipDrag(_chipRect, eventData);
    }
}
