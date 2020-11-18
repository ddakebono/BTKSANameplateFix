
using System;
using System.Linq;
using System.Reflection;
using UnhollowerRuntimeLib.XrefScans;
using MelonLoader;

public static class XrefScannerUtils
{

    /// <summary>
    ///     Scans global instances for the specific search-term
    /// </summary>
    /// <param name="methodBase">Method base to scan</param>
    /// <param name="searchTerm">What string the value should contain</param>
    /// <param name="ignoreCase">Ignore Casing?</param>
    /// <returns>if any of the instances contains the search-them</returns>
    public static bool XRefScanForGlobal(this MethodBase methodBase, string searchTerm, bool ignoreCase = true)
    {
        if (!string.IsNullOrEmpty(searchTerm))
            return XrefScanner.XrefScan(methodBase).Any(
                xref => xref.Type == XrefType.Global && xref.ReadAsObject()?.ToString().IndexOf(
                            searchTerm,
                            ignoreCase
                                ? StringComparison.OrdinalIgnoreCase
                                : StringComparison.Ordinal) >= 0);
        MelonLogger.LogError($"XRefScanForGlobal \"{methodBase}\" has an empty searchTerm. Returning false");
        return false;
    }

    /// <summary>
    ///     Scans method instances for the specific method-name and/or parent-type
    /// </summary>
    /// <param name="methodBase">Method base to scan</param>
    /// <param name="methodName">name of the method to scan for. can be null</param>
    /// <param name="parentType">type of the parent to scan for. can be null</param>
    /// <param name="ignoreCase">Ignore Casing?</param>
    /// <returns>if any of the instances contains the specified method-name/parent-type</returns>
    public static bool XRefScanForMethod(this MethodBase methodBase, string methodName = null, string parentType = null, bool ignoreCase = true)
    {
        if (!string.IsNullOrEmpty(methodName)
            || !string.IsNullOrEmpty(parentType))
            return XrefScanner.XrefScan(methodBase).Any(
                xref =>
                    {
                        if (xref.Type != XrefType.Method) return false;

                        var found = false;
                        MethodBase resolved = xref.TryResolve();
                        if (resolved == null) return false;

                        if (!string.IsNullOrEmpty(methodName))
                            found = !string.IsNullOrEmpty(resolved.Name) && resolved.Name.IndexOf(
                                        methodName,
                                        ignoreCase
                                            ? StringComparison.OrdinalIgnoreCase
                                            : StringComparison.Ordinal) >= 0;

                        if (!string.IsNullOrEmpty(parentType))
                            found = !string.IsNullOrEmpty(resolved.ReflectedType?.Name) && resolved.ReflectedType.Name.IndexOf(
                                        parentType,
                                        ignoreCase
                                            ? StringComparison
                                                .OrdinalIgnoreCase
                                            : StringComparison.Ordinal)
                                    >= 0;

                        return found;
                    });
        MelonLogger.LogError($"XRefScanForMethod \"{methodBase}\" has all null/empty parameters. Returning false");
        return false;
    }

    public static int XRefScanMethodCount(this MethodBase methodBase, string methodName = null, string parentType = null, bool ignoreCase = true)
    {
        if (!string.IsNullOrEmpty(methodName)
            || !string.IsNullOrEmpty(parentType))
            return XrefScanner.XrefScan(methodBase).Count(
                xref =>
                    {
                        if (xref.Type != XrefType.Method) return false;

                        var found = false;
                        MethodBase resolved = xref.TryResolve();
                        if (resolved == null) return false;

                        if (!string.IsNullOrEmpty(methodName))
                            found = !string.IsNullOrEmpty(resolved.Name) && resolved.Name.IndexOf(
                                        methodName,
                                        ignoreCase
                                            ? StringComparison.OrdinalIgnoreCase
                                            : StringComparison.Ordinal) >= 0;

                        if (!string.IsNullOrEmpty(parentType))
                            found = !string.IsNullOrEmpty(resolved.ReflectedType?.Name) && resolved.ReflectedType.Name.IndexOf(
                                        parentType,
                                        ignoreCase
                                            ? StringComparison
                                                .OrdinalIgnoreCase
                                            : StringComparison.Ordinal)
                                    >= 0;

                        return found;
                    });
        MelonLogger.LogError($"XRefScanMethodCount \"{methodBase}\" has all null/empty parameters. Returning -1");
        return -1;
    }

}
