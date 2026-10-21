namespace BingeX;
public static class StreamProviderScope
{
    public static IReadOnlyList<InstalledAddon> Select(IReadOnlyList<InstalledAddon> installed,MediaContext context,bool searchAll)
    {
        static bool Extension(InstalledAddon addon)=>addon.ManifestUrl.StartsWith("cloudstream://",StringComparison.OrdinalIgnoreCase);
        var extensionTitle=context.Id.StartsWith("cs:",StringComparison.Ordinal);
        return installed.Where(addon=>extensionTitle
            ? Extension(addon) ? addon.Id==context.AddonId : searchAll
            : !Extension(addon)).ToArray();
    }
}
