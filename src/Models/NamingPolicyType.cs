using System.Text.Json;
using System.Text.Json.Serialization;

namespace EventStorage.Models;

public enum NamingPolicyType
{
    PascalCase,
    CamelCase,
    SnakeCaseLower,
    SnakeCaseUpper,
    KebabCaseLower,
    KebabCaseUpper
}

public struct NamingPolicyTypeNames
{
    private const string PascalCase = nameof(NamingPolicyType.PascalCase);
    private const string CamelCase = nameof(NamingPolicyType.CamelCase);
    private const string SnakeCaseLower = nameof(NamingPolicyType.SnakeCaseLower);
    private const string SnakeCaseUpper = nameof(NamingPolicyType.SnakeCaseUpper);
    private const string KebabCaseLower = nameof(NamingPolicyType.KebabCaseLower);
    private const string KebabCaseUpper = nameof(NamingPolicyType.KebabCaseUpper);
    
    /// <summary>
    /// Create a JsonSerializerOptions to use on naming police for serializing and deserializing properties of Event 
    /// </summary>
    public static JsonSerializerOptions CreateJsonSerializer(string namingPolicyType)
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        switch (namingPolicyType)
        {
            case CamelCase:
                jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                break;
            case SnakeCaseLower:
                jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                break;
            case SnakeCaseUpper:
                jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper;
                break;
            case KebabCaseLower:
                jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower;
                break;
            case KebabCaseUpper:
                jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.KebabCaseUpper;
                break;
        }

        return jsonSerializerOptions;
    }
}