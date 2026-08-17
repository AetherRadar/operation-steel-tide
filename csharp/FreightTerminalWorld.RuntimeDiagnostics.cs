using System;
using System.Threading.Tasks;
using Godot;

namespace OperationSteelTide;

public partial class FreightTerminalWorld
{
    private static class RuntimeDiagnosticRunner
    {
        private static readonly (string Argument, Action<FreightTerminalWorld> Run)[] Commands =
        {
            ("--validate-operations-office", static world => world.ValidateOperationsOffice()),
            ("--validate-pause-ui", static world => world.ValidatePauseUi()),
            ("--validate-demolition", static world => world.ValidateDemolitionMode()),
            ("--validate-demolition-rules", static world => world.ValidateDemolitionRules()),
            ("--validate-demolition-arena", static world => world.ValidateDemolitionArena()),
            ("--validate-harbor-locks", static world => world.ValidateHarborLocks()),
            ("--validate-demolition-briefing", static world => world.ValidateDemolitionBriefing()),
            ("--validate-demolition-buy", static world => world.ValidateDemolitionBuy()),
            ("--capture-operations-office", static world => world.CaptureOperationsOffice()),
            ("--capture-demolition-briefing", static world => world.CaptureDemolitionBriefing()),
            ("--capture-demolition-buy", static world => world.CaptureDemolitionBuy()),
            ("--capture-demolition-arena", static world => world.CaptureDemolitionArena()),
            ("--capture-harbor-locks", static world => world.CaptureHarborLocks()),
            ("--capture-validation", static world => world.CaptureValidationFrame()),
            ("--capture-deployment", static world => world.CaptureDeploymentFrame()),
            ("--capture-pause", static world => world.CapturePauseFrame()),
            ("--validate-objectives", static world => world.ValidateObjectiveFlow()),
            ("--validate-reinforcements", static world => world.ValidateReinforcementFlow()),
            ("--capture-ads", static world => world.CaptureAdsFrame()),
            ("--validate-equipment", static world => world.ValidateEquipmentFlow()),
            ("--validate-pickup", static world => world.ValidatePickupFlow()),
            ("--validate-ammo-inventory", static world => world.ValidateAmmoInventoryFlow()),
            ("--capture-reload", static world => world.CaptureReloadFrame()),
            ("--capture-operator", static world => world.CaptureOperatorFrame()),
            ("--capture-zh", static world => world.CaptureChineseFrame()),
            ("--capture-knife", static world => world.CaptureKnifeFrame()),
            ("--validate-loot", static world => world.ValidateLootFlow()),
            ("--validate-corpse-loot", static world => world.ValidateCorpseLootFlow()),
            ("--capture-backpack", static world => world.CaptureBackpackFrame()),
            ("--capture-optics", static world => world.CaptureOpticsFrames()),
            ("--validate-ads-alignment", static world => world.ValidateAdsAlignment()),
            ("--validate-stance-armor", static world => world.ValidateStanceAndArmorFlow()),
            ("--capture-expanded-map", static world => world.CaptureExpandedMapFrame()),
            ("--capture-extraction", static world => world.CaptureExtractionFrame()),
            ("--validate-large-map", static world => world.ValidateLargeMapFlow()),
            ("--validate-weapon-ui", static world => world.ValidateWeaponUiFlow()),
            ("--validate-quick-slots", static world => world.ValidateQuickSlots()),
            ("--validate-arsenal", static world => world.ValidateArsenalFlow()),
            ("--validate-combat-models", static world => world.ValidateCombatModels()),
            ("--validate-boss", static world => world.ValidateWorldBoss()),
            ("--validate-squad", static world => world.ValidateSquadFlow()),
            ("--validate-squad-traversal", static world => world.ValidateSquadTraversal()),
            ("--validate-network-endpoint", static world => world.ValidateNetworkEndpoint()),
            ("--validate-aircraft-combat", static world => world.ValidateAircraftCombat()),
            ("--validate-map-density", static world => world.ValidateMapDensity()),
            ("--validate-district-network", static world => world.ValidateDistrictNetwork()),
            ("--validate-special-landmarks", static world => world.ValidateSpecialLandmarks()),
            ("--validate-goal-pack", static world => world.ValidateGoalPack()),
            ("--validate-extraction-spawns", static world => world.ValidateExtractionSpawns()),
            ("--validate-extraction-ai", static world => world.ValidateExtractionAi()),
            ("--validate-extraction-loot", static world => world.ValidateExtractionLoot()),
            ("--validate-extraction-loadout", static world => world.ValidateExtractionLoadout()),
            ("--validate-extraction-los", static world => world.ValidateExtractionLos()),
            ("--validate-extract-rank", static world => world.ValidateExtractRank()),
            ("--validate-refinery-map", static world => world.ValidateRefineryMap()),
            ("--validate-extraction-sequence", static world => world.ValidateExtractionSequence()),
            ("--capture-extraction-flight", static world => world.CaptureExtractionFlight()),
            ("--capture-refinery-map", static world => world.CaptureRefineryMap()),
            ("--validate-tactical-hud", static world => world.ValidateTacticalHud()),
            ("--validate-progression", static world => world.ValidateProgressionFlow()),
            ("--validate-deployment-ui", static world => world.ValidateDeploymentUi()),
            ("--validate-backpack-tab", static world => world.ValidateBackpackTab()),
            ("--validate-skylinks", static world => world.ValidateSkyLinks()),
            ("--validate-skybridge-access", static world => world.ValidateSkybridgeAccess()),
            ("--validate-vehicle-drive", static world => world.ValidateVehicleDrive()),
            ("--validate-vehicle-combat", static world => world.ValidateVehicleCombat()),
            ("--validate-stairs", static world => world.ValidateStairsClimb()),
            ("--validate-roof-access", static world => world.ValidateRoofAccess()),
            ("--validate-residential", static world => world.ValidateResidentialCommunity()),
            ("--validate-residential-gameplay", static world => world.ValidateResidentialGameplay()),
            ("--validate-residential-localization", static world => world.ValidateResidentialLocalization()),
            ("--validate-residential-cover", static world => world.ValidateResidentialCover()),
            ("--validate-residential-density", static world => world.ValidateResidentialDensity()),
            ("--validate-relay-stations", static world => world.ValidateRelayStations()),
            ("--validate-medical", static world => world.ValidateMedicalSystem()),
            ("--validate-stamina", static world => world.ValidateStaminaRecovery()),
            ("--validate-loot-variety", static world => world.ValidateLootVariety()),
            ("--validate-hit-feedback", static world => world.ValidateHitFeedback()),
            ("--validate-glass", static world => world.ValidateBreakableGlass()),
            ("--validate-performance", static world => world.ValidateMapPerformance()),
            ("--capture-residential", static world => world.CaptureResidentialCommunity()),
            ("--capture-special-landmarks", static world => world.CaptureSpecialLandmarks()),
            ("--capture-residential-gameplay", static world => world.CaptureResidentialGameplay()),
            ("--capture-skylinks", static world => world.CaptureResidentialSkyLinks()),
            ("--capture-skybridge-access", static world => world.CaptureSkybridgeAccess()),
            ("--capture-residential-stairs", static world => world.CaptureResidentialStairDetails()),
            ("--capture-roof-access", static world => world.CaptureRoofAccess()),
            ("--capture-relay-station", static world => world.CaptureRelayStation()),
            ("--capture-medical-wheel", static world => world.CaptureMedicalWheel()),
            ("--capture-hit-feedback", static world => world.CaptureHitFeedback()),
            ("--capture-glass", static world => world.CaptureGlassBreak()),
            ("--capture-squad", static world => world.CaptureSquadFrame()),
            ("--capture-squad-lobby", static world => world.CaptureSquadLobbyFrame()),
            ("--capture-tactical-hud", static world => world.CaptureTacticalHud()),
            ("--capture-boss", static world => world.CaptureWorldBoss())
        };

        public static void RunFirst(FreightTerminalWorld world, string[] args)
        {
            foreach (var command in Commands)
            {
                if (!Array.Exists(args, argument => argument == command.Argument))
                {
                    continue;
                }

                command.Run(world);
                return;
            }
        }
    }

    private void QuitDiagnosticAfterSceneCleanup(int exitCode)
    {
        var tree = GetTree();
        QueueFree();
        CompleteDiagnosticShutdown(tree, exitCode);
    }

    private static async void CompleteDiagnosticShutdown(SceneTree tree, int exitCode)
    {
        await WaitTreeFrames(tree, 3);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await WaitTreeFrames(tree, 24);
        tree.Quit(exitCode);
    }

    private static async Task WaitTreeFrames(SceneTree tree, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }
}
