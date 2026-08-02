namespace MicroEMR.Application.ClinicalUsers;

public sealed record ClinicalUser(
    long UserId,
    Guid UserUid,
    string Username,
    string DisplayName,
    bool IsActive,
    string AuthSubjectId);
