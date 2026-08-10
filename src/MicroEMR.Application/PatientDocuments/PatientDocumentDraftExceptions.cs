namespace MicroEMR.Application.PatientDocuments;

public sealed class PatientDocumentConcurrencyException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);

public sealed class PatientDocumentNotDraftException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);
