namespace Quipu.ParameterizationExtractor.Desktop.Services.Workspace;

/// <summary>
/// Source-database connection metadata stored inside the workspace file.
/// <para>
/// <c>PasswordEncrypted</c> is a <b>placeholder</b> in this feature — the round-trip-as-null
/// behaviour is pinned by <c>WorkspaceStoreTests</c>. The DPAPI encryption / opt-out flow
/// lands in the upcoming <c>desktop-connection-management</c> feature, which will own the
/// ADR for password-at-rest semantics.
/// </para>
/// </summary>
internal sealed class WorkspaceSource
{
    public string Server { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    /// <summary>One of <c>"windows"</c> or <c>"sql"</c>. Validated on load.</summary>
    public string Auth { get; set; } = "windows";

    /// <summary>SQL-auth user; ignored when <see cref="Auth"/> is <c>"windows"</c>.</summary>
    public string? User { get; set; }

    /// <summary>DPAPI-encrypted password (base64). Placeholder in this feature; always <c>null</c> until <c>desktop-connection-management</c> lands.</summary>
    public string? PasswordEncrypted { get; set; }
}
