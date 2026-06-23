namespace MicroEMR.Web.Authorization;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Physician = "Physician";
    public const string Nurse = "Nurse";
    public const string Receptionist = "Receptionist";
    public const string MedicalAssistant = "MedicalAssistant";

    public const string ClinicalStaff =
        Physician + "," +
        Nurse + "," +
        MedicalAssistant;

    public const string SchedulingStaff =
        Administrator + "," +
        Physician + "," +
        Nurse + "," +
        Receptionist + "," +
        MedicalAssistant;
}