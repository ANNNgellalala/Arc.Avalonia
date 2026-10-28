using System;
using System.Collections.Generic;
using System.Linq;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>The eight groups the picker browses, in the order Arc lists them.</summary>
public enum ArcEmojiCategory
{
    SmileysAndPeople,
    AnimalsAndNature,
    FoodAndDrink,
    Activity,
    TravelAndPlaces,
    Objects,
    Symbols,
    Flags
}

/// <summary>Skin tone, from the default yellow through light to dark.</summary>
public enum ArcSkinTone
{
    Default = 0,
    Light = 1,
    MediumLight = 2,
    Medium = 3,
    MediumDark = 4,
    Dark = 5
}

/// <summary>One emoji: its glyph, its human name and the words search matches.</summary>
public sealed class ArcEmoji
{
    internal ArcEmoji(string glyph, string name, string keywords, ArcEmojiCategory category, bool toneable)
    {
        Glyph = glyph;
        Name = name;
        Keywords = keywords;
        Category = category;
        Toneable = toneable;
        Search = (name + " " + keywords + " " + glyph).ToLowerInvariant();
    }

    /// <summary>The glyph itself, without a skin tone.</summary>
    public string Glyph { get; }

    /// <summary>Human name, for example "grinning face".</summary>
    public string Name { get; }

    /// <summary>Space-separated search words beyond the name.</summary>
    public string Keywords { get; }

    public ArcEmojiCategory Category { get; }

    /// <summary>True for the single-code-point people and hand emoji that take a tone modifier.</summary>
    public bool Toneable { get; }

    /// <summary>Lower-cased haystack for instant filtering.</summary>
    public string Search { get; }

    public override string ToString() => Name;
}

/// <summary>What a pick reports: the toned glyph, the base glyph, the name and the tone used.</summary>
public sealed class ArcEmojiSelection
{
    public ArcEmojiSelection(string emoji, string baseEmoji, string name, ArcSkinTone skinTone)
    {
        Emoji = emoji;
        Base = baseEmoji;
        Name = name;
        SkinTone = skinTone;
    }

    /// <summary>Glyph with the active skin tone applied.</summary>
    public string Emoji { get; }

    /// <summary>Glyph without a tone.</summary>
    public string Base { get; }

    public string Name { get; }

    public ArcSkinTone SkinTone { get; }
}

/// <summary>
/// The picker's bundled emoji set. It is a hand-curated list of 400 or so emoji written directly in this
/// file, one compact <c>glyph|name|keywords</c> line each, rather than a table generated from Unicode code
/// points: every entry therefore has a real name and real search words, and nothing renders as a box only
/// because it was swept out of a range. A leading <c>+</c> on the glyph marks the single-code-point people
/// and hand emoji that accept a skin-tone modifier.
///
/// Two deliberate subsets: country flag sequences are left out because Windows fonts do not carry flag
/// glyphs (they would render as regional-indicator letters), and multi-person ZWJ sequences are not marked
/// toneable because a modifier cannot be appended to them safely.
/// </summary>
public static class ArcEmojiData
{
    // glyph|name|keywords  (a leading + marks a tone-capable glyph)
    private static readonly string[] SmileysAndPeople =
    {
        "😀|grinning face|smile grin happy face",
        "😃|grinning face with big eyes|smile happy eyes",
        "😄|grinning face with smiling eyes|smile happy laugh eyes",
        "😁|beaming face with smiling eyes|grin happy teeth",
        "😆|grinning squinting face|laugh happy squint",
        "😅|grinning face with sweat|nervous laugh sweat",
        "🤣|rolling on the floor laughing|rofl laugh tears",
        "😂|face with tears of joy|laugh cry happy tears",
        "🙂|slightly smiling face|smile",
        "🙃|upside-down face|silly sarcasm",
        "😉|winking face|wink flirt",
        "😊|smiling face with smiling eyes|blush happy smile",
        "😇|smiling face with halo|angel innocent",
        "🥰|smiling face with hearts|love adore hearts",
        "😍|heart eyes|love crush hearts",
        "🤩|star-struck|excited wow star",
        "😘|face blowing a kiss|kiss love",
        "😋|face savoring food|tasty yum",
        "😛|face with tongue|tongue silly",
        "😜|winking face with tongue|tongue silly",
        "🤪|zany face|goofy crazy",
        "😝|squinting face with tongue|tongue",
        "🤑|money-mouth face|money rich",
        "🤗|hugging face|hug love",
        "🤭|face with hand over mouth|oops giggle",
        "🤫|shushing face|quiet secret",
        "🤔|thinking face|think hmm",
        "🤐|zipper-mouth face|quiet secret",
        "😐|neutral face|meh",
        "😏|smirking face|smirk smug",
        "😒|unamused face|annoyed",
        "🙄|face with rolling eyes|annoyed whatever",
        "😌|relieved face|calm",
        "😔|pensive face|sad",
        "😴|sleeping face|sleep tired",
        "😷|face with medical mask|sick mask",
        "🤒|face with thermometer|sick fever",
        "🤕|face with head bandage|hurt",
        "🤢|nauseated face|sick",
        "🤮|face vomiting|sick",
        "🤧|sneezing face|sick",
        "🥵|hot face|heat sweat",
        "🥶|cold face|freeze",
        "😵|dizzy face|dead",
        "🤯|exploding head|mind blown",
        "🥳|partying face|party celebrate",
        "😎|smiling face with sunglasses|cool",
        "🤓|nerd face|geek",
        "🧐|face with monocle|inspect",
        "😕|confused face|sad",
        "😮|face with open mouth|surprise",
        "😲|astonished face|surprise",
        "😳|flushed face|embarrassed",
        "🥺|pleading face|please sad",
        "😨|fearful face|scared",
        "😰|anxious face with sweat|nervous",
        "😢|crying face|sad tear",
        "😭|loudly crying face|sob sad",
        "😱|face screaming in fear|scared scream",
        "😞|disappointed face|sad",
        "😓|downcast face with sweat|stressed",
        "😩|weary face|tired",
        "🥱|yawning face|tired bored",
        "😤|face with steam from nose|angry",
        "😡|pouting face|angry",
        "😠|angry face|mad",
        "🤬|face with symbols on mouth|cursing",
        "😈|smiling face with horns|devil",
        "💀|skull|dead",
        "💩|pile of poo|poop",
        "🤡|clown face|clown",
        "👻|ghost|spooky",
        "👽|alien|ufo",
        "🤖|robot|bot",
        "😺|grinning cat|cat",
        "😻|smiling cat with heart eyes|cat love",
        "🙈|see-no-evil monkey|monkey",
        "🙉|hear-no-evil monkey|monkey",
        "🙊|speak-no-evil monkey|monkey",
        "+👋|waving hand|wave hello hi bye",
        "+✋|raised hand|stop high five hand",
        "+🖖|vulcan salute|spock",
        "+👌|ok hand|perfect okay",
        "+🤏|pinching hand|small tiny",
        "+✌|victory hand|peace",
        "+🤞|crossed fingers|luck hope",
        "+🤙|call me hand|phone",
        "+👈|backhand index pointing left|point",
        "+👉|pointing right|point",
        "+👆|pointing up|point",
        "+👇|pointing down|point",
        "+☝|index pointing up|point",
        "+👍|thumbs up|like approve yes",
        "+👎|thumbs down|dislike no",
        "+✊|raised fist|power",
        "+👊|oncoming fist|punch",
        "+👏|clapping hands|applause",
        "+🙌|raising hands|celebrate",
        "+🤲|palms up together|pray",
        "🤝|handshake|deal",
        "+🙏|folded hands|please thanks pray",
        "+💪|flexed biceps|strong",
        "🧠|brain|smart",
        "👀|eyes|look",
        "+👶|baby|child",
        "+🧒|child|kid",
        "+👦|boy|kid",
        "+👧|girl|kid",
        "+🧓|older person|senior",
        "+👨|man|person",
        "+👩|woman|person",
        "+👮|police officer|cop",
        "🧑‍🚀|astronaut|space",
        "🧑‍💻|technologist|developer",
        "+🦸|superhero|hero",
        "+🧙|mage|wizard",
        "+🧛|vampire|dracula",
    };

    private static readonly string[] AnimalsAndNature =
    {
        "🐶|dog face|dog pet",
        "🐱|cat face|cat pet",
        "🐭|mouse face|mouse",
        "🐰|rabbit face|rabbit bunny",
        "🦊|fox|fox",
        "🐻|bear|bear",
        "🐼|panda|panda",
        "🐯|tiger face|tiger",
        "🦁|lion|lion",
        "🐮|cow face|cow",
        "🐷|pig face|pig",
        "🐸|frog|frog",
        "🐵|monkey face|monkey",
        "🐔|chicken|chicken",
        "🐧|penguin|penguin",
        "🐦|bird|bird",
        "🦆|duck|duck",
        "🦅|eagle|eagle",
        "🦉|owl|owl",
        "🐴|horse face|horse",
        "🦄|unicorn|unicorn",
        "🐝|honeybee|bee insect",
        "🦋|butterfly|butterfly insect",
        "🐌|snail|snail",
        "🐞|lady beetle|bug insect",
        "🕷|spider|spider insect",
        "🐢|turtle|turtle slow",
        "🐍|snake|snake",
        "🦖|t-rex|dinosaur",
        "🐙|octopus|octopus",
        "🦀|crab|crab seafood",
        "🐟|fish|fish",
        "🐬|dolphin|dolphin",
        "🦈|shark|shark",
        "🐊|crocodile|crocodile",
        "🐘|elephant|elephant",
        "🦒|giraffe|giraffe",
        "🦌|deer|deer",
        "🐑|ewe|sheep",
        "🌵|cactus|desert plant",
        "🌲|evergreen tree|pine tree",
        "🌳|deciduous tree|tree",
        "🌴|palm tree|palm tropical",
        "🌱|seedling|plant grow",
        "🌿|herb|plant leaf",
        "🍀|four leaf clover|luck plant",
        "🌸|cherry blossom|flower spring",
        "🌹|rose|flower",
        "🌻|sunflower|flower",
        "🍁|maple leaf|autumn leaf",
        "🌈|rainbow|rainbow weather",
        "❄|snowflake|snow winter cold",
        "🔥|fire|hot flame",
        "🌊|water wave|sea ocean",
        "🌍|globe showing europe and africa|world earth",
        "🌙|crescent moon|night moon"
    };

    private static readonly string[] FoodAndDrink =
    {
        "🍎|red apple|fruit",
        "🍊|tangerine|orange fruit",
        "🍌|banana|fruit",
        "🍉|watermelon|fruit",
        "🍇|grapes|fruit wine",
        "🍓|strawberry|fruit",
        "🍒|cherries|fruit",
        "🍑|peach|fruit",
        "🥭|mango|fruit",
        "🍍|pineapple|fruit",
        "🍅|tomato|vegetable",
        "🥑|avocado|vegetable",
        "🥦|broccoli|vegetable",
        "🥕|carrot|vegetable",
        "🌽|ear of corn|vegetable",
        "🌶|hot pepper|spicy vegetable",
        "🍞|bread|bakery",
        "🥐|croissant|bakery",
        "🥖|baguette bread|bakery",
        "🧇|waffle|breakfast",
        "🥞|pancakes|breakfast",
        "🧀|cheese wedge|dairy",
        "🍗|poultry leg|meat",
        "🥩|cut of meat|steak",
        "🍔|hamburger|burger fast food",
        "🍟|french fries|fast food",
        "🍕|pizza|fast food",
        "🌭|hot dog|fast food",
        "🌮|taco|mexican food",
        "🌯|burrito|mexican food",
        "🥗|green salad|healthy food",
        "🍝|spaghetti|pasta",
        "🍜|steaming bowl|ramen noodles",
        "🍣|sushi|japanese food",
        "🍱|bento box|japanese food",
        "🍦|soft ice cream|dessert",
        "🍩|doughnut|dessert",
        "🎂|birthday cake|dessert celebration",
        "🍰|shortcake|dessert",
        "🍫|chocolate bar|dessert",
        "🍿|popcorn|snack cinema",
        "☕|hot beverage|coffee tea",
        "🍵|teacup without handle|tea drink",
        "🥤|cup with straw|drink soda",
        "🍺|beer mug|drink",
        "🍷|wine glass|drink",
    };

    private static readonly string[] Activity =
    {
        "⚽|soccer ball|football sport",
        "🏀|basketball|sport",
        "🏈|american football|sport",
        "⚾|baseball|sport",
        "🎾|tennis|sport",
        "🏐|volleyball|sport",
        "🎱|pool 8 ball|billiards",
        "🏓|ping pong|sport",
        "🏸|badminton|sport",
        "🥊|boxing glove|boxing sport",
        "🥋|martial arts uniform|judo karate",
        "🏆|trophy|award win",
        "🥇|gold medal|award first",
        "🥈|silver medal|award second",
        "🎯|bullseye|target darts",
        "🎳|bowling|sport",
        "🎮|video game|controller gaming",
        "🕹|joystick|gaming arcade",
        "🎲|game die|dice",
        "🧩|puzzle piece|puzzle",
        "🎨|artist palette|paint art",
        "🎭|performing arts|theatre drama",
        "🎬|clapper board|film movie",
        "🎤|microphone|sing karaoke",
        "🎧|headphone|music audio",
        "🎸|guitar|music instrument",
        "🎹|musical keyboard|piano music",
        "🥁|drum|music instrument",
        "🎺|trumpet|music instrument",
        "🎻|violin|music instrument",
        "🎷|saxophone|music instrument",
        "+🏋|person lifting weights|gym fitness",
        "+🚴|person biking|cycling",
        "+🏃|person running|run jog",
        "+🧘|person in lotus position|yoga meditate",
        "+🏊|person swimming|swim",
        "+🤸|person cartwheeling|gymnastics",
        "+🎿|skier|ski winter",
        "+🏂|snowboarder|snowboard winter"
    };

    private static readonly string[] TravelAndPlaces =
    {
        "🚗|automobile|car drive",
        "🚌|bus|transport",
        "🏎|racing car|speed",
        "🚓|police car|police",
        "🚑|ambulance|emergency",
        "🚒|fire engine|emergency",
        "🚲|bicycle|bike cycle",
        "🚨|police car light|siren emergency",
        "🚀|rocket|space launch",
        "🛸|flying saucer|ufo",
        "✈|airplane|flight travel",
        "🚁|helicopter|flight",
        "⛵|sailboat|boat sail",
        "🚢|ship|cruise boat",
        "🚂|locomotive|train steam",
        "🚆|train|rail",
        "🚇|metro|subway train",
        "🗺|world map|map travel",
        "🧭|compass|navigate",
        "🏔|snow-capped mountain|mountain",
        "⛰|mountain|mountain",
        "🌋|volcano|eruption",
        "🏕|camping|tent outdoors",
        "🏖|beach with umbrella|beach holiday",
        "🏝|desert island|island",
        "🌅|sunrise|morning",
        "🌄|sunrise over mountains|morning",
        "🌃|night with stars|city night",
        "🌇|sunset|city evening",
        "🌆|cityscape at dusk|city evening",
        "🌉|bridge at night|bridge",
        "🏙|cityscape|city",
        "🗽|statue of liberty|landmark",
        "🏰|castle|landmark",
        "🏛|classical building|museum",
        "🏟|stadium|sport venue",
        "🎡|ferris wheel|fair",
        "🎢|roller coaster|fair",
        "🏠|house|home",
        "🏡|house with garden|home",
        "🏢|office building|work",
        "🏥|hospital|health",
        "🏫|school|education",
        "🏨|hotel|travel",
        "🕌|mosque|religion"
    };

    private static readonly string[] Objects =
    {
        "⌚|watch|time",
        "📱|mobile phone|smartphone",
        "💻|laptop|computer",
        "⌨|keyboard|computer typing",
        "🖥|desktop computer|screen",
        "💾|floppy disk|save",
        "🎥|movie camera|film",
        "📷|camera|photo",
        "📸|camera with flash|photo",
        "📹|video camera|record",
        "📺|television|tv screen",
        "⏰|alarm clock|time wake",
        "⏳|hourglass done|time wait",
        "🔋|battery|power",
        "🔌|electric plug|power",
        "💡|light bulb|idea",
        "🔦|flashlight|light",
        "🕯|candle|light",
        "🛠|hammer and wrench|tools",
        "🔧|wrench|tool fix",
        "🔨|hammer|tool",
        "⚙|gear|settings",
        "🧰|toolbox|tools",
        "📦|package|box shipping",
        "📝|memo|note write",
        "✏|pencil|write edit",
        "📚|books|library read",
        "📖|open book|read",
        "📰|newspaper|news",
        "📎|paperclip|attach",
        "📌|pushpin|pin",
        "📍|round pushpin|location pin",
        "✂|scissors|cut",
        "🔒|locked|security",
        "🔓|unlocked|security",
        "🔑|key|unlock",
        "🔔|bell|notification",
        "🔕|bell with slash|mute notification",
        "🎁|wrapped gift|present",
        "🎈|balloon|party",
        "🎉|party popper|celebrate",
        "🧸|teddy bear|toy",
        "🪄|magic wand|magic",
        "🛒|shopping cart|shop",
        "☂|umbrella|rain",
        "👓|glasses|eyewear",
        "👑|crown|royal",
        "💎|gem stone|diamond",
        "💍|ring|jewel",
        "🎓|graduation cap|school",
        "🧢|billed cap|hat",
        "💼|briefcase|work",
        "💰|money bag|money",
        "💳|credit card|pay",
        "🪙|coin|money",
        "💵|dollar banknote|money cash",
        "📅|calendar|date",
        "📈|chart increasing|growth data",
        "📉|chart decreasing|loss data",
        "📊|bar chart|data stats",
        "📁|file folder|folder",
        "🔍|magnifying glass tilted left|search zoom",
        "🔗|link|url chain",
    };

    private static readonly string[] SymbolSet =
    {
        "❤|red heart|love heart",
        "💙|blue heart|love heart",
        "💜|purple heart|love heart",
        "💔|broken heart|sad love",
        "💕|two hearts|love",
        "💖|sparkling heart|love",
        "💗|growing heart|love",
        "💓|beating heart|love",
        "💞|revolving hearts|love",
        "💘|heart with arrow|love",
        "💯|hundred points|score perfect",
        "✅|check mark button|done yes",
        "❌|cross mark|no wrong",
        "⭕|hollow red circle|circle",
        "❗|red exclamation mark|alert",
        "❓|red question mark|help",
        "⚠|warning|alert caution",
        "🚫|prohibited|no forbidden",
        "♻|recycling symbol|recycle",
        "⚡|high voltage|electric lightning",
        "✨|sparkles|shine magic",
        "🎵|musical note|music",
        "🎶|musical notes|music",
        "➕|plus|add",
        "💲|heavy dollar sign|money",
        "🔴|red circle|dot",
        "🟠|orange circle|dot",
        "🟡|yellow circle|dot",
        "🟢|green circle|dot",
        "🔵|blue circle|dot",
        "🟣|purple circle|dot",
        "⚫|black circle|dot",
        "⚪|white circle|dot",
        "🟤|brown circle|dot",
        "🔺|red triangle pointed up|up",
        "🔻|red triangle pointed down|down",
        "🔶|large orange diamond|diamond",
        "🔷|large blue diamond|diamond",
        "💤|zzz|sleep",
        "💢|anger symbol|angry",
        "💥|collision|boom",
        "💫|dizzy|star",
        "💦|sweat droplets|water",
        "💨|dashing away|wind fast",
        "💬|speech balloon|comment chat",
        "💭|thought balloon|think",
        "🗯|right anger bubble|angry",
        "ℹ|information|info help",
        "🌐|globe with meridians|web internet",
        "🔤|input latin letters|letters text"
    };

    private static readonly string[] FlagSet =
    {
        "🏁|chequered flag|race finish",
        "🚩|triangular flag|flag",
        "🎌|crossed flags|celebration japan",
        "🏴|black flag|flag",
        "🏳|white flag|surrender flag",
        "🏴‍☠️|pirate flag|skull flag",
        "🎏|carp streamer|flag festival"
    };

    private static readonly Dictionary<ArcEmojiCategory, string[]> Sources = new()
    {
        [ArcEmojiCategory.SmileysAndPeople] = SmileysAndPeople,
        [ArcEmojiCategory.AnimalsAndNature] = AnimalsAndNature,
        [ArcEmojiCategory.FoodAndDrink] = FoodAndDrink,
        [ArcEmojiCategory.Activity] = Activity,
        [ArcEmojiCategory.TravelAndPlaces] = TravelAndPlaces,
        [ArcEmojiCategory.Objects] = Objects,
        [ArcEmojiCategory.Symbols] = SymbolSet,
        [ArcEmojiCategory.Flags] = FlagSet
    };

    private static readonly Dictionary<ArcEmojiCategory, ArcEmoji[]> Groups = new();

    static ArcEmojiData()
    {
        var all = new List<ArcEmoji>();
        foreach (var category in Enum.GetValues<ArcEmojiCategory>())
        {
            var entries = new List<ArcEmoji>();
            foreach (var line in Sources[category])
            {
                var parts = line.Split('|');
                if (parts.Length < 3)
                {
                    continue;
                }

                var glyph = parts[0];
                var toneable = glyph.StartsWith('+');
                if (toneable)
                {
                    glyph = glyph[1..];
                }

                entries.Add(new ArcEmoji(glyph, parts[1], parts[2], category, toneable));
            }

            Groups[category] = entries.ToArray();
            all.AddRange(entries);
        }

        All = all;
    }

    /// <summary>Every bundled emoji, grouped in category order.</summary>
    public static IReadOnlyList<ArcEmoji> All { get; }

    /// <summary>Emoji in one category, in display order.</summary>
    public static IReadOnlyList<ArcEmoji> For(ArcEmojiCategory category) =>
        Groups.TryGetValue(category, out var group) ? group : Array.Empty<ArcEmoji>();

    /// <summary>Full category name, as the group header shows it.</summary>
    public static string Label(ArcEmojiCategory category) => category switch
    {
        ArcEmojiCategory.SmileysAndPeople => "Smileys & people",
        ArcEmojiCategory.AnimalsAndNature => "Animals & nature",
        ArcEmojiCategory.FoodAndDrink => "Food & drink",
        ArcEmojiCategory.Activity => "Activity",
        ArcEmojiCategory.TravelAndPlaces => "Travel & places",
        ArcEmojiCategory.Objects => "Objects",
        ArcEmojiCategory.Symbols => "Symbols",
        _ => "Flags"
    };

    /// <summary>Short category name for the tab bar.</summary>
    public static string ShortLabel(ArcEmojiCategory category) => category switch
    {
        ArcEmojiCategory.SmileysAndPeople => "Smileys",
        ArcEmojiCategory.AnimalsAndNature => "Nature",
        ArcEmojiCategory.FoodAndDrink => "Food",
        ArcEmojiCategory.Activity => "Activity",
        ArcEmojiCategory.TravelAndPlaces => "Travel",
        ArcEmojiCategory.Objects => "Objects",
        ArcEmojiCategory.Symbols => "Symbols",
        _ => "Flags"
    };

    /// <summary>The Unicode skin-tone modifier for a tone, or an empty string for the default tone.</summary>
    public static string Modifier(ArcSkinTone tone) => tone switch
    {
        ArcSkinTone.Light => "\U0001F3FB",
        ArcSkinTone.MediumLight => "\U0001F3FC",
        ArcSkinTone.Medium => "\U0001F3FD",
        ArcSkinTone.MediumDark => "\U0001F3FE",
        ArcSkinTone.Dark => "\U0001F3FF",
        _ => string.Empty
    };

    /// <summary>The tone's name, for accessible labels.</summary>
    public static string ToneName(ArcSkinTone tone) => tone switch
    {
        ArcSkinTone.Light => "Light",
        ArcSkinTone.MediumLight => "Medium light",
        ArcSkinTone.Medium => "Medium",
        ArcSkinTone.MediumDark => "Medium dark",
        ArcSkinTone.Dark => "Dark",
        _ => "Default"
    };

    /// <summary>The glyph with a tone applied; only tone-capable emoji change.</summary>
    public static string WithTone(ArcEmoji emoji, ArcSkinTone tone) =>
        emoji.Toneable ? emoji.Glyph + Modifier(tone) : emoji.Glyph;

    /// <summary>A toned hand, used as the swatch inside each tone chip.</summary>
    public static string ToneSwatch(ArcSkinTone tone) => "👋" + Modifier(tone);

    /// <summary>
    /// Filters the set by category and query. A query is split on spaces and every word must appear in the
    /// emoji's name, keywords or glyph; name matches sort ahead of keyword matches, then catalogue order.
    /// </summary>
    public static IReadOnlyList<ArcEmoji> Filter(ArcEmojiCategory? category, string? query)
    {
        IEnumerable<ArcEmoji> source = category is { } single ? For(single) : All;
        var words = (query ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            return source as IReadOnlyList<ArcEmoji> ?? source.ToList();
        }

        var scored = new List<(ArcEmoji Emoji, int Score)>();
        foreach (var emoji in source)
        {
            var score = 0;
            var matched = true;
            foreach (var word in words)
            {
                var needle = word.ToLowerInvariant();
                if (emoji.Name.StartsWith(needle, StringComparison.Ordinal))
                {
                    score += 3;
                }
                else if (emoji.Name.Contains(needle, StringComparison.Ordinal))
                {
                    score += 2;
                }
                else if (emoji.Search.Contains(needle, StringComparison.Ordinal))
                {
                    score += 1;
                }
                else
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                scored.Add((emoji, score));
            }
        }

        return scored
            .OrderByDescending(pair => pair.Score)
            .Select(pair => pair.Emoji)
            .ToList();
    }

    /// <summary>Finds one bundled emoji by glyph, ignoring any skin-tone modifier.</summary>
    public static ArcEmoji? Find(string? glyph)
    {
        if (string.IsNullOrEmpty(glyph))
        {
            return null;
        }

        foreach (var emoji in All)
        {
            if (emoji.Glyph == glyph)
            {
                return emoji;
            }
        }

        // A toned glyph is the base plus a modifier; retry with the last rune removed.
        if (glyph.Length > 2)
        {
            var trimmed = glyph[..^2];
            foreach (var emoji in All)
            {
                if (emoji.Glyph == trimmed)
                {
                    return emoji;
                }
            }
        }

        return null;
    }
}
