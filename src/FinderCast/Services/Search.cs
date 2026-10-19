using System.Globalization;
using System.Text;

namespace FinderCast.Services;

/// <summary>
/// Ordena los resultados para lo que se ha escrito. De más a menos relevante: nombre exacto,
/// empieza por, alguna palabra empieza por, iniciales ("vsc" → Visual Studio Code), contiene, y
/// las letras en orden aunque haya otras en medio y, por último, con errores de tecleo
/// ("discrod" → Discord, "fotoshop" → Photoshop). Los más abiertos suben un poco.
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
            .Select(item => (item, score: Math.Max(Score(q, item.SearchName), item.AltSearchName is { } alt ? Score(q, alt) : 0)))
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
            if (found < 0) return TypoScore(query, name, words);
            gaps += found - position;
            position = found + 1;
        }
        return Math.Max(1, 300 - gaps * 5);
    }

    /// <summary>
    /// Errores de tecleo: letras cambiadas, de más, de menos o dos seguidas al revés. Se compara
    /// lo escrito con el principio del nombre desde cada palabra ("chrme" encuentra "Google
    /// Chrome"). Se permite un error hasta 5 letras y dos a partir de 6; con menos de 3 letras,
    /// ninguno, porque casi todo coincidiría. Siempre puntúa por debajo de las demás coincidencias.
    /// </summary>
    private static int TypoScore(string query, string name, string[] words)
    {
        if (query.Length < 3) return 0;
        var allowed = query.Length <= 5 ? 1 : 2;

        var best = int.MaxValue;
        var start = 0;
        foreach (var word in words)
        {
            var at = name.IndexOf(word, start, StringComparison.Ordinal);
            if (at < 0) continue;
            start = at + word.Length;
            var distance = PrefixDistance(query, name.AsSpan(at), allowed);
            // Desempate: mejor si coincide desde la primera palabra.
            best = Math.Min(best, distance * 2 + (at > 0 ? 1 : 0));
        }

        if (best == int.MaxValue || best / 2 > allowed) return 0;
        return 200 - best * 30;
    }

    /// <summary>
    /// Menor distancia de Damerau-Levenshtein (con transposiciones de letras vecinas) entre
    /// <paramref name="query"/> y algún principio de <paramref name="text"/>. Corta en cuanto la
    /// distancia supera <paramref name="limit"/>: con eso basta para descartar.
    /// </summary>
    private static int PrefixDistance(string query, ReadOnlySpan<char> text, int limit)
    {
        var n = query.Length;
        var m = Math.Min(text.Length, n + limit);
        Span<int> previous2 = stackalloc int[m + 1];
        Span<int> previous = stackalloc int[m + 1];
        Span<int> current = stackalloc int[m + 1];
        for (var j = 0; j <= m; j++) previous[j] = j;

        for (var i = 1; i <= n; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= m; j++)
            {
                var cost = query[i - 1] == text[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && query[i - 1] == text[j - 2] && query[i - 2] == text[j - 1])
                    value = Math.Min(value, previous2[j - 2] + 1);
                current[j] = value;
                rowMin = Math.Min(rowMin, value);
            }
            if (rowMin > limit) return limit + 1;

            var spare = previous2;
            previous2 = previous;
            previous = current;
            current = spare;
        }

        // Al acabar lo escrito, el mejor principio del texto de cualquier largo.
        var best = int.MaxValue;
        for (var j = 0; j <= m; j++) best = Math.Min(best, previous[j]);
        return best;
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
