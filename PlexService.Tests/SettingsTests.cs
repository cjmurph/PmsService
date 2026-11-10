using Newtonsoft.Json;
using PlexServiceCommon;
using Xunit;

namespace PlexService.Tests
{
    public class SettingsTests
    {
        [Fact]
        public void Sanitize_repairs_values_written_by_older_versions()
        {
            var settings = JsonConvert.DeserializeObject<Settings>(
                "{ \"ServerPort\": 0, \"RestartDelay\": -1, \"AutoRemountCount\": 0, \"AutoRemountDelay\": -3," +
                "  \"AuxiliaryApplications\": null, \"DriveMaps\": [ null ] }");

            settings.Sanitize();

            Assert.Equal(8787, settings.ServerPort);
            Assert.Equal(5, settings.RestartDelay);
            Assert.Equal(1, settings.AutoRemountCount);
            Assert.Equal(5, settings.AutoRemountDelay);
            Assert.Empty(settings.AuxiliaryApplications);
            Assert.Empty(settings.DriveMaps);
        }

        [Theory]
        [InlineData(65536)]
        [InlineData(-1)]
        public void Sanitize_rejects_ports_out_of_range(int port)
        {
            var settings = new Settings { ServerPort = port };
            settings.Sanitize();
            Assert.Equal(8787, settings.ServerPort);
        }

        [Fact]
        public void Sanitize_keeps_valid_values()
        {
            var settings = new Settings { ServerPort = 9000, RestartDelay = 0, AutoRemountCount = 3, AutoRemountDelay = 0 };
            settings.Sanitize();

            Assert.Equal(9000, settings.ServerPort);
            Assert.Equal(0, settings.RestartDelay);
            Assert.Equal(3, settings.AutoRemountCount);
            Assert.Equal(0, settings.AutoRemountDelay);
        }
    }
}
