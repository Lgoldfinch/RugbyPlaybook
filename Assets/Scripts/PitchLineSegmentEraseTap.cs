using UnityEngine;
using UnityEngine.EventSystems;

public class PitchLineSegmentEraseTap : MonoBehaviour, IPointerDownHandler
{
    PitchSetup _pitch;
    int _playerNumber;

    public void Init(PitchSetup pitch, int playerNumber)
    {
        _pitch = pitch;
        _playerNumber = playerNumber;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (_pitch == null || !_pitch.EraseModeActive)
            return;

        _pitch.TryDeleteLineOnly(_playerNumber);
    }
}
