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
        var playerAuthored = _player.UsesAuthoredPrimaryWeaponForDiagnostics;
        var squadAuthored = _squadMates.Count > 0
            && _squadMates.Where(IsInstanceValid).All(mate => mate.UsesAuthoredOperatorForDiagnostics);
        var livingEnemies = _enemies.Where(IsInstanceValid).ToArray();
        var enemiesAuthored = livingEnemies.Length > 0
            && livingEnemies.All(enemy => enemy.UsesAuthoredOperatorForDiagnostics);
        var valid = weaponGeometry
            && operatorGeometry
            && playerAuthored
            && squadAuthored
            && enemiesAuthored;

        GD.Print(
            $"COMBAT_MODELS_CHECK weapon_loaded={weapon.Loaded} weapon_nodes={weapon.RequiredNodes} "
            + $"weapon_meshes={weapon.MeshCount} weapon_size={weapon.Size} "
            + $"operator_loaded={operatorModel.Loaded} operator_nodes={operatorModel.RequiredNodes} "
            + $"operator_meshes={operatorModel.MeshCount} operator_size={operatorModel.Size} "
            + $"player_authored={playerAuthored} squad_authored={squadAuthored} "
            + $"enemies_authored={enemiesAuthored} enemies={livingEnemies.Length}");
        GD.Print($"COMBAT_MODELS_PASS valid={valid}");
        QuitDiagnosticAfterSceneCleanup(valid ? 0 : 2);
    }
}
