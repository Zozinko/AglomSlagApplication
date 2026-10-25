namespace AglomSlagServer.Errors;

/// <summary>An expected failure with a deliberately public, safe message.</summary>
public sealed class ApiException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class PublicErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string ReferenceNotFound = "REFERENCE_NOT_FOUND";
    public const string ReferenceConflict = "REFERENCE_CONFLICT";
    public const string ImportProtected = "IMPORT_PROTECTED";
    public const string Forbidden = "FORBIDDEN";
    public const string FileError = "FILE_ERROR";
    public const string CalculationFailed = "CALCULATION_FAILED";
    public const string PresetUnsupported = "PRESET_NOT_SUPPORTED";
    public const string InternalError = "INTERNAL_ERROR";
}
