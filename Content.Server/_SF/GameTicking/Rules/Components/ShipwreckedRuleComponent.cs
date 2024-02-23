using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Robust.Shared.Utility;
using Robust.Shared.Map.Components;
using Content.Server.Shipwrecked;
using Content.Shared.Procedural;
using Content.Shared.Roles;
using Content.Server.Maps;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(ShipwreckedRuleSystem))]
public sealed partial class ShipwreckedRuleComponent : Component
{
    #region Config
    // TODO Replace with GameRuleComponent.minPlayers
    /// <summary>
    /// The minimum needed amount of players
    /// </summary>
    [DataField]
    public int MinPlayers = 1;

    /// <summary>
    /// The shuttle that the game will start on.
    /// </summary>
    [ViewVariables]
    [DataField("supportedMaps", customTypeSerializer: typeof(PrototypeIdSerializer<GameMapPoolPrototype>))]
    public string? MapPool;

    /// <summary>
    /// The schedule of events to occur.
    /// </summary>
    [ViewVariables]
    [DataField("eventSchedule")]
    public List<(TimeSpan timeOffset, ShipwreckedEventId eventId)> EventSchedule = new();

    /// <summary>
    /// The destinations for the shipwreck.
    /// </summary>
    [ViewVariables]
    [DataField("destinations", required: true, customTypeSerializer: typeof(PrototypeIdListSerializer<ShipwreckDestinationPrototype>))]
    public List<string> ShipwreckDestinationPrototypes = default!;

    #endregion

    #region Live Data

    /// <summary>
    /// A list of all survivors and their player sessions.
    /// </summary>
    //[ViewVariables]
    //public List<(EntityUid entity, IPlayerSession session)> Survivors = new();

    /// <summary>
    /// Where the game starts and ends.
    /// </summary>
    [ViewVariables]
    public MapId? SpaceMapId;

    /// <summary>
    /// The shuttle's grid entity.
    /// </summary>
    [ViewVariables]
    public EntityUid? Shuttle;

    /// <summary>
    /// The chosen destination for the shipwreck.
    /// </summary>
    [ViewVariables]
    public ShipwreckDestinationPrototype? Destination;

    /// <summary>
    /// The map of the shipwreck destination.
    /// </summary>
    [ViewVariables]
    public MapId? PlanetMapId;

    /// <summary>
    /// The grid entity of the shipwreck destination.
    /// </summary>
    [ViewVariables]
    public EntityUid? PlanetMap;

    /// <summary>
    /// The MapGrid component of the PlanetMap entity.
    /// </summary>
    [ViewVariables]
    public MapGridComponent? PlanetGrid;

    /// <summary>
    /// A dictionary of vital shuttle pieces and their eventual destinations once the shuttle decouples the engine.
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, (EntityCoordinates destination, Dungeon? structure)> VitalPieces = new();

    /// <summary>
    /// Keeps track of the internal event scheduler.
    /// </summary>
    [ViewVariables]
    [DataField("nextEventTick", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextEventTick;

    /// <summary>
    /// The planetary structures.
    /// </summary>
    [ViewVariables]
    public List<Dungeon> Structures = new();

    /// <summary>
    /// If true, the game has been won.
    /// </summary>
    [ViewVariables]
    public bool AllObjectivesComplete;

    #endregion

}

public enum ShipwreckedEventId : int
{
    AnnounceTransit,
    EncounterTurbulence,
    MidflightDamage,
    Alert,
    DecoupleEngine,
    SendDistressSignal,
    InterstellarBody,
    EnteringAtmosphere,
    Crash,
    AfterCrash,

    // The win event
    Launch,
}

