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
    public const string PascalCase = nameof(NamingPolicyType.PascalCase);
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
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = GetEventNamingPolicy(namingPolicyType)
        };

        return jsonSerializerOptions;
    }
    
    /// <summary>
    /// Create a JsonSerializerOptions to use on naming police for serializing and deserializing properties of Event 
    /// </summary>
    public static JsonNamingPolicy GetEventNamingPolicy(string namingPolicyType)
    {
        switch (namingPolicyType)
        {
            case CamelCase:
                return JsonNamingPolicy.CamelCase;
            case SnakeCaseLower:
                return JsonNamingPolicy.SnakeCaseLower;
            case SnakeCaseUpper:
                return JsonNamingPolicy.SnakeCaseUpper;
            case KebabCaseLower:
                return JsonNamingPolicy.KebabCaseLower;
            case KebabCaseUpper:
                return JsonNamingPolicy.KebabCaseUpper;
            default:
                return null;
        }
    }
}