using System.Collections.Generic;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProgrammedTaskData))]
public partial class ProgrammedTaskDataContext : JsonSerializerContext
{
}