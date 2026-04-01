namespace Kava.Desktop;

internal static class DesktopInputNormalizer
{
    internal static bool TryNormalizeCalDavServerUrl(string serverUrl, out string normalizedUrl, out string error)
    {
        normalizedUrl = string.Empty;

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var serverUri)
            || !string.Equals(serverUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Please enter a valid HTTPS server URL.";
            return false;
        }

        normalizedUrl = serverUri.ToString().TrimEnd('/');
        error = string.Empty;
        return true;
    }

    internal static bool TryNormalizeSubscriptionUrl(string icsUrl, out string normalizedUrl, out string error)
    {
        normalizedUrl = icsUrl.Trim();
        if (normalizedUrl.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
            normalizedUrl = "https://" + normalizedUrl[9..];

        if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            error = "Please enter a valid URL (https:// or webcal://).";
            normalizedUrl = string.Empty;
            return false;
        }

        error = string.Empty;
        return true;
    }
}