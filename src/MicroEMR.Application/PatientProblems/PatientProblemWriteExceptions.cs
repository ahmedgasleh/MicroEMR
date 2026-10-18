namespace MicroEMR.Application.PatientProblems;

public sealed class PatientProblemConcurrencyException : Exception;
public sealed class PatientProblemValidationException(string message) : Exception(message);
public sealed class PatientProblemNotFoundException : Exception;
