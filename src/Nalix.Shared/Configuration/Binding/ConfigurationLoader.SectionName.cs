namespace Nalix.Shared.Configuration.Binding;

public partial class ConfigurationLoader
{
    /// <summary>
    /// Gets the section name for a configuration type, with caching for performance.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String GetSectionName(System.Type type)
        => _sectionNameCache.GetOrAdd(type, t =>
        {
            System.String section = t.Name;

            foreach (System.String suffix in System.Linq.Enumerable.OrderByDescending(
                         _suffixesToTrim, s => s.Length))
            {
                if (section.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                {
                    section = section[..^suffix.Length];
                    break;
                }
            }

            return Capitalize(section);
        });

    /// <summary>
    /// Capitalizes the first letter of a string.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static System.String Capitalize(System.String input)
        => System.String.IsNullOrEmpty(input) ? input :
           System.Char.ToUpperInvariant(input[0]) + input[1..];
}
