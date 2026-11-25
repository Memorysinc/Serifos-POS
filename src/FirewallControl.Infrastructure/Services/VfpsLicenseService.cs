using FirewallControl.Core.Interfaces;

namespace FirewallControl.Infrastructure.Services;

/// <summary>
/// Priority 3 requirement: Replaces clases\vfps_licencia.vcx so that the license validation
/// never blocks startup, never shows activation prompts, and never restricts records to 10 (gothis.lldemo = .T.).
/// </summary>
public class VfpsLicenseService : ILicenseService
{
    public bool IsLicensed => true;

    public string RegisteredCompany => "SISTEMA LICENCIADO - SEFIROS POS .NET 10";

    public string LicenseKeyDisplay => "SEFIROS-NET10-FULL-EDITION";

    public bool VerifyLicense() => true;
}
