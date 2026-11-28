using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using static Relewise.Integrations.Umbraco.DemoStorefront.DemoConstants;

namespace Relewise.Integrations.Umbraco.DemoStorefront.Seeding;

internal static class DemoEditorial
{
    public static void Ensure(IServiceProvider services)
    {
        var content = services.GetRequiredService<IContentService>();
        var types = services.GetRequiredService<IContentTypeService>();
        using IScope scope = services.GetRequiredService<IScopeProvider>().CreateScope();

        scope.EagerWriteLock(Constants.Locks.ContentTree);

        IContent home = content.GetRootContent().Single(node => node.ContentType.Alias == DocumentTypeAliases.Home);

        foreach (Story story in Stories)
        {
            Guid key = new(SHA256.HashData(Encoding.UTF8.GetBytes("fieldwork-story-" + story.Slug))[..16]);
            IContent? existing = content.GetById(key);

            if (existing != null)
            {
                continue;
            }

            IContent node = content.Create(story.EnglishTitle, home.Id, DocumentTypeAliases.Article);

            node.Key = key;
            node.TemplateId = types.Get(DocumentTypeAliases.Article)!.AllowedTemplates!.Single().Id;
            node.SetCultureName(story.EnglishTitle, "en-US");
            node.SetCultureName(story.DanishTitle, "da-DK");
            node.SetValue(PropertyAliases.Heading, story.EnglishTitle, "en-US");
            node.SetValue(PropertyAliases.Heading, story.DanishTitle, "da-DK");
            node.SetValue(PropertyAliases.BodyText, story.EnglishIntro, "en-US");
            node.SetValue(PropertyAliases.BodyText, story.DanishIntro, "da-DK");
            node.SetValue(PropertyAliases.HeroImage, "/images/" + story.Image + ".svg");
            node.SetValue(PropertyAliases.ContentRows, DemoBlockValues.Story(types, story.EnglishBody), "en-US");
            node.SetValue(PropertyAliases.ContentRows, DemoBlockValues.Story(types, story.DanishBody), "da-DK");
            content.Save(node);

            PublishResult published = content.Publish(node, ["en-US", "da-DK"]);

            if (!published.Success)
            {
                throw new InvalidOperationException($"Failed publishing story '{node.Name}': {published.Result}.");
            }
        }

        foreach (IContent node in content.GetPagedDescendants(home.Id, 0, 1000, out _)
                     .Where(node => !node.Edited && node.Published &&
                                    (node.ContentType.Alias == DocumentTypeAliases.LocalizedArticle ||
                                     (node.ContentType.Alias == DocumentTypeAliases.Article &&
                                      node.GetCultureName("en-US") is "Finding your trail"
                                          or "A slower kind of weekend"))))
        {
            var changed = false;

            foreach (string culture in new[] { "en-US", "da-DK" })
            {
                foreach (string alias in new[] { PropertyAliases.ContentRows, PropertyAliases.PageGrid })
                {
                    if (node.GetValue<string>(alias, culture) is { } json && RefreshCopy(json) is { } updated)
                    {
                        node.SetValue(alias, updated, culture);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                content.Save(node);

                if (!content.Publish(node, ["en-US", "da-DK"]).Success)
                {
                    throw new InvalidOperationException($"Failed updating editorial copy for '{node.Name}'.");
                }
            }
        }

        var homeChanged = false;

        foreach ((string culture, string oldTitle, string title, string intro) in new[]
                 {
                     ("en-US", "Fieldwork storefront", "Make room for the outdoors",
                         "Thoughtful equipment and fresh ideas for days on the trail, nights under canvas and " +
                         "the adventures in between."),
                     ("da-DK", "Fieldwork friluftsbutik", "Gør plads til friluftslivet",
                         "Gennemtænkt udstyr og nye idéer til dage på stien, " +
                            "nætter i telt og alle eventyrene derimellem.")
                 })
        {
            if (!home.Edited && home.Published && home.GetValue<string>(PropertyAliases.Heading, culture) == oldTitle)
            {
                home.SetValue(PropertyAliases.Heading, title, culture);
                home.SetValue(PropertyAliases.BodyText, intro, culture);
                homeChanged = true;
            }
        }

        if (homeChanged)
        {
            content.Save(home);

            if (!content.Publish(home, ["en-US", "da-DK"]).Success)
            {
                throw new InvalidOperationException("Failed publishing the storefront welcome page.");
            }
        }

        scope.Complete();
    }

    private static string? RefreshCopy(string json)
    {
        var replacements = new Dictionary<string, string>
        {
            ["Nested editorial content"] = "Plan a little, explore a little",
            ["Nested trail advice"] = "On the trail",
            ["English nested heading"] = "Follow the changing seasons",
            ["Dansk indlejret overskrift"] = "Følg årstidernes skiften",
            ["Indlejret redaktionelt indhold"] = "Planlæg lidt, og gå på opdagelse",
            ["Grid adventure planner"] = "Plan your next outing",
            ["Inside the Grid area"] = "Leave room for a break",
            ["<p>English <strong>woodland</strong> route &amp; picnic.</p>"] =
                "<p>Follow a <strong>woodland</strong> route and stop for a picnic along the way.</p>",
            ["<p>Dansk <strong>skovtur</strong> med kaffe &amp; madpakke.</p>"] =
                "<p>Tag på <strong>skovtur</strong> med kaffe og madpakke.</p>"
        };
        JsonNode? document = JsonNode.Parse(json);
        var changed = false;

        void Visit(JsonNode? value)
        {
            if (value is JsonObject obj)
            {
                foreach (KeyValuePair<string, JsonNode?> field in obj.ToArray())
                {
                    if (field.Value is JsonValue scalar && scalar.TryGetValue(out string? text))
                    {
                        if (replacements.TryGetValue(text, out string? replacement))
                        {
                            obj[field.Key] = replacement;
                            changed = true;
                        }
                        else if (text.StartsWith('{') && text.Contains("contentData") &&
                                 RefreshCopy(text) is { } nested)
                        {
                            obj[field.Key] = nested;
                            changed = true;
                        }
                    }
                    else
                    {
                        Visit(field.Value);
                    }
                }
            }
            else if (value is JsonArray array)
            {
                foreach (JsonNode? child in array)
                {
                    Visit(child);
                }
            }
        }

        Visit(document);

        return changed ? document!.ToJsonString() : null;
    }

    private static readonly Story[] Stories =
    [
        new(
            "daypack",
            "How to pack for a day outdoors",
            "Pak rygsækken til en dag i det fri",
            "A lighter bag starts with a little planning. Here is what earns its place on a day hike.",
            "En lettere rygsæk begynder med lidt planlægning. Her er det, der fortjener en plads på dagsturen.",
            "daypack",
            "Start with your route, the weather and how long you will be away. Keep water, an " +
            "extra layer and a simple lunch easy to reach. A waterproof bag protects the things " +
            "that need to stay dry.\nPlace heavier items close to your back and use the small " +
            "pockets for keys and a map. Adjust the straps before you leave, then check the fit " +
            "again after the first few minutes of walking.\nLeave some space rather than filling " +
            "every pocket. A comfortable daypack should let you enjoy the path ahead, not remind " +
            "you of everything you brought along.",
            "Begynd med ruten, vejret og hvor længe du er væk. Hav vand, et ekstra lag og en " +
            "enkel frokost lige ved hånden. En vandtæt pose beskytter det, der skal forblive " +
            "tørt.\nLæg de tunge ting tæt på ryggen, og brug de små lommer til nøgler og kort. " +
            "Tilpas stropperne, før du går, og kontrollér pasformen igen efter de første " +
            "minutter.\nLad lidt plads være fri i stedet for at fylde alle lommer. En behagelig " +
            "dagtursrygsæk skal lade dig nyde stien, ikke minde dig om alt det, du har taget med."
        ),
        new(
            "coffee",
            "Coffee tastes better outside",
            "Kaffen smager bedre udenfor",
            "Turn a short walk into a small ritual with a warm drink and a quiet place to stop.",
            "Gør en kort gåtur til et lille ritual med en varm drik og et roligt sted at holde pause.",
            "mug",
            "Warm your flask with hot water before filling it with freshly brewed coffee. It is a " +
            "small habit that helps keep your drink warm while you walk. Bring a cup that is " +
            "comfortable to hold, even with cold fingers.\nChoose a sheltered spot away from the " +
            "busiest stretch of trail. Sit for a few minutes, listen and let the next part of the " +
            "day wait. There is no special distance you need to cover to earn a break.\nTake your " +
            "coffee grounds and packaging home. Rinse your flask when you return and let it dry " +
            "with the lid off, ready for another morning outside.",
            "Varm termoflasken med varmt vand, før du fylder friskbrygget kaffe i. Den lille vane " +
            "hjælper med at holde drikken varm på turen. Tag en kop med, der er behagelig at " +
            "holde om, også med kolde fingre.\nFind læ væk fra den travleste del af stien. Sæt dig " +
            "et øjeblik, lyt, og lad resten af dagen vente. Du behøver ikke gå en bestemt " +
            "distance for at fortjene en pause.\nTag kaffegrums og emballage med hjem. Skyl " +
            "flasken, når du kommer tilbage, og lad den tørre uden låg, klar til endnu en morgen " +
            "i det fri."
        ),
        new(
            "rain",
            "A good day in the rain",
            "En god dag i regnvejr",
            "Wet weather does not have to cancel your plans. Prepare for a shorter, slower adventure.",
            "Regn behøver ikke aflyse dine planer. Gør klar til et kortere eventyr i roligt tempo.",
            "daypack",
            "Pick a familiar route with a few options for turning back. Look at the forecast and " +
            "avoid exposed places when strong winds or storms are expected. A shorter walk can " +
            "still be a good one.\nKeep spare clothing inside a dry bag and put a rain cover over " +
            "your pack. Small essentials are easier to find when they have their own place. Stop " +
            "somewhere sheltered before you get cold.\nAt home, empty your bag and allow " +
            "everything to dry naturally. Check pockets for damp snacks and maps. A little care " +
            "after a wet walk makes the next outing much easier.",
            "Vælg en kendt rute med mulighed for at vende om. Se vejrudsigten, og undgå udsatte " +
            "steder, når der ventes kraftig vind eller torden. En kortere tur kan stadig være en " +
            "god tur.\nOpbevar ekstra tøj i en vandtæt pakkepose, og sæt regnslag på rygsækken. " +
            "Småting er lettere at finde, når de har deres faste plads. Hold pause i læ, før du " +
            "bliver kold.\nTøm tasken derhjemme, og lad alt tørre naturligt. Se lommerne efter for " +
            "fugtige snacks og kort. Lidt pleje efter en våd tur gør den næste meget nemmere."
        ),
        new(
            "camp",
            "Your first night under canvas",
            "Din første nat i telt",
            "Keep the first camping trip simple: a nearby campsite, a familiar meal and time to settle in.",
            "Gør den første telttur enkel: en lejrplads tæt på, et velkendt måltid og tid til at finde ro.",
            "shelter",
            "Try pitching your shelter at home so the parts feel familiar when you reach camp. " +
            "Choose a permitted campsite and arrive with enough daylight to find a level place " +
            "and unpack without rushing.\nAn insulated sleeping mat is as important as a " +
            "comfortable sleeping bag. Keep your sleeping layers dry and make a small place for " +
            "your torch, water and shoes before darkness falls.\nFor dinner, choose food you " +
            "already know how to prepare. Follow the site's rules for stoves and fires. The goal " +
            "of a first night is not a perfect camp, but the confidence to go again.",
            "Prøv at sætte teltet op derhjemme, så delene føles velkendte, når du når " +
            "lejrpladsen. Vælg en tilladt plads, og kom frem i dagslys, så du kan finde et jævnt " +
            "sted uden at skynde dig.\nEt isoleret liggeunderlag er lige så vigtigt som en " +
            "behagelig sovepose. Hold sovetøjet tørt, og find en fast plads til lygte, vand og " +
            "sko, inden mørket falder på.\nVælg aftensmad, du allerede ved, hvordan du tilbereder. " +
            "Følg pladsens regler for blus og bål. Målet med den første nat er ikke den perfekte " +
            "lejr, men lysten til at tage af sted igen."
        ),
        new(
            "layers",
            "Small comforts for cooler walks",
            "Små glæder på kølige gåture",
            "Warm socks, a spare layer and a well-timed pause can change the whole day.",
            "Varme sokker, et ekstra lag og en pause på det rigtige tidspunkt kan forandre hele dagen.",
            "accessories",
            "Dress so you can adjust your layers as you move. Start a little cool rather than " +
            "overheating straight away. Keep a dry layer accessible for stops, when your body " +
            "cools more quickly.\nComfortable socks and footwear make a bigger difference than " +
            "another gadget in your bag. Check for rubbing early and take a short break to adjust " +
            "things before a small annoyance becomes the focus of the walk.\nPack something you " +
            "look forward to eating and enough to drink. Choose a sheltered lunch spot and put a " +
            "layer on before you feel cold. Simple routines help you stay outside for longer.",
            "Tag tøj på, så du kan tilpasse lagene undervejs. Begynd lidt køligt i stedet for " +
            "straks at få det for varmt. Hav et tørt lag klar til pauserne, hvor kroppen køler " +
            "hurtigere ned.\nBehagelige sokker og godt fodtøj gør en større forskel end endnu en " +
            "dims i tasken. Reagér tidligt, hvis noget gnaver, og brug en kort pause på at " +
            "justere det.\nPak noget, du glæder dig til at spise, og rigeligt at drikke. Find læ " +
            "til frokosten, og tag et ekstra lag på, før du bliver kold. Enkle vaner gør det " +
            "lettere at være ude længere."
        ),
        new(
            "water",
            "Make room for water",
            "Gør plads til vand",
            "A refillable bottle belongs close at hand, from the morning commute to a full day on the trail.",
            "En genanvendelig flaske hører til lige ved hånden, fra morgenturen til en hel dag på stien.",
            "flask",
            "Plan where you can refill before setting out and carry enough for the route and " +
            "conditions. Do not assume that every stream or tap along the way provides drinking " +
            "water.\nChoose a bottle you enjoy using and keep it within reach. A simple carry loop " +
            "is useful when your hands are full, while an insulated flask gives you more choices " +
            "on colder days.\nWash bottles and lids regularly and let them dry open. Pay attention " +
            "to seals and threads, where small amounts of moisture can stay behind. Taking care " +
            "of one good bottle helps it last.",
            "Planlæg, hvor du kan fylde op, før du tager af sted, og medbring nok til ruten og " +
            "forholdene. Gå ikke ud fra, at alle vandløb eller haner undervejs giver " +
            "drikkevand.\nVælg en flaske, du er glad for at bruge, og hav den inden for " +
            "rækkevidde. En bærestrop er praktisk med hænderne fulde, mens en termoflaske giver " +
            "flere muligheder på kolde dage.\nVask flasker og låg jævnligt, og lad dem tørre åbne. " +
            "Vær opmærksom på pakninger og gevind, hvor fugt kan gemme sig. God pleje hjælper én " +
            "god flaske med at holde længe."
        ),
        new(
            "picnic",
            "Pack a picnic, take the long way",
            "Pak en picnic, og tag den lange vej",
            "You do not need a remote destination to turn lunch into an outdoor occasion.",
            "Du behøver ikke et fjernt rejsemål for at gøre frokosten til en oplevelse i det fri.",
            "mug",
            "Choose food that travels well and pack it in reusable containers. Keep anything that " +
            "needs cooling properly chilled. A flask of soup can make an ordinary bench feel like " +
            "the best table around.\nBring a small cloth, a cup and a bag for your rubbish. Leave " +
            "delicate plants and busy paths clear when choosing a place to sit. Sometimes the " +
            "nicest spot is only ten minutes from home.\nAllow time to wander before heading back. " +
            "A picnic is a reason to pause rather than a checklist to complete. Take everything " +
            "home, including the small scraps that are easy to overlook.",
            "Vælg mad, der er let at transportere, og pak den i genanvendelige beholdere. Hold " +
            "mad, der kræver køling, tilstrækkeligt kold. En flaske suppe kan gøre en almindelig " +
            "bænk til det bedste bord.\nTag en lille dug, en kop og en pose til affald med. Hold " +
            "sarte planter og travle stier fri, når du finder et sted at sidde. Det bedste sted " +
            "ligger måske kun ti minutter hjemmefra.\nGiv dig tid til at gå lidt på opdagelse, før " +
            "du vender hjem. En picnic er en anledning til at holde pause, ikke en opgave, der " +
            "skal klares. Tag alt med hjem, også de små rester."
        ),
        new(
            "care",
            "Give your gear a longer life",
            "Giv dit udstyr et længere liv",
            "A few minutes of care after each trip can keep your favourite equipment ready for " +
            "years of adventures.",
            "Få minutters pleje efter hver tur kan holde dit yndlingsudstyr klar til mange års eventyr.",
            "daypack",
            "Empty bags after a trip and brush away loose dirt before it dries into seams. Follow " +
            "the maker's care instructions rather than reaching for strong cleaners. Gentle " +
            "treatment is usually the best starting point.\nLet shelters, sleeping bags and towels " +
            "dry fully before putting them away. Store soft equipment loosely where possible. " +
            "Check straps, zips and lids while small problems are still easy to deal with.\nRepair " +
            "what you can and replace only the part that needs it. Familiar equipment often " +
            "becomes more useful over time because you know exactly how it fits into your routine.",
            "Tøm taskerne efter turen, og børst løst snavs væk, før det tørrer ind i sømmene. " +
            "Følg producentens plejeanvisning i stedet for at bruge kraftige rengøringsmidler. " +
            "Skånsom behandling er et godt udgangspunkt.\nLad telte, soveposer og håndklæder tørre " +
            "helt, før du pakker dem væk. Opbevar blødt udstyr løst, når det er muligt. Se " +
            "stropper, lynlåse og låg efter, mens små problemer stadig er lette at løse.\nReparér " +
            "det, du kan, og udskift kun den del, der trænger. Velkendt udstyr bliver ofte mere " +
            "nyttigt med tiden, fordi du ved, hvordan det passer ind i dine rutiner."
        ),
        new(
            "local",
            "Find an adventure close to home",
            "Find et eventyr tæt på hjemmet",
            "Look again at the paths, green spaces and quiet corners you usually pass by.",
            "Se på ny på stierne, de grønne områder og de stille steder, du normalt går forbi.",
            "accessories",
            "Pick a direction rather than an ambitious distance. A local map can reveal a " +
            "footpath, a small woodland or a stretch of water you have never followed. Check " +
            "access rules and respect private land.\nTake the same small essentials you would " +
            "bring on a longer walk: water, a weather layer and a charged phone. Tell someone " +
            "your plans if you are heading out alone.\nTry visiting a familiar place at a " +
            "different time of day. Morning light and a quiet evening can make an ordinary route " +
            "feel entirely new. Good outdoor habits start with places you can return to easily.",
            "Vælg en retning frem for en ambitiøs distance. Et lokalt kort kan afsløre en sti, en " +
            "lille skov eller et stykke vand, du aldrig har fulgt. Undersøg adgangsreglerne, og " +
            "respekter privat jord.\nTag de samme små nødvendigheder med som på en længere tur: " +
            "vand, et ekstra lag og en opladet telefon. Fortæl nogen om dine planer, hvis du går " +
            "alene.\nPrøv at besøge et kendt sted på et nyt tidspunkt. Morgenlys og en stille " +
            "aften kan få en almindelig rute til at føles helt ny. Gode friluftsvaner begynder " +
            "med steder, du let kan vende tilbage til."
        ),
        new(
            "light",
            "When the campsite grows quiet",
            "Når lejrpladsen bliver stille",
            "Soft light, a comfortable seat and a warm cup are enough for a good evening outside.",
            "Blødt lys, en behagelig stol og en varm kop er nok til en god aften i det fri.",
            "lantern",
            "Set up your evening things while there is still daylight. Put your lantern where it " +
            "will not be knocked over and keep its light low enough to enjoy the night around " +
            "you.\nChoose a seat that suits the ground and keep an extra layer nearby. Be " +
            "considerate of neighbouring campers: quiet conversation and gentle light help " +
            "everyone enjoy the same calm setting.\nBefore turning in, put food away securely and " +
            "leave the area tidy. Keep a torch within reach for the night. A little preparation " +
            "lets the final minutes of the day stay wonderfully simple.",
            "Gør klar til aftenen, mens der stadig er dagslys. Sæt lygten, hvor den ikke let " +
            "vælter, og dæmp lyset, så du også kan nyde natten omkring dig.\nVælg en stol, der " +
            "passer til underlaget, og hav et ekstra lag i nærheden. Tag hensyn til andre på " +
            "pladsen: en stille samtale og blødt lys hjælper alle med at nyde roen.\nPak maden " +
            "sikkert væk inden sengetid, og efterlad området ryddeligt. Hav en lommelygte inden " +
            "for rækkevidde til natten. Lidt forberedelse gør dagens sidste minutter dejligt " +
            "enkle."
        )
    ];

    private sealed record Story(
        string Slug,
        string EnglishTitle,
        string DanishTitle,
        string EnglishIntro,
        string DanishIntro,
        string Image,
        string EnglishBody,
        string DanishBody);
}
