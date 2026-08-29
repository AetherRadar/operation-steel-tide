using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

internal sealed record JianghaiInteriorRoom(
    string SourceName,
    string Archetype,
    MeshInstance3D Source,
    Node3D Root,
    InteractiveBuildingDoor Door,
    IReadOnlyList<Node3D> Furniture,
    IReadOnlyList<ResidentialSearchableFurniture> Searchables,
    Vector3 OutsidePoint,
    Vector3 InsidePoint,
    Vector3 TraversalLocalPoint,
    Vector3 ResidentLocalPoint,
    float Width,
    float Depth,
    float FrontInset);

internal sealed class JianghaiInteriorBuildResult
{
    public List<JianghaiInteriorRoom> Rooms { get; } = new();
    public List<InteractiveBuildingDoor> Doors { get; } = new();
    public List<ResidentialSearchableFurniture> Searchables { get; } = new();
    public List<CivilianNpc> Residents { get; } = new();
    public int SourceCount { get; internal set; }
    public int UnexpectedSourceCount { get; internal set; }
    public int AuthoredFurnitureMeshCount { get; internal set; }
    public int TraversalLinkCount { get; internal set; }
}
