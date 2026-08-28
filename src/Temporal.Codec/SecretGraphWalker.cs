using System.Collections;
using System.Reflection;

namespace Kogoshvili.Temporal.Codec;

/// <summary>
/// Walks an object graph of workflow/activity arguments, applying an action to
/// every <see cref="ISecret"/> it finds. Used by
/// <see cref="SecretEncryptionInterceptor"/> to encrypt secrets on the way out
/// and decrypt them on the way in.
/// </summary>
internal static class SecretGraphWalker
{
    public static async Task WalkAsync(IEnumerable<object?> values, Func<ISecret, Task> action)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var value in values)
        {
            await WalkValueAsync(value, action, visited).ConfigureAwait(false);
        }
    }

    private static async Task WalkValueAsync(
        object? value,
        Func<ISecret, Task> action,
        HashSet<object> visited)
    {
        if (value is null)
        {
            return;
        }

        if (value is ISecret secret)
        {
            await action(secret).ConfigureAwait(false);
            return;
        }

        if (value is string)
        {
            return;
        }

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is DateTime or DateTimeOffset or Guid or decimal)
        {
            return;
        }

        if (!visited.Add(value))
        {
            return;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                await WalkValueAsync(entry.Value, action, visited).ConfigureAwait(false);
            }

            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var element in enumerable)
            {
                await WalkValueAsync(element, action, visited).ConfigureAwait(false);
            }

            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || !property.CanRead)
            {
                continue;
            }

            object? propertyValue;
            try
            {
                propertyValue = property.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            await WalkValueAsync(propertyValue, action, visited).ConfigureAwait(false);
        }
    }
}
