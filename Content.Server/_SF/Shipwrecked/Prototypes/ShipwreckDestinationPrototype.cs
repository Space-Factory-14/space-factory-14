using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;
using Content.Server.Atmos;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Procedural;

namespace Content.Server.Shipwrecked
{
    [Prototype("shipwreckDestination")]
    public sealed class ShipwreckDestinationPrototype : IPrototype
    {
        [ViewVariables]
        [IdDataField]
        public string ID { get; } = default!;

        [ViewVariables]
        [DataField("biome", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<BiomeTemplatePrototype>))]
        public readonly string BiomePrototype = default!;

        [ViewVariables]
        [DataField("gravity")]
        public readonly bool Gravity = true;

        [ViewVariables]
        [DataField("atmosphere")]
        public readonly GasMixture? Atmosphere = null;
    }
}
