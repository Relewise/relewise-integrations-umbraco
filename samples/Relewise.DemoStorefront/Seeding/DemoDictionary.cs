using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Relewise.DemoStorefront.Seeding;

internal static class DemoDictionary
{
    internal static async Task EnsureAsync(IServiceProvider services)
    {
        var dictionaries = services.GetRequiredService<IDictionaryItemService>();
        ILanguage[] languages = [.. (await services.GetRequiredService<ILanguageService>().GetAllAsync())];

        foreach ((string key, string english, string danish) in Labels)
        {
            string dictionaryKey = "Fieldwork." + key;
            IDictionaryItem? existing = await dictionaries.GetAsync(dictionaryKey);
            IDictionaryItem item = existing ?? new DictionaryItem(dictionaryKey);
            List<IDictionaryTranslation> translations = [.. item.Translations];

            foreach ((string culture, string text) in new[] { ("en-US", english), ("da-DK", danish) })
            {
                ILanguage language = languages.Single(language =>
                    language.IsoCode.Equals(culture, StringComparison.OrdinalIgnoreCase));

                if (!translations.Any(translation =>
                        translation.LanguageIsoCode.Equals(culture, StringComparison.OrdinalIgnoreCase)))
                {
                    translations.Add(new DictionaryTranslation(language, text));
                }
            }

            if (existing != null && translations.Count == item.Translations.Count())
            {
                continue;
            }

            item.Translations = translations;

            Attempt<IDictionaryItem, DictionaryItemOperationStatus> result = existing == null
                ? await dictionaries.CreateAsync(item, Constants.Security.SuperUserKey)
                : await dictionaries.UpdateAsync(item, Constants.Security.SuperUserKey);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Failed seeding dictionary item '{dictionaryKey}': {result.Status}.");
            }
        }
    }

    private static readonly (string Key, string English, string Danish)[] Labels =
    [
        ("DemoConfiguration", "Demo configuration", "Demokonfiguration"),
        ("RunningWithoutRelewise", "Running without Relewise", "Kører uden Relewise"),
        ("DemoPreviewHelp",
            "Content preview only. Configure Relewise to enable search, facets, predictions, " +
            "ingestion and tracked shopping.",
            "Kun indholdsvisning. Konfigurer Relewise for at aktivere søgning, facetter, " +
            "søgeforslag, indlæsning og tracking af køb."),
        ("PreviewMode", "Content preview", "Indholdsvisning"),
        ("PreviewModeHelp",
            "Relewise is not configured. You can explore the seeded pages, but search, filters, " +
            "offers, cart and checkout require a Relewise connection.",
            "Relewise er ikke konfigureret. Du kan udforske de oprettede sider, men søgning, " +
            "filtre, tilbud, kurv og bestilling kræver en Relewise-forbindelse."),
        ("ContentPreview",
            "A small selection of published CMS pages. Connect Relewise to search and filter the " +
            "complete catalogue.",
            "Et lille udvalg af publicerede CMS-sider. Tilslut Relewise for at søge og filtrere i " +
            "hele kataloget."),
        ("SearchDisabled", "Search not enabled", "Søgning er ikke aktiveret"),
        ("ShoppingDisabled", "Connect Relewise to use the cart and demo checkout.",
            "Tilslut Relewise for at bruge kurv og demobestilling."),
        ("SearchNotEnabled", "Enable the demo's Relewise search providers to use search, filters and offers.",
            "Aktiver demoens Relewise-søgeudbydere for at bruge søgning, filtre og tilbud."),
        ("FeatureUnavailable", "This feature requires Relewise", "Denne funktion kræver Relewise"),
        ("ConfigureRelewise", "Configure Relewise", "Konfigurer Relewise"),
        ("SimilarProducts", "Similar products", "Lignende produkter"),
        ("RecentlyViewedProducts", "You recently viewed", "Du har senest set"),
        ("PopularProducts", "Popular products", "Populære produkter"),
        ("PopularCategories", "Popular categories", "Populære kategorier"),
        ("LoadingRecommendations", "Loading product recommendations", "Henter produktforslag"),
        ("UpdatingResults", "Updating results…", "Opdaterer resultater…"),
        ("ResultsUpdated", "Results updated", "Resultaterne er opdateret"),
        ("ListingUpdateFailed",
            "Results could not be updated. Previous results remain displayed. Check your " +
            "connection and try again.",
            "Resultaterne kunne ikke opdateres. De tidligere resultater vises stadig. Kontroller " +
            "forbindelsen, og prøv igen."),
        ("TryAgain", "Try again", "Prøv igen"),
        ("SearchProvider", "Search provider", "Søgeudbyder"),
        ("RelewiseActionFailed",
            "The Relewise connection could not complete this action. Check the Dataset ID, API " +
            "credentials and API server; see the application logs for details.",
            "Relewise-forbindelsen kunne ikke fuldføre handlingen. Kontroller Dataset-ID, " +
            "API-oplysninger og API-serveren; se detaljer i applikationens log."),
        ("ConnectionProblem", "Relewise connection problem", "Problem med forbindelsen til Relewise"),
        ("ConnectionUnavailable",
            "The configured Relewise connection could not complete this request. Check the " +
            "Dataset ID, API credentials and API server. See the application logs for " +
            "details.",
            "Den konfigurerede Relewise-forbindelse kunne ikke fuldføre denne forespørgsel. " +
            "Kontroller Dataset-ID, API-oplysninger og API-serveren. Se detaljer i " +
            "applikationens log."),
        ("Menu", "Menu", "Menu"),
        ("Close", "Close", "Luk"),
        ("ChooseMarket", "Choose your market", "Vælg dit marked"),
        ("EuropeShort", "Europe · EUR", "Europa · EUR"),
        ("DenmarkShort", "Denmark · DKK", "Danmark · DKK"),
        ("UnitedStatesShort", "United States · USD", "USA · USD"),
        ("SearchResults", "Search results", "Søgeresultater"),
        ("Products", "Products", "Produkter"),
        ("ShopAllProducts", "Shop all products", "Se alle produkter"),
        ("FooterIntroduction", "Gear and inspiration for your next adventure.",
            "Udstyr og inspiration til dit næste eventyr."),
        ("FooterNavigation", "Footer navigation", "Navigation i sidefoden"),
        ("OurBrands", "Our brands", "Vores mærker"),
        ("Explore", "Explore", "Gå på opdagelse"),
        ("ChangeMarket", "Change market", "Skift marked"),
        ("DemoDetails", "Demo details", "Demodetaljer"),
        ("RelewiseConfigured", "Relewise configured", "Relewise konfigureret"),
        ("AddedToCart", "Added to your cart", "Lagt i din kurv"),
        ("AddingToCart", "Adding…", "Tilføjer…"),
        ("CartUpdating", "Updating your cart…", "Opdaterer din kurv…"),
        ("CartItems", "Your items", "Dine varer"),
        ("EmptyCartAction", "Empty cart", "Tøm kurv"),
        ("EmptyCartConfirm", "Remove all items from your cart?", "Fjern alle varer fra din kurv?"),
        ("Cancel", "Cancel", "Annuller"),
        ("PurchaseFailed", "Your cart could not be updated. Please try again. Your previous cart remains available.",
            "Din kurv kunne ikke opdateres. Prøv igen. Din tidligere kurv er stadig tilgængelig."),
        ("ChangeQuantity", "Change quantity", "Ret antal"),
        ("Price", "Price", "Pris"),
        ("UnderPrice", "Under {0}", "Under {0}"),
        ("Availability", "Availability", "Lagerstatus"),
        ("ShowOnly", "Show only", "Vis kun"),
        ("Brands", "Brands", "Mærker"),
        ("FiltersShort", "Filters", "Filtre"),
        ("OnSale", "On sale", "På tilbud"),
        ("ShowProducts", "Show {0} products", "Vis {0} produkter"),
        ("ShowProduct", "Show 1 product", "Vis 1 produkt"),
        ("ShowingRange", "Showing {0}–{1} of {2}", "Viser {0}–{1} af {2}"),
        ("SavePercent", "Save {0}%", "Spar {0}%"),
        ("SaveAmount", "Save {0}", "Spar {0}"),
        ("InStock", "In stock", "På lager"),
        ("SortBy", "Sort by", "Sortér efter"),
        ("RecommendedRelewise", "Recommended", "Anbefalet"),
        ("Recommended", "Default order", "Standardrækkefølge"),
        ("NameAscending", "Name: A–Z", "Navn: A–Å"),
        ("NameDescending", "Name: Z–A", "Navn: Å–A"),
        ("PriceAscending", "Price: low to high", "Pris: lav til høj"),
        ("PriceDescending", "Price: high to low", "Pris: høj til lav"),
        ("Specifications", "Specifications", "Specifikationer"),
        ("CustomPriceRange", "Custom price range", "Valgfri prisgrænse"),
        ("MinimumPrice", "Min", "Min"),
        ("MaximumPrice", "Max", "Maks"),
        ("PriceRangeInvalid", "Min must be less than or equal to Max.", "Min skal være mindre end eller lig med Maks."),
        ("FilterAndSort", "Filter and sort products", "Filtrer og sorter produkter"),
        ("ViewCart", "View cart", "Se kurven"),
        ("CartAdded", "{0} was added to your cart.", "{0} er lagt i kurven."),
        ("CartUpdated", "Your cart has been updated.", "Din kurv er opdateret."),
        ("CartCleared", "Your cart has been cleared.", "Din kurv er tømt."),
        ("CartStockLimit",
            "{0}: only {1} available in total across all options. Your previous cart is " +
            "unchanged. Reduce the quantity or remove another option of this Product.",
            "{0}: kun {1} tilgængelige i alt på tværs af alle varianter. Din tidligere kurv er " +
            "uændret. Reducer antallet, eller fjern en anden variant af produktet."),
        ("MarketChanged", "Market changed to {0}. Prices and your cart are now shown in {1}.",
            "Markedet er ændret til {0}. Priser og kurv vises nu i {1}."),
        ("ShopAvailable", "Shop available products in this category", "Se produkter på lager i denne kategori"),
        ("ChooseColour", "Choose colour", "Vælg farve"),
        ("ChooseCapacity", "Choose capacity", "Vælg rumindhold"),
        ("SizeGuide", "Size guide", "Størrelsesguide"),
        ("ChooseSize", "Choose size", "Vælg størrelse"),
        ("ChooseDimensions", "Choose dimensions", "Vælg mål"),
        ("ChooseOccupancy", "Choose number of people", "Vælg antal personer"),
        ("ChoosePackSize", "Choose pack or configuration", "Vælg sæt eller udførelse"),
        ("ChooseLength", "Choose length", "Vælg længde"),
        ("Weight", "Weight", "Vægt"),
        ("Capacity", "Capacity", "Kapacitet"),
        ("Filters", "Filter products", "Filtrer produkter"),
        ("ApplySort", "Sort", "Sorter"),
        ("AppliedFilters", "Applied filters", "Valgte filtre"),
        ("RemoveFilter", "Remove filter: {0}", "Fjern filter: {0}"),
        ("InvalidSearch", "This search could not be completed. Check your filters and try again.",
            "Søgningen kunne ikke gennemføres. Tjek dine filtre, og prøv igen."),
        ("NoMatchesHeading", "No matching products or stories", "Ingen matchende produkter eller fortællinger"),
        ("PageUnavailableHeading", "This page is no longer available", "Denne side er ikke længere tilgængelig"),
        ("PageUnavailableHelp", "The results have changed. Return to the last available page to continue browsing.",
            "Resultaterne er ændret. Gå til den sidste tilgængelige side for at fortsætte."),
        ("ReturnToResults", "Return to results", "Tilbage til resultaterne"),
        ("NoMatchesHelp",
            "Try a shorter search or check the spelling. If you selected filters, remove one or " +
            "clear them to see more results.",
            "Prøv en kortere søgning, eller tjek stavningen. Fjern et filter, eller ryd dem alle " +
            "for at se flere resultater."),
        ("BrowseWithoutQuery", "Browse without this search", "Se udvalget uden denne søgning"),
        ("OutOfStock", "Out of stock", "Udsolgt"),
        ("LowStock", "Only {0} left", "Kun {0} tilbage"),
        ("AvailableOptions", "Options: {0}", "Varianter: {0}"),
        ("OnOffer", "On offer", "På tilbud"),
        ("ApplyFilters", "Apply filters", "Anvend filtre"),
        ("ClearFilters", "Clear filters", "Ryd filtre"),
        ("CartIntro", "Adjust your items before continuing to checkout.", "Tilpas dine varer, før du går til kassen."),
        ("ReviewOrder", "Review your order", "Gennemse din ordre"),
        ("ReviewIntro", "Check your items and total before placing your demo order.",
            "Tjek dine varer og det samlede beløb, før du afgiver din demoordre."),
        ("OrderSummary", "Order summary", "Ordreoversigt"),
        ("UnavailableItem", "Unavailable item", "Utilgængelig vare"),
        ("UnavailableTotal", "Unavailable items are excluded from the total. Remove them before checkout.",
            "Utilgængelige varer er ikke med i totalen. Fjern dem, før du går til kassen."),
        ("OffersNavigation", "Offers", "Tilbud"),
        ("Home", "Home", "Forside"),
        ("MainNavigation", "Main navigation", "Hovednavigation"),
        ("Breadcrumb", "Breadcrumb", "Brødkrumme"),
        ("Categories", "Categories", "Kategorier"),
        ("ShopByCategory", "Shop by category", "Find udstyr efter kategori"),
        ("ExploreCollection", "Explore the collection", "Se kollektionen"),
        ("ExploreJournal", "Find your next adventure", "Find dit næste eventyr"),
        ("FromCollection", "From the collection", "Fra kollektionen"),
        ("ViewAllProducts", "View all products", "Se alle produkter"),
        ("ViewAllStories", "View all stories", "Se alle fortællinger"),
        ("ReturnJournal", "← Back to the journal", "← Tilbage til journalen"),
        ("ChooseOption", "Choose a colour or size", "Vælg farve eller størrelse"),
        ("SelectVariant", "Choose an option above to add this product to your cart.",
            "Vælg en mulighed ovenfor for at lægge produktet i kurven."),
        ("Unavailable", "Currently unavailable in this market.", "Ikke tilgængelig på dette marked i øjeblikket."),
        ("Quantity", "Quantity", "Antal"),
        ("AddCart", "Add to cart", "Læg i kurv"),
        ("Cart", "Cart", "Kurv"),
        ("Checkout", "Checkout", "Gå til bestilling"),
        ("DemoCheckout",
            "This is a demo shop. No payment or personal information is needed, and nothing will be shipped.",
            "Dette er en demobutik. Der kræves ingen betaling eller personoplysninger, og der " +
            "sendes ingen varer."),
        ("CartInvalid", "This action could not be completed. Open your cart and try again.",
            "Handlingen kunne ikke gennemføres. Åbn kurven, og prøv igen."),
        ("CartUnavailable",
            "One or more items are unavailable, or the quantity exceeds available stock. Please " +
            "update your cart.",
            "En eller flere varer er ikke tilgængelige, eller antallet overstiger " +
            "lagerbeholdningen. Opdater kurven."),
        ("CartFailed", "We could not update your cart right now. Your previous cart is unchanged. Please try again.",
            "Vi kunne ikke opdatere kurven lige nu. Din tidligere kurv er uændret. Prøv igen."),
        ("ClearCart", "Clear cart", "Tøm kurv"),
        ("EmptyCartHeading", "Your cart is empty", "Din kurv er tom"),
        ("EmptyCartDescription", "Find the gear for your next adventure.", "Find udstyret til dit næste eventyr."),
        ("BrowseOffers", "Browse offers", "Se tilbud"),
        ("CartEmpty", "Your cart is empty. Find something for your next adventure.",
            "Din kurv er tom. Find noget til dit næste eventyr."),
        ("ContinueShopping", "Continue shopping", "Shop videre"),
        ("Each", "each", "pr. stk."),
        ("UpdateCart", "Update", "Opdater"),
        ("RemoveCart", "Remove", "Fjern"),
        ("Total", "Total", "I alt"),
        ("PlaceOrder", "Place demo order", "Afgiv demobestilling"),
        ("EditCart", "Back to cart", "Tilbage til kurven"),
        ("Confirmation", "Order confirmation", "Ordrebekræftelse"),
        ("DemoOrder", "Demo order", "Demobestilling"),
        ("ThankYou", "Thank you for exploring Fieldwork", "Tak, fordi du gik på opdagelse hos Fieldwork"),
        ("OrderConfirmed", "Your demo order is complete. No payment was taken and no items will be shipped.",
            "Din demobestilling er gennemført. Der er ikke trukket nogen betaling, og der sendes ingen varer."),
        ("OrderNumber", "Order number", "Ordrenummer"),
        ("Collection", "The collection", "Kollektionen"),
        ("Journal", "The journal", "Journalen"),
        ("Guides", "Field guides", "Friluftsguides"),
        ("Editorial", "Stories and guides", "Fortællinger og guides"),
        ("Localized", "Stories in your language", "Fortællinger på dit sprog"),
        ("Offers", "Selected offers", "Udvalgte tilbud"),
        ("SearchSite", "Search the site", "Søg på siden"),
        ("EditorialIntro", "Find inspiration for your next adventure.", "Find inspiration til dit næste eventyr."),
        ("CollectionIntro", "Explore outdoor essentials for your next adventure.",
            "Find friluftsudstyr til dit næste eventyr."),
        ("OutdoorGoods", "Outdoor goods", "Friluftsudstyr"),
        ("ClearSearch", "Clear search", "Ryd søgning"),
        ("SuggestedSearches", "Suggested searches", "Søgeforslag"),
        ("SuggestionsAvailable", "{0} search suggestions available", "{0} søgeforslag tilgængelige"),
        ("RefineSuggestion", "Use {0} in the search field", "Brug {0} i søgefeltet"),
        ("SearchPlaceholder", "Try Trail, flask or camp", "Prøv Trail, termoflaske eller camping"),
        ("SearchProducts", "Search products", "Søg efter produkter"),
        ("SearchJournal", "Search the journal", "Søg i journalen"),
        ("EditorialSearchPlaceholder", "Try seasons or trail", "Prøv årstider eller sti"),
        ("Search", "Search", "Søg"),
        ("Browse", "Browse", "Gå på opdagelse"),
        ("ResultsFor", "Results for “{0}”", "Resultater for “{0}”"),
        ("Result", "{0} result", "{0} resultat"),
        ("Results", "{0} results", "{0} resultater"),
        ("Equipment", "Equipment", "Udstyr"),
        ("FieldNotes", "Field notes", "Noter fra naturen"),
        ("ReadStory", "Read the story →", "Læs fortællingen →"),
        ("Updating", "Some pages are being updated. Please try again shortly.",
            "Nogle sider er ved at blive opdateret. Prøv igen om lidt."),
        ("NoMatches", "No matches. Try another search.", "Ingen resultater. Prøv en anden søgning."),
        ("Pagination", "Pagination", "Sidenavigation"),
        ("Previous", "← Previous", "← Forrige"),
        ("Next", "Next →", "Næste →"),
        ("Page", "Page {0} of {1}", "Side {0} af {1}"),
        ("PriceUnavailable", "Price unavailable in {0}", "Pris ikke tilgængelig i {0}"),
        ("Market", "Market", "Marked"),
        ("Europe", "Europe · English · EUR", "Europa · English · EUR"),
        ("Denmark", "Denmark · Dansk · DKK", "Danmark · Dansk · DKK"),
        ("UnitedStates", "United States · English · USD", "USA · English · USD"),
        ("Apply", "Apply", "Anvend"),
        ("Shop", "Shop", "Butik"),
        ("DemoStorefront", "Demo Storefront", "Demobutik"),
        ("Product", "Product", "Produkt"),
        ("Variant", "Variant", "Variant"),
        ("Brand", "Brand", "Mærke"),
        ("Category", "Category", "Kategori"),
        ("Content", "Content", "Indhold"),
        ("ReturnShop", "← Return to shop", "← Tilbage til butikken"),
        ("SalesPrice", "Sales price", "Salgspris"),
        ("ListPrice", "List price", "Listepris"),
        ("ViewProduct", "View product", "Se produkt"),
        ("IntegrationDetails", "Integration details", "Integrationsdetaljer"),
        ("UmbracoKey", "Umbraco key", "Umbraco-nøgle"),
        ("EntityId", "Relewise {0} ID", "Relewise {0}-ID"),
        ("ParentProductId", "Parent Product ID", "Overordnet produkt-ID"),
        ("Tracking", "This page uses the integration's automatic entity view tracking.",
            "Denne side bruger integrationens automatiske registrering af visninger."),
        ("EditorialBlocks", "From the journal", "Fra journalen"),
        ("EditorialGrid", "Ideas for your next trip", "Idéer til din næste tur")
    ];
}
