using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public sealed record TacticalMapLandmark(
    Vector3 Position,
    string LocalizationKey,
    string EnglishName,
    Color Accent);

public partial class TacticalMinimap : Control
{
    private const ulong DynamicRedrawIntervalMilliseconds = 50;
    private const float PlayerRedrawDistanceSquared = 0.08f * 0.08f;
    private const float PlayerRedrawHeadingDegrees = 1.0f;

    private readonly List<TacticalMapLandmark> _landmarks = new();
    private readonly List<string> _localizedLandmarkLabels = new();
    private readonly List<Rect2> _occupiedLabels = new();
    private readonly Vector2[] _worldBossDiamond = new Vector2[4];
    private readonly Vector2[] _worldBossOutline = new Vector2[5];
    private readonly Vector2[] _playerArrow = new Vector2[4];
    private readonly Vector2[] _playerArrowOutline = new Vector2[5];
    private Rect2 _worldBounds = new(-170.0f, -220.0f, 340.0f, 320.0f);
    private Vector3 _playerPosition;
    private float _headingDegrees;
    private string _language = "en";
    private Vector3 _worldBossPosition;
    private bool _worldBossVisible;
    private Vector3 _queuedPlayerPosition;
    private float _queuedHeadingDegrees;
    private Vector3 _queuedWorldBossPosition;
    private bool _queuedWorldBossVisible;
    private ulong _nextDynamicRedrawMilliseconds;

    public int LandmarkCount => _landmarks.Count;
    public Vector2 PlayerMapPosition => WorldToMap(_playerPosition);
    public Vector2 WorldBossMapPosition => WorldToMap(_worldBossPosition);
    public bool WorldBossVisible => _worldBossVisible;

    public TacticalMinimap()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        CustomMinimumSize = new Vector2(250, 202);
    }

    public void Configure(Rect2 worldBounds, IReadOnlyList<TacticalMapLandmark> landmarks)
    {
        _worldBounds = worldBounds;
        _landmarks.Clear();
        _landmarks.AddRange(landmarks);
        RefreshLocalizedLandmarkLabels();
        QueueImmediateRedraw();
    }

    public void SetLanguage(string language)
    {
        if (_language == language)
        {
            return;
        }
        _language = language;
        RefreshLocalizedLandmarkLabels();
        QueueImmediateRedraw();
    }

    public void SetPlayerState(Vector3 position, float headingDegrees)
    {
        var changed = _queuedPlayerPosition.DistanceSquaredTo(position) >= PlayerRedrawDistanceSquared
            || Mathf.Abs(Mathf.AngleDifference(
                    Mathf.DegToRad(_queuedHeadingDegrees),
                    Mathf.DegToRad(headingDegrees)))
                >= Mathf.DegToRad(PlayerRedrawHeadingDegrees);
        _playerPosition = position;
        _headingDegrees = headingDegrees;
        if ((changed || _worldBossVisible) && QueueDynamicRedraw())
        {
            CaptureQueuedDynamicState();
        }
    }

    public void SetWorldBoss(Vector3 position, bool visible)
    {
        var changed = _queuedWorldBossVisible != visible
            || _queuedWorldBossPosition.DistanceSquaredTo(position) >= PlayerRedrawDistanceSquared;
        _worldBossPosition = position;
        _worldBossVisible = visible;
        if ((changed || visible) && QueueDynamicRedraw())
        {
            CaptureQueuedDynamicState();
        }
    }

    public override void _Draw()
    {
        var panel = new Rect2(Vector2.Zero, Size);
        DrawRect(panel, new Color(0.008f, 0.014f, 0.016f, 0.9f));
        DrawRect(panel, new Color(0.18f, 0.78f, 0.66f, 0.78f), false, 1.0f);
        DrawRect(new Rect2(0, 0, 3, Size.Y), new Color(0.22f, 0.84f, 0.69f));

        var mapRect = MapRect();
        DrawRect(mapRect, new Color(0.028f, 0.045f, 0.047f, 0.92f));
        for (var index = 1; index < 4; index++)
        {
            var x = mapRect.Position.X + mapRect.Size.X * index / 4.0f;
            var y = mapRect.Position.Y + mapRect.Size.Y * index / 4.0f;
            DrawLine(new Vector2(x, mapRect.Position.Y), new Vector2(x, mapRect.End.Y), new Color(0.16f, 0.25f, 0.25f, 0.62f), 1.0f);
            DrawLine(new Vector2(mapRect.Position.X, y), new Vector2(mapRect.End.X, y), new Color(0.16f, 0.25f, 0.25f, 0.62f), 1.0f);
        }

        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(13, 18),
            GameLocalization.Get("minimap_title", _language, "TACTICAL MAP"),
            HorizontalAlignment.Left,
            -1,
            12,
            new Color(0.56f, 0.9f, 0.78f));
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(Size.X - 28, 18),
            "N",
            HorizontalAlignment.Left,
            -1,
            11,
            new Color(0.82f, 0.9f, 0.86f));

        _occupiedLabels.Clear();
        for (var index = 0; index < _landmarks.Count; index++)
        {
            var landmark = _landmarks[index];
            var point = WorldToMap(landmark.Position);
            DrawCircle(point, 3.4f, landmark.Accent);
            DrawCircle(point, 6.0f, new Color(landmark.Accent.R, landmark.Accent.G, landmark.Accent.B, 0.28f), false, 1.0f);
            var label = _localizedLandmarkLabels[index];
            var labelWidth = Mathf.Clamp(label.Length * 6.2f, 24.0f, 76.0f);
            var labelRect = PlaceLandmarkLabel(mapRect, point, labelWidth, index, _occupiedLabels);
            _occupiedLabels.Add(labelRect);
            DrawString(
                ThemeDB.FallbackFont,
                labelRect.Position + new Vector2(0, 9),
                label,
                HorizontalAlignment.Left,
                76,
                9,
                new Color(0.77f, 0.85f, 0.82f));
        }

        if (_worldBossVisible)
        {
            var boss = WorldToMap(_worldBossPosition);
            var pulse = 7.0f + Mathf.Sin(Time.GetTicksMsec() * 0.008f) * 1.5f;
            var bossColor = new Color(1.0f, 0.25f, 0.16f);
            _worldBossDiamond[0] = boss + new Vector2(0, -6);
            _worldBossDiamond[1] = boss + new Vector2(6, 0);
            _worldBossDiamond[2] = boss + new Vector2(0, 6);
            _worldBossDiamond[3] = boss + new Vector2(-6, 0);
            CopyClosedPolyline(_worldBossDiamond, _worldBossOutline);
            DrawColoredPolygon(_worldBossDiamond, bossColor);
            DrawPolyline(_worldBossOutline, Colors.White, 1.0f, true);
            DrawCircle(boss, pulse, new Color(1.0f, 0.3f, 0.18f, 0.4f), false, 1.5f);
        }

        var player = WorldToMap(_playerPosition);
        var radians = Mathf.DegToRad(_headingDegrees);
        var forward = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
        var side = new Vector2(-forward.Y, forward.X);
        _playerArrow[0] = player + forward * 8.0f;
        _playerArrow[1] = player - forward * 5.0f + side * 4.5f;
        _playerArrow[2] = player - forward * 2.5f;
        _playerArrow[3] = player - forward * 5.0f - side * 4.5f;
        CopyClosedPolyline(_playerArrow, _playerArrowOutline);
        DrawColoredPolygon(_playerArrow, new Color(0.94f, 0.98f, 0.94f));
        DrawPolyline(_playerArrowOutline, new Color(0.12f, 0.32f, 0.28f), 1.0f, true);
    }

    private Rect2 MapRect() => new(new Vector2(9, 25), new Vector2(Size.X - 18, Size.Y - 34));

    private static Rect2 PlaceLandmarkLabel(
        Rect2 mapRect,
        Vector2 point,
        float width,
        int index,
        IReadOnlyList<Rect2> occupied)
    {
        Rect2 fallback = default;
        for (var candidateIndex = 0; candidateIndex < 4; candidateIndex++)
        {
            var offset = LandmarkLabelOffset(width, index % 4, candidateIndex);
            var position = point + offset;
            position.X = Mathf.Clamp(position.X, mapRect.Position.X + 2.0f, mapRect.End.X - width - 2.0f);
            position.Y = Mathf.Clamp(position.Y, mapRect.Position.Y + 2.0f, mapRect.End.Y - 12.0f);
            var candidate = new Rect2(position, new Vector2(width, 11));
            fallback = candidate;
            var intersects = false;
            foreach (var previous in occupied)
            {
                if (candidate.Grow(1.5f).Intersects(previous))
                {
                    intersects = true;
                    break;
                }
            }
            if (!intersects)
            {
                return candidate;
            }
        }
        return fallback;
    }

    private static Vector2 LandmarkLabelOffset(float width, int order, int candidateIndex)
    {
        var placement = order switch
        {
            0 => candidateIndex,
            1 => candidateIndex switch { 0 => 3, 1 => 2, 2 => 1, _ => 0 },
            2 => candidateIndex switch { 0 => 1, 1 => 2, 2 => 0, _ => 3 },
            _ => candidateIndex switch { 0 => 2, 1 => 1, 2 => 3, _ => 0 }
        };
        return placement switch
        {
            0 => new Vector2(7, -12),
            1 => new Vector2(7, 5),
            2 => new Vector2(-width - 7, -12),
            _ => new Vector2(-width - 7, 5)
        };
    }

    private void RefreshLocalizedLandmarkLabels()
    {
        _localizedLandmarkLabels.Clear();
        foreach (var landmark in _landmarks)
        {
            _localizedLandmarkLabels.Add(GameLocalization.Get(
                landmark.LocalizationKey,
                _language,
                landmark.EnglishName));
        }
    }

    private void QueueImmediateRedraw()
    {
        QueueRedraw();
        CaptureQueuedDynamicState();
        _nextDynamicRedrawMilliseconds = Time.GetTicksMsec() + DynamicRedrawIntervalMilliseconds;
    }

    private bool QueueDynamicRedraw()
    {
        var now = Time.GetTicksMsec();
        if (now < _nextDynamicRedrawMilliseconds)
        {
            return false;
        }
        QueueRedraw();
        _nextDynamicRedrawMilliseconds = now + DynamicRedrawIntervalMilliseconds;
        return true;
    }

    private void CaptureQueuedDynamicState()
    {
        _queuedPlayerPosition = _playerPosition;
        _queuedHeadingDegrees = _headingDegrees;
        _queuedWorldBossPosition = _worldBossPosition;
        _queuedWorldBossVisible = _worldBossVisible;
    }

    private static void CopyClosedPolyline(Vector2[] source, Vector2[] destination)
    {
        for (var index = 0; index < source.Length; index++)
        {
            destination[index] = source[index];
        }
        destination[^1] = source[0];
    }

    private Vector2 WorldToMap(Vector3 worldPosition)
    {
        var map = MapRect();
        var x = Mathf.InverseLerp(_worldBounds.Position.X, _worldBounds.End.X, worldPosition.X);
        var y = Mathf.InverseLerp(_worldBounds.Position.Y, _worldBounds.End.Y, worldPosition.Z);
        return map.Position + new Vector2(
            Mathf.Clamp(x, 0.0f, 1.0f) * map.Size.X,
            Mathf.Clamp(y, 0.0f, 1.0f) * map.Size.Y);
    }
}
