using System;
using System.IO;
using System.Threading.Tasks;
using FirewallControl.Infrastructure.Database;
using FirewallControl.Infrastructure.Services;

namespace FirewallControl.Tests;

internal static class TestPathHelper
{
    public static string GetSolutionRoot()
    {
        string? current = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8 && current != null; i++)
        {
            if (File.Exists(Path.Combine(current, "FirewallControl.sln")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    public static string GetSourceRoot()
        => Path.Combine(GetSolutionRoot(), "src");

    public static string? TryFindLocalWorkspaceRoot()
    {
        string? current = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 9 && current != null; i++)
        {
            foreach (string probe in new[] { current, Path.Combine(current, "ParaOtraPC_FirewallControl_NET10") })
            {
                if (File.Exists(Path.Combine(probe, "FireWall.Ini")) &&
                    File.Exists(Path.Combine(probe, "genes_productos.dbf")) &&
                    Directory.Exists(Path.Combine(probe, "Styles")))
                {
                    return probe;
                }
            }
            current = Directory.GetParent(current)?.FullName;
        }
        return null;
    }

    public static bool HasLocalLegacyDbfs()
        => TryFindLocalWorkspaceRoot() != null;

    public static string GetWorkspaceRootOrTemp()
    {
        string? local = TryFindLocalWorkspaceRoot();
        if (local != null)
            return local;

        string dir = Path.Combine(Path.GetTempPath(), "sefiros_pos_test_workspace");
        Directory.CreateDirectory(dir);
        string iniPath = Path.Combine(dir, "FireWall.Ini");
        if (!File.Exists(iniPath))
            File.WriteAllText(iniPath, "[Config]\r\nlcBaseDatos=firewallcontrol\r\n");
        return dir;
    }

    public static async Task<bool> CanConnectMySqlAsync()
    {
        try
        {
            var cfg = new ConfigService(GetWorkspaceRootOrTemp());
            var factory = new MySqlConnectionFactory(cfg);
            return await factory.TestConnectionAsync();
        }
        catch
        {
            return false;
        }
    }
}
