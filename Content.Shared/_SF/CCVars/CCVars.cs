using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.SF14.CCVar;

[CVarDefs]
public sealed class SF14CVars
{
    /// <summary>
    /// Whether the spawn should be on a planet map.
    /// </summary>
    public static readonly CVarDef<bool> ArrivalsPlanet =
        CVarDef.Create("shuttle.spawn_planet", true, CVar.SERVERONLY);
}
