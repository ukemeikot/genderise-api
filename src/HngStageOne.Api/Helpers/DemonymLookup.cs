namespace HngStageOne.Api.Helpers;

/// <summary>
/// Maps adjectival forms (demonyms) to a country alias accepted by <see cref="CountryLookup"/>.
/// Deterministic, no AI. Designed to keep query normalization predictable.
/// </summary>
public static class DemonymLookup
{
    private static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nigerian"]      = "nigeria",
            ["kenyan"]        = "kenya",
            ["ghanaian"]      = "ghana",
            ["senegalese"]    = "senegal",
            ["ethiopian"]     = "ethiopia",
            ["egyptian"]      = "egypt",
            ["moroccan"]      = "morocco",
            ["south african"] = "south africa",
            ["tunisian"]      = "tunisia",
            ["algerian"]      = "algeria",
            ["zimbabwean"]    = "zimbabwe",
            ["ugandan"]       = "uganda",
            ["rwandan"]       = "rwanda",
            ["tanzanian"]     = "tanzania",
            ["cameroonian"]   = "cameroon",
            ["ivorian"]       = "ivory coast",
            ["liberian"]      = "liberia",
            ["malian"]        = "mali",
            ["sudanese"]      = "sudan",
            ["congolese"]     = "democratic republic of congo",

            ["american"]      = "united states",
            ["british"]       = "united kingdom",
            ["english"]       = "united kingdom",
            ["scottish"]      = "united kingdom",
            ["welsh"]         = "united kingdom",
            ["canadian"]      = "canada",
            ["mexican"]       = "mexico",
            ["brazilian"]     = "brazil",
            ["argentine"]     = "argentina",
            ["argentinian"]   = "argentina",
            ["chilean"]       = "chile",
            ["colombian"]     = "colombia",
            ["peruvian"]      = "peru",
            ["venezuelan"]    = "venezuela",

            ["chinese"]       = "china",
            ["japanese"]      = "japan",
            ["korean"]        = "south korea",
            ["indian"]        = "india",
            ["pakistani"]     = "pakistan",
            ["bangladeshi"]   = "bangladesh",
            ["indonesian"]    = "indonesia",
            ["thai"]          = "thailand",
            ["vietnamese"]    = "vietnam",
            ["filipino"]      = "philippines",
            ["malaysian"]     = "malaysia",
            ["singaporean"]   = "singapore",

            ["german"]        = "germany",
            ["french"]        = "france",
            ["italian"]       = "italy",
            ["spanish"]       = "spain",
            ["portuguese"]    = "portugal",
            ["dutch"]         = "netherlands",
            ["belgian"]       = "belgium",
            ["swiss"]         = "switzerland",
            ["austrian"]      = "austria",
            ["swedish"]       = "sweden",
            ["norwegian"]     = "norway",
            ["danish"]        = "denmark",
            ["finnish"]       = "finland",
            ["irish"]         = "ireland",
            ["polish"]        = "poland",
            ["greek"]         = "greece",
            ["turkish"]       = "turkey",
            ["russian"]       = "russia",
            ["ukrainian"]     = "ukraine",

            ["australian"]    = "australia",
            ["new zealander"] = "new zealand",
            ["kiwi"]          = "new zealand",

            ["israeli"]       = "israel",
            ["saudi"]         = "saudi arabia",
            ["emirati"]       = "united arab emirates",
            ["iranian"]       = "iran",
            ["iraqi"]         = "iraq",
            ["lebanese"]      = "lebanon"
        };

    public static bool TryResolveCountryAlias(string demonym, out string countryAlias)
    {
        if (string.IsNullOrWhiteSpace(demonym))
        {
            countryAlias = string.Empty;
            return false;
        }

        return Map.TryGetValue(demonym.Trim(), out countryAlias!);
    }
}
