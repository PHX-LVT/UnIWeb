using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Contracts.Icons;

[Flags]
public enum IconContext
{
    None = 0,
    GeneralContent = 1,
    Button = 2,
    FormInformation = 4,
    SocialBrand = 8,
    All = GeneralContent | Button | FormInformation | SocialBrand
}

public sealed record IconDefinition(
    string Key,
    string ClassName,
    string Label,
    string Category,
    IReadOnlyList<string> Keywords,
    IconContext Contexts);

public static class IconCatalog
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly IReadOnlyList<IconDefinition> Definitions = Build();
    private static readonly IReadOnlyDictionary<string, IconDefinition> ByClass = BuildClassIndex(Definitions);

    public static IReadOnlyList<IconDefinition> All => Definitions;

    public static IReadOnlyList<IconDefinition> ForContext(IconContext context) =>
        Definitions.Where(icon => IsInContext(icon, context)).ToList();

    public static IReadOnlyList<IconDefinition> Search(string? query, IconContext context = IconContext.All)
    {
        var terms = (query ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return Definitions
            .Where(icon => IsInContext(icon, context))
            .Where(icon => terms.Length == 0 || terms.All(term => Matches(icon, term)))
            .ToList();
    }

    public static bool TryResolve(string? value, out IconDefinition icon)
    {
        var normalized = NormalizeClass(value);
        return ByClass.TryGetValue(normalized, out icon!);
    }

    public static bool IsAllowed(string? value, IconContext context)
    {
        return TryResolve(value, out var icon) && IsInContext(icon, context);
    }

    public static string Normalize(string? value, IconContext context = IconContext.All)
    {
        if (!TryResolve(value, out var icon) || !IsInContext(icon, context))
            return string.Empty;
        return icon.ClassName;
    }

    private static bool IsInContext(IconDefinition icon, IconContext context)
    {
        var effective = context.HasFlag(IconContext.Button)
            ? context | IconContext.GeneralContent
            : context;
        return (icon.Contexts & effective) != 0;
    }

    public static string NormalizeClass(string? value)
    {
        var tokens = Whitespace.Replace(value?.Trim().ToLowerInvariant() ?? string.Empty, " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.StartsWith("fa", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (tokens.Count == 0) return string.Empty;
        var iconName = tokens.FirstOrDefault(token => token.StartsWith("fa-", StringComparison.Ordinal) &&
            token is not "fa-solid" and not "fa-regular" and not "fa-brands");
        if (iconName is null) return string.Empty;

        var family = tokens.Any(token => token is "fab" or "fa-brands")
            ? "fa-brands"
            : tokens.Any(token => token is "far" or "fa-regular")
                ? "fa-regular"
                : "fa-solid";
        return $"{family} {iconName}";
    }

    private static bool Matches(IconDefinition icon, string term)
    {
        return icon.Key.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               icon.Label.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               icon.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               icon.Keywords.Any(keyword => keyword.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, IconDefinition> BuildClassIndex(IEnumerable<IconDefinition> icons)
    {
        var result = new Dictionary<string, IconDefinition>(StringComparer.Ordinal);
        foreach (var icon in icons)
        {
            result[NormalizeClass(icon.ClassName)] = icon;
            var legacyFamily = icon.ClassName.StartsWith("fa-brands", StringComparison.Ordinal) ? "fab" :
                icon.ClassName.StartsWith("fa-regular", StringComparison.Ordinal) ? "far" : "fas";
            var iconName = icon.ClassName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
            result[NormalizeClass($"{legacyFamily} {iconName}")] = icon;
            result[NormalizeClass($"fa {iconName}")] = icon;
        }
        return new ReadOnlyDictionary<string, IconDefinition>(result);
    }

    private static IReadOnlyList<IconDefinition> Build()
    {
        var icons = new List<IconDefinition>();

        Add(icons, "Actions", IconContext.GeneralContent | IconContext.Button,
            "plus,minus,check,xmark,pen,pencil,trash,rotate,arrows-rotate,download,upload,link,copy,share-nodes,filter,magnifying-glass,gear,sliders,lock,unlock,eye,eye-slash,print,floppy-disk,sort,expand,compress,play,pause,stop,repeat,wand-magic-sparkles,scissors,eraser,thumbtack");
        Add(icons, "Navigation", IconContext.GeneralContent | IconContext.Button,
            "arrow-left,arrow-right,arrow-up,arrow-down,chevron-left,chevron-right,chevron-up,chevron-down,angle-left,angle-right,angle-up,angle-down,caret-left,caret-right,caret-up,caret-down,circle-arrow-left,circle-arrow-right,circle-arrow-up,circle-arrow-down,right-to-bracket,right-from-bracket,up-right-from-square,reply,forward,right-left,turn-up,turn-down,bars,ellipsis,ellipsis-vertical");
        Add(icons, "Communication", IconContext.GeneralContent,
            "envelope,phone,mobile-screen,comment,comments,message,paper-plane,bell,address-book,at,globe,rss,fax,inbox,headset,microphone,video,location-arrow,language,voicemail,tty");
        Add(icons, "Business", IconContext.GeneralContent,
            "briefcase,building,city,store,shop,industry,chart-line,chart-bar,chart-pie,chart-simple,coins,dollar-sign,credit-card,wallet,receipt,calculator,calendar,clock,handshake,users,user-tie,id-card,award,certificate,trophy,bullseye,lightbulb,scale-balanced,landmark,percent,tags,barcode");
        Add(icons, "Logistics", IconContext.GeneralContent,
            "truck,truck-fast,truck-moving,ship,plane,train,car,van-shuttle,bus,motorcycle,bicycle,warehouse,boxes-stacked,box,box-open,dolly,cart-flatbed,route,road,anchor,compass,gas-pump,oil-can,weight-hanging,bridge,person-walking-luggage,suitcase,cart-shopping,basket-shopping");
        Add(icons, "Location", IconContext.GeneralContent,
            "location-dot,map,map-location-dot,map-pin,compass,crosshairs,signs-post,landmark,mountain,flag,earth-americas,earth-asia,earth-europe,house,house-chimney,hotel,school,hospital,tree-city");
        Add(icons, "People", IconContext.GeneralContent,
            "user,users,user-group,user-plus,user-check,user-shield,user-lock,user-gear,person,child,people-group,wheelchair,hand-holding-heart,hands-holding,person-circle-check,person-circle-question,person-running,person-walking");
        Add(icons, "Status", IconContext.GeneralContent | IconContext.Button,
            "circle-check,circle-xmark,triangle-exclamation,circle-info,circle-question,ban,shield,shield-halved,star,heart,thumbs-up,thumbs-down,flag,bookmark,bolt,fire,spinner,hourglass,clock-rotate-left,battery-full,signal,check-double");
        Add(icons, "Files", IconContext.GeneralContent,
            "file,file-lines,file-pdf,file-word,file-excel,file-powerpoint,file-image,file-video,file-audio,file-code,folder,folder-open,box-archive,clipboard,clipboard-check,book,newspaper,note-sticky,book-open,table-list,list-check,signature,paperclip");
        Add(icons, "Media", IconContext.GeneralContent,
            "image,images,camera,photo-film,circle-play,film,music,volume-high,podcast,radio,palette,paintbrush,crop,pen-nib,quote-left,quote-right,closed-captioning,icons,panorama");
        Add(icons, "Technology", IconContext.GeneralContent,
            "laptop,desktop,mobile,tablet-screen-button,server,database,cloud,cloud-arrow-up,cloud-arrow-down,code,terminal,bug,wifi,network-wired,microchip,robot,plug,satellite-dish,keyboard,memory,hard-drive,qrcode,fingerprint");
        Add(icons, "Nature", IconContext.GeneralContent,
            "leaf,seedling,tree,sun,moon,cloud,water,wind,snowflake,recycle,droplet,feather,fish,mountain-sun");

        Add(icons, "Information", IconContext.FormInformation,
            "circle-info,phone,envelope,location-dot,clock,link,globe,address-book,calendar,headset,message");

        AddBrands(icons,
            "facebook,facebook-f,instagram,linkedin,linkedin-in,youtube,x-twitter,twitter,tiktok,whatsapp,telegram,weixin,github,gitlab,microsoft,apple,google,android,discord,slack,pinterest,reddit,vimeo-v,flickr,dribbble,behance,medium,wordpress,skype,line");

        return icons
            .GroupBy(icon => icon.ClassName, StringComparer.Ordinal)
            .Select(group => group.Aggregate((left, right) => left with { Contexts = left.Contexts | right.Contexts }))
            .OrderBy(icon => icon.Category, StringComparer.Ordinal)
            .ThenBy(icon => icon.Label, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
    }

    private static void Add(List<IconDefinition> destination, string category, IconContext contexts, string names)
    {
        foreach (var name in names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            destination.Add(new IconDefinition(
                name,
                $"fa-solid fa-{name}",
                Label(name),
                category,
                Keywords(category, name),
                contexts));
        }
    }

    private static void AddBrands(List<IconDefinition> destination, string names)
    {
        foreach (var name in names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            destination.Add(new IconDefinition(
                $"brand-{name}",
                $"fa-brands fa-{name}",
                Label(name),
                "Social Brands",
                Keywords("social brand", name),
                IconContext.SocialBrand));
        }
    }

    private static IReadOnlyList<string> Keywords(string category, string name)
    {
        var synonyms = name switch
        {
            "truck-fast" => "express delivery shipping",
            "warehouse" => "storage depot logistics",
            "envelope" => "email mail contact",
            "location-dot" => "pin address map",
            "circle-check" => "success complete approved",
            "triangle-exclamation" => "warning alert danger",
            "magnifying-glass" => "search find",
            "gear" => "settings configuration",
            "x-twitter" => "twitter social",
            "weixin" => "wechat social",
            _ => string.Empty
        };
        return $"{category} {name.Replace('-', ' ')} {synonyms}"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Label(string name) => string.Join(' ', name
        .Split('-', StringSplitOptions.RemoveEmptyEntries)
        .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
