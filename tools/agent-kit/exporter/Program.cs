using Orbit.AgentKit.Exporter;
using Orbit.Application.Graphs;
using Orbit.Application.Persistence;
using Orbit.Presentation.Canvas;

var exportRules = args.Length == 2 && args[0] == "--connection-rules";
var exportPatch = args.Length == 2 && args[0] == "--patch-contract";
var validatePatch = args.Length == 2 && args[0] == "--validate-patch";
if (!exportRules && !exportPatch && !validatePatch && (args.Length != 1 || args[0].StartsWith("--", StringComparison.Ordinal)))
{
    Console.Error.WriteLine("Usage: Orbit.AgentKit.Exporter <catalog.json> or --connection-rules <rules.json> or --patch-contract <directory> or --validate-patch <patch.json>");
    return 2;
}

try
{
    if (validatePatch)
    {
        var serializer = new PatchDocumentSerializer();
        var document = serializer.Deserialize(File.ReadAllText(args[1]));
        using var session = serializer.Restore(document, BuiltInNodes.CreateEditorRegistry());
        using var navigation = new CanvasNavigationService(session, layouts: document.CanvasLayouts,
            activeGraphId: document.ActiveGraphId);
        Console.WriteLine("Native patch import accepted.");
        return 0;
    }

    if (exportPatch)
    {
        var directory = Path.GetFullPath(args[1]);
        var files = PatchContractExporter.Export();
        Directory.CreateDirectory(directory);
        foreach (var (name, content) in files)
            File.WriteAllText(Path.Combine(directory, name), content);
        return 0;
    }

    var json = exportRules ? ConnectionRulesExporter.Export() : CatalogExporter.Export(BuiltInNodes.CreateEditorRegistry());
    var path = Path.GetFullPath(args[^1]);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, json);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Agent Kit export failed: {ex.Message}");
    return 1;
}
