// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Nooa.Errors;

namespace Nooa.Config;

/// <summary>
/// Base for configuration records that merge by explicitly set fields.
/// </summary>
/// <remarks>
/// <para>
/// Upstream uses the pydantic <c>model_fields_set</c> set: a merge copies only the fields the caller set,
/// so a default value does not count, and a value of <c>null</c> that the caller set still counts.
/// This base keeps that set.
/// </para>
/// <para>
/// Pattern for a derived record. Each property writes its value and then calls <see cref="MarkSet"/>:
/// <c>public int MaxRetries { get; init { field = value; MarkSet(); } } = 3;</c>.
/// The property initializer writes the backing field directly, so a default is not marked as set.
/// </para>
/// <para>
/// Object initializers run after the constructor, so a config is validated by an explicit call to
/// <see cref="Validate"/>, not at construction.
/// </para>
/// </remarks>
public abstract record ConfigBase
{
    private readonly HashSet<string> _fieldsSet;

    protected ConfigBase()
    {
        _fieldsSet = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Copy constructor. A copy made by <c>with</c> gets its own set, so setting a field on the copy
    /// does not change the original.
    /// </summary>
    protected ConfigBase(ConfigBase original)
    {
        _fieldsSet = new HashSet<string>(original._fieldsSet, StringComparer.Ordinal);
    }

    /// <summary>The names of the properties that the caller set explicitly.</summary>
    public IReadOnlyCollection<string> FieldsSet => _fieldsSet;

    /// <summary>Returns true when the caller set the named property explicitly.</summary>
    public bool IsSet(string propertyName) => _fieldsSet.Contains(propertyName);

    /// <summary>
    /// Throws <see cref="ConfigValidationException"/> when a value is out of range.
    /// The default implementation accepts every value.
    /// </summary>
    public virtual void Validate()
    {
    }

    /// <summary>The set of explicitly set fields does not take part in value equality.</summary>
    public virtual bool Equals(ConfigBase? other) => other is not null && EqualityContract == other.EqualityContract;

    public override int GetHashCode() => EqualityContract.GetHashCode();

    /// <summary>Marks a property as explicitly set. Call it from the property <c>init</c> accessor.</summary>
    protected void MarkSet([CallerMemberName] string propertyName = "") => _fieldsSet.Add(propertyName);

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when <paramref name="other"/> has no explicitly set field.
    /// </summary>
    protected static void RequireFieldsSet<T>(T other)
        where T : ConfigBase
    {
        if (other.FieldsSet.Count == 0)
        {
            throw new ArgumentException(
                "MergeWith() received a config with no fields set. "
                + $"Config objects must be freshly constructed with explicit values: {typeof(T).Name} {{ Field = value }}.",
                nameof(other));
        }
    }

    /// <summary>
    /// Copies every explicitly set property of <paramref name="other"/> onto <paramref name="target"/>
    /// and returns <paramref name="target"/>. Pass a fresh copy (<c>this with { }</c>) as the target.
    /// Properties named in <paramref name="skip"/> are left alone.
    /// </summary>
    protected static T CopySetFields<T>(T target, T other, params string[] skip)
        where T : ConfigBase
    {
        foreach (var name in other._fieldsSet)
        {
            if (Array.IndexOf(skip, name) >= 0)
            {
                continue;
            }

            var property = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"{typeof(T).Name} has no property '{name}'.");
            property.SetValue(target, property.GetValue(other));
        }

        return target;
    }

    /// <summary>Throws <see cref="ConfigValidationException"/> with the upstream multi-line message when there are errors.</summary>
    protected static void ThrowIfInvalid(string configName, IReadOnlyCollection<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new ConfigValidationException(
                $"Invalid {configName}:\n" + string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }

    /// <summary>Formats an integer with thousands separators, as the upstream messages do.</summary>
    protected static string Group(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
