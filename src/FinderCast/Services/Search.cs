using System.Globalization;
using System.Text;

namespace FinderCast.Services;

/// <summary>
/// Ordena los resultados para lo que se ha escrito. De más a menos relevante: nombre exacto,
/// empieza por, alguna palabra empieza por, iniciales ("vsc" → Visual Studio Code), contiene, y
/// por último las letras en orden aunque haya otras en medio. Los más abiertos suben un poco.
/// </summary>
internal static class Search
{
    public const int MaxResults = 50;

    public static IReadOnlyList<LaunchItem> Rank(string query, IReadOnlyList<LaunchItem> items, IReadOnlyDictionary<string, int> launches)
    {
        var q = Normalize(query.Trim());
        if (q.Length == 0)
        {
            // Sin texto: los más usados primero, luego el resto en orden alfabético.
            return [.. items
                .Where(i => i.Kind == LaunchKind.Application)
                .OrderByDescending(i => launches.GetValueOrDefault(i.Key))
                .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(MaxResults)];
        }

        return [.. items
            .Select(item => (item, score: Score(q, item.SearchName)))
            .Where(r => r.score > 0)
            .Select(r => (r.item, score: r.score + Math.Min(launches.GetValueOrDefault(r.item.Key), 20) * 10))
            .OrderByDescending(r => r.score)
            .ThenBy(r => r.item.Name.Length)
            .Take(MaxResults)
            .Select(r => r.item)];
    }

    public static int Score(string query, string name)
    {
        if (name == query) return 1000;
        if (name.StartsWith(query, StringComparison.Ordinal)) return 900;

        var words = name.Split([' ', '-', '_', '.', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w.StartsWith(query, StringComparison.Ordinal))) return 700;

        var initials = string.Concat(words.Select(w => w[0]));
        if (initials.StartsWith(query, StringComparison.Ordinal)) return 600;

        var index = name.IndexOf(query, StringComparison.Ordinal);
        if (index >= 0) return 500 - Math.Min(index, 100);

        // Subsecuencia: cada letra en orden; menos huecos, más puntos.
        var gaps = 0;
        var position = 0;
        foreach (var c in query)
        {
            var found = name.IndexOf(c, position);
            if (found < 0) return 0;
            gaps += found - position;
            position = found + 1;
        }
        return Math.Max(1, 300 - gaps * 5);
    }

    /// <summary>Minúsculas y sin tildes: "Configuración" encuentra con "configuracion".</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
