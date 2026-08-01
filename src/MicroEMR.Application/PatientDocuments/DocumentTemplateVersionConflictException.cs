namespace MicroEMR.Application.PatientDocuments;

public sealed class DocumentTemplateVersionConflictException(
    string message,
    Exception? innerException = null) : Exception(message, innerException);
