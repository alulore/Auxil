using System.Text.Json.Nodes;

namespace Auxil.Extensions;

/// <summary>Provides JSON node access helpers.</summary>
[PublicAPI]
public static class JsonExtensions
{
    /// <summary>Reads a value from a JSON node by dot-separated property path.</summary>
    /// <typeparam name="T">The target value type.</typeparam>
    /// <param name="node">The JSON node, or <c>null</c>.</param>
    /// <param name="path">The dot-separated property path; an empty path reads the node itself.</param>
    /// <param name="defaultValue">The value returned when the path is missing or cannot be converted.</param>
    /// <returns>The value at the path, or the default.</returns>
    public static T? Get<T>(this JsonNode? node, string path, T? defaultValue = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        JsonNode? target = node;

        if (path.Length > 0)
        {
            foreach (string segment in path.Split('.'))
            {
                if (target is not JsonObject jsonObject)
                {
                    return defaultValue;
                }

                if (!jsonObject.TryGetPropertyValue(segment, out JsonNode? next) || next is null)
                {
                    return defaultValue;
                }

                target = next;
            }
        }

        return TryGetValue(target, out T? value) ? value : defaultValue;
    }

    /// <summary>Reads a node as <typeparamref name="T"/>; a <see cref="JsonValue"/> backed by a null string reports success with null text at runtime despite the NotNullWhen annotation on <c>TryGetValue&lt;string&gt;</c>, so null text is handled explicitly.</summary>
    private static bool TryGetValue<T>(JsonNode? node, out T? value)
    {
        value = default;

        if (node is not JsonValue jsonValue)
        {
            return false;
        }

        if (jsonValue.TryGetValue(out T? direct))
        {
            value = direct;
            return true;
        }

        if (!jsonValue.TryGetValue(out string? text) || text is null)
        {
            return false;
        }

        if (typeof(T) == typeof(bool) && bool.TryParse(text, out bool parsedBool))
        {
            value = (T)(object)parsedBool;
            return true;
        }

        if (typeof(T) == typeof(int) && int.TryParse(text, out int parsedInt))
        {
            value = (T)(object)parsedInt;
            return true;
        }

        if (typeof(T) == typeof(double) && double.TryParse(text, out double parsedDouble))
        {
            value = (T)(object)parsedDouble;
            return true;
        }

        return false;
    }
}
