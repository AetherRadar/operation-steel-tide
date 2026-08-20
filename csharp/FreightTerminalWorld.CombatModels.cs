using System.Linq;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private async void ValidateCombatModels()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var weapon = CombatModelLibrary.InspectWeapon();
        var operatorModel = CombatModelLibrary.InspectOperator();
        var previewOperator = CombatModelLibrary.InspectPreviewOperator();
        var gsh18 = CombatModelLibrary.InspectGsh18();
        var desertEagle = CombatModelLibrary.InspectDesertEagle();
        var weaponGeometry = weapon.Loaded
            && weapon.RequiredNodes
            && weapon.MeshCount >= 8
            && weapon.Size.X is >= 0.15f and <= 0.8f
            && weapon.Size.Y is >= 0.25f and <= 1.15f
            && weapon.Size.Z is >= 1.4f and <= 2.5f;
        var operatorGeometry = operatorModel.Loaded
            && operatorModel.RequiredNodes
            && operatorModel.MeshCount >= 7
            && operatorModel.Size.X is >= 0.55f and <= 1.15f
            && operatorModel.Size.Y is >= 1.75f and <= 2.3f
            && operatorModel.Size.Z is >= 0.4f and <= 1.1f;
        var previewOperatorGeometry = previewOperator.Loaded
            && previewOperator.RequiredNodes
            && previewOperator.MeshCount >= 1
            && previewOperator.MaterialCount >= 8
            && previewOperator.Size.X is >= 1.3f and <= 1.9f
            && previewOperator.Size.Y is >= 2.45f and <= 2.65f
            && previewOperator.Size.Z is >= 0.35f and <= 0.8f;
        var gsh18Geometry = gsh18.Loaded
            && gsh18.RequiredNodes
            && gsh18.MeshCount >= 10
            && gsh18.MaterialCount >= 1
            && gsh18.Size.X is >= 0.08f and <= 0.3f
            && gsh18.Size.Y is >= 0.3f and <= 0.75f
            && gsh18.Size.Z is >= 0.65f and <= 0.9f;
        var desertEagleGeometry = desertEagle.Loaded
            && desertEagle.RequiredNodes
            && desertEagle.MeshCount >= 20
            && desertEagle.MaterialCount >= 4
            && desertEagle.Size.X is >= 0.1f and <= 0.3f
            && desertEagle.Size.Y is >= 0.45f and <= 0.75f
            && desertEagle.Size.Z is >= 0.9f and <= 1.2f;
        var playerAuthored = _player.UsesAuthoredPrimaryWeaponForDiagnostics;
        var squadAuthored = _squadMates.Count > 0
            && _squadMates.Where(IsInstanceValid).All(mate => mate.UsesAuthoredOperatorForDiagnostics);
        var livingEnemies = _enemies.Where(IsInstanceValid).ToArray();
        var enemiesAuthored = livingEnemies.Length > 0
            && livingEnemies.All(enemy => enemy.UsesAuthoredOperatorForDiagnostics);
        var garrison = livingEnemies.FirstOrDefault(enemy => !enemy.IsRivalSquad && !enemy.IsWorldBoss);
        var rivals = livingEnemies.Where(enemy => enemy.IsRivalSquad).ToArray();
        var garrisonColor = garrison?.AuthoredTeamColorForDiagnostics ?? Colors.Transparent;
        var factionAppearance = garrison is not null
            && rivals.Length >= 2
            && garrison.AuthoredGearOverlayCountForDiagnostics >= 3
            && garrisonColor.G > garrisonColor.R + 0.3f
            && garrisonColor.B > garrisonColor.R + 0.2f
            && rivals.All(enemy => enemy.AuthoredGearOverlayCountForDiagnostics >= 3)
            && rivals.Any(enemy =>
                ColorDistance(enemy.AuthoredTeamColorForDiagnostics, garrisonColor) > 0.55f)
            && rivals.Select(enemy => enemy.AuthoredTeamColorForDiagnostics).Distinct().Count() >= 2;
        var valid = weaponGeometry
            && operatorGeometry
            && previewOperatorGeometry
            && gsh18Geometry
            && desertEagleGeometry
            && playerAuthored
            && squadAuthored
            && enemiesAuthored
            && factionAppearance;

        GD.Print(
            $"COMBAT_MODELS_CHECK weapon_loaded={weapon.Loaded} weapon_nodes={weapon.RequiredNodes} "
            + $"weapon_meshes={weapon.MeshCount} weapon_size={weapon.Size} "
            + $"operator_loaded={operatorModel.Loaded} operator_nodes={operatorModel.RequiredNodes} "
            + $"operator_meshes={operatorModel.MeshCount} operator_size={operatorModel.Size} "
            + $"preview_operator_loaded={previewOperator.Loaded} preview_operator_nodes={previewOperator.RequiredNodes} "
            + $"preview_operator_meshes={previewOperator.MeshCount} preview_operator_materials={previewOperator.MaterialCount} "
            + $"preview_operator_size={previewOperator.Size} "
            + $"gsh18_loaded={gsh18.Loaded} gsh18_nodes={gsh18.RequiredNodes} "
            + $"gsh18_meshes={gsh18.MeshCount} gsh18_materials={gsh18.MaterialCount} gsh18_size={gsh18.Size} "
            + $"deagle_loaded={desertEagle.Loaded} deagle_nodes={desertEagle.RequiredNodes} "
            + $"deagle_meshes={desertEagle.MeshCount} deagle_materials={desertEagle.MaterialCount} "
            + $"deagle_size={desertEagle.Size} "
            + $"player_authored={playerAuthored} squad_authored={squadAuthored} "
            + $"enemies_authored={enemiesAuthored} enemies={livingEnemies.Length} "
            + $"faction_appearance={factionAppearance} garrison_color={garrisonColor} "
            + $"rival_colors={rivals.Select(enemy => enemy.AuthoredTeamColorForDiagnostics).Distinct().Count()}");
        GD.Print($"COMBAT_MODELS_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }

    private static float ColorDistance(Color left, Color right)
    {
        var red = left.R - right.R;
        var green = left.G - right.G;
        var blue = left.B - right.B;
        return Mathf.Sqrt(red * red + green * green + blue * blue);
    }
}
