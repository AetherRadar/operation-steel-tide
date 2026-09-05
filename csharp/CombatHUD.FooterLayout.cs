using Godot;

namespace OperationSteelTide;

public partial class CombatHUD
{
    private const float FooterPanelGap = 18.0f;
    private const float StatusHudLeftMargin = 30.0f;
    private const float StatusHudBottomMargin = 32.0f;
    private const float StatusHudWidth = 245.0f;
    private const float StatusHudHeight = 92.0f;
    private const float StatusHudRightEdge = StatusHudLeftMargin + StatusHudWidth;
    private const float WeaponHudRightMargin = 30.0f;
    private const float WeaponHudBottomMargin = 32.0f;
    private const float WeaponHudWidth = 750.0f;
    private const float WeaponHudHeight = 104.0f;
    private const float BackpackHudRightMargin = 30.0f;
    private const float BackpackHudBottomOffset = 198.0f;
    private const float BackpackHudWidth = 210.0f;
    private const float BackpackHudHeight = 52.0f;
    private const float SquadRosterLeft = 28.0f;
    private const float SquadRosterTop = 352.0f;
    private const float SquadRosterWidth = 250.0f;
    private const float SquadRosterHeight = 158.0f;
    private const float ClassSkillHudWidth = 430.0f;
    private const float ClassSkillHudHeight = 92.0f;
    private const float DemolitionClassSkillHudHeight = 46.0f;
    private const float ClassSkillHudBottomOffset = 122.0f;
    private const float MinimumSupportedFooterWidth = 1280.0f;

    private Control _statusHudRoot = null!;
    private Control _weaponHudRoot = null!;
    private Control _footerLayoutRoot = null!;
    private bool _downedFooterSuppressed;
    private bool _weaponHudVisibleBeforeDowned;
    private bool _classSkillHudVisibleBeforeDowned;

    internal bool WeaponHudVisibleForDiagnostics
        => IsInstanceValid(_weaponHudRoot) && _weaponHudRoot.Visible;

    internal bool ClassSkillHudVisibleForDiagnostics
        => IsInstanceValid(_classSkillRoot) && _classSkillRoot.Visible;

    internal bool DownedFooterSuppressedForDiagnostics
        => _downedFooterSuppressed
        && IsInstanceValid(_weaponHudRoot)
        && !_weaponHudRoot.Visible
        && IsInstanceValid(_classSkillRoot)
        && !_classSkillRoot.Visible;

    internal bool FooterHudRuntimeSeparatedForDiagnostics
    {
        get
        {
            if (!TryGetFooterViewportSize(out var viewportSize)
                || !IsInstanceValid(_statusHudRoot)
                || !IsInstanceValid(_weaponHudRoot)
                || !IsInstanceValid(_backpackHotkeyButton)
                || !IsInstanceValid(_classSkillRoot))
            {
                return false;
            }
            var statusRect = AnchoredRect(_statusHudRoot, viewportSize);
            var weaponRect = AnchoredRect(_weaponHudRoot, viewportSize);
            var backpackRect = AnchoredRect(_backpackHotkeyButton, viewportSize);
            var skillRect = AnchoredRect(_classSkillRoot, viewportSize);
            var expectedStatusRect = StatusHudRect(viewportSize);
            var expectedWeaponRect = WeaponHudRect(viewportSize);
            var expectedBackpackRect = BackpackHudRect(viewportSize);
            var expectedSkillRect = ResolveClassSkillHudRect(viewportSize, _demolitionGameplayPresentation);
            var rects = _demolitionGameplayPresentation
                ? new[] { statusRect, weaponRect, backpackRect, skillRect }
                : new[] { statusRect, weaponRect, backpackRect, skillRect, AnchoredRect(_squadRoster, viewportSize) };
            return RectApproximatelyMatches(statusRect, expectedStatusRect)
                && RectApproximatelyMatches(weaponRect, expectedWeaponRect)
                && RectApproximatelyMatches(backpackRect, expectedBackpackRect)
                && RectApproximatelyMatches(skillRect, expectedSkillRect)
                && RectanglesAreInsideViewport(rects, viewportSize)
                && RectanglesAreSeparated(rects);
        }
    }

    internal bool FooterHudResponsiveScenariosValidForDiagnostics
    {
        get
        {
            var viewportSizes = new[]
            {
                new Vector2(1280.0f, 720.0f),
                new Vector2(1280.0f, 1024.0f),
                new Vector2(1440.0f, 1080.0f),
                new Vector2(1920.0f, 1080.0f),
                new Vector2(2560.0f, 1440.0f),
                new Vector2(3440.0f, 1440.0f)
            };
            foreach (var viewportSize in viewportSizes)
            {
                if (!FooterLayoutIsSeparated(viewportSize, demolition: false)
                    || !FooterLayoutIsSeparated(viewportSize, demolition: true))
                {
                    return false;
                }
            }
            return true;
        }
    }

    internal string FooterHudLayoutForDiagnostics
    {
        get
        {
            if (!TryGetFooterViewportSize(out var viewportSize))
            {
                return "footer=unavailable";
            }
            if (!IsInstanceValid(_weaponHudRoot) || !IsInstanceValid(_classSkillRoot))
            {
                return "footer=unavailable";
            }
            return $"weapon={FormatRect(AnchoredRect(_weaponHudRoot, viewportSize))} "
                + $"skill={FormatRect(AnchoredRect(_classSkillRoot, viewportSize))}";
        }
    }

    private void BindFooterLayout(Control root)
    {
        _footerLayoutRoot = root;
        _footerLayoutRoot.Resized += RefreshFooterLayout;
        RefreshFooterLayout();
    }

    private void SetDownedFooterSuppressed(bool suppressed)
    {
        if (suppressed == _downedFooterSuppressed
            || !IsInstanceValid(_weaponHudRoot)
            || !IsInstanceValid(_classSkillRoot))
        {
            return;
        }

        _downedFooterSuppressed = suppressed;
        if (suppressed)
        {
            _weaponHudVisibleBeforeDowned = _weaponHudRoot.Visible;
            _classSkillHudVisibleBeforeDowned = _classSkillRoot.Visible;
            _weaponHudRoot.Visible = false;
            _classSkillRoot.Visible = false;
            return;
        }

        _weaponHudRoot.Visible = _weaponHudVisibleBeforeDowned;
        _classSkillRoot.Visible = _classSkillHudVisibleBeforeDowned;
    }

    private void RefreshFooterLayout()
    {
        if (!IsInstanceValid(_footerLayoutRoot) || !IsInstanceValid(_classSkillRoot))
        {
            return;
        }

        if (!TryGetFooterViewportSize(out var viewportSize))
        {
            return;
        }
        var skillRect = ResolveClassSkillHudRect(viewportSize, _demolitionGameplayPresentation);
        _classSkillRoot.OffsetLeft = skillRect.Position.X - viewportSize.X * 0.5f;
        _classSkillRoot.OffsetTop = skillRect.Position.Y - viewportSize.Y;
        _classSkillRoot.OffsetRight = _classSkillRoot.OffsetLeft + skillRect.Size.X;
        _classSkillRoot.OffsetBottom = _classSkillRoot.OffsetTop + skillRect.Size.Y;
    }

    private bool TryGetFooterViewportSize(out Vector2 viewportSize)
    {
        viewportSize = IsInstanceValid(_footerLayoutRoot)
            ? _footerLayoutRoot.Size
            : Vector2.Zero;
        if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
        {
            viewportSize = GetViewport().GetVisibleRect().Size;
        }
        return viewportSize.X > 0.0f && viewportSize.Y > 0.0f;
    }

    private static Rect2 ResolveClassSkillHudRect(Vector2 viewportSize, bool demolition)
    {
        var skillSize = new Vector2(
            ClassSkillHudWidth,
            demolition ? DemolitionClassSkillHudHeight : ClassSkillHudHeight);
        var centeredX = (viewportSize.X - skillSize.X) * 0.5f;
        var weaponRect = WeaponHudRect(viewportSize);
        var rightAlignedX = weaponRect.Position.X - FooterPanelGap - skillSize.X;
        var inlineX = Mathf.Min(centeredX, rightAlignedX);
        var minimumInlineX = StatusHudRightEdge + FooterPanelGap;
        if (inlineX >= minimumInlineX)
        {
            return new Rect2(
                new Vector2(inlineX, viewportSize.Y - ClassSkillHudBottomOffset),
                skillSize);
        }

        var backpackRect = BackpackHudRect(viewportSize);
        var stackedTop = Mathf.Min(weaponRect.Position.Y, backpackRect.Position.Y)
            - FooterPanelGap
            - skillSize.Y;
        return new Rect2(new Vector2(centeredX, stackedTop), skillSize);
    }

    private static Rect2 WeaponHudRect(Vector2 viewportSize)
        => new(
            new Vector2(
                viewportSize.X - WeaponHudRightMargin - WeaponHudWidth,
                viewportSize.Y - WeaponHudBottomMargin - WeaponHudHeight),
            new Vector2(WeaponHudWidth, WeaponHudHeight));

    private static Rect2 BackpackHudRect(Vector2 viewportSize)
        => new(
            new Vector2(
                viewportSize.X - BackpackHudRightMargin - BackpackHudWidth,
                viewportSize.Y - BackpackHudBottomOffset),
            new Vector2(BackpackHudWidth, BackpackHudHeight));

    private static bool FooterLayoutIsSeparated(Vector2 viewportSize, bool demolition)
    {
        if (viewportSize.X < MinimumSupportedFooterWidth)
        {
            return false;
        }
        var skillRect = ResolveClassSkillHudRect(viewportSize, demolition);
        var weaponRect = WeaponHudRect(viewportSize);
        var backpackRect = BackpackHudRect(viewportSize);
        var statusRect = StatusHudRect(viewportSize);
        var visibleRects = demolition
            ? new[] { skillRect, weaponRect, backpackRect, statusRect }
            : new[] { skillRect, weaponRect, backpackRect, statusRect, SquadRosterRect() };
        foreach (var rect in visibleRects)
        {
            if (!RectIsInsideViewport(rect, viewportSize))
            {
                return false;
            }
        }
        return RectanglesAreSeparated(visibleRects);
    }

    private static bool RectanglesAreInsideViewport(Rect2[] rects, Vector2 viewportSize)
    {
        foreach (var rect in rects)
        {
            if (!RectIsInsideViewport(rect, viewportSize))
            {
                return false;
            }
        }
        return true;
    }

    private static bool RectanglesAreSeparated(Rect2[] rects)
    {
        for (var left = 0; left < rects.Length; left++)
        {
            for (var right = left + 1; right < rects.Length; right++)
            {
                if (rects[left].Intersects(rects[right]))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static Rect2 StatusHudRect(Vector2 viewportSize)
        => new(
            new Vector2(
                StatusHudLeftMargin,
                viewportSize.Y - StatusHudBottomMargin - StatusHudHeight),
            new Vector2(StatusHudWidth, StatusHudHeight));

    private static Rect2 SquadRosterRect()
        => new(
            new Vector2(SquadRosterLeft, SquadRosterTop),
            new Vector2(SquadRosterWidth, SquadRosterHeight));

    private static bool RectIsInsideViewport(Rect2 rect, Vector2 viewportSize)
        => rect.Position.X >= 0.0f
        && rect.Position.Y >= 0.0f
        && rect.End.X <= viewportSize.X
        && rect.End.Y <= viewportSize.Y;

    private static bool RectApproximatelyMatches(Rect2 actual, Rect2 expected)
        => actual.Position.DistanceSquaredTo(expected.Position) <= 0.01f
        && actual.Size.DistanceSquaredTo(expected.Size) <= 0.01f;

    private static Rect2 AnchoredRect(Control control, Vector2 parentSize)
    {
        var position = new Vector2(
            control.AnchorLeft * parentSize.X + control.OffsetLeft,
            control.AnchorTop * parentSize.Y + control.OffsetTop);
        var end = new Vector2(
            control.AnchorRight * parentSize.X + control.OffsetRight,
            control.AnchorBottom * parentSize.Y + control.OffsetBottom);
        return new Rect2(position, end - position);
    }

    private static string FormatRect(Rect2 rect)
        => $"{rect.Position.X:0.0},{rect.Position.Y:0.0},{rect.Size.X:0.0},{rect.Size.Y:0.0}";
}
