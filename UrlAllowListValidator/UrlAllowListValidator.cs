using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public class UrlAllowListValidator
{
    private readonly List<AllowListEntry> _allowList;

    // Constructor for list-based allow list
    public UrlAllowListValidator(IEnumerable<string> allowList)
    {
        if (allowList == null)
            throw new ArgumentNullException(nameof(allowList));

        _allowList = allowList
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Select(ParseEntry)
            .ToList();

        if (!_allowList.Any())
            throw new ArgumentException("Allow list cannot be empty.");
    }

    // Constructor for comma-delimited string
    public UrlAllowListValidator(string commaDelimitedAllowList)
    {
        if (string.IsNullOrWhiteSpace(commaDelimitedAllowList))
            throw new ArgumentException("Comma-delimited allow list cannot be empty.");

        var entries = commaDelimitedAllowList
            .Split(',')
            .Select(entry => entry.Trim())
            .Where(entry => !string.IsNullOrEmpty(entry))
            .ToList();

        if (!entries.Any())
            throw new ArgumentException("No valid entries found in comma-delimited allow list.");

        _allowList = entries.Select(ParseEntry).ToList();
    }

    public bool IsUrlAllowed(string userUrl)
    {
        if (string.IsNullOrWhiteSpace(userUrl))
            return false;

        // Normalize the user URL
        if (!NormalizeUrl(userUrl, out var normalizedUri))
            return false;

        return _allowList.Any(entry => MatchesEntry(normalizedUri, entry));
    }

    private bool NormalizeUrl(string url, out Uri normalizedUri)
    {
        normalizedUri = null;

        // Step 1: Decode percent-encoded characters
        string decodedUrl;
        try
        {
            decodedUrl = Uri.UnescapeDataString(url);
        }
        catch
        {
            return false; // Invalid encoding
        }

        // Step 2: Create URI and perform initial validation
        if (!Uri.TryCreate(decodedUrl, UriKind.Absolute, out var uri))
            return false;

        // Step 3: Normalize scheme and host (lowercase)
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant()
        };

        // Step 4: Normalize path (handle ../, ./, multiple slashes)
        string normalizedPath = NormalizePath(builder.Path);
        if (normalizedPath == null)
            return false; // Invalid path

        builder.Path = normalizedPath;

        // Step 5: Remove default ports for http, https, ws, wss
        if ((builder.Port == 80 && (builder.Scheme == "http" || builder.Scheme == "ws")) ||
            (builder.Port == 443 && (builder.Scheme == "https" || builder.Scheme == "wss")))
        {
            builder.Port = -1; // Use -1 to indicate default port
        }

        // Step 6: Remove fragment and normalize query
        builder.Fragment = "";
        if (!string.IsNullOrEmpty(builder.Query))
        {
            builder.Query = builder.Query.TrimStart('?');
        }

        normalizedUri = builder.Uri;
        return true;
    }

    private AllowListEntry ParseEntry(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            throw new ArgumentException("Allow list entry cannot be empty.");

        // If entry doesn't start with a scheme, treat it as a hostname
        if (!entry.Contains("://"))
        {
            // Normalize hostname
            string normalizedHost = entry.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedHost))
                throw new ArgumentException($"Invalid hostname in allow list: {entry}");

            return new AllowListEntry
            {
                Host = normalizedHost,
                IsHostOnly = true
            };
        }

        // Normalize the full URL
        if (!NormalizeUrl(entry, out var uri))
            throw new ArgumentException($"Invalid URI in allow list: {entry}");

        return new AllowListEntry
        {
            Scheme = uri.Scheme,
            Host = uri.Host,
            Port = uri.IsDefaultPort ? null : (int?)uri.Port,
            Path = uri.AbsolutePath == "/" ? null : uri.AbsolutePath,
            IsHostOnly = false
        };
    }

    private bool MatchesEntry(Uri userUri, AllowListEntry entry)
    {
        // Host-only entries allow any scheme, port, or path
        if (entry.IsHostOnly)
        {
            return string.Equals(userUri.Host, entry.Host, StringComparison.OrdinalIgnoreCase);
        }

        // Check scheme
        if (!string.Equals(userUri.Scheme, entry.Scheme, StringComparison.OrdinalIgnoreCase))
            return false;

        // Check host
        if (!string.Equals(userUri.Host, entry.Host, StringComparison.OrdinalIgnoreCase))
            return false;

        // Check port (if specified in allow list)
        if (entry.Port.HasValue && userUri.Port != entry.Port.Value)
            return false;

        // Check path (if specified in allow list)
        if (entry.Path != null)
        {
            var userPath = userUri.AbsolutePath == "/" ? "" : userUri.AbsolutePath;
            var entryPath = entry.Path.EndsWith("/") ? entry.Path : entry.Path + "/";
            return userPath.StartsWith(entryPath, StringComparison.OrdinalIgnoreCase) ||
                   userPath.Equals(entry.Path, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "/";

        // Collapse multiple slashes (e.g., // → /)
        path = Regex.Replace(path, "/{2,}", "/");

        // Split path into segments
        var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var normalizedSegments = new List<string>();

        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                // Remove the last segment if it exists
                if (normalizedSegments.Count > 0)
                    normalizedSegments.RemoveAt(normalizedSegments.Count - 1);
                else
                    return null; // Attempt to go above root
            }
            else if (segment != "." && !string.IsNullOrEmpty(segment))
            {
                // Ignore "." and empty segments
                normalizedSegments.Add(segment);
            }
        }

        // Reconstruct the path
        string normalizedPath = "/" + string.Join("/", normalizedSegments);
        return normalizedPath == "" ? "/" : normalizedPath;
    }

    private class AllowListEntry
    {
        public string Scheme { get; set; }
        public string Host { get; set; }
        public int? Port { get; set; }
        public string Path { get; set; }
        public bool IsHostOnly { get; set; }
    }
}