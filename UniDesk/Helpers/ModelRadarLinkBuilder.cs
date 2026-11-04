using UniDesk.Models;

namespace UniDesk.Helpers;

public static class ModelRadarLinkBuilder
{
    private const string Origin = "https://modeldial.com";

    public static Uri BuildFullRankingUri(ModelRadarCategory category) =>
        new($"{Origin}/radar?axis={GetAxis(category)}");

    public static Uri? BuildModelDetailUri(string? model, string? effort, ModelRadarCategory category)
    {
        if (!IsModelIdentifier(model) || !IsIdentifier(effort, allowDots: false))
            return null;

        var address = $"{Origin}/models/{Uri.EscapeDataString(model!)}?axis={GetAxis(category)}&config={Uri.EscapeDataString($"{model}~{effort}")}";
        return Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri : null;
    }

    public static bool IsAllowedNavigationUri(Uri? uri)
    {
        if (uri is not { IsAbsoluteUri: true } || uri.Scheme != Uri.UriSchemeHttps ||
            uri.Host != "modeldial.com" || uri.Port != 443 ||
            uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            return false;

        // Compare the original input with the route we construct. Uri normalizes dot
        // segments, so checking only AbsolutePath would accept traversal-shaped input.
        if (uri.AbsolutePath == "/data-license")
            return uri.OriginalString == $"{Origin}/data-license";

        if (uri.AbsolutePath == "/radar" && uri.Query.Length == 0)
            return uri.OriginalString == $"{Origin}/radar";

        var parameters = uri.Query.TrimStart('?').Split('&');
        if (parameters.Length == 0 || !parameters[0].StartsWith("axis=", StringComparison.Ordinal) ||
            !TryGetCategory(parameters[0][5..], out var category))
            return false;

        if (uri.AbsolutePath == "/radar")
            return parameters.Length == 1 && uri.OriginalString == BuildFullRankingUri(category).AbsoluteUri;

        const string modelPathPrefix = "/models/";
        if (!uri.AbsolutePath.StartsWith(modelPathPrefix, StringComparison.Ordinal) ||
            parameters.Length != 2 || !parameters[1].StartsWith("config=", StringComparison.Ordinal))
            return false;

        var model = Uri.UnescapeDataString(uri.AbsolutePath[modelPathPrefix.Length..]);
        var configuration = Uri.UnescapeDataString(parameters[1][7..]);
        var prefix = model + "~";
        if (!configuration.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var expected = BuildModelDetailUri(model, configuration[prefix.Length..], category);
        return expected is not null && uri.OriginalString == expected.AbsoluteUri;
    }

    // A published ID may include provider namespaces and variants; encode the entire validated
    // ID below the /models/ route instead of letting its slashes form URL segments.
    private static bool IsModelIdentifier(string? value) =>
        !string.IsNullOrEmpty(value) && value.Split('/', ':').All(segment => IsIdentifier(segment, allowDots: true));

    private static bool IsIdentifier(string? value, bool allowDots) =>
        !string.IsNullOrEmpty(value) && value is not "." and not ".." &&
        value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' ||
            (allowDots && character == '.'));

    private static string GetAxis(ModelRadarCategory category) => category switch
    {
        ModelRadarCategory.Backend => "backend",
        ModelRadarCategory.Frontend => "frontend",
        ModelRadarCategory.Knowledge => "knowledge",
        ModelRadarCategory.Aesthetic => "aesthetic",
        ModelRadarCategory.Maintainability => "maintainability",
        _ => "overall"
    };

    private static bool TryGetCategory(string axis, out ModelRadarCategory category)
    {
        category = axis switch
        {
            "overall" => ModelRadarCategory.Overall,
            "backend" => ModelRadarCategory.Backend,
            "frontend" => ModelRadarCategory.Frontend,
            "knowledge" => ModelRadarCategory.Knowledge,
            "aesthetic" => ModelRadarCategory.Aesthetic,
            "maintainability" => ModelRadarCategory.Maintainability,
            _ => (ModelRadarCategory)(-1)
        };
        return category != (ModelRadarCategory)(-1);
    }
}
