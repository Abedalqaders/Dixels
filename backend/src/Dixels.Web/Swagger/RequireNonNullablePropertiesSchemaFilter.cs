using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Dixels.Swagger;

/// <summary>
/// Marks every non-nullable value-type property (int, Guid, bool, enums, DateTime) as
/// <c>required</c> in the OpenAPI schema. Together with
/// <c>NonNullableReferenceTypesAsRequired()</c> (which does the same for non-nullable
/// reference types) this makes the generated TypeScript types say exactly what the DTOs
/// say: a property that is always present is not optional. Without it, Swashbuckle marks
/// nothing required and every generated field becomes <c>?:</c>.
/// </summary>
public class RequireNonNullablePropertiesSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete || concrete.Properties is null || context.Type is null)
        {
            return;
        }

        foreach (var property in context.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var type = property.PropertyType;
            if (!type.IsValueType || Nullable.GetUnderlyingType(type) is not null)
            {
                continue;
            }

            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            if (concrete.Properties.ContainsKey(name))
            {
                concrete.Required ??= new HashSet<string>();
                concrete.Required.Add(name);
            }
        }
    }
}
