using System.Collections.Generic;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private readonly JianghaiInteriorPopulationService _jianghaiInteriorPopulation = new();
    private JianghaiInteriorBuildResult? _jianghaiInteriors;

    private void BuildJianghaiResidentialInteriors()
    {
        _jianghaiInteriors = null;
        if (_jianghaiOldCityScene is not { } authored
            || !IsInstanceValid(authored.Root)
            || !IsInstanceValid(_levelRoot))
        {
            return;
        }

        var build = _jianghaiInteriorPopulation.Build(
            authored.Root,
            _levelRoot,
            _refineryDoors.Count + 1,
            CreateJianghaiInteriorFurnitureLoot);
        _jianghaiInteriors = build;
        _refineryDoors.AddRange(build.Doors);
        RegisterJianghaiInteriorLoot(build);
        RegisterJianghaiInteriorTraversal(build);
        SpawnJianghaiInteriorResidents(build);

        if (build.SourceCount != JianghaiInteriorPopulationService.ExpectedRoomCount)
        {
            GD.PushError(
                "Jianghai enterable residence metadata count mismatch: "
                + $"{build.SourceCount}/{JianghaiInteriorPopulationService.ExpectedRoomCount}.");
        }
    }

    private IEnumerable<LootItem> CreateJianghaiInteriorFurnitureLoot(
        ResidentialFurnitureKind kind,
        string archetype,
        int roomIndex,
        int itemIndex)
    {
        var residentialArchetype = archetype switch
        {
            "tea_house" => ResidentialRoomArchetype.CommunityKitchen,
            "repair_shop" => ResidentialRoomArchetype.MaintenanceWorkshop,
            _ => ResidentialRoomArchetype.FamilyApartment
        };
        return CreateResidentialFurnitureLoot(
            kind,
            residentialArchetype,
            220 + roomIndex,
            0,
            itemIndex == 0 ? -1.0f : 1.0f);
    }

    private void RegisterJianghaiInteriorLoot(JianghaiInteriorBuildResult build)
    {
        foreach (var searchable in build.Searchables)
        {
            searchable.FirstSearched += OnResidentialFurnitureSearched;
            _residentialFurniture.Add(searchable);
            _lootSources.Add(searchable);
            _lootWorldPoints.Add(searchable.GlobalPosition);
        }
    }

    private void RegisterJianghaiInteriorTraversal(JianghaiInteriorBuildResult build)
    {
        foreach (var room in build.Rooms)
        {
            var exterior = room.Root.ToGlobal(new Vector3(0, 0.12f, 1.8f));
            var interior = room.Root.ToGlobal(new Vector3(
                room.TraversalLocalPoint.X,
                0.12f,
                room.TraversalLocalPoint.Z));
            var id = RegisterSquadTraversalLink(
                $"jianghai_interior_door:{room.Door.DoorId}",
                SquadTraversalKind.Walk,
                bidirectional: true,
                new[] { exterior, room.OutsidePoint, room.InsidePoint, interior },
                costMultiplier: 1.03f);
            if (id >= 0)
            {
                build.TraversalLinkCount++;
            }
        }
    }

    private void SpawnJianghaiInteriorResidents(JianghaiInteriorBuildResult build)
    {
        var roles = new[]
        {
            CivilianRole.Resident,
            CivilianRole.Evacuee,
            CivilianRole.UtilityWorker,
            CivilianRole.CommunityGuard
        };
        var visuals = new[]
        {
            OperatorVisualId.Viper,
            OperatorVisualId.Magpie,
            OperatorVisualId.Heron,
            OperatorVisualId.Jackal
        };
        var count = Mathf.Min(
            JianghaiInteriorPopulationService.ExpectedResidentCount,
            build.Rooms.Count);
        for (var index = 0; index < count; index++)
        {
            var room = build.Rooms[index];
            var resident = new CivilianNpc
            {
                Name = $"JianghaiEnterableResident_{index + 1:00}"
            };
            resident.UseAuthoredVisual(visuals[index]);
            resident.Configure(
                this,
                roles[index],
                220 + index,
                0,
                room.Root.GlobalTransform,
                room.ResidentLocalPoint,
                new Vector2(
                    Mathf.Min(0.7f, room.Width * 0.14f),
                    Mathf.Min(0.65f, room.Depth * 0.12f)));
            RegisterResidentialLanguageRefresher(resident.SetLanguage);
            _levelRoot.AddChild(resident);
            resident.AddToGroup("jianghai_enterable_resident");
            resident.AddToGroup("jianghai_interior_resident");
            _civilians.Add(resident);
            build.Residents.Add(resident);
        }
    }
}
