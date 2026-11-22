using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace YegozaWire.Server;

// SPT 4.1: AbstractModMetadata заменён интерфейсом IModMetadata (без override),
// IsBundleMod переименован в HasPrepatcher.
public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.yegoza.wire";
    public string Name { get; init; } = "Yegoza Wire - Placeable Barbed Wire";
    public string Author { get; init; } = "Leopard_2K"; // TODO: replace with your Forge username
    public List<string>? Contributors { get; init; }
    public Version Version { get; init; } = new("1.0.0");
    public Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? LoadBefore { get; init; }
    public List<string>? LoadAfter { get; init; }
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public bool HasPrepatcher { get; init; } = false;
    public string License { get; init; } = "MIT";
}
