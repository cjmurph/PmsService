using PlexServiceTray;
using Xunit;

namespace PlexService.Tests
{
    public class TrayTests
    {
        [Theory]
        [InlineData("http://localhost:32400/web")]
        [InlineData("https://app.plex.tv/")]
        [InlineData("  https://example.com  ")]
        public void Web_urls_are_allowed(string url)
        {
            Assert.True(Shell.IsWebUrl(url));
        }

        // values can come from the service, so nothing that would run or open a local file
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("file:///C:/Windows/System32/calc.exe")]
        [InlineData(@"C:\Windows\System32\calc.exe")]
        [InlineData(@"\\server\share\setup.exe")]
        [InlineData("javascript:alert(1)")]
        [InlineData("ms-settings:")]
        [InlineData("localhost:32400")]
        public void Anything_else_is_refused(string url)
        {
            Assert.False(Shell.IsWebUrl(url));
        }

        [Theory]
        [InlineData("localhost", true)]
        [InlineData("127.0.0.1", true)]
        [InlineData("::1", true)]
        [InlineData("plexbox", false)]
        [InlineData("192.168.1.20", false)]
        public void Knows_when_the_service_is_on_this_machine(string address, bool local)
        {
            Assert.Equal(local, new TrayApplicationSettings { ServerAddress = address }.IsLocal);
        }

        [Theory]
        [InlineData("Dark.Amber", "Dark")]
        [InlineData("Light.Blue", "Light")]
        [InlineData("System", "System")]
        [InlineData("Purple", "System")]
        [InlineData(null, "System")]
        public void Old_themes_map_to_light_or_dark(string theme, string expected)
        {
            Assert.Equal(expected, new TrayApplicationSettings { Theme = theme }.BaseTheme);
        }

        [Fact]
        public void Service_address_uses_host_and_port()
        {
            var settings = new TrayApplicationSettings { ServerAddress = "plexbox", ServerPort = 9000 };
            Assert.Equal("net.tcp://plexbox:9000/PlexService/", settings.GetServiceAddress());
        }
    }
}
