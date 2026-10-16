// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa;

/// <summary>
/// Environment variable names that <see cref="Secrets"/> refuses to set from a secrets file.
/// </summary>
/// <remarks>
/// A project-layer <c>.nooa/secrets.yaml</c> can come from a cloned repository, so it is untrusted.
/// These names change how the process loads code, runs commands, trusts certificates or chooses the host that receives an API key.
/// Matching ignores case. This is defence in depth for every layer. The project layer has a stricter rule
/// (credential-shaped names only, see <see cref="IsCredentialShaped"/>). This is a deliberate deviation from upstream, which sets any name.
/// </remarks>
internal static class SecretEnvDenylist
{
    private static readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase)
    {
        "PATH", "HOME", "EDITOR", "VISUAL", "PAGER", "LESSOPEN", "ZDOTDIR", "NODE_PATH", "PERLLIB", "SHELL", "ENV", "BASH_ENV", "IFS", "PS4", "PROMPT_COMMAND", "CDPATH", "TMPDIR",
        "NODE_OPTIONS", "NODE_EXTRA_CA_CERTS", "PYTHONPATH", "PYTHONSTARTUP", "PYTHONHOME", "PERL5OPT", "PERL5LIB", "RUBYOPT", "RUBYLIB",
        "JAVA_TOOL_OPTIONS", "_JAVA_OPTIONS", "JDK_JAVA_OPTIONS", "MSBUILDENABLEALLPROPERTYFUNCTIONS",
        "SSL_CERT_FILE", "SSL_CERT_DIR", "REQUESTS_CA_BUNDLE", "CURL_CA_BUNDLE", "SSLKEYLOGFILE",
        "ALL_PROXY", "NO_PROXY", "HTTP_PROXY", "HTTPS_PROXY",
    };

    private static readonly string[] _prefixes = ["LD_", "DYLD_", "DOTNET_", "COMPlus_", "COREHOST_", "CORECLR_", "NEMO_OO_", "NOOA_", "XDG_", "GIT_", "SSH_", "NPM_CONFIG_", "PIP_"];

    private static readonly string[] _suffixes = ["_PROXY", "_BASE_URL", "_API_BASE", "_BASE_URI", "_API_URL", "_ENDPOINT", "_HOST", "_URL"];

    private static readonly string[] _credentialSuffixes = ["_API_KEY", "_KEY", "_TOKEN", "_SECRET", "_PASSWORD"];

    /// <summary>
    /// Returns true when <paramref name="name"/> looks like a credential (ends with <c>_API_KEY</c>, <c>_KEY</c>,
    /// <c>_TOKEN</c>, <c>_SECRET</c> or <c>_PASSWORD</c>). Only such names are accepted from an untrusted project layer.
    /// </summary>
    public static bool IsCredentialShaped(string name) =>
        _credentialSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns true when <paramref name="name"/> must not be set from a secrets file.</summary>
    public static bool IsDenied(string name) =>
        _names.Contains(name)
        || _prefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase))
        || _suffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase));
}
