using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Relewise.DemoStorefront.Seeding;

internal static class DemoLocalization
{
    internal static async Task EnsureAsync(IServiceProvider services)
    {
        var content = services.GetRequiredService<IContentService>();
        var types = services.GetRequiredService<IContentTypeService>();
        IContent[] nodes =
        [
            .. content.GetRootContent()
                .SelectMany(root => new[] { root }.Concat(content.GetPagedDescendants(root.Id, 0, 1000, out _)))
        ];

        // Keep the invariant values before changing the schema, so existing local edits become English variants.
        string[] translatedFields = ["heading", "bodyText", "color", "size", "contentRows", "pageGrid"];
        Dictionary<Guid, (string? Name, bool Invariant, Dictionary<string, object?> Values)> snapshots = nodes
            .Where(node => !node.ContentType.VariesByCulture() ||
                           (node.ContentType.Alias == "localizedArticle" &&
                            node.Properties.Any(property =>
                                translatedFields.Contains(property.Alias) &&
                                !property.PropertyType.VariesByCulture())))
            .ToDictionary(
                node => node.Key,
                node => (node.Name, Invariant: !node.ContentType.VariesByCulture(),
                    Values: translatedFields
                        .Where(alias => node.HasProperty(alias) && !node.Properties
                            .Single(property => property.Alias == alias)
                            .PropertyType.VariesByCulture())
                        .ToDictionary(alias => alias, alias => node.GetValue(alias))));

        foreach (var alias in new[]
                 {
                     "home", "article", "guide", "folder", "brand", "productCategory", "product", "productVariant",
                     "localizedArticle"
                 })
        {
            IContentType type = types.Get(alias)!;

            if (type.VariesByCulture() && type.PropertyTypes
                    .Where(property => translatedFields.Contains(property.Alias))
                    .All(property => property.VariesByCulture()))
            {
                continue;
            }

            type.Variations = ContentVariation.Culture;

            foreach (IPropertyType property in type.PropertyTypes.Where(property =>
                         translatedFields.Contains(property.Alias)))
            {
                property.Variations = ContentVariation.Culture;
            }

            await types.UpdateAsync(type, Constants.Security.SuperUserKey);
        }

        foreach (var (parent, children) in new (string Parent, string[] Children)[]
                 {
                     ("home", ["article", "localizedArticle", "guide", "folder", "brand", "productCategory"]),
                     ("folder", ["article", "localizedArticle", "guide", "folder", "productCategory"]),
                     ("productCategory", ["productCategory", "product"]),
                     ("product", ["productVariant"])
                 })
        {
            IContentType type = types.Get(parent)!;
            List<ContentTypeSort> allowed = [.. (type.AllowedContentTypes ?? [])];

            foreach (var child in children)
            {
                IContentType childType = types.Get(child)!;

                if (allowed.All(value => value.Key != childType.Key))
                {
                    allowed.Add(new ContentTypeSort(childType.Key, allowed.Count, childType.Alias));
                }
            }

            if (allowed.Count != (type.AllowedContentTypes ?? []).Count())
            {
                type.AllowedContentTypes = allowed;
                await types.UpdateAsync(type, Constants.Security.SuperUserKey);
            }
        }

        var domains = services.GetRequiredService<IDomainService>();
        IContent home = content.GetById(nodes.Single(node => node.ContentType.Alias == "home").Key)!;
        IContent story = nodes.Single(node => node.ContentType.Alias == "localizedArticle");

        // One site root gives every entity the same native culture-aware URL hierarchy.
        if (story.ParentId == -1)
        {
            Attempt<DomainUpdateResult, DomainOperationStatus> removed = await domains.UpdateDomainsAsync(
                story.Key,
                new DomainsUpdateModel { DefaultIsoCode = "en-US", Domains = [] });

            if (!removed.Success)
            {
                throw new InvalidOperationException($"Failed removing the old story domains: {removed.Status}.");
            }
        }

        using (IScope scope = services.GetRequiredService<IScopeProvider>().CreateScope())
        {
            scope.EagerWriteLock(Constants.Locks.ContentTree);

            foreach (IContent original in nodes.OrderBy(node => node.Level))
            {
                IContent node = content.GetById(original.Key)!;
                var changed = false;
                var publish = original.Published && !original.Edited;

                if (snapshots.TryGetValue(
                        node.Key,
                        out (string? Name, bool Invariant, Dictionary<string, object?> Values) snapshot))
                {
                    if (snapshot.Invariant)
                    {
                        node.SetCultureName(snapshot.Name, "en-US");
                    }

                    foreach (KeyValuePair<string, object?> value in snapshot.Values)
                    {
                        node.SetValue(value.Key, value.Value, "en-US");
                    }

                    changed = true;
                }

                if (publish && !node.AvailableCultures.Contains("da-DK", StringComparer.OrdinalIgnoreCase))
                {
                    (string Name, string Body)? translation = Translate(node, content);

                    // Authored content may remain English-only; only known seed items receive automatic translations.
                    if (translation != null)
                    {
                        node.SetCultureName(translation.Value.Name, "da-DK");
                        node.SetValue("heading", translation.Value.Name, "da-DK");
                        node.SetValue("bodyText", translation.Value.Body, "da-DK");

                        if (node.ContentType.Alias == "productVariant")
                        {
                            foreach (var alias in new[] { "color", "size" })
                            {
                                node.SetValue(alias, TranslateOption(node.GetValue<string>(alias, "en-US")), "da-DK");
                            }
                        }

                        changed = true;
                    }
                }

                if (node.AvailableCultures.Contains("da-DK", StringComparer.OrdinalIgnoreCase) &&
                    snapshots.TryGetValue(
                        node.Key,
                        out (string? Name, bool Invariant, Dictionary<string, object?> Values) blocks))
                {
                    foreach (var alias in new[] { "contentRows", "pageGrid" })
                    {
                        if (blocks.Values.TryGetValue(alias, out var value) && value is string json)
                        {
                            node.SetValue(alias, DemoBlockValues.InDanish(json), "da-DK");
                        }
                    }
                }

                if (changed)
                {
                    content.Save(node);

                    PublishResult? result = publish ? content.Publish(node, [.. node.AvailableCultures]) : null;

                    if (result is { Success: false })
                    {
                        throw new InvalidOperationException(
                            $"Failed publishing translations for '{node.Name}': {result.Result}.");
                    }
                }

                if (node.ParentId == -1 && node.Id != home.Id && Translate(node, content) != null)
                {
                    OperationResult moved = content.Move(node, home.Id);

                    if (!moved.Success)
                    {
                        throw new InvalidOperationException(
                            $"Failed moving '{node.Name}' below the site root: {moved.Result}.");
                    }
                }
            }

            scope.Complete();
        }

        var baseUrl = services.GetRequiredService<IConfiguration>()["Umbraco:CMS:WebRouting:UmbracoApplicationUrl"]!
            .TrimEnd('/');
        Attempt<DomainUpdateResult, DomainOperationStatus> resultDomains = await domains.UpdateDomainsAsync(
            home.Key,
            new DomainsUpdateModel
            {
                DefaultIsoCode = "en-US",
                Domains =
                [
                    new DomainModel { DomainName = baseUrl + "/en", IsoCode = "en-US" },
                    new DomainModel { DomainName = baseUrl + "/da", IsoCode = "da-DK" }
                ]
            }
        );

        if (!resultDomains.Success)
        {
            throw new InvalidOperationException($"Failed configuring the bilingual site: {resultDomains.Status}.");
        }

        await DemoDictionary.EnsureAsync(services);
    }

    private static (string Name, string Body)? Translate(
        IContent node,
        IContentService content)
    {
        var sku = node.GetValue<string>("sku");

        if (node.ContentType.Alias == "productVariant")
        {
            IContent parent = content.GetById(node.ParentId)!;

            if (!Products.TryGetValue(parent.GetValue<string>("sku") ?? "", out (string Name, string Body) product))
            {
                return null;
            }

            var raw = node.GetValue<string>("color", "en-US");

            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = node.GetValue<string>("size", "en-US");
            }

            if (raw is not ("Forest" or "Sand" or "Slate" or "Rust" or "Copper" or "Moss" or "Small" or "Medium"
                or "Large" or "Solo" or "Duo"))
            {
                return null;
            }

            var option = TranslateOption(raw)!;

            return (product.Name + " " + option, product.Body + " Fås i " + option.ToLowerInvariant() + ".");
        }

        if (sku != null && Products.TryGetValue(sku, out (string Name, string Body) translated))
        {
            return translated;
        }

        return node.GetValue<string>("categoryId") switch
        {
            "U19-SMOKE-ADVENTURES" => ("Fieldwork oplevelser", "Find inspiration til din næste tur i det fri."),
            "U19-SMOKE-DAYHIKES" => ("Dagsture", "Udstyr til en dag på vandrestierne."),
            "U19-SMOKE-EQUIPMENT" => ("Fieldwork udstyr", "Gennemtænkt udstyr til store og små eventyr."),
            "FIELDWORK-PACKS" => ("Rygsække og tasker", "Find den rette taske til din næste tur."),
            "FIELDWORK-DRINKWARE" => ("Flasker og krus", "Hold dig frisk med kolde drikke og varm kaffe."),
            "FIELDWORK-CAMPING" => ("Camping", "Alt til en behagelig nat under åben himmel."),
            "FIELDWORK-ACCESSORIES" => ("Tilbehør", "De små ting, der gør turen bedre."),
            _ => node.Name switch
            {
                "Fieldwork storefront" => ("Fieldwork friluftsbutik", "Udstyr og inspiration til dit næste eventyr."),
                "Fieldwork" => ("Fieldwork", "Friluftsudstyr til hverdagens eventyr."),
                "Ridgeway" => ("Ridgeway", "Praktiske tasker og udstyr til dage på vandrestien."),
                "Embercamp" => ("Embercamp", "Gennemtænkt udstyr til afslappede dage i det fri."),
                "Trail routes" => ("Vandreruter", "Find din næste vandrerute."),
                "Finding your trail" => ("Find din egen sti",
                    "Vores guide til friluftslivet. Vælg din rute, pak let, og efterlad ingen spor."),
                "A slower kind of weekend" => ("En weekend i roligt tempo",
                    "God kaffe, en stille lejrplads og tid til at gå på opdagelse. Det vigtigste til en " +
                    "weekend i det fri."),
                "Packing for your first camp" => ("Pak til din første telttur",
                    "En praktisk pakkeliste fra Fieldwork-holdet."),
                _ => null
            }
        };
    }

    private static string? TranslateOption(string? value)
    {
        return value switch
        {
            null or "" => null,
            "Forest" => "Skovgrøn",
            "Sand" => "Sand",
            "Slate" => "Skifergrå",
            "Rust" => "Rustrød",
            "Copper" => "Kobber",
            "Moss" => "Mosgrøn",
            "Small" => "Lille",
            "Medium" => "Mellem",
            "Large" => "Stor",
            "Solo" => "En person",
            "Duo" => "To personer",
            _ => value
        };
    }

    private static readonly Dictionary<string, (string Name, string Body)> Products = new(StringComparer.Ordinal)
    {
        ["FIELDWORK-COMPACT-HEADLAMP"] = ("Kompakt pandelampe til turen",
            "En kompakt pandelampe med 200 lumen giver frie hænder på aftenture."),
        ["FIELDWORK-HIGH-OUTPUT-HEADLAMP"] = ("Kraftig pandelampe til vandring",
            "En genopladelig pandelampe med 600 lumen og en lang lysstråle til mørke stier."),
        ["FIELDWORK-INSULATED-GLOVES"] = ("Isolerede vinterhandsker",
            "Isolerede vandrehandsker holder hænderne varme på kolde vinterture og giver et godt " +
            "greb om stavene."),
        ["FIELDWORK-WATERPROOF-GLOVES"] = ("Vandtætte vandrehandsker",
            "Vandtætte handsker beskytter hænderne mod vind og regn med fleksible fingre og et sikkert greb."),
        ["FIELDWORK-COMPACT-FIRST-AID"] = ("Kompakt førstehjælpssæt",
            "Et lille førstehjælpssæt til dagsture med tydelige rum til forbindinger og basisudstyr."),
        ["FIELDWORK-FAMILY-FIRST-AID"] = ("Førstehjælpssæt til familien",
            "Et rummeligt førstehjælpssæt til familiens teltture med plads til ekstra " +
            "forbindinger og personligt udstyr."),
        ["FIELDWORK-LIGHT-CAMP-CHAIR"] = ("Ultralet trekkingstol",
            "En let foldbar campingstol med rygstøtte som pakkes i en lille taske til vandreture " +
            "og lejraftener."),
        ["FIELDWORK-RECLINING-CHAIR"] = ("Campingstol med justerbart ryglæn",
            "En komfortabel foldestol med justerbart ryglæn og polstret sæde til rolige aftener i lejren."),
        ["FIELDWORK-LIGHT-DRY-BAG"] = ("Let vandtæt rullepose",
            "En let vandtæt pose på 10 liter med rullelukning holder skiftetøjet tørt i rygsækken."),
        ["FIELDWORK-EXPEDITION-DRY-BAG"] = ("Vandtæt ekspeditionspose",
            "En robust vandtæt pose på 30 liter med forstærket rullelukning til kanoture og vådt " +
            "campingudstyr."),
        ["FIELDWORK-POCKET-BINOCULARS"] = ("Let lommekikkert",
            "En kompakt kikkert med 8 gange forstørrelse og 25 mm linser til fugle og dyreliv på dagsturen."),
        ["FIELDWORK-WATERPROOF-BINOCULARS"] = ("Vandtæt trekkingkikkert",
            "En vandtæt kikkert med 10 gange forstørrelse og 42 mm linser til dyreliv i regn og svagt lys."),
        ["U19-SMOKE-PACK"] = ("Trail dagtursrygsæk",
            "En let rygsæk til vandrestier, ture gennem byen og alt derimellem."),
        ["FIELDWORK-SUMMIT-PACK"] = ("Summit vandrerygsæk",
            "En rummelig vandrerygsæk med et støttende hoftebælte til hele dage i bjergene."),
        ["FIELDWORK-RIDGE-PACK"] = ("Ridge rejserygsæk", "En alsidig rejserygsæk med praktiske rum til weekendture."),
        ["FIELDWORK-HIP-PACK"] = ("Trail bæltetaske", "Hav det vigtigste lige ved hånden i denne kompakte bæltetaske."),
        ["FIELDWORK-SLING"] = ("Kompakt skuldertaske",
            "En lille skuldertaske til kamera, notesbog og hverdagens nødvendigheder."),
        ["FIELDWORK-DRY-BAG"] = ("Vandtæt pakkepose",
            "En pose med rullelukning, der holder ekstra tøj tørt på regnfulde vandre- og kanoture."),
        ["U19-SMOKE-FLASK"] = ("Trail termoflaske",
            "Hold kaffen varm og vandet koldt fra dagens første lys til den sene aften."),
        ["FIELDWORK-WATER-BOTTLE"] = ("Trail drikkeflaske",
            "En genanvendelig drikkeflaske med en praktisk bærestrop til gåture og træning."),
        ["FIELDWORK-CAMP-MUG"] = ("Isoleret campingkrus",
            "Et isoleret krus til kaffe ved teltet eller te på vandreturen."),
        ["FIELDWORK-TUMBLER"] = ("Termokop til rejsen",
            "En rejsekop med tæt låg til den daglige transport og weekendens planer."),
        ["FIELDWORK-HYDRATION-BOTTLE"] = ("Let drikkeflaske",
            "En let drikkeflaske til korte løbeture og lange sommervandringer."),
        ["FIELDWORK-FOOD-FLASK"] = ("Madtermoflaske med bred åbning",
            "Tag en varm frokost med i denne isolerede madbeholder med bred åbning."),
        ["U19-SMOKE-SHELTER"] = ("Camp overdækning",
            "En enkel overdækning med gennemtænkte detaljer. Et sted at holde pause på dit næste eventyr."),
        ["FIELDWORK-TARP"] = ("Woodland tarp", "En alsidig tarp til en frokostpause i læ eller en åben lejrplads."),
        ["FIELDWORK-SLEEPING-BAG"] = ("Sovepose til weekendturen", "En behagelig sovepose til milde nætter i telt."),
        ["FIELDWORK-CAMP-CHAIR"] = ("Sammenklappelig campingstol", "En foldestol til rolige aftener på lejrpladsen."),
        ["FIELDWORK-SLEEPING-MAT"] = ("Let liggeunderlag",
            "Et isoleret liggeunderlag, der fylder lidt på ture med overnatning."),
        ["FIELDWORK-LANTERN"] = ("Kompakt campinglygte",
            "En genopladelig lygte med blødt lys til teltet og picnicbordet."),
        ["FIELDWORK-POLES"] = ("Vandrestave", "Justerbare vandrestave til sikre skridt på ujævnt underlag."),
        ["FIELDWORK-SOCKS"] = ("Vandresokker i merinould",
            "Bløde sokker i merinould, der holder fødderne varme og behagelige på vandreturen."),
        ["FIELDWORK-CAP"] = ("Trail vandrekasket", "En åndbar kasket til solrige stier og hverdagens eventyr."),
        ["FIELDWORK-PACK-COVER"] = ("Vandtæt regnslag til rygsæk",
            "Et let regnslag, der beskytter rygsækken på regnfulde dage."),
        ["FIELDWORK-COOKWARE"] =
            ("Kogesæt til camping", "Et kompakt sæt med gryde og kop til enkle måltider i det fri."),
        ["FIELDWORK-PICNIC-BLANKET"] = ("Pakketæppe til picnic",
            "Et tæppe til morgenmad på lejrpladsen og picnicpauser. Kun to tilbage; kun på tilbud i Europa."),
        ["FIELDWORK-REPAIR-KIT"] = ("Reparationssæt til vandreturen",
            "Et kompakt reparationssæt til rygsække og campingudstyr. Midlertidigt udsolgt; kun " +
            "på tilbud i USA."),
        ["FIELDWORK-HYDRATION-PACK"] = ("Løberygsæk med væskesystem",
            "En tætsiddende løberygsæk med plads til vand og en let trøje."),
        ["FIELDWORK-OVERNIGHT-PACK"] = ("Trekkingrygsæk til overnatning",
            "En støttende trekkingrygsæk til ture med telt og soveudstyr."),
        ["FIELDWORK-EXPEDITION-PACK"] = ("Ekspeditionsrygsæk",
            "En stor rygsæk til længere ruter og varmt udstyr til kolde dage."),
        ["FIELDWORK-FOLDABLE-PACK"] = ("Sammenfoldelig dagtursrygsæk",
            "En sammenfoldelig dagtursrygsæk med plads i den store rejsetaske."),
        ["FIELDWORK-WEEKEND-DUFFEL"] = ("Weekendtaske", "En weekendtaske med bred åbning til tøj og friluftsudstyr."),
        ["FIELDWORK-BASECAMP-DUFFEL"] = ("Udstyrstaske til basecamp",
            "En slidstærk udstyrstaske med bærehåndtag til turen til basecamp."),
        ["FIELDWORK-PACKING-CUBES"] =
            ("Pakkeposer til rejsen", "Hold orden på rejsetøjet med lette pakkeposer med lynlås."),
        ["FIELDWORK-COMPRESSION-SACK"] = ("Kompressionspose til sovepose",
            "Kompressionsremme mindsker pakkestørrelsen på soveposen eller ekstra tøj."),
        ["FIELDWORK-ROLLTOP-PACK"] = ("Rullerygsæk til hverdagen",
            "En rygsæk med rullelukning og polstret computerrum til hverdagens ture."),
        ["FIELDWORK-CAMERA-PACK"] = ("Kamerarygsæk til vandreturen",
            "Polstrede rum holder orden på kameraudstyret på vandreture og byture."),
        ["FIELDWORK-MAP-POUCH"] = ("Vejrbestandig korttaske",
            "En taske med klar forside beskytter kort og rutenoter mod regnbyger."),
        ["FIELDWORK-TOILETRY-BAG"] = ("Toilettaske med ophæng",
            "En toilettaske med ophæng samler de små nødvendigheder på campingturen."),
        ["FIELDWORK-TRAVEL-WALLET"] = ("Rejsepung til turen",
            "En smal pung med lynlås til rejsekort og det vigtigste på turen."),
        ["FIELDWORK-BIKE-PANNIER"] = ("Cykeltaske til langturen",
            "En cykeltaske med rullelukning til tøj og frokost på cykelferien."),
        ["FIELDWORK-FRAME-BAG"] = ("Steltaske til cyklen",
            "En kompakt steltaske holder værktøj og snacks inden for rækkevidde."),
        ["FIELDWORK-ESPRESSO-CUP"] = ("Espressokop til camping",
            "En lille kop i rustfrit stål til morgenespresso ved teltet."),
        ["FIELDWORK-ENAMEL-MUG"] = ("Klassisk emaljekrus", "Et klassisk emaljekrus til tepauser og kaffe ved bålet."),
        ["FIELDWORK-FOLDING-CUP"] = ("Sammenfoldelig turkop",
            "En sammenfoldelig kop med plads i jakkelommen til korte drikkepauser."),
        ["FIELDWORK-LARGE-THERMOS"] = ("Termoflaske til hele dagen",
            "En stor termoflaske til varme drikke med vennerne på lange ture."),
        ["FIELDWORK-KIDS-BOTTLE"] = ("Drikkeflaske til små eventyrere",
            "En kompakt drikkeflaske med bærestrop til de yngste eventyrere."),
        ["FIELDWORK-FILTER-BOTTLE"] = ("Filterflaske til vandreturen",
            "En drikkeflaske med udskifteligt filter til friluftsrejser."),
        ["FIELDWORK-STEEL-BOTTLE"] = ("Enkeltvægget stålflaske",
            "En enkeltvægget stålflaske til kolde drikke på hverdagens gåture."),
        ["FIELDWORK-HYDRATION-RESERVOIR"] = ("Væskebeholder til rygsæk",
            "En fleksibel vandbeholder til rygsække med plads til væskesystem."),
        ["FIELDWORK-TEA-FLASK"] = ("Termoflaske med tefilter",
            "Bryg løs te direkte i en termoflaske med aftageligt tefilter."),
        ["FIELDWORK-CAMP-JUG"] = ("Isoleret kande til lejren",
            "En isoleret kande holder drikke klar til hele gruppen i lejren."),
        ["FIELDWORK-CUP-SET"] = ("Stabelbare campingkopper",
            "Stabelbare kopper sparer plads i det fælles campingkøkken."),
        ["FIELDWORK-WATER-CARRIER"] = ("Sammenfoldelig vandbeholder",
            "Hent vand til lejren i en beholder, der foldes flad, når den er tom."),
        ["FIELDWORK-TREKKING-TENT"] = ("Let trekkingtelt",
            "Et let telt med beskyttet indgang til vandreture med overnatning."),
        ["FIELDWORK-FAMILY-TENT"] = ("Familietelt til basecamp",
            "Et rummeligt familietelt med plads til samvær på regnfulde campingdage."),
        ["FIELDWORK-BIVVY-BAG"] = ("Let bivakpose", "En kompakt bivakpose til enkle overnatninger under stjernerne."),
        ["FIELDWORK-CAMP-HAMMOCK"] = ("Pakkeklar campinghængekøje",
            "En hængekøje med træstropper til rolige eftermiddage i det fri."),
        ["FIELDWORK-CAMP-PILLOW"] = ("Oppustelig campingpude",
            "En oppustelig pude giver ekstra komfort uden at fylde rygsækken."),
        ["FIELDWORK-FOAM-MAT"] = ("Foldbart skumliggeunderlag",
            "Et foldbart skumunderlag isolerer på campingpladser og ved frokostpauser."),
        ["FIELDWORK-WINTER-SLEEPING-BAG"] =
            ("Sovepose til kolde nætter", "En godt isoleret sovepose til kolde nætter i lejren."),
        ["FIELDWORK-CAMP-TABLE"] = ("Sammenklappeligt campingbord",
            "Et foldbart bord giver en stabil plads til måltider og kort i lejren."),
        ["FIELDWORK-CAMP-STOOL"] = ("Kompakt campingtaburet",
            "En kompakt trebenet taburet giver en siddeplads på gåture og fisketure."),
        ["FIELDWORK-CAMP-STOVE"] = ("Kompakt campingblus", "Et kompakt campingblus til varme måltider på lejrpladsen."),
        ["FIELDWORK-COOKING-POT"] = ("Let campinggryde", "En let gryde med foldbare håndtag til campingkøkkenet."),
        ["FIELDWORK-CAMP-KETTLE"] = ("Kedel til campingturen",
            "En kompakt kedel gør det let at holde fælles tepauser i det fri."),
        ["FIELDWORK-SOLAR-LIGHT"] = ("Solcellelampe til lejren",
            "En genopladelig campinglampe med solpanel til solrige hviledage."),
        ["FIELDWORK-TENT-FOOTPRINT"] = ("Beskyttende teltunderlag",
            "Et teltunderlag beskytter teltbunden mod ujævnt underlag og fugtigt græs."),
        ["FIELDWORK-TENT-PEG-SET"] = ("Sæt med lette teltpløkker",
            "Ekstra teltpløkker hjælper med at sikre teltet på næste campingtur."),
        ["FIELDWORK-HIKING-GLOVES"] =
            ("Lette vandrehandsker", "Lette handsker holder hænderne lune på kølige morgenture."),
        ["FIELDWORK-NECK-WARMER"] = ("Halsedisse i merinould",
            "En blød halsedisse i merinould giver varme på blæsende stier."),
        ["FIELDWORK-TRAIL-BEANIE"] = ("Ribstrikket hue til turen",
            "En varm ribstrikket hue til kolde lejre og vintergåture."),
        ["FIELDWORK-RAIN-GAITERS"] = ("Vandtætte vandregamacher",
            "Lette gamacher holder vådt græs og småsten ude af vandrestøvlerne."),
        ["FIELDWORK-HEADLAMP"] = ("Genopladelig pandelampe",
            "En genopladelig pandelampe giver frie hænder på mørke stier."),
        ["FIELDWORK-FIRST-AID-KIT"] = ("Førstehjælpssæt til friluftsliv",
            "En kompakt taske holder det grundlæggende førstehjælpsudstyr samlet på turen."),
        ["FIELDWORK-TRAIL-COMPASS"] = ("Pladekompas til vandreturen",
            "Et pladekompas bruges sammen med papirkortet til ruteplanlægning."),
        ["FIELDWORK-BINOCULARS"] = ("Kompakt kikkert til turen",
            "En kompakt kikkert bringer dyreliv og landskabets detaljer tættere på."),
        ["FIELDWORK-PHONE-POUCH"] = ("Vejrbestandigt telefonetui",
            "Et klart telefonetui giver beskyttelse på våde gåture og kanoture."),
        ["FIELDWORK-TRAIL-SUNGLASSES"] = ("Omsluttende solbriller til turen",
            "Lette omsluttende solbriller er behagelige på aktive dage i det fri."),
        ["FIELDWORK-TRAIL-SIT-PAD"] = ("Foldbart siddeunderlag",
            "Et foldbart siddeunderlag giver en tør plads til korte pauser på turen."),
        ["FIELDWORK-GEAR-STRAPS"] = ("Genanvendelige udstyrsremme",
            "Genanvendelige remme fastgør liggeunderlag eller ekstra udstyr på rygsækken."),
        ["FIELDWORK-TOWEL"] = ("Hurtigtørrende rejsehåndklæde",
            "Et kompakt håndklæde til friluftsbadning, camping og rejser.")
    };
}
