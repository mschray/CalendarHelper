#load "LogHelper.csx"

using System.Configuration;

public static class AppSettingsHelper
{
    public static string GetAppSetting(string SettingName, bool LogValue = true )
    {

        string SettingValue = "";

        try
        {
            SettingValue = ConfigurationManager.AppSettings[SettingName].ToString();

            if ((!String.IsNullOrEmpty(SettingValue)) && LogValue)
            {
                LogHelper.Info($"Retreived AppSetting {SettingName} with a value of {SettingValue}");    
            }
            else if((!String.IsNullOrEmpty(SettingValue)) && !LogValue)
            {
                 LogHelper.Info($"Retreived AppSetting {SettingName} but logging value was turned off");  
            }
            else if(!String.IsNullOrEmpty(SettingValue))
            {
                LogHelper.Info($"AppSetting {SettingName} was null of empty");
            }

        }
        catch (ConfigurationErrorsException ex)
        {
            LogHelper.Error($"Unable to find AppSetting {SettingName} with exception of {ex.Message}");
        }
        catch (System.Exception ex)
        {
            LogHelper.Error($"Looking for AppSetting {SettingName} caused an exception of {ex.Message}");
        }

        return SettingValue;

    }

}