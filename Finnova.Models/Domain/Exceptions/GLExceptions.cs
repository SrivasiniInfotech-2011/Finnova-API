namespace Finnova.Models.Domain.Exceptions;

/// <summary>GL account not found by id/company. Maps to ERR-GL-404.</summary>
public class GLNotFoundException : Exception
{
    public const string ErrorCode = "ERR-GL-404";
    public GLNotFoundException() : base("GL account was not found.") { }
    public GLNotFoundException(string message) : base(message) { }
}

/// <summary>Create with a code already in use (non-Inactive). Maps to ERR-GL-409 (R2.3).</summary>
public class GLDuplicateCodeException : Exception
{
    public const string ErrorCode = "ERR-GL-409";
    public GLDuplicateCodeException() : base("GL code must be unique") { }
}

/// <summary>Create reusing a retired (Inactive) code. Maps to ERR-GL-409 (R2.4).</summary>
public class GLRetiredCodeException : Exception
{
    public const string ErrorCode = "ERR-GL-409";
    public GLRetiredCodeException() : base("GL code has been retired and cannot be reused") { }
}

/// <summary>Create/update with a name already in use. Maps to ERR-GL-409 (R2.5).</summary>
public class GLDuplicateNameException : Exception
{
    public const string ErrorCode = "ERR-GL-409";
    public GLDuplicateNameException() : base("GL name must be unique") { }
}

/// <summary>
/// Governance/lifecycle conflict: self-authorization (R3.3/R4.6/R5.12), structural lock once
/// postings exist (R4.2), modification already pending (R4.4), lifecycle pre-condition failure
/// (R5.4-R5.7), delete of an ever-authorized GL (R5.9), duplicate active system-GL mapping
/// (R7.7), unmap while operational (R7.5), audit immutability (R10.4), not-awaiting-authorization
/// (R3.10). Maps to ERR-GL-409.
/// </summary>
public class GLConflictException : Exception
{
    public const string ErrorCode = "ERR-GL-409";
    public GLConflictException(string message) : base(message) { }
}
