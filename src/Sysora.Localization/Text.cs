using System.Globalization;

namespace Sysora.Localization;

/// <summary>Plural categories a count can take, following the rules of the Unicode CLDR.</summary>
public enum PluralCategory
{
    /// <summary>The singular form ("1 alert"; also 21, 31... in Russian and Ukrainian).</summary>
    One,

    /// <summary>The form of 2 to 4 (and 22–24...) in Czech, Polish, Russian and Ukrainian.</summary>
    Few,

    /// <summary>Every other count: the plural form ("5 alerts").</summary>
    Other,
}

/// <summary>Builds sentences from translated templates.</summary>
public static class Text
{
    /// <summary>
    /// Separates the forms of a plural template in languages with more than one plural form: the "other" template of
    /// <see cref="Plural"/> then holds the form of <see cref="PluralCategory.Few"/>, this character, and the form of
    /// <see cref="PluralCategory.Other"/> ("{0} файла|{0} файлов").
    /// </summary>
    public const char PluralFormSeparator = '|';

    /// <summary>
    /// Fills a translated template ("{0} started using a lot of CPU") with values formatted for the regional format
    /// chosen in Windows.
    /// </summary>
    public static string Format(string template, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, template, args);

    /// <summary>
    /// Picks the singular or plural template for <paramref name="count"/>, following the rules of the interface language,
    /// and fills <c>{0}</c> with the count.
    /// </summary>
    /// <param name="count">The number of items.</param>
    /// <param name="one">Singular template, e.g. "{0} alert".</param>
    /// <param name="other">
    /// Plural template, e.g. "{0} alerts". In languages with two plural forms it holds both, separated by
    /// <see cref="PluralFormSeparator"/>.
    /// </param>
    /// <param name="args">Values for <c>{1}</c> and later placeholders.</param>
    public static string Plural(long count, string one, string other, params object?[] args)
    {
        var template = Template(count, one, other, AppLanguage.Current);
        if (args.Length == 0)
        {
            return string.Format(CultureInfo.CurrentCulture, template, count);
        }

        var all = new object?[args.Length + 1];
        all[0] = count;
        args.CopyTo(all, 1);
        return string.Format(CultureInfo.CurrentCulture, template, all);
    }

    /// <summary>The template of <paramref name="count"/> among the singular and plural forms, in <paramref name="language"/>.</summary>
    public static string Template(long count, string one, string other, CultureInfo language)
    {
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(other);
        var category = Category(count, language);
        if (category == PluralCategory.One)
        {
            return one;
        }

        var separator = other.IndexOf(PluralFormSeparator, StringComparison.Ordinal);
        if (separator < 0)
        {
            return other;
        }

        return category == PluralCategory.Few ? other[..separator] : other[(separator + 1)..];
    }

    /// <summary>Whether <paramref name="count"/> takes the singular in the interface language.</summary>
    public static bool IsSingular(long count) => IsSingular(count, AppLanguage.Current);

    /// <summary>Whether <paramref name="count"/> takes the singular in <paramref name="language"/>.</summary>
    public static bool IsSingular(long count, CultureInfo language) => Category(count, language) == PluralCategory.One;

    /// <summary>The plural category of a whole number in <paramref name="language"/> (CLDR rules for integers).</summary>
    public static PluralCategory Category(long count, CultureInfo language)
    {
        ArgumentNullException.ThrowIfNull(language);
        var n = count == long.MinValue ? long.MaxValue : Math.Abs(count);
        var lastDigit = n % 10;
        var lastTwoDigits = n % 100;
        switch (language.TwoLetterISOLanguageName)
        {
            // Singular for 0 and 1.
            case "fr" or "pt":
                return n is 0 or 1 ? PluralCategory.One : PluralCategory.Other;

            // No grammatical plural: the same text for every count.
            case "ja" or "ko" or "zh":
                return PluralCategory.Other;

            case "ru" or "uk":
                return lastDigit == 1 && lastTwoDigits != 11 ? PluralCategory.One
                    : lastDigit is >= 2 and <= 4 && lastTwoDigits is < 12 or > 14 ? PluralCategory.Few
                    : PluralCategory.Other;

            case "pl":
                return n == 1 ? PluralCategory.One
                    : lastDigit is >= 2 and <= 4 && lastTwoDigits is < 12 or > 14 ? PluralCategory.Few
                    : PluralCategory.Other;

            case "cs":
                return n == 1 ? PluralCategory.One : n is >= 2 and <= 4 ? PluralCategory.Few : PluralCategory.Other;

            default:
                return n == 1 ? PluralCategory.One : PluralCategory.Other;
        }
    }

    /// <summary>
    /// Puts a text written to stand alone ("Memory", "High confidence", "The processor was saturated") inside a
    /// sentence: its first letter in lowercase, by the rules of the interface language. Texts that start with an acronym
    /// ("CPU usage", "GPU") are kept as they are, and so is every text in German, where nouns keep their capital.
    /// </summary>
    public static string InSentence(string text) => InSentence(text, AppLanguage.Current);

    /// <inheritdoc cref="InSentence(string)"/>
    internal static string InSentence(string text, CultureInfo language)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(language);
        if (text.Length == 0
            || !char.IsUpper(text[0])
            || (text.Length > 1 && char.IsUpper(text[1]))
            || language.TwoLetterISOLanguageName == "de")
        {
            return text;
        }

        return char.ToLower(text[0], language) + text[1..];
    }

    /// <summary>Joins items as a readable list: "a, b and c" (", " and " and " are translated).</summary>
    public static string List(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var list = items.ToList();
        return list.Count switch
        {
            0 => string.Empty,
            1 => list[0],
            _ => string.Join(Strings.List_Separator, list.Take(list.Count - 1)) + Strings.List_LastSeparator + list[^1],
        };
    }
}
