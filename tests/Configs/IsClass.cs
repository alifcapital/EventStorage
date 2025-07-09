using System.Reflection;
using NUnit.Framework.Constraints;

namespace EventStorage.Tests.Configs;

public abstract class IsClass : Is
{
    /// <summary>
    /// Compares the equivalency of two objects, including reference properties. It compares the values of only public properties.
    /// </summary>
    /// <typeparam name="T">Class type to compare</typeparam>
    /// <param name="expectedClass">Expected object instance</param>
    /// <param name="excludedPropertyNames">Property names to exclude from comparison</param>
    /// <returns>Configured EqualConstraint for deep property-based equality checking</returns>
    public static EqualConstraint EquivalentTo<T>(T expectedClass, params string[] excludedPropertyNames) where T : class
    {
        var equalConstraint = new EqualConstraint(expectedClass);
        return equalConstraint.Using<object>((actual, expected) =>
        {
            var propertiesFromExpectedType = GetPublicPropertiesOfTypeExcludingHiddenProperties(expected.GetType());
            var propertiesFromActualType = GetPublicPropertiesOfTypeExcludingHiddenProperties(actual.GetType());

            foreach (var expectedProperty in propertiesFromExpectedType)
            {
                if (excludedPropertyNames.Contains(expectedProperty.Name))
                    continue;

                var propertyFromActualType =
                    propertiesFromActualType.FirstOrDefault(ap => ap.Name == expectedProperty.Name);
                if (propertyFromActualType == null)
                {
                    Assert.Fail(
                        $"The expected property '{expectedProperty.Name}' is missing in '{actual.GetType().Name}'.");
                }

                var expectedValue = expectedProperty.GetValue(expected);
                var actualValue = propertyFromActualType.GetValue(actual);

                ComparePropertyValues(expectedProperty, expectedValue, actualValue);
            }

            return true;
        });
    }

    #region Helper Methods

    private static readonly BindingFlags BindingFlags = BindingFlags.Instance | BindingFlags.Public;

    /// <summary>
    /// Retrieves public instance properties of a type, excluding properties hidden by inheritance
    /// </summary>
    /// <param name="classType">Type to inspect</param>
    /// <returns>Array of filtered PropertyInfo entries</returns>
    private static PropertyInfo[] GetPublicPropertiesOfTypeExcludingHiddenProperties(Type classType)
    {
        var properties = classType.GetProperties(BindingFlags)
            .Where(p => p.GetIndexParameters().Length == 0)
            .GroupBy(p => p.Name)
            .Select(g => g.First())
            .ToArray();

        return properties;
    }

    /// <summary>
    /// Recursively compares all public properties between two class instances
    /// </summary>
    private static void AreClassesEqual(object expected, object actual)
    {
        var properties = GetPublicPropertiesOfTypeExcludingHiddenProperties(expected.GetType());
        foreach (var property in properties)
        {
            var actualValue = property.GetValue(actual);
            var expectedValue = property.GetValue(expected);
            ComparePropertyValues(property, expectedValue, actualValue);
        }
    }

    /// <summary>
    /// Compares individual property values with type checking and null validation
    /// </summary>
    /// <param name="property">Property metadata being compared</param>
    /// <param name="expectedValue">Value from expected object</param>
    /// <param name="actualValue">Value from actual object</param>
    private static void ComparePropertyValues(
        PropertyInfo property,
        object expectedValue,
        object actualValue
    )
    {
        if (expectedValue == null && actualValue == null)
            return;

        if (expectedValue == null || actualValue == null)
            Assert.Fail($"Property {property.Name} is null in one of the objects but not the other");

        var expectedType = GetOriginalType(expectedValue);
        var actualType = GetOriginalType(actualValue);
        if (expectedType != actualType)
        {
            Assert.Fail(
                $"The navigation '{property.Name}' property's type does not match. The expected type is  '{expectedType.Name}', but was '{actualType.Name}'.");
        }

        if (IsComplexType(expectedType))
        {
            AreClassesEqual(expectedValue, actualValue);
        }
        else
        {
            Assert.That(actualValue, Is.EqualTo(expectedValue), $"Property {property.Name} value mismatch");
        }
    }

    /// <summary>
    /// Determines if a type requires deep comparison
    /// </summary>
    /// <param name="type">Type to evaluate</param>
    /// <returns>
    /// True for class types excluding strings, false for value types and strings
    /// </returns>
    private static bool IsComplexType(Type type)
    {
        return type.IsClass && type != typeof(string);
    }

    /// <summary>
    /// Gets the original (non-proxy) type of the given object. 
    /// When lazy loading is enabled, the object's type may be a proxy.
    /// </summary>
    /// <param name="value">The object to get the type from.</param>
    /// <returns>The original (non-proxy) type of the object.</returns>
    private static Type GetOriginalType(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
    
        var type = value.GetType();
        return IsProxyType(type) ? type.BaseType! : type;
    }

    /// <summary>
    /// Determines if the given type is a proxy type (has a base type and suspicious namespace or name).
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a proxy; otherwise, false.</returns>
    private static bool IsProxyType(Type type)
    {
        const string proxyNamespaceSuffix = "Proxies";
        const string proxyTypeSuffix = "Proxy";

        return type.BaseType is not null &&
               (type.Namespace?.EndsWith(proxyNamespaceSuffix, StringComparison.Ordinal) == true ||
                type.Name.EndsWith(proxyTypeSuffix, StringComparison.Ordinal));
    }

    #endregion
}