using Godot;

namespace OperationSteelTide;

/// <summary>
/// Player-scale vertical access for Falltide.  The authored scene supplies the
/// catwalks and service decks; these routes make the three height bands usable
/// instead of leaving the upper ring as scenery.
/// </summary>
public partial class FreightTerminalWorld
{
    private bool _orbitalComplexRuntimeAccessBuilt;

    private void BuildOrbitalComplexRuntimeAccess()
    {
        if (!IsOrbitalComplexRuntimeMapSelected || _orbitalComplexRuntimeAccessBuilt)
        {
            return;
        }

        _roofAccessRoutes.Clear();
        _roofAccessRoot = new Node3D { Name = "FalltideVerticalAccess" };
        _roofAccessRoot.AddToGroup("orbital_complex_vertical_access");
        _levelRoot.AddChild(_roofAccessRoot);

        var safety = Mat(
            "falltide_access_safety",
            new Color(0.92f, 0.42f, 0.08f),
            metallic: 0.22f,
            roughness: 0.4f,
            emission: new Color(0.32f, 0.06f, 0.01f));

        // The lower dock has two emergency ladders in addition to the sloped
        // ramps.  They are deliberately offset from the pit rim so a squad can
        // choose a fast vertical escape while the bridge is contested.
        AddRoofLadder(
            "DrydockWestEmergencyLadder",
            "Impact dry dock",
            new Vector3(-34.0f, -31.0f, -24.0f),
            new Vector3(-34.0f, -15.32f, -24.0f),
            Vector3.Right,
            safety);
        AddRoofLadder(
            "DrydockEastEmergencyLadder",
            "Impact dry dock",
            new Vector3(34.0f, -31.0f, -44.0f),
            new Vector3(34.0f, -15.32f, -44.0f),
            Vector3.Left,
            safety);

        // Four service-deck ladders reach the calibration ring.  The ring is a
        // high-risk overwatch route, not a decorative balcony: each end has a
        // separate dismount so a team can cross or drop back under fire.
        AddRoofLadder(
            "CalibrationWestLadder",
            "Calibration catwalk",
            new Vector3(-76.0f, -15.32f, -34.0f),
            new Vector3(-76.0f, -2.36f, -34.0f),
            Vector3.Left,
            safety);
        AddRoofLadder(
            "CalibrationEastLadder",
            "Calibration catwalk",
            new Vector3(76.0f, -15.32f, -34.0f),
            new Vector3(76.0f, -2.36f, -34.0f),
            Vector3.Right,
            safety);
        AddRoofLadder(
            "NorthWatchWestLadder",
            "North watch spine",
            new Vector3(-43.0f, -15.32f, -88.0f),
            new Vector3(-43.0f, -2.36f, -88.0f),
            Vector3.Left,
            safety);
        AddRoofLadder(
            "NorthWatchEastLadder",
            "North watch spine",
            new Vector3(43.0f, -15.32f, -88.0f),
            new Vector3(43.0f, -2.36f, -88.0f),
            Vector3.Right,
            safety);

        // Tide-gate control towers provide a final elevated rotation.  These
        // ladders stay available in blackout; only the bypass gate itself is
        // stage-locked, preserving a meaningful risk/reward flank.
        AddRoofLadder(
            "TideGateControlWestLadder",
            "Tide Gate control",
            new Vector3(-31.0f, -15.32f, -178.0f),
            new Vector3(-31.0f, -2.36f, -178.0f),
            Vector3.Left,
            safety);
        AddRoofLadder(
            "TideGateControlEastLadder",
            "Tide Gate control",
            new Vector3(31.0f, -15.32f, -178.0f),
            new Vector3(31.0f, -2.36f, -178.0f),
            Vector3.Right,
            safety);

        _levelRoot.SetMeta("falltide_vertical_access_routes", _roofAccessRoutes.Count);
        _levelRoot.SetMeta("falltide_vertical_access_layers", 3);
        _orbitalComplexRuntimeAccessBuilt = true;
    }

    private string OrbitalComplexAccessInteractionLabel(
        RoofAccessRoute route,
        bool startAtTop)
    {
        var verb = startAtTop
            ? GameLocalization.Get("falltide_descend", _languageSetting, "DESCEND TO SERVICE DECK")
            : GameLocalization.Get("falltide_climb", _languageSetting, "CLIMB TO UPPER RING");
        return $"{verb}  //  {route.Building.ToUpperInvariant()}";
    }
}
