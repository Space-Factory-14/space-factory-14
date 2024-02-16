using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server.Access.Systems;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Buckle.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Destructible;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Server.NPC.Systems;
using Content.Server.Paper;
using Content.Server.Parallax;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Server.Procedural;
using Content.Server.RoundEnd;
using Content.Server.Shipwrecked;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Content.Shared.Lock;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Content.Server.Station.Components;

namespace Content.Server.GameTicking.Rules;

public sealed class ShipwreckedRuleSystem : GameRuleSystem<ShipwreckedRuleComponent>
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly AccessSystem _accessSystem = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly BiomeSystem _biomeSystem = default!;
    [Dependency] private readonly BuckleSystem _buckleSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly DestructibleSystem _destructibleSystem = default!;
    [Dependency] private readonly DungeonSystem _dungeonSystem = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookupSystem = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoidAppearanceSystem = default!;
    [Dependency] private readonly IdCardSystem _cardSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly LockSystem _lockSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoaderSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly NPCSystem _npcSystem = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly PhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly ShuttleSystem _shuttleSystem = default!;
    [Dependency] private readonly SmokeSystem _smokeSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawningSystem = default!;
    [Dependency] private readonly ThrusterSystem _thrusterSystem = default!;
    [Dependency] private readonly TileSystem _tileSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("shipwrecked");

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out _, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            var minPlayers = 1; // SpaceFactory - Move to Cvar
            if (!ev.Forced && ev.Players.Length < minPlayers)
            {
                _chatManager.SendAdminAnnouncement(Loc.GetString("shipwrecked-not-enough-ready-players",
                    ("readyPlayersCount", ev.Players.Length), ("minimumPlayers", minPlayers)));
                ev.Cancel();
                continue;
            }

            if (ev.Players.Length != 0)
                continue;

            _chatManager.DispatchServerAnnouncement(Loc.GetString("shipwrecked-no-one-ready"));
            ev.Cancel();
        }
    }

    protected override void ActiveTick(EntityUid uid, ShipwreckedRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        var curTime = _gameTiming.CurTime;

        if (component.EventSchedule.Count > 0 && curTime >= component.NextEventTick)
        {
            // Pop the event.
            var curEvent = component.EventSchedule[0];
            component.EventSchedule.RemoveAt(0);

            // Add the next event's offset to the ticker.
            if (component.EventSchedule.Count > 0)
                component.NextEventTick = curTime + component.EventSchedule[0].timeOffset;

            _sawmill.Info($"Running event: {curEvent}");

            switch (curEvent.eventId)
            {
                case ShipwreckedEventId.AnnounceTransit:
                    {
                        // We have to wait for the dungeon atlases to be ready, so do this here.
                        //DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-in-transit"),
                        //    new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_shuttle_in_transit.ogg"),
                        //    component);
                        break;
                    }
                case ShipwreckedEventId.EncounterTurbulence:
                    {
                        //DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-turbulence-nebula"),
                        //    new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_shuttle_turbulence_nebula.ogg"),
                        //    component);
                        break;
                    }
                case ShipwreckedEventId.MidflightDamage:
                    {
                        //DamageShuttleMidflight(component);
                        break;
                    }
                case ShipwreckedEventId.Alert:
                    {
                        //new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_report_alert.ogg"),
                        //    component);
                        break;
                    }
                case ShipwreckedEventId.DecoupleEngine:
                    {
                        //DecoupleShuttleEngine(component);
                        //new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_report_decouple_engine.ogg"),
                        //    component);
                        break;
                    }
                case ShipwreckedEventId.SendDistressSignal:
                    {
                        //DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-distress-signal"),
                        //    new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_shuttle_distress_signal.ogg"),
                        //    component);
                        break;
                    }
                case ShipwreckedEventId.InterstellarBody:
                    {
                        //new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_report_interstellar_body.ogg"),
                        //    component);
                        break;
                    }
                case ShipwreckedEventId.EnteringAtmosphere:
                    {
                        //new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_report_entering_atmosphere.ogg"),
                        //    component);
                        break;
                    }
                case ShipwreckedEventId.Crash:
                    {
                        //CrashShuttle(uid, component);
                        break;
                    }
                case ShipwreckedEventId.AfterCrash:
                    {
                        //DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-crashed"),
                        //    new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_shuttle_crashed.ogg"),
                        //    component);
                        break;
                    }
                case ShipwreckedEventId.Launch:
                    {
                        if (component.Shuttle == null || component.SpaceMapId == null)
                            break;

                        var shuttle = component.Shuttle.Value;
                        var spaceMap = _mapManager.GetMapEntityId(component.SpaceMapId.Value);

                        var query = EntityQueryEnumerator<TransformComponent, ActorComponent>();
                        while (query.MoveNext(out var actorUid, out var xform, out _))
                        {
                            if (xform.GridUid == component.Shuttle)
                                continue;

                            _popupSystem.PopupEntity(Loc.GetString("shipwrecked-shuttle-popup-left-behind"),
                                actorUid, actorUid, PopupType.Large);
                        }

                        //new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_launch.ogg"),
                        //component);

                        _shuttleSystem.FTLTravel(shuttle,
                            Comp<ShuttleComponent>(shuttle),
                            new EntityCoordinates(spaceMap, 0, 0),
                            hyperspaceTime: 120f);
                        break;
                    }
            }
        }
    }

    protected override void Started(EntityUid uid, ShipwreckedRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        if (!TryGetRandomStation(out var chosenStation, HasComp<StationJobsComponent>))
            return;

        component.Shuttle = chosenStation;
        var shuttle = component.Shuttle.Value;

        // Do some quick math to figure out at which point the FTL should end.
        // Do this when the rule starts and not when it's added so the timing is correct.
        var flightTime = TimeSpan.Zero;
        foreach (var item in component.EventSchedule)
        {
            flightTime += item.timeOffset;

            if (item.eventId == ShipwreckedEventId.Crash)
                break;
        }

        // Tiny adjustment back in time so Crash runs just after FTL ends.
        flightTime -= TimeSpan.FromMilliseconds(10);

        //component.NextEventTick = _gameTiming.CurTime + component.EventSchedule[0].timeOffset;
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            ev.AddLine(Loc.GetString("shipwrecked-list-start"));

            //foreach (var (survivor, session) in shipwrecked.Survivors)
            //{
            //    if (IsDead(survivor))
            //    {
            //        ev.AddLine(Loc.GetString("shipwrecked-list-perished-name",
            //            ("name", MetaData(survivor).EntityName),
            //            ("user", session.Name)));
            //    }
            //    else if (shipwrecked.AllObjectivesComplete &&
            //        Transform(survivor).GridUid == shipwrecked.Shuttle)
            //    {
            //        ev.AddLine(Loc.GetString("shipwrecked-list-escaped-name",
            //            ("name", MetaData(survivor).EntityName),
            //            ("user", session.Name)));
            //    }
            //    else
            //    {
            //        ev.AddLine(Loc.GetString("shipwrecked-list-survived-name",
            //            ("name", MetaData(survivor).EntityName),
            //            ("user", session.Name)));
            //    }
            //}

            //ev.AddLine("");
            //ev.AddLine(Loc.GetString("shipwrecked-list-start-objectives"));

            //if (GetLaunchConditionConsole(shipwrecked))
            //    ev.AddLine(Loc.GetString("shipwrecked-list-objective-console-pass"));
            //else
            //    ev.AddLine(Loc.GetString("shipwrecked-list-objective-console-fail"));

            //if (GetLaunchConditionGenerator(shipwrecked))
            //    ev.AddLine(Loc.GetString("shipwrecked-list-objective-generator-pass"));
            //else
            //    ev.AddLine(Loc.GetString("shipwrecked-list-objective-generator-fail"));

            //if (GetLaunchConditionThrusters(shipwrecked, out var goodThrusters))
            //{
            //    ev.AddLine(Loc.GetString("shipwrecked-list-objective-thrusters-pass",
            //            ("totalThrusterCount", shipwrecked.OriginalThrusterCount)));
            //}
            //else if (goodThrusters == 0)
            //{
            //    ev.AddLine(Loc.GetString("shipwrecked-list-objective-thrusters-fail",
            //            ("totalThrusterCount", shipwrecked.OriginalThrusterCount)));
            //}
            //else
            //{
            //    ev.AddLine(Loc.GetString("shipwrecked-list-objective-thrusters-partial",
            //            ("goodThrusterCount", shipwrecked.OriginalThrusterCount),
            //            ("totalThrusterCount", shipwrecked.OriginalThrusterCount)));
            //}

            //if (shipwrecked.AllObjectivesComplete)
            //{
            //    ev.AddLine("");
            //    ev.AddLine(Loc.GetString("shipwrecked-list-all-objectives-complete"));
            //}
        }
    }
}
