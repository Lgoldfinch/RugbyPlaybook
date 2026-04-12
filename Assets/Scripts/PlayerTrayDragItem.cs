using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerTrayDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    PitchSetup _pitchSetup;
    int _playerNumber;

    public void Init(PitchSetup pitchSetup, int playerNumber)
    {
        _pitchSetup = pitchSetup;
        _playerNumber = playerNumber;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_pitchSetup != null && _pitchSetup.EraseModeActive)
            return;

        _pitchSetup?.OnTrayBeginDrag(_playerNumber, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_pitchSetup != null && _pitchSetup.EraseModeActive)
            return;

        _pitchSetup?.OnTrayDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_pitchSetup != null && _pitchSetup.EraseModeActive)
            return;

        _pitchSetup?.OnTrayEndDrag(_playerNumber, eventData);
    }
}
