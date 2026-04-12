using UnityEngine;
using UnityEngine.EventSystems;

public class PitchChipEraseTap : MonoBehaviour, IPointerClickHandler
{
    PitchSetup _pitch;
    int _playerNumber;

    public void Init(PitchSetup pitch, int playerNumber)
    {
        _pitch = pitch;
        _playerNumber = playerNumber;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_pitch == null || !_pitch.EraseModeActive)
            return;

        _pitch.TryDeletePlacedPlayer(_playerNumber);
    }
}
