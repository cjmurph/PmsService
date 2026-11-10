using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using PlexServiceCommon;
using PlexServiceWCF;
using Xunit;

namespace PlexService.Tests
{
    public class DriveMapSecretsTests
    {
        private const string Secret = "s3cret-Pa55";

        private static DriveMap Map(string id, string user = @"NAS\plex", string password = null) =>
            new(@"\\nas\media", "M") { Id = id, UserName = user, Password = password };

        private static Settings With(params DriveMap[] maps) => new() { DriveMaps = new List<DriveMap>(maps) };

        /// <summary>
        /// Saves a new password the way the service does and returns what ends up in the settings file
        /// </summary>
        private static Settings Saved(string password = Secret)
        {
            var incoming = With(Map("a", password: password));
            DriveMapSecrets.Apply(incoming, With());
            return incoming;
        }

        [Fact]
        public void A_new_password_is_encrypted_and_can_be_read_back()
        {
            var map = Saved().DriveMaps[0];

            Assert.Null(map.Password);
            Assert.NotNull(map.ProtectedPassword);
            Assert.DoesNotContain(Secret, map.ProtectedPassword);
            Assert.Equal(Secret, DriveMapSecrets.Reveal(map));
        }

        [Fact]
        public void The_settings_file_never_contains_the_password()
        {
            var json = JsonConvert.SerializeObject(Saved());
            Assert.DoesNotContain(Secret, json);

            // even a password that hasn't been through Apply is not written
            Assert.DoesNotContain(Secret, JsonConvert.SerializeObject(Map("b", password: Secret)));
        }

        [Fact]
        public void Saving_without_a_password_keeps_the_saved_one()
        {
            var stored = Saved();
            var incoming = With(Map("a"));

            DriveMapSecrets.Apply(incoming, stored);

            Assert.Equal(Secret, DriveMapSecrets.Reveal(incoming.DriveMaps[0]));
        }

        [Fact]
        public void A_password_never_moves_to_another_mapping()
        {
            var stored = Saved();
            var incoming = With(Map("other"));

            DriveMapSecrets.Apply(incoming, stored);

            Assert.Null(incoming.DriveMaps[0].ProtectedPassword);
        }

        [Fact]
        public void Removing_the_user_name_removes_the_password()
        {
            var stored = Saved();
            var incoming = With(Map("a", user: " ", password: Secret));

            DriveMapSecrets.Apply(incoming, stored);

            Assert.Null(incoming.DriveMaps[0].UserName);
            Assert.Null(incoming.DriveMaps[0].ProtectedPassword);
        }

        [Fact]
        public void Non_administrators_see_no_accounts_or_passwords()
        {
            var settings = Saved();

            DriveMapSecrets.Redact(settings, includeAccounts: false);

            var map = settings.DriveMaps[0];
            Assert.Null(map.UserName);
            Assert.Null(map.ProtectedPassword);
            Assert.Null(map.Password);
            Assert.False(map.HasPassword);
        }

        [Fact]
        public void Administrators_see_the_account_but_not_the_password()
        {
            var settings = Saved();

            DriveMapSecrets.Redact(settings, includeAccounts: true);

            var map = settings.DriveMaps[0];
            Assert.Equal(@"NAS\plex", map.UserName);
            Assert.True(map.HasPassword);
            Assert.Null(map.ProtectedPassword);
            Assert.Null(map.Password);
        }

        [Fact]
        public void A_test_prefers_the_password_just_entered()
        {
            var stored = Saved();

            Assert.Equal("typed", DriveMapSecrets.ForTest(Map("a", password: "typed"), stored));
            Assert.Equal(Secret, DriveMapSecrets.ForTest(Map("a"), stored));
            Assert.Null(DriveMapSecrets.ForTest(Map("other"), stored));
        }

        [Theory]
        [InlineData("not base64!")]
        [InlineData("AAAA")]
        public void A_password_that_cant_be_decrypted_asks_to_enter_it_again(string protectedPassword)
        {
            var map = Map("a");
            map.ProtectedPassword = protectedPassword;

            Assert.Throws<InvalidOperationException>(() => DriveMapSecrets.Reveal(map));
        }

        [Fact]
        public void Sanitize_gives_every_mapping_an_id()
        {
            var settings = With(Map(null), Map(""));
            settings.Sanitize();

            Assert.All(settings.DriveMaps, m => Assert.False(string.IsNullOrEmpty(m.Id)));
            Assert.NotEqual(settings.DriveMaps[0].Id, settings.DriveMaps[1].Id);
        }
    }
}
