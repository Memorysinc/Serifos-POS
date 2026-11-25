using System;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Dapper;
using FirewallControl.Core.Interfaces;
using MySqlConnector;

namespace FirewallControl.Infrastructure.Database;

/// <summary>
/// MySQL connection factory implementing IDbConnectionFactory.
/// Registers a global Dapper TypeHandler so VFP zero-dates ('0000-00-00'), MySqlDateTime, or NULLs
/// never fail when mapping to DateTime.
/// </summary>
public class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly IConfigService _configService;

    static MySqlConnectionFactory()
    {
        SqlMapper.AddTypeHandler(new VfpSafeDateTimeHandler());
    }

    public MySqlConnectionFactory(IConfigService configService)
    {
        _configService = configService;
    }

    private sealed class VfpSafeDateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.Value = value.Year < 1900 ? DateTime.Today : value;
        }

        public override DateTime Parse(object value)
        {
            if (value == null || value is DBNull)
                return DateTime.Today;

            if (value is DateTime dt)
                return dt.Year < 1900 ? DateTime.Today : dt;

            if (value is MySqlDateTime mdt)
                return mdt.IsValidDateTime ? mdt.GetDateTime() : DateTime.Today;

            if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed.Year < 1900 ? DateTime.Today : parsed;

            return DateTime.Today;
        }
    }

    public IDbConnection CreateConnection()
    {
        string connStr = _configService.GetMySqlConnectionString();
        try
        {
            var conn = new MySqlConnection(connStr);
            conn.Open();
            return conn;
        }
        catch
        {
            // Automatic fallback: if a saved LAN IP (e.g., 192.168.x.x) is unreachable on this PC,
            // try localhost so the Server PC works out of the box without manual reconfiguration.
            string currentHost = _configService.Config.SqlServerHost ?? "";
            if (!string.Equals(currentHost, "localhost", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(currentHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                string fallbackHost = "localhost";
                string fallbackStr = $"Server={fallbackHost};Port={_configService.Config.Port};Database={_configService.Config.BaseDatos};User ID={_configService.Config.SqlUser};Password={_configService.Config.SqlPassword};AllowZeroDateTime=False;ConvertZeroDateTime=True;ConnectionTimeout=3;DefaultCommandTimeout=30;CharacterSet=latin1;";
                var fallbackConn = new MySqlConnection(fallbackStr);
                fallbackConn.Open();
                _configService.Config.SqlServerHost = fallbackHost;
                return fallbackConn;
            }
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync(string? customConnectionString = null)
    {
        try
        {
            string connStr = string.IsNullOrWhiteSpace(customConnectionString)
                ? _configService.GetMySqlConnectionString()
                : customConnectionString;

            await using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            if (string.IsNullOrWhiteSpace(customConnectionString))
            {
                string currentHost = _configService.Config.SqlServerHost ?? "";
                if (!string.Equals(currentHost, "localhost", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(currentHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        string fallbackStr = $"Server=localhost;Port={_configService.Config.Port};Database={_configService.Config.BaseDatos};User ID={_configService.Config.SqlUser};Password={_configService.Config.SqlPassword};AllowZeroDateTime=False;ConvertZeroDateTime=True;ConnectionTimeout=3;DefaultCommandTimeout=30;CharacterSet=latin1;";
                        await using var fallbackConn = new MySqlConnection(fallbackStr);
                        await fallbackConn.OpenAsync();
                        _configService.Config.SqlServerHost = "localhost";
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            return false;
        }
    }
}
