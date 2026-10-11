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
        var finnish = CultureInfo.GetCultureInfo("fi");
        Assert.Equal("Not available", Strings.ResourceManager.GetString(nameof(Strings.Common_NotAvailable), finnish));
    }

    [Theory]
    [InlineData("", "fr-FR", "fr")]
    [InlineData("", "fr-CA", "fr")]
    [InlineData("", "fi-FI", "en")]
    [InlineData("", "de-AT", "de")]
    [InlineData("", "en-US", "en")]
    [InlineData("en", "fr-FR", "en")]
    [InlineData("fr", "en-US", "fr")]
    [InlineData("FR-be", "en-US", "fr")]
    [InlineData("xx", "fr-FR", "fr")]
    [InlineData(null, "fi-FI", "en")]
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
    [InlineData("zh-CN", "en")]
    [InlineData("zh-TW", "en")]
    [InlineData("zh-HK", "en")]
    public void TraditionalChineseWindowsKeepsEnglish(string windows, string expectedWithoutChinese)
    {
        var resolved = AppLanguage.Resolve(AppLanguage.System, CultureInfo.GetCultureInfo(windows)).Name;
        if (windows == "zh-CN" && AppLanguage.Find("zh") is not null)
        {
            Assert.Equal("zh", resolved);
        }
        else
        {
            Assert.Equal(expectedWithoutChinese, resolved);
        }
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

    [GeneratedRegex(@"[A-Za-z]{3,}")]
    private static partial Regex LetterPattern();

    [GeneratedRegex(@"(?<![\w.:])(Text|AutomationProperties\.Name|Label|Header|ToolTipService\.ToolTip|Content|Description|Hint|Title|Message|PlaceholderText|OnContent|OffContent|PrimaryButtonText|SecondaryButtonText|CloseButtonText)=""(?<value>[^""{][^""]*)""")]
    private static partial Regex LiteralTextAttribute();
}
