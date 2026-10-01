using Cemetery.Api;

var app = CemeteryHost.Create(args);
if (args.Contains("--export-openapi"))
{
    await OpenApiExporter.ExportAsync(app).ConfigureAwait(false);
    return;
}

if (!app.Environment.IsEnvironment("Testing"))
    await app.InitializeDatabaseAsync().ConfigureAwait(false);

app.Run();

public partial class Program;
