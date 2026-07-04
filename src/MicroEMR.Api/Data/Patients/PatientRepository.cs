using System.Data;
using Microsoft.Data.SqlClient;
using MicroEMR.Api.Contracts.Patients;

namespace MicroEMR.Api.Data.Patients;

public sealed class PatientRepository : IPatientRepository
{
    private readonly string _connectionString;
    private readonly ILogger<PatientRepository> _logger;

    public PatientRepository (
        IConfiguration configuration,
        ILogger<PatientRepository> logger )
    {
        _connectionString =
            configuration.GetConnectionString("MicroEmrDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'MicroEmrDatabase' was not found.");

        _logger = logger;
    }

    public async Task<PatientSearchResponse> SearchAsync (
        string? searchText,
        DateOnly? dateOfBirth,
        int pageNumber,
        int pageSize,
        bool includeInactive,
        CancellationToken cancellationToken = default )
    {
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var items = new List<PatientListItemResponse>();

        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand("dbo.Patient_Search", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        AddNullableString(
            command,
            "@SearchText",
            SqlDbType.NVarChar,
            200,
            searchText);

        command.Parameters.Add(
            new SqlParameter("@DateOfBirth", SqlDbType.Date)
            {
                Value = dateOfBirth.HasValue
                    ? dateOfBirth.Value.ToDateTime(TimeOnly.MinValue)
                    : DBNull.Value
            });

        command.Parameters.Add(
            new SqlParameter("@PageNumber", SqlDbType.Int)
            {
                Value = pageNumber
            });

        command.Parameters.Add(
            new SqlParameter("@PageSize", SqlDbType.Int)
            {
                Value = pageSize
            });

        command.Parameters.Add(
            new SqlParameter("@IncludeInactive", SqlDbType.Bit)
            {
                Value = includeInactive
            });

        await connection.OpenAsync(cancellationToken);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapListItem(reader));
        }

        var totalRows =
            items.Count > 0
                ? items [0].TotalRows
                : 0;

        return new PatientSearchResponse
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRows = totalRows
        };
    }

    public async Task<PatientDetailsResponse?> GetByUidAsync (
        Guid patientUid,
        CancellationToken cancellationToken = default )
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand("dbo.Patient_GetByUid", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        command.Parameters.Add(
            new SqlParameter(
                "@PatientUid",
                SqlDbType.UniqueIdentifier)
            {
                Value = patientUid
            });

        await connection.OpenAsync(cancellationToken);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapDetails(reader);
    }

    public async Task<PatientDetailsResponse> CreateAsync (
        CreatePatientRequest request,
        long? createdBy,
        CancellationToken cancellationToken = default )
    {
        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand("dbo.Patient_Create", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        AddNullableString(
            command,
            "@HealthCardNumber",
            SqlDbType.NVarChar,
            50,
            request.HealthCardNumber);

        AddNullableString(
            command,
            "@HealthCardVersion",
            SqlDbType.NVarChar,
            10,
            request.HealthCardVersion);

        AddRequiredString(
            command,
            "@FirstName",
            SqlDbType.NVarChar,
            100,
            request.FirstName);

        AddNullableString(
            command,
            "@MiddleName",
            SqlDbType.NVarChar,
            100,
            request.MiddleName);

        AddRequiredString(
            command,
            "@LastName",
            SqlDbType.NVarChar,
            100,
            request.LastName);

        command.Parameters.Add(
            new SqlParameter("@DateOfBirth", SqlDbType.Date)
            {
                Value = request.DateOfBirth!.Value
                    .ToDateTime(TimeOnly.MinValue)
            });

        AddNullableString(
            command,
            "@SexAtBirth",
            SqlDbType.NVarChar,
            20,
            request.SexAtBirth);

        AddNullableString(
            command,
            "@GenderIdentity",
            SqlDbType.NVarChar,
            50,
            request.GenderIdentity);

        AddNullableString(
            command,
            "@PreferredName",
            SqlDbType.NVarChar,
            100,
            request.PreferredName);

        AddNullableString(
            command,
            "@PhoneNumber",
            SqlDbType.NVarChar,
            30,
            request.PhoneNumber);

        AddNullableString(
            command,
            "@AlternatePhoneNumber",
            SqlDbType.NVarChar,
            30,
            request.AlternatePhoneNumber);

        AddNullableString(
            command,
            "@Email",
            SqlDbType.NVarChar,
            255,
            request.Email);

        AddNullableString(
            command,
            "@AddressLine1",
            SqlDbType.NVarChar,
            255,
            request.AddressLine1);

        AddNullableString(
            command,
            "@AddressLine2",
            SqlDbType.NVarChar,
            255,
            request.AddressLine2);

        AddNullableString(
            command,
            "@City",
            SqlDbType.NVarChar,
            100,
            request.City);

        AddNullableString(
            command,
            "@Province",
            SqlDbType.NVarChar,
            50,
            request.Province);

        AddNullableString(
            command,
            "@PostalCode",
            SqlDbType.NVarChar,
            20,
            request.PostalCode);

        AddRequiredString(
            command,
            "@CountryCode",
            SqlDbType.Char,
            2,
            string.IsNullOrWhiteSpace(request.CountryCode)
                ? "CA"
                : request.CountryCode);

        command.Parameters.Add(
            new SqlParameter("@CreatedBy", SqlDbType.BigInt)
            {
                Value = createdBy.HasValue
                    ? createdBy.Value
                    : DBNull.Value
            });

        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    "Patient_Create returned no patient record.");
            }

            return MapDetails(reader);
        }
        catch (SqlException exception)
        {
            _logger.LogError(
                exception,
                "Failed to create patient {FirstName} {LastName}.",
                request.FirstName,
                request.LastName);

            throw;
        }
    }

    public async Task<PatientDetailsResponse?> UpdateDemographicsAsync(
        Guid patientUid,
        UpdatePatientDemographicsRequest request,
        long? updatedBy,
        CancellationToken cancellationToken = default)
    {
        byte[] rowVersion;

        try
        {
            rowVersion = Convert.FromBase64String(request.RowVersion);
        }
        catch (FormatException exception)
        {
            _logger.LogWarning(
                exception,
                "Invalid row version supplied for patient {PatientUid}.",
                patientUid);

            throw new PatientDemographicsConcurrencyException();
        }

        await using var connection =
            new SqlConnection(_connectionString);

        await using var command =
            new SqlCommand("dbo.Patient_UpdateDemographics", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

        command.Parameters.Add(
            new SqlParameter(
                "@PatientUid",
                SqlDbType.UniqueIdentifier)
            {
                Value = patientUid
            });

        AddRequiredString(
            command,
            "@FirstName",
            SqlDbType.NVarChar,
            100,
            request.FirstName);

        AddNullableString(
            command,
            "@MiddleName",
            SqlDbType.NVarChar,
            100,
            request.MiddleName);

        AddRequiredString(
            command,
            "@LastName",
            SqlDbType.NVarChar,
            100,
            request.LastName);

        AddNullableString(
            command,
            "@PreferredName",
            SqlDbType.NVarChar,
            100,
            request.PreferredName);

        command.Parameters.Add(
            new SqlParameter("@DateOfBirth", SqlDbType.Date)
            {
                Value = request.DateOfBirth!.Value
                    .ToDateTime(TimeOnly.MinValue)
            });

        AddNullableString(
            command,
            "@SexAtBirth",
            SqlDbType.NVarChar,
            20,
            request.SexAtBirth);

        AddNullableString(
            command,
            "@GenderIdentity",
            SqlDbType.NVarChar,
            50,
            request.GenderIdentity);

        AddNullableString(
            command,
            "@HealthCardNumber",
            SqlDbType.NVarChar,
            50,
            request.HealthCardNumber);

        AddNullableString(
            command,
            "@HealthCardVersion",
            SqlDbType.NVarChar,
            10,
            request.HealthCardVersion);

        AddNullableString(
            command,
            "@PhoneNumber",
            SqlDbType.NVarChar,
            30,
            request.PhoneNumber);

        AddNullableString(
            command,
            "@AlternatePhoneNumber",
            SqlDbType.NVarChar,
            30,
            request.AlternatePhoneNumber);

        AddNullableString(
            command,
            "@Email",
            SqlDbType.NVarChar,
            255,
            request.Email);

        AddNullableString(
            command,
            "@AddressLine1",
            SqlDbType.NVarChar,
            255,
            request.AddressLine1);

        AddNullableString(
            command,
            "@AddressLine2",
            SqlDbType.NVarChar,
            255,
            request.AddressLine2);

        AddNullableString(
            command,
            "@City",
            SqlDbType.NVarChar,
            100,
            request.City);

        AddNullableString(
            command,
            "@Province",
            SqlDbType.NVarChar,
            50,
            request.Province);

        AddNullableString(
            command,
            "@PostalCode",
            SqlDbType.NVarChar,
            20,
            request.PostalCode);

        AddRequiredString(
            command,
            "@CountryCode",
            SqlDbType.Char,
            2,
            string.IsNullOrWhiteSpace(request.CountryCode)
                ? "CA"
                : request.CountryCode);

        command.Parameters.Add(
            new SqlParameter("@IsActive", SqlDbType.Bit)
            {
                Value = request.IsActive
            });

        command.Parameters.Add(
            new SqlParameter("@UpdatedBy", SqlDbType.BigInt)
            {
                Value = updatedBy.HasValue
                    ? updatedBy.Value
                    : DBNull.Value
            });

        command.Parameters.Add(
            new SqlParameter("@RowVersion", SqlDbType.VarBinary, 8)
            {
                Value = rowVersion
            });

        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return MapDetails(reader);
        }
        catch (SqlException exception)
            when (exception.Number == 51020)
        {
            return null;
        }
        catch (SqlException exception)
            when (exception.Number == 51021)
        {
            throw new PatientDemographicsConcurrencyException();
        }
        catch (SqlException exception)
        {
            _logger.LogError(
                exception,
                "Failed to update demographics for patient {PatientUid}.",
                patientUid);

            throw;
        }
    }

    private static PatientListItemResponse MapListItem (
        SqlDataReader reader )
    {
        return new PatientListItemResponse
        {
            PatientUid =
                reader.GetGuid(reader.GetOrdinal("PatientUid")),

            ChartNumber =
                reader.GetString(reader.GetOrdinal("ChartNumber")),

            FirstName =
                reader.GetString(reader.GetOrdinal("FirstName")),

            MiddleName =
                GetNullableString(reader, "MiddleName"),

            LastName =
                reader.GetString(reader.GetOrdinal("LastName")),

            PreferredName =
                GetNullableString(reader, "PreferredName"),

            DateOfBirth =
                DateOnly.FromDateTime(
                    reader.GetDateTime(
                        reader.GetOrdinal("DateOfBirth"))),

            SexAtBirth =
                GetNullableString(reader, "SexAtBirth"),

            HealthCardNumber =
                GetNullableString(reader, "HealthCardNumber"),

            HealthCardVersion =
                GetNullableString(reader, "HealthCardVersion"),

            PhoneNumber =
                GetNullableString(reader, "PhoneNumber"),

            Email =
                GetNullableString(reader, "Email"),

            IsActive =
                reader.GetBoolean(reader.GetOrdinal("IsActive")),

            TotalRows =
                reader.GetInt32(reader.GetOrdinal("TotalRows"))
        };
    }

    private static PatientDetailsResponse MapDetails (
        SqlDataReader reader )
    {
        var rowVersionOrdinal =
            reader.GetOrdinal("RowVersion");

        var rowVersion =
            (byte [])reader.GetValue(rowVersionOrdinal);

        return new PatientDetailsResponse
        {
            PatientUid =
                reader.GetGuid(reader.GetOrdinal("PatientUid")),

            ChartNumber =
                reader.GetString(reader.GetOrdinal("ChartNumber")),

            HealthCardNumber =
                GetNullableString(reader, "HealthCardNumber"),

            HealthCardVersion =
                GetNullableString(reader, "HealthCardVersion"),

            FirstName =
                reader.GetString(reader.GetOrdinal("FirstName")),

            MiddleName =
                GetNullableString(reader, "MiddleName"),

            LastName =
                reader.GetString(reader.GetOrdinal("LastName")),

            DateOfBirth =
                DateOnly.FromDateTime(
                    reader.GetDateTime(
                        reader.GetOrdinal("DateOfBirth"))),

            SexAtBirth =
                GetNullableString(reader, "SexAtBirth"),

            GenderIdentity =
                GetNullableString(reader, "GenderIdentity"),

            PreferredName =
                GetNullableString(reader, "PreferredName"),

            PhoneNumber =
                GetNullableString(reader, "PhoneNumber"),

            AlternatePhoneNumber =
                GetNullableString(
                    reader,
                    "AlternatePhoneNumber"),

            Email =
                GetNullableString(reader, "Email"),

            AddressLine1 =
                GetNullableString(reader, "AddressLine1"),

            AddressLine2 =
                GetNullableString(reader, "AddressLine2"),

            City =
                GetNullableString(reader, "City"),

            Province =
                GetNullableString(reader, "Province"),

            PostalCode =
                GetNullableString(reader, "PostalCode"),

            CountryCode =
                reader.GetString(reader.GetOrdinal("CountryCode")),

            IsActive =
                reader.GetBoolean(reader.GetOrdinal("IsActive")),

            CreatedAt =
                reader.GetDateTime(reader.GetOrdinal("CreatedAt")),

            UpdatedAt =
                GetNullableDateTime(reader, "UpdatedAt"),

            RowVersion =
                Convert.ToBase64String(rowVersion)
        };
    }

    private static void AddRequiredString (
        SqlCommand command,
        string parameterName,
        SqlDbType sqlDbType,
        int size,
        string value )
    {
        command.Parameters.Add(
            new SqlParameter(parameterName, sqlDbType, size)
            {
                Value = value.Trim()
            });
    }

    private static void AddNullableString (
        SqlCommand command,
        string parameterName,
        SqlDbType sqlDbType,
        int size,
        string? value )
    {
        command.Parameters.Add(
            new SqlParameter(parameterName, sqlDbType, size)
            {
                Value = string.IsNullOrWhiteSpace(value)
                    ? DBNull.Value
                    : value.Trim()
            });
    }

    private static string? GetNullableString (
        SqlDataReader reader,
        string columnName )
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetString(ordinal);
    }

    private static DateTime? GetNullableDateTime (
        SqlDataReader reader,
        string columnName )
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : reader.GetDateTime(ordinal);
    }
}
