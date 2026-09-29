using System.IO;
using System.Threading.Tasks;
using XenRGB.Core.Canvas;
using XenRGB.Core.Configuration;
using Xunit;

namespace XenRGB.Tests;

public class ProfileSerializationTests
{
    [Fact]
    public async Task JsonConfigurationService_SaveAndLoad_PreservesProfileAndPlacements()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"xenrgb_test_{System.Guid.NewGuid():N}.json");

        try
        {
            var service = new JsonConfigurationService(tempFile);
            var profile = service.ActiveProfile;
            profile.Name = "Test Gaming Setup";
            profile.SelectedEffectId = "biohazard";
            profile.MasterBrightness = 0.85f;
            profile.Automation.InactivityTimeoutMinutes = 25;

            profile.Placements.Clear();
            profile.Placements.Add(new CanvasDevicePlacement("dev-1", "Test Strip", 0.2f, 0.4f, 0.5f, 0.1f, 45f));

            await service.SaveAsync();

            Assert.True(File.Exists(tempFile));

            // Load in fresh service instance
            var reloadedService = new JsonConfigurationService(tempFile);
            await reloadedService.LoadAsync();

            var loadedProfile = reloadedService.ActiveProfile;
            Assert.Equal("Test Gaming Setup", loadedProfile.Name);
            Assert.Equal("biohazard", loadedProfile.SelectedEffectId);
            Assert.Equal(0.85f, loadedProfile.MasterBrightness, 0.001f);
            Assert.Equal(25, loadedProfile.Automation.InactivityTimeoutMinutes);

            Assert.Single(loadedProfile.Placements);
            var placement = loadedProfile.Placements[0];
            Assert.Equal("dev-1", placement.DeviceId);
            Assert.Equal("Test Strip", placement.DeviceName);
            Assert.Equal(0.2f, placement.X, 0.001f);
            Assert.Equal(0.4f, placement.Y, 0.001f);
            Assert.Equal(45f, placement.RotationDegrees, 0.001f);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task JsonConfigurationService_CorruptedJson_RecoversWithDefaults()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"xenrgb_corrupt_test_{System.Guid.NewGuid():N}.json");

        try
        {
            // Write malformed/corrupted JSON
            await File.WriteAllTextAsync(tempFile, "{ this is not valid json : [ }");

            var service = new JsonConfigurationService(tempFile);
            // Should not throw, and should recover default configuration
            await service.LoadAsync();

            Assert.NotNull(service.Settings);
            Assert.NotEmpty(service.Settings.Profiles);
            Assert.NotNull(service.ActiveProfile);
            Assert.Equal("default-profile", service.ActiveProfile.Id);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
