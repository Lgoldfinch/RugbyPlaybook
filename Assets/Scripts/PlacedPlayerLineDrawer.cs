using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlacedPlayerLineDrawer : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    const float MinCommitLength = 4f;
    const float PathSampleDistance = 6f;

    PitchSetup _pitch;
    RectTransform _chip;
    int _playerNumber;
    RectTransform _lineRoot;
    readonly List<Vector2> _path = new();

    public void Init(PitchSetup pitch, RectTransform chip, int playerNumber)
    {
        _pitch = pitch;
        _chip = chip;
        _playerNumber = playerNumber;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_pitch == null || _chip == null || _pitch.InteractionMode != PitchInteractionMode.DrawLines)
            return;

        _pitch.SetActiveLineDrawer(this);
        _pitch.RemoveCommittedLineForPlayer(_playerNumber);
        if (_lineRoot != null)
        {
            Destroy(_lineRoot.gameObject);
            _lineRoot = null;
        }

        _lineRoot = _pitch.CreatePlayerLineRoot();
        _path.Clear();
        _path.Add(_chip.anchoredPosition);

        var end = _pitch.ScreenPointToClampedPitchLocal(eventData.position);
        _pitch.RebuildPlayerPolyline(_lineRoot, _path, end, drawTrail: true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_lineRoot == null || _chip == null || _pitch == null || _pitch.InteractionMode != PitchInteractionMode.DrawLines)
            return;

        var end = _pitch.ScreenPointToClampedPitchLocal(eventData.position);
        AppendCornerIfNeeded(end);
        _pitch.RebuildPlayerPolyline(_lineRoot, _path, end, drawTrail: true);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_pitch == null)
            return;

        _pitch.ClearActiveLineDrawer(this);

        if (_lineRoot == null)
            return;

        if (_pitch.InteractionMode != PitchInteractionMode.DrawLines)
        {
            Destroy(_lineRoot.gameObject);
            _lineRoot = null;
            _path.Clear();
            return;
        }

        var end = _pitch.ScreenPointToClampedPitchLocal(eventData.position);
        AppendCornerIfNeeded(end);
        if (_path.Count > 0 && (_path[_path.Count - 1] - end).sqrMagnitude > 0.0001f)
            _path.Add(end);

        _pitch.RebuildPlayerPolyline(_lineRoot, _path, end, drawTrail: false);

        if (ComputePolylineLength() < MinCommitLength)
        {
            Destroy(_lineRoot.gameObject);
            _lineRoot = null;
            _path.Clear();
            return;
        }

        _pitch.CommitPlayerLine(_playerNumber, _lineRoot, _path);
        _lineRoot = null;
        _path.Clear();
    }

    public void CancelRubberBand()
    {
        if (_lineRoot != null)
        {
            Destroy(_lineRoot.gameObject);
            _lineRoot = null;
        }

        _path.Clear();
        _pitch?.ClearActiveLineDrawer(this);
    }

    void AppendCornerIfNeeded(Vector2 point)
    {
        if (_path.Count == 0)
            return;

        var last = _path[_path.Count - 1];
        if ((point - last).sqrMagnitude >= PathSampleDistance * PathSampleDistance)
            _path.Add(point);
    }

    float ComputePolylineLength()
    {
        float t = 0f;
        for (int i = 0; i < _path.Count - 1; i++)
            t += Vector2.Distance(_path[i], _path[i + 1]);
        return t;
    }
}
