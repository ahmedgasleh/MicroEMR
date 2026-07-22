namespace MicroEMR.Application.PatientProblems;

public sealed class PatientProblemResolvedException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
