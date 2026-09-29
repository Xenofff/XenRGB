using System.Threading;
using System.Threading.Tasks;

namespace XenRGB.Core.Configuration;

public interface IConfigurationService
{
    AppSettings Settings { get; }
    UserProfile ActiveProfile { get; }
    string ConfigFilePath { get; }

    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    void SetActiveProfile(string profileId);
}
