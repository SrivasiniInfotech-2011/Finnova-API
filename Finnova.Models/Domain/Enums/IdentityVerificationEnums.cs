namespace Finnova.Models.Domain.Enums;

public enum VerificationOperationType { PanIdentifierValidation, AadhaarIdentifierValidation, PanNameVerification, AadhaarAddressVerification }
public enum ValidationStatus { Success, Failure, Unavailable }
public enum ComparisonOutcome { Match, NonMatch, NoUsableMatch }
public enum VerificationMethod { Auto, ManualOverride }
public enum VerificationWorkflowType { Single, PanAndName, AadhaarAndAddress, Full }
public enum WorkflowInputMode { Partial, Atomic }
public enum VerificationSourceType { None, DocumentFile, ExtractedValues, DocumentAndExtractedValues }
