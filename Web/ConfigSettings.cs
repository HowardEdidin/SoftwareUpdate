#region

using System.Fabric;
using System.Fabric.Description;

#endregion

namespace Web
{
    public class ConfigSettings
    {
        public ConfigSettings(ServiceContext context)
        {
            context.CodePackageActivationContext.ConfigurationPackageModifiedEvent +=
                CodePackageActivationContext_ConfigurationPackageModifiedEvent;
            UpdateConfigSettings(context.CodePackageActivationContext.GetConfigurationPackageObject("Config").Settings);
        }

        public string GuestExeBackendServiceName { get; private set; }

        public int ReverseProxyPort { get; private set; }

        public string SoftwareUpdateServiceName { get; private set; }


        private void CodePackageActivationContext_ConfigurationPackageModifiedEvent(object sender,
            PackageModifiedEventArgs<ConfigurationPackage> e)
        {
            UpdateConfigSettings(e.NewPackage.Settings);
        }

        private void UpdateConfigSettings(ConfigurationSettings settings)
        {
            var section = settings.Sections["MyConfigSection"];
            GuestExeBackendServiceName = section.Parameters["GuestExeBackendServiceName"].Value;
            ReverseProxyPort = int.Parse(section.Parameters["ReverseProxyPort"].Value);
            SoftwareUpdateServiceName = section.Parameters["SoftwareUpdateServiceName"].Value;

        }
    }
}