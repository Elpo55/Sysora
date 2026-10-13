using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Sysora.Core.Settings;
using Sysora.Localization;

namespace Sysora.Tests.Localization;

public sealed partial class LocalizationTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr");

    /// <summary>Every resource set in every language Sysora is translated into (all supported languages but English).</summary>
    public static TheoryData<string, string> Translations
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var language in AppLanguage.Supported.Where(l => l.Code != AppLanguage.Fallback))
            {
                data.Add(nameof(Strings), language.Code);
                data.Add(nameof(UiStrings), language.Code);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void EveryTextIsTranslated(string set, string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        var english = Read(Manager(set), CultureInfo.InvariantCulture);
        var translated = Read(Manager(set), culture);

        var missing = english.Keys.Where(k => !translated.ContainsKey(k) || string.IsNullOrWhiteSpace(translated[k])).ToArray();
        Assert.True(missing.Length == 0, $"{set}: no {language} text for {string.Join(", ", missing)}");
        Assert.Empty(translated.Keys.Except(english.Keys));
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void TranslationsUseOnlyThePlaceholdersTheCodeProvides(string set, string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        var english = Read(Manager(set), CultureInfo.InvariantCulture);
        var translated = Read(Manager(set), culture);
        foreach (var (key, text) in english)
        {
            var count = MaxPlaceholder(text) + 1;
            var args = Enumerable.Range(0, count).Select(i => (object)i).ToArray();

            // The code fills the English placeholders: a translation must format with the same arguments.
            _ = string.Format(CultureInfo.InvariantCulture, text, args);
            if (translated.TryGetValue(key, out var translation))
            {
                Assert.True(MaxPlaceholder(translation) < count, $"{set}.{key}: the {language} text uses a placeholder the code does not provide.");
                _ = string.Format(culture, translation, args);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void NoTextShowsItsResourceName(string set, string language)
    {
        foreach (var culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo(language) })
        {
            foreach (var (key, text) in Read(Manager(set), culture))
            {
                Assert.False(string.IsNullOrWhiteSpace(text), $"{set}.{key} is empty ({culture.Name}).");
                Assert.NotEqual(key, text);
                Assert.DoesNotContain("⟦", text, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void PluralTemplatesHoldTheFormsOfTheirLanguage(string set, string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        var english = Read(Manager(set), CultureInfo.InvariantCulture);
        var translated = Read(Manager(set), culture);
        // Romanian only switches to its third form at 20 ("20 de fișiere"), so 2 and 5 alone would not reveal it.
        var threeForms = new[] { 2L, 5L, 20L }.Select(n => Text.Category(n, culture)).Contains(PluralCategory.Few);
        foreach (var (key, text) in translated)
        {
            // The "other" template of a plural pair (key_One / key_Other) holds "few|many" in languages with three forms.
            var isPlural = key.EndsWith("_Other", StringComparison.Ordinal) && english.ContainsKey(key[..^"_Other".Length] + "_One");
            var forms = text.Split(Text.PluralFormSeparator).Length;
            Assert.True(forms == (isPlural && threeForms ? 2 : 1), $"{set}.{key} ({language}): {forms} form(s) in \"{text}\".");
        }
    }

    [Theory]
    [MemberData(nameof(Translations))]
    public void ComparisonExplanationsCanBeSaidOfToday(string set, string language)
    {
        // "Since yesterday" reuses the comparison explanations, replacing the second-period phrase with "today"
        // (or its capitalized form when the sentence opens with it).
        var culture = CultureInfo.GetCultureInfo(language);
        var english = Read(Manager(set), CultureInfo.InvariantCulture);
        var translated = Read(Manager(set), culture);
        var englishPhrase = Strings.ResourceManager.GetString(nameof(Strings.Since_SecondPeriodPhrase), CultureInfo.InvariantCulture)!;
        var phrase = Strings.ResourceManager.GetString(nameof(Strings.Since_SecondPeriodPhrase), culture)!;
        var opening = char.ToUpper(phrase[0], culture) + phrase[1..];
        foreach (var (key, text) in english.Where(e => e.Key != nameof(Strings.Since_SecondPeriodPhrase) && e.Value.Contains(englishPhrase, StringComparison.Ordinal)))
        {
            var translation = translated[key];
            Assert.True(
                translation.Contains(phrase, StringComparison.Ordinal) || translation.StartsWith(opening, StringComparison.Ordinal),
                $"{set}.{key} ({language}): \"{translation}\" does not contain \"{phrase}\".");
        }
    }

    [Fact]
    public void FrenchTextsAreLoadedFromTheSatelliteAssembly()
    {
        Assert.Equal("Not available", Strings.ResourceManager.GetString(nameof(Strings.Common_NotAvailable), CultureInfo.InvariantCulture));
        Assert.Equal("Non disponible", Strings.ResourceManager.GetString(nameof(Strings.Common_NotAvailable), French));
        Assert.Equal("Paramètres", UiStrings.ResourceManager.GetString(nameof(UiStrings.Settings_Settings), French));
    }

    [Fact]
    public void AMissingTranslationFallsBackToEnglish()
    {
        // A language Sysora is not translated into resolves every text to the English one.
        var basque = CultureInfo.GetCultureInfo("eu");
        Assert.Equal("Not available", Strings.ResourceManager.GetString(nameof(Strings.Common_NotAvailable), basque));
    }

    [Theory]
    [InlineData("", "fr-FR", "fr")]
    [InlineData("", "fr-CA", "fr")]
    [InlineData("", "eu-ES", "en")]
    [InlineData("", "de-AT", "de")]
    [InlineData("", "en-US", "en")]
    [InlineData("en", "fr-FR", "en")]
    [InlineData("fr", "en-US", "fr")]
    [InlineData("FR-be", "en-US", "fr")]
    [InlineData("xx", "fr-FR", "fr")]
    [InlineData(null, "eu-ES", "en")]
    public void TheLanguageFollowsThePreferenceThenWindowsThenEnglish(string? preference, string windows, string expected)
    {
        Assert.Equal(expected, AppLanguage.Resolve(preference, CultureInfo.GetCultureInfo(windows)).Name);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    [InlineData("fr", "fr")]
    [InlineData("fr-FR", "fr")]
    [InlineData("EN", "en")]
    [InlineData("klingon", "")]
    public void APreferenceIsBroughtBackToASupportedValue(string? preference, string expected)
    {
        Assert.Equal(expected, AppLanguage.Normalize(preference));
    }

    [Fact]
    public void TheDefaultLanguageFollowsWindows()
    {
        Assert.Equal(AppLanguage.System, new AppSettings().General.Language);
    }

    [Fact]
    public void TheChosenLanguageIsSavedAndReadBack()
    {
        var settings = new AppSettings() with { General = new GeneralSettings { Language = "fr" } };
        Assert.True(SettingsSerializer.TryDeserialize(SettingsSerializer.Serialize(settings), out var read, out _));
        Assert.Equal("fr", read.General.Language);
    }

    [Fact]
    public void AnUnknownStoredLanguageFallsBackToWindows()
    {
        Assert.True(SettingsSerializer.TryDeserialize("""{ "general": { "language": "zz-ZZ" } }""", out var read, out _));
        Assert.Equal(AppLanguage.System, read.General.Language);
    }

    [Fact]
    public void SettingsWrittenBeforeLanguagesExistedFollowWindows()
    {
        Assert.True(SettingsSerializer.TryDeserialize("""{ "general": { "theme": "Dark" } }""", out var read, out _));
        Assert.Equal(AppLanguage.System, read.General.Language);
        Assert.Equal(ThemePreference.Dark, read.General.Theme);
    }

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(1, true, true)]
    [InlineData(2, false, false)]
    public void PluralsFollowTheRulesOfEachLanguage(long count, bool englishSingular, bool frenchSingular)
    {
        Assert.Equal(englishSingular, Text.IsSingular(count, CultureInfo.GetCultureInfo("en")));
        Assert.Equal(frenchSingular, Text.IsSingular(count, French));
    }

    [Theory]
    [InlineData("en", 1, PluralCategory.One)]
    [InlineData("en", 0, PluralCategory.Other)]
    [InlineData("de", 0, PluralCategory.Other)]
    [InlineData("pt", 0, PluralCategory.One)]
    [InlineData("pt", 2, PluralCategory.Other)]
    [InlineData("ja", 1, PluralCategory.Other)]
    [InlineData("zh", 1, PluralCategory.Other)]
    [InlineData("ru", 1, PluralCategory.One)]
    [InlineData("ru", 3, PluralCategory.Few)]
    [InlineData("ru", 5, PluralCategory.Other)]
    [InlineData("ru", 11, PluralCategory.Other)]
    [InlineData("ru", 12, PluralCategory.Other)]
    [InlineData("ru", 21, PluralCategory.One)]
    [InlineData("ru", 22, PluralCategory.Few)]
    [InlineData("ru", 111, PluralCategory.Other)]
    [InlineData("uk", -21, PluralCategory.One)]
    [InlineData("pl", 1, PluralCategory.One)]
    [InlineData("pl", 4, PluralCategory.Few)]
    [InlineData("pl", 12, PluralCategory.Other)]
    [InlineData("pl", 21, PluralCategory.Other)]
    [InlineData("pl", 24, PluralCategory.Few)]
    [InlineData("cs", 2, PluralCategory.Few)]
    [InlineData("cs", 5, PluralCategory.Other)]
    [InlineData("cs", 22, PluralCategory.Other)]
    [InlineData("sk", 3, PluralCategory.Few)]
    [InlineData("sk", 5, PluralCategory.Other)]
    [InlineData("ro", 0, PluralCategory.Few)]
    [InlineData("ro", 1, PluralCategory.One)]
    [InlineData("ro", 19, PluralCategory.Few)]
    [InlineData("ro", 20, PluralCategory.Other)]
    [InlineData("ro", 101, PluralCategory.Few)]
    [InlineData("ro", 120, PluralCategory.Other)]
    [InlineData("hi", 0, PluralCategory.One)]
    [InlineData("vi", 1, PluralCategory.Other)]
    [InlineData("sv", 1, PluralCategory.One)]
    [InlineData("sv", 0, PluralCategory.Other)]
    public void PluralCategoriesFollowTheRulesOfEachLanguage(string language, long count, PluralCategory expected)
    {
        Assert.Equal(expected, Text.Category(count, CultureInfo.GetCultureInfo(language)));
    }

    [Fact]
    public void LanguagesWithTwoPluralFormsKeepBothInThePluralTemplate()
    {
        var russian = CultureInfo.GetCultureInfo("ru");
        const string One = "{0} файл";
        const string Other = "{0} файла|{0} файлов";
        Assert.Equal(One, Text.Template(21, One, Other, russian));
        Assert.Equal("{0} файла", Text.Template(3, One, Other, russian));
        Assert.Equal("{0} файлов", Text.Template(15, One, Other, russian));
        Assert.Equal("{0} files", Text.Template(15, "{0} file", "{0} files", CultureInfo.GetCultureInfo("en")));
    }

    [Theory]
    [InlineData("en", "Memory", "memory")]
    [InlineData("en", "CPU usage", "CPU usage")]
    [InlineData("en", "GPU (busiest adapter)", "GPU (busiest adapter)")]
    [InlineData("en", "high", "high")]
    [InlineData("en", "", "")]
    [InlineData("fr", "Élevé", "élevé")]
    [InlineData("de", "Arbeitsspeicher", "Arbeitsspeicher")]
    [InlineData("tr", "Işlem", "ışlem")]
    public void TextsArePutInsideASentenceByTheRulesOfTheInterfaceLanguage(string language, string text, string expected)
    {
        Assert.Equal(expected, Text.InSentence(text, CultureInfo.GetCultureInfo(language)));
    }

    [Theory]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-SG", "zh-Hans")]
    [InlineData("zh-TW", "zh-Hant")]
    [InlineData("zh-HK", "zh-Hant")]
    [InlineData("zh-MO", "zh-Hant")]
    public void ChineseWindowsGetsTheTranslationWrittenInItsScript(string windows, string expected)
    {
        // Without a translation in its script, English: a reader of traditional characters never gets simplified ones.
        var resolved = AppLanguage.Resolve(AppLanguage.System, CultureInfo.GetCultureInfo(windows)).Name;
        Assert.Equal(AppLanguage.Find(expected) is null ? AppLanguage.Fallback : expected, resolved);
    }

    [Theory]
    [InlineData("zh", "zh-Hans")]
    [InlineData("ZH-hans", "zh-Hans")]
    [InlineData("zh_CN", "zh-Hans")]
    [InlineData("nn-NO", "nb")]
    [InlineData("pt-PT", "pt")]
    public void RegionsAndScriptsLeadToTheirTranslation(string preference, string expected)
    {
        Assert.Equal(AppLanguage.Find(expected)?.Code ?? AppLanguage.System, AppLanguage.Normalize(preference));
    }

    [Fact]
    public void ListsAreJoinedWithTheLastSeparator()
    {
        Assert.Equal(string.Empty, Text.List([]));
        Assert.Equal("a", Text.List(["a"]));
        Assert.Equal("a and b", Text.List(["a", "b"]));
        Assert.Equal("a, b and c", Text.List(["a", "b", "c"]));
    }

    [Fact]
    public void PagesContainNoHardCodedText()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "Sysora.App");
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(views, "*.xaml", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                foreach (Match match in LiteralTextAttribute().Matches(line))
                {
                    var value = match.Groups["value"].Value;
                    if (value is not "Sysora" && LetterPattern().IsMatch(value))
                    {
                        offenders.Add($"{Path.GetFileName(file)}:{lineNumber}: {match.Value}");
                    }
                }

                if (line.Contains("<x:String>", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{lineNumber}: {line.Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Text to translate in XAML:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Theory]
    [InlineData(nameof(Strings))]
    [InlineData(nameof(UiStrings))]
    public void EveryTextIsUsed(string set)
    {
        // A text no code shows would still have to be translated into every language: remove it instead.
        var used = new HashSet<string>(StringComparer.Ordinal);
        var source = Path.Combine(RepositoryRoot(), "src");
        var tests = Path.Combine(source, "Sysora.Tests") + Path.DirectorySeparatorChar;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            if (file.StartsWith(tests, StringComparison.Ordinal)
                || IsBuildOutput(file)
                || !(file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".xaml", StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (Match match in TextReference().Matches(File.ReadAllText(file)))
            {
                used.Add(match.Value);
            }
        }

        var unused = Read(Manager(set), CultureInfo.InvariantCulture).Keys.Where(k => !used.Contains($"{set}.{k}")).Order().ToArray();
        Assert.True(unused.Length == 0, $"{set}: texts no code uses: {string.Join(", ", unused)}");
    }

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static ResourceManager Manager(string set) => set == nameof(Strings) ? Strings.ResourceManager : UiStrings.ResourceManager;

    /// <summary>The texts of one language only (no fallback to the parent language).</summary>
    private static Dictionary<string, string> Read(ResourceManager manager, CultureInfo culture)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        foreach (DictionaryEntry entry in set)
        {
            texts[(string)entry.Key] = (string)entry.Value!;
        }

        return texts;
    }

    private static int MaxPlaceholder(string text) =>
        PlaceholderPattern().Matches(text).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).DefaultIfEmpty(-1).Max();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sysora.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    [GeneratedRegex(@"\{(\d+)[^}]*\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\b(?:Strings|UiStrings)\.\w+")]
    private static partial Regex TextReference();

    [GeneratedRegex(@"[A-Za-z]{3,}")]
    private static partial Regex LetterPattern();

    [GeneratedRegex(@"(?<![\w.:])(Text|AutomationProperties\.Name|Label|Header|ToolTipService\.ToolTip|Content|Description|Hint|Title|Message|PlaceholderText|OnContent|OffContent|PrimaryButtonText|SecondaryButtonText|CloseButtonText)=""(?<value>[^""{][^""]*)""")]
    private static partial Regex LiteralTextAttribute();
}
