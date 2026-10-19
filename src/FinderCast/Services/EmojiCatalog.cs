using System.Reflection;
using Windows.ApplicationModel.DataTransfer;

namespace FinderCast.Services;

/// <summary>
/// Emojis y símbolos que se buscan por su nombre tras ":" (":corazon", ":flecha", ":euro") y se
/// copian al portapapeles. Los nombres y etiquetas, en español e inglés, salen de emojibase-data
/// (licencia MIT); los símbolos que no son emojis (©, →, ≠…) se añaden a mano. La tabla va dentro
/// de la app (Assets/Emoji.tsv) y la genera scripts/generar-emojis.py.
/// </summary>
internal static class EmojiCatalog
{
    public const char Prefix = ':';

    private static readonly Lazy<IReadOnlyList<Entry>> Entries = new(Load);

    /// <summary>Lo escrito tras el prefijo, o null si el texto no empieza por él.</summary>
    public static string? Parse(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith(Prefix) ? trimmed[1..].Trim() : null;
    }

    /// <summary>
    /// Los que mejor coinciden por nombre o etiqueta, en los dos idiomas (con la misma búsqueda
    /// que las apps, errores de tecleo incluidos). Sin texto, los primeros de la tabla.
    /// </summary>
    public static IReadOnlyList<LaunchItem> Find(string query)
    {
        var q = Search.Normalize(query);
        var spanish = Strings.Current == Strings.Spanish;
        IEnumerable<Entry> found = q.Length == 0
            ? Entries.Value
            : Entries.Value
                .Select((entry, order) => (entry, order, score: entry.Score(q)))
                .Where(r => r.score > 0)
                .OrderByDescending(r => r.score)
                // A igual puntuación, el nombre más corto: ":corazon" da antes ❤️ «corazón rojo».
                .ThenBy(r => (spanish ? r.entry.Spanish : r.entry.English).Length)
                .ThenBy(r => r.order)
                .Select(r => r.entry);

        return [.. found.Take(Search.MaxResults).Select(e => new LaunchItem(spanish ? e.Spanish : e.English, "emoji:" + e.Character, LaunchKind.Emoji)
        {
            Glyph = e.Character,
        })];
    }

    /// <summary>Copia el carácter; se queda en el portapapeles aunque se cierre FinderCast.</summary>
    public static void Copy(string character)
    {
        var package = new DataPackage();
        package.SetText(character);
        try
        {
            Clipboard.SetContent(package);
            Clipboard.Flush();
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            // Otra app tiene el portapapeles abierto en este momento: no se puede hacer nada más.
        }
    }

    private static IReadOnlyList<Entry> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FinderCast.Assets.Emoji.tsv");
        if (stream is null) return [];
        using var reader = new StreamReader(stream);
        var entries = new List<Entry>(2048);
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split('\t');
            if (parts.Length >= 5) entries.Add(new Entry(parts[0], parts[1], parts[2], parts[3], parts[4]));
        }
        return entries;
    }

    private sealed class Entry(string character, string spanish, string spanishTags, string english, string englishTags)
    {
        private readonly string[] _names = [Search.Normalize(spanish), Search.Normalize(english)];

        private readonly string[] _tags = [.. $"{spanishTags} {englishTags}"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Search.Normalize)
            .Distinct()];

        public string Character { get; } = character;

        public string Spanish { get; } = Capitalize(spanish);

        public string English { get; } = Capitalize(english);

        /// <summary>Primera letra en mayúscula, como los nombres de las apps.</summary>
        private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0]) + text[1..];

        /// <summary>Por el nombre cuenta entero; por una etiqueta, algo menos.</summary>
        public int Score(string query)
        {
            var best = _names.Max(name => Search.Score(query, name));
            foreach (var tag in _tags) best = Math.Max(best, Search.Score(query, tag) - 150);
            return best;
        }
    }
}
