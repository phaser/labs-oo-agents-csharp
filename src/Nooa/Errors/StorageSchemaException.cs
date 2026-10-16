// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Errors;

/// <summary>
/// A session database does not have the expected schema, or it holds objects that this program did not create
/// (for example triggers or views). The file is refused.
/// </summary>
public class StorageSchemaException : NooaException
{
    public StorageSchemaException(string message)
        : base(message)
    {
    }
}
