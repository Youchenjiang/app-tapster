using System.Linq;
using Tapster;
using Xunit;

namespace Tapster.Core.Tests;

public class LocalizationTests
{
    [Theory]
    [InlineData(LocalizationManager.LangZhTw)]
    [InlineData(LocalizationManager.LangEnUs)]
    [InlineData(LocalizationManager.LangJaJp)]
    [InlineData(LocalizationManager.LangZhCn)]
    public void SupportedLocales_ExistInTranslations(string locale)
    {
        Assert.True(LocalizationManager.Translations.ContainsKey(locale));
    }

    [Fact]
    public void AllLocales_HaveIdenticalKeySet()
    {
        var englishKeys = LocalizationManager.Translations[LocalizationManager.LangEnUs].Keys.OrderBy(k => k).ToList();

        foreach (var (code, _) in LocalizationManager.SupportedLanguages.Where(l => l.Code != LocalizationManager.LangAuto))
        {
            var localeKeys = LocalizationManager.Translations[code].Keys.OrderBy(k => k).ToList();

            var missingInLocale = englishKeys.Except(localeKeys).ToList();
            var extraInLocale = localeKeys.Except(englishKeys).ToList();

            Assert.True(missingInLocale.Count == 0, $"Locale '{code}' is missing keys: {string.Join(", ", missingInLocale)}");
            Assert.True(extraInLocale.Count == 0, $"Locale '{code}' has extra keys: {string.Join(", ", extraInLocale)}");
        }
    }

    [Fact]
    public void AllTranslationValues_AreNonEmpty()
    {
        foreach (var (locale, dict) in LocalizationManager.Translations)
        {
            foreach (var (key, value) in dict)
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"Locale '{locale}' has empty value for key '{key}'");
            }
        }
    }

    [Theory]
    [InlineData("zh-TW", LocalizationManager.LangZhTw)]
    [InlineData("zh-HK", LocalizationManager.LangZhTw)]
    [InlineData("zh-Hant", LocalizationManager.LangZhTw)]
    [InlineData("zh-CN", LocalizationManager.LangZhCn)]
    [InlineData("zh-Hans", LocalizationManager.LangZhCn)]
    [InlineData("ja-JP", LocalizationManager.LangJaJp)]
    [InlineData("ja", LocalizationManager.LangJaJp)]
    [InlineData("en-US", LocalizationManager.LangEnUs)]
    [InlineData("en-GB", LocalizationManager.LangEnUs)]
    [InlineData("fr-FR", LocalizationManager.LangEnUs)]
    public void SetLanguage_NormalizesCodesCorrectly(string inputCode, string expectedEffective)
    {
        LocalizationManager.SetLanguage(inputCode);
        Assert.Equal(expectedEffective, LocalizationManager.EffectiveLanguage);
    }

    [Fact]
    public void Get_ReturnsExpectedTranslation_AndTriggersEvent()
    {
        bool eventFired = false;
        Action handler = () => eventFired = true;

        LocalizationManager.LanguageChanged += handler;
        try
        {
            LocalizationManager.SetLanguage(LocalizationManager.LangEnUs);
            Assert.True(eventFired);
            Assert.Equal("Settings", LocalizationManager.Get("Settings_Title"));

            LocalizationManager.SetLanguage(LocalizationManager.LangZhTw);
            Assert.Equal("偏好設定", LocalizationManager.Get("Settings_Title"));

            LocalizationManager.SetLanguage(LocalizationManager.LangJaJp);
            Assert.Equal("設定", LocalizationManager.Get("Settings_Title"));

            LocalizationManager.SetLanguage(LocalizationManager.LangZhCn);
            Assert.Equal("设置", LocalizationManager.Get("Settings_Title"));
        }
        finally
        {
            LocalizationManager.LanguageChanged -= handler;
        }
    }

    [Fact]
    public void Get_UnknownKey_ReturnsKeyName()
    {
        string unknown = "NonExistent_Test_Key_123";
        Assert.Equal(unknown, LocalizationManager.Get(unknown));
    }

    [Fact]
    public void AsianLocales_DoNotContainMixedEnglishBracketsInCoreTitles()
    {
        var testKeys = new[]
        {
            "Settings_Title",
            "Settings_BootTitle",
            "Settings_StartMinimizedTitle",
            "Settings_CloseToTrayTitle",
            "Settings_AlwaysOnTopTitle",
            "Settings_HotkeysTitle",
            "About_Title",
            "About_SpecsTitle",
            "About_PrivacyTitle",
            "Typer_Title",
            "KeyHolder_Title",
            "Clicker_Title",
            "Macro_Title"
        };

        var asianLocales = new[]
        {
            LocalizationManager.LangZhTw,
            LocalizationManager.LangZhCn,
            LocalizationManager.LangJaJp
        };

        foreach (var locale in asianLocales)
        {
            var dict = LocalizationManager.Translations[locale];
            foreach (var key in testKeys)
            {
                string val = dict[key];
                Assert.False(val.Contains("(Settings)") || val.Contains("(About)") || val.Contains("(Auto Typer)") || val.Contains("(Key Holder)"),
                    $"Locale '{locale}' key '{key}' contains unwanted mixed English in brackets: '{val}'");
            }
        }
    }
}
