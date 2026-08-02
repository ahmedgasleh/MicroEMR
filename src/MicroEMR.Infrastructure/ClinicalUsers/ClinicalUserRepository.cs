using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Application.ClinicalUsers;
using MicroEMR.Infrastructure.Tenancy;

namespace MicroEMR.Infrastructure.ClinicalUsers;

public sealed class ClinicalUserRepository(
    ITenantSqlConnectionFactory connectionFactory) : IClinicalUserRepository
{
    public async Task<ClinicalUser?> GetByAuthSubjectIdAsync(
        string authSubjectId,
        CancellationToken cancellationToken = default)
    {
        ValidateSubject(authSubjectId);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.ApplicationUser_GetByAuthSubjectId");
        AddSubject(command, authSubjectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
    }

    public async Task<ClinicalUser> SetAuthSubjectIdAsync(
        long userId,
        string authSubjectId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
        ValidateSubject(authSubjectId);
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = CreateCommand(connection, "dbo.ApplicationUser_SetAuthSubjectId");
        command.Parameters.Add("@UserId", SqlDbType.BigInt).Value = userId;
        AddSubject(command, authSubjectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("The clinical user mapping operation returned no user.");
        return Map(reader);
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string name) =>
        new(name, connection) { CommandType = CommandType.StoredProcedure };

    private static void AddSubject(SqlCommand command, string authSubjectId) =>
        command.Parameters.Add("@AuthSubjectId", SqlDbType.NVarChar, 450).Value = authSubjectId;

    private static void ValidateSubject(string authSubjectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authSubjectId);
        if (authSubjectId.Length > 450)
            throw new ArgumentOutOfRangeException(nameof(authSubjectId));
        if (!string.Equals(authSubjectId, authSubjectId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Auth subject must not contain leading or trailing whitespace.", nameof(authSubjectId));
    }

    private static ClinicalUser Map(SqlDataReader reader) => new(
        reader.GetInt64(reader.GetOrdinal("UserId")),
        reader.GetGuid(reader.GetOrdinal("UserUid")),
        reader.GetString(reader.GetOrdinal("Username")),
        reader.GetString(reader.GetOrdinal("DisplayName")),
        reader.GetBoolean(reader.GetOrdinal("IsActive")),
        reader.GetString(reader.GetOrdinal("AuthSubjectId")));
}
