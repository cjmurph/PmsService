using PlexServiceTray.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlexServiceTray.Mock
{
    public class MockSettingsViewModel:SettingsViewModel
    {
        public MockSettingsViewModel():base(new PlexServiceCommon.Settings())
        {
            //add some mock data
            AuxiliaryApplications.Add(new AuxiliaryApplicationViewModel(new PlexServiceCommon.AuxiliaryApplication()
            {
                Name = "My Aux Application",
                FilePath = @"C:\Something\execute_me.exe",
                LogOutput = true,
                Argument = "-i someExtraInfo",
                KeepAlive = true,
                WorkingFolder = @"C:\Something",
                Url = "https://auxiliaryapps.com"
            }, this));

            AuxiliaryApplications.Add(new AuxiliaryApplicationViewModel(new PlexServiceCommon.AuxiliaryApplication()
            {
                Name = "Another Aux Application",
                FilePath = @"C:\Something\dont_execute_me.exe",
                LogOutput = true,
                Argument = "--help",
                KeepAlive = false,
                WorkingFolder = @"C:\Something",
                Url = "https://bad.com"
            }, this));

            DriveMaps.Add(new DriveMapViewModel(new PlexServiceCommon.DriveMap(@"\\myserver\media", @"M")));
            DriveMaps.Add(new DriveMapViewModel(new PlexServiceCommon.DriveMap(@"\\myserver\photos", @"P")));
        }
    }
}
