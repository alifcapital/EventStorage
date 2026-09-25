using System.Reflection;

namespace EventStorage.Tests.Infrastructure.Extensions;

public static class ObjectExtensions
{
    /// <summary>
    /// Sets a value of the property even it has a non-public setter.
    /// </summary>
    /// <param name="instance">The object to set the property of.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The value to set.</param>
    public static void SetPropertyValue(this object instance, string propertyName, object value)
    {
        var property = instance.GetType().GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var setter = property?.GetSetMethod(nonPublic: true);
        if (setter is null)
            throw new InvalidOperationException(
                $"Setter for {propertyName} not found in {instance.GetType().Name}");

        setter.Invoke(instance, [value]);
    }
}
