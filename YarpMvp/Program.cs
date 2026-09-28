using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using YarpMvp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddOpenApiForYarp()
    .AddDocumentTransformer<AddConfiguredHeadersTransformer>();
builder.Logging.AddProvider(new PathConflictToExceptionLoggerProvider());

var app = builder.Build();

app.UseHttpsRedirection();

app.MapReverseProxy();
app.MapOpenApiForYarp();   // /openapi/{cluster}.json  (+ /openapi/all.json when merging)
app.MapScalarForYarp();
    
app.Start(); // Запускаем приложение, но не блокируем текущий поток

try
{
    // Проверка, что open-api файл генерится без ошибок, (обычно он генерится при первом запросе, что нам не подходит)
    var server = app.Services.GetRequiredService<IServer>();
    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
    var baseUrl = addresses!.First();
    var allJsonUrl = $"{baseUrl.TrimEnd('/')}/openapi/all.json";
    
    app.Logger.LogInformation("Проверка Merged-документа по адресу: {Url}", allJsonUrl);
    
    var client = new HttpClient();
    var response = await client.GetAsync(allJsonUrl);
    
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"Merged OpenAPI document failed: {response.StatusCode}");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[FATAL] OpenAPI aggregation failed: {ex.Message}");
    Environment.FailFast("Path conflict detected in OpenAPI aggregation.", ex);
}

app.WaitForShutdown(); // Заменяет блокирующую функцию app.Run()