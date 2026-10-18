using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WebApi.Swagger;

public sealed class AuthorizationDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        foreach (var description in context.ApiDescriptions)
        {
            var metadata = description.ActionDescriptor.EndpointMetadata;
            if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any()) continue;
            var path = "/" + description.RelativePath?.Split('?')[0];
            if (!document.Paths.TryGetValue(path, out var item) || description.HttpMethod == null) continue;
            var method = new HttpMethod(description.HttpMethod);
            if (item.Operations == null || !item.Operations.TryGetValue(method, out var operation)) continue;
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            }];
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Missing, invalid, expired or stale Bearer token." });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Insufficient role or resource permission." });
        }
    }
}
