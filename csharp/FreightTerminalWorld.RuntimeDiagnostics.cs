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
            ("--validate-launch-isolation", static world => world.ValidateLaunchIsolation()),
            ("--validate-backend-client", static world => world.ValidateBackendClient()),
            ("--validate-operations-office", static world => world.ValidateOperationsOffice()),
            ("--validate-pause-ui", static world => world.ValidatePauseUi()),
            ("--validate-demolition", static world => world.ValidateDemolitionMode()),
            ("--validate-demolition-lighting", static world => world.ValidateDemolitionLighting()),
            ("--validate-demolition-rules", static world => world.ValidateDemolitionRules()),
            ("--validate-demolition-arena", static world => world.ValidateDemolitionArena()),
            ("--validate-harbor-locks", static world => world.ValidateHarborLocks()),
            ("--validate-tideglass-reactor", static world => world.ValidateTideglassReactor()),
            ("--validate-demolition-briefing", static world => world.ValidateDemolitionBriefing()),
            ("--validate-demolition-buy", static world => world.ValidateDemolitionBuy()),
            ("--validate-demolition-round-result", static world => world.ValidateDemolitionRoundResult()),
            ("--validate-demolition-network-host", static world => world.ValidateDemolitionNetworkSession(host: true)),
            ("--validate-demolition-network-client", static world => world.ValidateDemolitionNetworkSession(host: false)),
            ("--validate-demolition-network-alpha-host", static world => world.ValidateDemolitionNetworkSession(host: true, DemolitionNetworkTeam.Alpha)),
            ("--validate-demolition-network-alpha-client", static world => world.ValidateDemolitionNetworkSession(host: false, DemolitionNetworkTeam.Alpha)),
            ("--validate-demolition-network-late-client", static world => world.ValidateDemolitionNetworkJoinRejection(mapMismatch: false)),
            ("--validate-demolition-network-mismatch-client", static world => world.ValidateDemolitionNetworkJoinRejection(mapMismatch: true)),
            ("--validate-demolition-network-roster-host", static world => world.ValidateDemolitionNetworkRoster(host: true)),
            ("--validate-demolition-network-roster-alpha-client", static world => world.ValidateDemolitionNetworkRoster(host: false, DemolitionNetworkTeam.Alpha)),
            ("--validate-demolition-network-roster-bravo-client", static world => world.ValidateDemolitionNetworkRoster(host: false, DemolitionNetworkTeam.Bravo)),
            ("--validate-extraction-network-host", static world => world.ValidateExtractionNetworkSession(host: true)),
            ("--validate-extraction-network-client", static world => world.ValidateExtractionNetworkSession(host: false)),
            ("--capture-operations-office", static world => world.CaptureOperationsOffice()),
            ("--capture-demolition-briefing", static world => world.CaptureDemolitionBriefing()),
            ("--capture-demolition-buy", static world => world.CaptureDemolitionBuy()),
            ("--capture-demolition-round-result", static world => world.CaptureDemolitionRoundResult()),
            ("--capture-demolition-arena", static world => world.CaptureDemolitionArena()),
            ("--capture-harbor-locks", static world => world.CaptureHarborLocks()),
            ("--capture-tideglass-reactor", static world => world.CaptureTideglassReactor()),
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
            ("--capture-melee", static world => world.CaptureMelee()),
            ("--validate-loot", static world => world.ValidateLootFlow()),
            ("--validate-corpse-loot", static world => world.ValidateCorpseLootFlow()),
            ("--capture-backpack", static world => world.CaptureBackpackFrame()),
            ("--capture-optics", static world => world.CaptureOpticsFrames()),
            ("--capture-optics-narrow", static world => world.CaptureOpticsFrames(narrow: true)),
            ("--capture-optics-ultrawide", static world => world.CaptureOpticsFrames(ultrawide: true)),
            ("--validate-ads-alignment", static world => world.ValidateAdsAlignment()),
            ("--validate-stance-armor", static world => world.ValidateStanceAndArmorFlow()),
            ("--capture-expanded-map", static world => world.CaptureExpandedMapFrame()),
            ("--capture-extraction", static world => world.CaptureExtractionFrame()),
            ("--validate-large-map", static world => world.ValidateLargeMapFlow()),
            ("--validate-weapon-ui", static world => world.ValidateWeaponUiFlow()),
            ("--validate-melee", static world => world.ValidateMelee()),
            ("--validate-melee-impact", static world => world.ValidateMeleeImpact()),
            ("--validate-weapon-audio", static world => world.ValidateWeaponAudio()),
            ("--validate-weapon-impact", static world => world.ValidateWeaponImpact()),
            ("--validate-quick-slots", static world => world.ValidateQuickSlots()),
            ("--validate-arsenal", static world => world.ValidateArsenalFlow()),
            ("--validate-combat-models", static world => world.ValidateCombatModels()),
            ("--validate-operator-roster", static world => world.ValidateOperatorRoster()),
            ("--validate-operator-animations", static world => world.ValidateOperatorAnimations()),
            ("--validate-operator-carry", static world => world.ValidateOperatorCarry()),
            ("--capture-operator-carry", static world => world.CaptureOperatorCarry()),
            ("--validate-boss", static world => world.ValidateWorldBoss()),
            ("--validate-squad", static world => world.ValidateSquadFlow()),
            ("--validate-squad-tactics", static world => world.ValidateSquadTactics()),
            ("--validate-squad-performance", static world => world.ValidateSquadPerformance()),
            ("--validate-squad-traversal", static world => world.ValidateSquadTraversal()),
            ("--validate-squad-indoor-revive", static world => world.ValidateSquadIndoorRevive()),
            ("--validate-residential-squad-stairs", static world => world.ValidateResidentialSquadStairs()),
            ("--validate-network-endpoint", static world => world.ValidateNetworkEndpoint()),
            ("--validate-lan-discovery", static world => world.ValidateLanRoomDiscovery()),
            ("--validate-aircraft-behavior", static world => world.ValidateAircraftBehavior()),
            ("--validate-aircraft-combat", static world => world.ValidateAircraftCombat()),
            ("--validate-explosion-cover", static world => world.ValidateExplosionCover()),
            ("--validate-map-density", static world => world.ValidateMapDensity()),
            ("--validate-district-network", static world => world.ValidateDistrictNetwork()),
            ("--validate-special-landmarks", static world => world.ValidateSpecialLandmarks()),
            ("--validate-goal-pack", static world => world.ValidateGoalPack()),
            ("--validate-extraction-spawns", static world => world.ValidateExtractionSpawns()),
            ("--validate-extraction-ai", static world => world.ValidateExtractionAi()),
            ("--validate-extraction-ai-deployment", static world => world.ValidateExtractionAiDeployment()),
            ("--validate-ai-navigation", static world => world.ValidateAiNavigation()),
            ("--validate-extraction-loot", static world => world.ValidateExtractionLoot()),
            ("--validate-extraction-loadout", static world => world.ValidateExtractionLoadout()),
            ("--validate-extraction-los", static world => world.ValidateExtractionLos()),
            ("--validate-extract-rank", static world => world.ValidateExtractRank()),
            ("--validate-refinery-map", static world => world.ValidateRefineryMap()),
            ("--validate-refinery-collision", static world => world.ValidateRefineryCollision()),
            ("--validate-refinery-doors", static world => world.ValidateRefineryDoors()),
            ("--validate-refinery-atmosphere", static world => world.ValidateRefineryAtmosphere()),
            ("--validate-freight-terminal-doors", static world => world.ValidateFreightTerminalDoors()),
            ("--validate-industrial-interiors", static world => world.ValidateIndustrialInteriors()),
            ("--validate-extraction-sequence", static world => world.ValidateExtractionSequence()),
            ("--capture-extraction-flight", static world => world.CaptureExtractionFlight()),
            ("--capture-refinery-map", static world => world.CaptureRefineryMap()),
            ("--capture-promotion", static world => world.CapturePromotionMedia()),
            ("--capture-readme-zh", static world => world.CaptureReadmeChineseGallery()),
            ("--capture-industrial-interiors", static world => world.CaptureIndustrialInteriors()),
            ("--validate-tactical-hud", static world => world.ValidateTacticalHud()),
            ("--validate-hud-performance", static world => world.ValidateHudPerformance()),
            ("--validate-progression", static world => world.ValidateProgressionFlow()),
            ("--validate-deployment-ui", static world => world.ValidateDeploymentUi()),
            ("--validate-backpack-tab", static world => world.ValidateBackpackTab()),
            ("--validate-skylinks", static world => world.ValidateSkyLinks()),
            ("--validate-skybridge-access", static world => world.ValidateSkybridgeAccess()),
            ("--validate-vehicle-drive", static world => world.ValidateVehicleDrive()),
            ("--validate-hand-diagnostics", static world => world.ValidateHandDiagnostics()),
            ("--validate-hand-diagnostics-narrow", static world => world.ValidateHandDiagnostics(narrow: true)),
            ("--validate-hand-diagnostics-ultrawide", static world => world.ValidateHandDiagnostics(ultrawide: true)),
            ("--validate-sidearm-reload", static world => world.ValidateSidearmReloadDiagnostics()),
            ("--capture-open-hand", static world => world.CaptureOpenHandValidation()),
            ("--capture-open-hand-narrow", static world => world.CaptureOpenHandValidation(narrow: true)),
            ("--capture-open-hand-ultrawide", static world => world.CaptureOpenHandValidation(ultrawide: true)),
            ("--validate-vehicle-combat", static world => world.ValidateVehicleCombat()),
            ("--validate-stairs", static world => world.ValidateStairsClimb()),
            ("--validate-roof-access", static world => world.ValidateRoofAccess()),
            ("--validate-residential", static world => world.ValidateResidentialCommunity()),
            ("--validate-residential-gameplay", static world => world.ValidateResidentialGameplay()),
            ("--validate-residential-localization", static world => world.ValidateResidentialLocalization()),
            ("--validate-residential-cover", static world => world.ValidateResidentialCover()),
            ("--validate-residential-density", static world => world.ValidateResidentialDensity()),
            ("--validate-residential-diversity", static world => world.ValidateResidentialDiversity()),
            ("--validate-residential-street-art", static world => world.ValidateResidentialStreetArt()),
            ("--validate-relay-stations", static world => world.ValidateRelayStations()),
            ("--validate-medical", static world => world.ValidateMedicalSystem()),
            ("--validate-field-use-presentation", static world => world.ValidateFieldUsePresentation()),
            ("--validate-field-use-lifecycle", static world => world.ValidateFieldUseLifecycle()),
            ("--validate-stamina", static world => world.ValidateStaminaRecovery()),
            ("--validate-loot-variety", static world => world.ValidateLootVariety()),
            ("--validate-hit-feedback", static world => world.ValidateHitFeedback()),
            ("--validate-glass", static world => world.ValidateBreakableGlass()),
            ("--validate-performance", static world => world.ValidateMapPerformance()),
            ("--validate-crowd-performance", static world => world.ValidateCrowdPerformance()),
            ("--capture-residential", static world => world.CaptureResidentialCommunity()),
            ("--capture-residential-diversity", static world => world.CaptureResidentialDiversity()),
            ("--capture-residential-street-art", static world => world.CaptureResidentialStreetArt()),
            ("--capture-special-landmarks", static world => world.CaptureSpecialLandmarks()),
            ("--capture-residential-gameplay", static world => world.CaptureResidentialGameplay()),
            ("--capture-skylinks", static world => world.CaptureResidentialSkyLinks()),
            ("--capture-skybridge-access", static world => world.CaptureSkybridgeAccess()),
            ("--capture-residential-stairs", static world => world.CaptureResidentialStairDetails()),
            ("--capture-roof-access", static world => world.CaptureRoofAccess()),
            ("--capture-relay-station", static world => world.CaptureRelayStation()),
            ("--capture-medical-wheel", static world => world.CaptureMedicalWheel()),
            ("--capture-field-use-presentation", static world => world.CaptureFieldUsePresentation()),
            ("--capture-hit-feedback", static world => world.CaptureHitFeedback()),
            ("--capture-glass", static world => world.CaptureGlassBreak()),
            ("--capture-squad", static world => world.CaptureSquadFrame()),
            ("--capture-squad-spectator", static world => world.CaptureSquadSpectatorFrame()),
            ("--capture-squad-lobby", static world => world.CaptureSquadLobbyFrame()),
            ("--capture-operator-roster", static world => world.CaptureOperatorRosterFrame()),
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
