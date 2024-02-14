using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Server.Access.Systems;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Buckle.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Chemistry.Components;
using Content.Server.Construction.Components;
using Content.Server.Destructible;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Ghost.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Humanoid;
using Content.Server.Maps;
using Content.Server.Mind;
using Content.Server.NPC.Systems;
using Content.Server.Paper;
using Content.Server.Parallax;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Preferences.Managers;
using Content.Server.Procedural;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Shipwrecked;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Server.Storage.Components;
using Content.Server.Warps;
using Content.Shared.Access.Components;
using Content.Shared.Atmos;
using Content.Shared.Buckle.Components;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Lock;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Parallax;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Procedural;
using Content.Shared.Random.Helpers;
using Content.Shared.Random;
using Content.Shared.Roles;
using Content.Shared.Shuttles.Components;
using Content.Shared.Storage;
using Content.Shared.Zombies;
using Robust.Server.Audio;
using Content.Shared.Ghost;

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

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("shipwrecked");

        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<FTLStartedEvent>(OnFTLStarted);

        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<LoadingMapsEvent>(OnLoadingMaps);

        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent ev)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (ev.Entity != shipwrecked.Shuttle)
                continue;

            if (shipwrecked.AllObjectivesComplete)
                _roundEndSystem.EndRound();
        }
    }

    private void OnFTLStarted(ref FTLStartedEvent ev)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (ev.Entity != shipwrecked.Shuttle)
                continue;

            if (!shipwrecked.AllObjectivesComplete)
                continue;

            // You win!
            _roundEndSystem.EndRound();

        }
    }

    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            if (ev.Players.Length == 0)
            {
                _chatManager.DispatchServerAnnouncement(Loc.GetString("shipwrecked-no-one-ready"));
                ev.Cancel();
            }
        }
    }

    private void OnLoadingMaps(LoadingMapsEvent ev)
    {
        var query = EntityQueryEnumerator<ShipwreckedRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var shipwrecked, out var gameRule))
        {
            if (!GameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            // This gamemode does not need a station. Revolutionary.
            ev.Maps.Clear();

            // NOTE: If we could disable the cargo shuttle, emergency shuttle,
            // arrivals station, and centcomm station from loading that would be perfect.
        }
    }

    private void SpawnPlanet(EntityUid uid, ShipwreckedRuleComponent component)
    {
        // Most of this code below comes from a protected function in SpawnSalvageMissionJob
        // which really should be made more generic and public...
        //
        // Some of it has been modified to suit my needs.

        var planetMapId = _mapManager.CreateMap();
        var mapUid = _mapManager.GetMapEntityId(planetMapId);
        _mapManager.AddUninitializedMap(planetMapId);

        var ftl = _shuttleSystem.AddFTLDestination(mapUid, true);
        ftl.Whitelist = new();

        var planetGrid = EnsureComp<MapGridComponent>(mapUid);

        var destination = component.Destination;
        if (destination == null)
            throw new ArgumentException("There is no destination for Shipwrecked.");

        var biome = _entManager.AddComponent<BiomeComponent>(mapUid);
        var biomeSystem = _entManager.System<BiomeSystem>();
        biomeSystem.SetTemplate(mapUid, biome, _prototypeManager.Index<BiomeTemplatePrototype>(destination.BiomePrototype));
        biomeSystem.SetSeed(mapUid, biome, _random.Next());
        _entManager.Dirty(mapUid, biome);

        // Gravity
        if (destination.Gravity)
        {
            var gravity = EnsureComp<GravityComponent>(mapUid);
            gravity.Enabled = true;
            Dirty(gravity);
        }

        // Atmos
        var atmos = EnsureComp<MapAtmosphereComponent>(mapUid);

        if (destination.Atmosphere != null)
        {
            _atmosphereSystem.SetMapAtmosphere(mapUid, false, destination.Atmosphere, atmos);
        }
        else
        {
            // Some very generic default breathable atmosphere.
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;

            var mixture = new GasMixture(2500)
            {
                Temperature = 293.15f,
                Moles = moles,
            };

            _atmosphereSystem.SetMapAtmosphere(mapUid, false, mixture, atmos);
        }

        _mapManager.DoMapInitialize(planetMapId);
        _mapManager.SetMapPaused(planetMapId, true);

        component.PlanetMapId = planetMapId;
        component.PlanetMap = mapUid;
        component.PlanetGrid = planetGrid;
    }

    private bool SpawnMap(EntityUid uid, ShipwreckedRuleComponent component)
    {
        var mapId = _mapManager.CreateMap();
        var mapUid = _mapManager.GetMapEntityId(mapId);
        _mapManager.AddUninitializedMap(mapId);

        if (!_mapLoaderSystem.TryLoad(mapId, "Maps/_SF/planet.yml", out var roots))
        {
            _sawmill.Error($"Unable to load map");
            return false;
        }

        var shuttleGrid = _mapManager.GetGrid(roots[0]);
        EnsureComp<PreventPilotComponent>(roots[0]);
        component.Shuttle = roots[0];

        return true;
    }

    private void DamageShuttleMidflight(ShipwreckedRuleComponent component)
    {
        if (component.Shuttle == null)
            return;

        // Damage vital pieces of the shuttle.
        //
        // * Console can go crunch when the ship smashes.
        // * Thrusters can be blown out safely.
        // * Generator will need to be replaced anyway as it's dying.
        //

        // Blow the thrusters.
        var query = EntityQueryEnumerator<ThrusterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var thruster, out var xform))
        {
            if (xform.GridUid != component.Shuttle)
                continue;

            if (thruster.Type == ThrusterType.Angular)
                // Don't blow up the gyroscope.
                // It's the thruster that's inside.
                continue;

            // If these get destroyed at any point during the round, escape becomes impossible.
            // So make them indestructible.
            RemComp<DestructibleComponent>(uid);

            // Disallow them to be broken down, too.
            RemComp<ConstructionComponent>(uid);

            // These should be weak enough to rough up the walls but not destroy them.
            _explosionSystem.QueueExplosion(uid, "DemolitionCharge",
                2f,
                2f,
                2f,
                // Try not to break any tiles.
                tileBreakScale: 0,
                maxTileBreak: 0,
                canCreateVacuum: false,
                addLog: false);
        }

        // Ensure that all generators on the shuttle will decay.
        // Get the total power supply so we know how much to damage the generators by.
        var totalPowerSupply = 0f;
        var generatorQuery = EntityQueryEnumerator<PowerSupplierComponent, TransformComponent>();
        while (generatorQuery.MoveNext(out _, out var powerSupplier, out var xform))
        {
            if (xform.GridUid != component.Shuttle)
                continue;

            totalPowerSupply += powerSupplier.MaxSupply;
        }

        generatorQuery = EntityQueryEnumerator<PowerSupplierComponent, TransformComponent>();
        while (generatorQuery.MoveNext(out var uid, out var powerSupplier, out var xform))
        {
            if (xform.GridUid != component.Shuttle)
                continue;
        }
    }

    private void DecoupleShuttleEngine(ShipwreckedRuleComponent component)
    {
        if (component.Shuttle == null)
            return;

        // Stop thrusters from burning anyone when re-anchored.
        _thrusterSystem.DisableLinearThrusters(Comp<ShuttleComponent>(component.Shuttle.Value));

        // Move the vital pieces of the shuttle down to the planet.
        foreach (var (uid, (destination, _)) in component.VitalPieces)
        {
            var warpPoint = EnsureComp<WarpPointComponent>(uid);
            warpPoint.Location = Loc.GetString("shipwrecked-warp-point-vital-piece");

            _transformSystem.SetCoordinates(uid, destination);
        }

        if (component.Shuttle == null)
            return;

        // Spawn scrap in front of the shuttle's window.
        // It'll look cool.
        var shuttleXform = Transform(component.Shuttle.Value);
        var spot = shuttleXform.MapPosition.Offset(-3, 60);

        for (var i = 0; i < 9; ++i)
        {
            var scrap = Spawn("SheetSteel1", spot.Offset(_random.NextVector2(-4, 3)));
            Transform(scrap).LocalRotation = _random.NextAngle();
        }
    }

    private void CrashShuttle(EntityUid uid, ShipwreckedRuleComponent component)
    {
        if (component.Shuttle == null)
            return;

        if (!TryComp<MapGridComponent>(component.Shuttle, out var grid))
            return;

        // Slam the front window.
        var aabb = grid.LocalAABB;
        var topY = grid.LocalAABB.Top + 1;
        var bottomY = grid.LocalAABB.Bottom - 1;
        var centeredX = grid.LocalAABB.Width / 2 + aabb.Left;

        var xform = Transform(component.Shuttle.Value);
        var mapPos = xform.MapPosition;
        var smokeSpots = new List<MapCoordinates>();
        var front = mapPos.Offset(new Vector2(centeredX, topY));
        smokeSpots.Add(front);
        smokeSpots.Add(mapPos.Offset(new Vector2(centeredX, bottomY)));

        _explosionSystem.QueueExplosion(front, "Minibomb",
            200f,
            1f,
            100f,
            // Try not to break any tiles.
            tileBreakScale: 0,
            maxTileBreak: 0,
            canCreateVacuum: false,
            addLog: false);

        // Send up smoke and dust plumes.
        foreach (var spot in smokeSpots)
        {
            var smokeEnt = Spawn("Smoke", spot);
            var smoke = EnsureComp<SmokeComponent>(smokeEnt);
            smoke.SpreadAmount = 70;

            // Breathing smoke is not good for you.
            var toxin = new Solution("Toxin", FixedPoint2.New(2));
            //_smokeSystem.Start(smokeEnt, smoke, toxin, duration: 20f);
        }

        // Fry the console.
        var consoleQuery = EntityQueryEnumerator<TransformComponent, ShuttleConsoleComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out var consoleXform, out _))
        {
            if (consoleXform.GridUid != component.Shuttle)
                continue;

            var limit = _destructibleSystem.DestroyedAt(consoleUid);

            // Here at Nyanotrasen, we have damage variance, so...
            //var damageVariance = _configurationManager.GetCVar(CCVars.DamageVariance);
            limit *= 1f; //+ damageVariance;

            var smash = new DamageSpecifier();
            smash.DamageDict.Add("Structural", limit);
            _damageableSystem.TryChangeDamage(consoleUid, smash, ignoreResistances: true);

            // Break, because we're technically modifying the enumeration by destroying the console.
            break;
        }

        var crashSound = new SoundPathSpecifier("/Audio/Nyanotrasen/Effects/crash_impact_metal.ogg");
        _audioSystem.PlayPvs(crashSound, component.Shuttle.Value);
    }

    private void DispatchShuttleAnnouncement(string message, SoundSpecifier audio, ShipwreckedRuleComponent component)
    {
        var wrappedMessage = Loc.GetString("shipwrecked-shuttle-announcement",
            ("sender", "Hecate"),
            ("message", FormattedMessage.EscapeText(message)));

        var ghostQuery = GetEntityQuery<GhostComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();
        var filter = Filter.Empty();

        //foreach (var player in _playerManager.ServerSessions)
        //{
        //    if (player.AttachedEntity is not { Valid: true } playerEntity)
        //        continue;

        //    if (ghostQuery.HasComponent(playerEntity))
        //    {
        //        // Add ghosts.
        //        filter.AddPlayer(player);
        //        continue;
        //    }

        //    var xform = xformQuery.GetComponent(playerEntity);
        //    if (xform.GridUid != component.Shuttle)
        //        continue;

        //    // Add entities inside the shuttle.
        //    filter.AddPlayer(player);
        //}

        _chatManager.ChatMessageToManyFiltered(filter,
            ChatChannel.Radio,
            message,
            wrappedMessage,
            component.Shuttle.GetValueOrDefault(),
            false,
            true,
            Color.SeaGreen);

        var audioPath = _audioSystem.GetSound(audio);
        _audioSystem.PlayGlobal(audioPath, filter, true, AudioParams.Default.WithVolume(1f));
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
                        DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-in-transit"),
                            new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_shuttle_in_transit.ogg"),
                            component);
                        break;
                    }
                case ShipwreckedEventId.EncounterTurbulence:
                    {
                        DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-turbulence-nebula"),
                            new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_shuttle_turbulence_nebula.ogg"),
                            component);
                        break;
                    }
                case ShipwreckedEventId.MidflightDamage:
                    {
                        DamageShuttleMidflight(component);
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
                        DecoupleShuttleEngine(component);
                        //new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_report_decouple_engine.ogg"),
                        //    component);
                        break;
                    }
                case ShipwreckedEventId.SendDistressSignal:
                    {
                        DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-distress-signal"),
                            new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_shuttle_distress_signal.ogg"),
                            component);
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
                        CrashShuttle(uid, component);
                        break;
                    }
                case ShipwreckedEventId.AfterCrash:
                    {
                        DispatchShuttleAnnouncement(Loc.GetString("shipwrecked-hecate-shuttle-crashed"),
                            new SoundPathSpecifier("/Audio/Nyanotrasen/Dialogue/Hecate/shipwrecked_hecate_shuttle_crashed.ogg"),
                            component);
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

    protected override void Added(EntityUid uid, ShipwreckedRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        var destination = _random.Pick(component.ShipwreckDestinationPrototypes);

        component.Destination = _prototypeManager.Index<ShipwreckDestinationPrototype>(destination);

        SpawnMap(uid, component);
        SpawnPlanet(uid, component);

        if (component.Shuttle == null)
            throw new ArgumentException($"Shipwrecked failed to spawn a Shuttle.");

        // Currently, the AutoCallStartTime is part of the public API and not access restricted.
        // If this ever changes, I will send a patch upstream to allow it to be altered.
        _roundEndSystem.AutoCallStartTime = TimeSpan.MaxValue;
    }

    protected override void Started(EntityUid uid, ShipwreckedRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        if (component.Shuttle == null || component.PlanetMapId == null || component.PlanetMap == null)
            return;

        _mapManager.SetMapPaused(component.PlanetMapId.Value, false);

        var loadQuery = EntityQueryEnumerator<ApcPowerReceiverComponent, TransformComponent>();
        while (loadQuery.MoveNext(out _, out var apcPowerReceiver, out var xform))
        {
            if (xform.GridUid != component.Shuttle)
                continue;
        }

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

        component.NextEventTick = _gameTiming.CurTime + component.EventSchedule[0].timeOffset;

        _shuttleSystem.FTLTravel(shuttle,
            Comp<ShuttleComponent>(shuttle),
            Transform(component.PlanetMap.GetValueOrDefault()).Coordinates,
            // The travellers are already in FTL by the time the gamemode starts.
            startupTime: 0,
            hyperspaceTime: (float) flightTime.TotalSeconds);
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
