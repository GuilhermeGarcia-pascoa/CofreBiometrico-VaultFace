using PapAtualizacaoBeleza;
using PapAtualizacaoBeleza.Components;

// Verificação de SO — DPAPI e SQL Server LocalDB são exclusivos do Windows
if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("[VaultFace] Este sistema requer Windows. A encriptação DPAPI não está disponível neste SO.");
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<BaseSql>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<EstadoApp>();
builder.Services.AddSingleton<TemaService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<RelatorioPdfService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ── Download PDF do relatório ─────────────────────────────────────────────────
app.MapGet("/api/relatorio-pdf", (string inicio, string fim, RelatorioPdfService pdf) =>
{
    // Parse robusto — aceita ISO 8601 completo (yyyy-MM-ddTHH:mm:ss)
    if (!DateTime.TryParse(inicio, out var dtI)) dtI = DateTime.Today.AddDays(-6);
    if (!DateTime.TryParse(fim, out var dtF)) dtF = DateTime.Now;

    byte[] bytes = pdf.GerarRelatorio(dtI, dtF);
    string nome = $"VaultFace_Relatorio_{dtI:yyyyMMdd}_{dtF:yyyyMMdd}.pdf";
    return Results.File(bytes, "application/pdf", nome);
});

app.Run();
