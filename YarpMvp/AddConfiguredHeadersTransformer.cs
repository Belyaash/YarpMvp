using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace YarpMvp;

// Добавляет информацию о Headers при создании OpenApi файла
public sealed class AddConfiguredHeadersTransformer : MMLib.OpenApiForYarp.Abstractions.IOpenApiDocumentTransformer
{
    private readonly IConfiguration _configuration;

    public AddConfiguredHeadersTransformer(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task TransformAsync(OpenApiDocument document, 
        MMLib.OpenApiForYarp.Abstractions.OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var routes = _configuration.GetSection("ReverseProxy:Routes").GetChildren();

        var routeForCluster = routes.FirstOrDefault(r =>
            r.GetValue<string>("ClusterId") == context.ClusterName);

        if (routeForCluster is null)
            return Task.CompletedTask; // Нет маршрута — нет заголовков

        var headersSection = routeForCluster.GetSection("Match:Headers");
        var headerEntries = headersSection.GetChildren();

        if (!headerEntries.Any())
            return Task.CompletedTask;

        foreach (var path in document.Paths.Values)
        {
            foreach (var operation in path.Operations.Values)
            {
                operation.Parameters ??= new List<IOpenApiParameter>();

                foreach (var headerEntry in headerEntries)
                {
                    var headerName = headerEntry.GetValue<string>("Name");
                    var values = headerEntry.GetSection("Values").Get<string[]>();

                    if (string.IsNullOrEmpty(headerName) || values is null || !values.Any())
                        continue;

                    if (operation.Parameters.Any(p => p.Name == headerName && p.In == ParameterLocation.Header))
                        continue;

                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = headerName,
                        In = ParameterLocation.Header,
                        Required = true,
                        Description = $"Service identifier header. Expected values: {string.Join(", ", values)}",
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.String,
                            Enum = values.Select(v => JsonValue.Create(v)).Cast<JsonNode>().ToList()
                        }
                    });
                }
            }
        }

        return Task.CompletedTask;
    }
}