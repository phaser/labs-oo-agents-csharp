// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa;

/// <summary>
/// Decides whether a layered config file is trusted. A project-layer file can come from a cloned repository, so it is not.
/// </summary>
/// <remarks>
/// Secrets loading and the LLM model registry share this one rule, so the two cannot drift apart.
/// </remarks>
internal static class LayerTrust
{
    /// <summary>
    /// Fails closed: a file is trusted only when it is the user-layer file, or a path from <paramref name="envVar"/> that lies
    /// outside the project directory and the project root. The user-layer file is trusted even when the project root
    /// contains the user directory. Paths compare with the platform rule in <see cref="NooaPaths.PathComparison"/>.
    /// </summary>
    /// <param name="canonicalPath">The canonical path of the layer file.</param>
    /// <param name="fileName">The bare name of the config file, used to find the user-layer file.</param>
    /// <param name="envVar">The environment variable with override paths.</param>
    public static bool IsTrusted(string canonicalPath, string fileName, string envVar)
    {
        if (NooaPaths.PathEquals(canonicalPath, NooaPaths.Canonicalize(NooaPaths.GetUserDir(fileName))))
        {
            return true;
        }

        var fromEnv = LayeredConfig.EnvPaths(envVar).Any(p => NooaPaths.PathEquals(canonicalPath, NooaPaths.Canonicalize(p)));
        return fromEnv
            && !NooaPaths.IsInside(canonicalPath, NooaPaths.Canonicalize(NooaPaths.GetProjectDir()))
            && !NooaPaths.IsInside(canonicalPath, NooaPaths.Canonicalize(NooaPaths.FindProjectRoot()));
    }
}
