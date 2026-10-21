namespace Finnova.Models.Domain.Exceptions;

/// <summary>Targeted document id does not exist (R3.3, R5.2, R7.4).</summary>
public class DocumentNotFoundException : Exception
{
    public DocumentNotFoundException() : base("The document was not found.") { }
}

/// <summary>DocCode already exists (trim + case-insensitive) (R2.2).</summary>
public class DocumentDuplicateCodeException : Exception
{
    public const string ErrorCode = "ERR-DOC-409";
    public DocumentDuplicateCodeException()
        : base("A document with this Doc Code already exists.") { }
}

/// <summary>State conflict: no pending change, already pending, or active-delete (R3.5, R5.9, R7.3).</summary>
public class DocumentConflictException : Exception
{
    public DocumentConflictException(string message) : base(message) { }
}

/// <summary>Delete blocked because the document is in use by Customer Master (R7.1).</summary>
public class DocumentInUseException : Exception
{
    public const string ErrorCode = "ERR-DOC-002";
    public DocumentInUseException()
        : base("Document is in use and cannot be deleted. You can mark it Inactive") { }
}

/// <summary>Self-authorization prohibited: maker == checker (R5.5, R12.6).</summary>
public class DocumentSelfAuthorizationException : Exception
{
    public const string ErrorCode = "ERR-DOC-001";
    public DocumentSelfAuthorizationException()
        : base("You cannot authorize a record you created or edited") { }
}