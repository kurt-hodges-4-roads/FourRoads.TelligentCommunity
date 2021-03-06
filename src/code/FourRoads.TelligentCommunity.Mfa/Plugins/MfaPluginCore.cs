﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using DryIoc;
using FourRoads.Common.TelligentCommunity.Plugins.Base;
using FourRoads.Common.TelligentCommunity.Plugins.Interfaces;
using FourRoads.TelligentCommunity.Mfa.DataProvider;
using FourRoads.TelligentCommunity.Mfa.Interfaces;
using FourRoads.TelligentCommunity.Mfa.Logic;
using FourRoads.TelligentCommunity.Mfa.Resources;
using Telligent.Evolution.Components;
using Telligent.Evolution.Extensibility.Configuration.Version1;
using Telligent.Evolution.Extensibility.Urls.Version1;
using Telligent.Evolution.Extensibility.Version1;
using IConfigurablePlugin = Telligent.Evolution.Extensibility.Version2.IConfigurablePlugin;
using IPluginConfiguration = Telligent.Evolution.Extensibility.Version2.IPluginConfiguration;
using PluginManager = Telligent.Evolution.Extensibility.Version1.PluginManager;

namespace FourRoads.TelligentCommunity.Mfa.Plugins
{
    public class MfaPluginCore : IPluginGroup , IBindingsLoader, INavigable, IConfigurablePlugin, ITranslatablePlugin
    {
        private IPluginConfiguration _configuration;
        private ITranslatablePluginController _translations;

        public void Initialize()
        {
            Injector.Get<IMfaLogic>().Initialize(_configuration.GetBool("emailVerification").Value ,
                PluginManager.Get<VerifyEmailPlugin>().FirstOrDefault() , 
                PluginManager.Get<EmailVerifiedSocketMessage>().FirstOrDefault(),
            _configuration.GetDateTime("emailCutoffDate").GetValueOrDefault(DateTime.MinValue));
        }

        public string Name => "4 Roads - MFA Plugin";

        public string Description => "Plugin for adding MFA using the google authenticator";

        public void RegisterUrls(IUrlController controller)
        {
            Injector.Get<IMfaLogic>().RegisterUrls(controller);
        }

        public IEnumerable<Type> Plugins => new[]
        {
            typeof (MfaSqlScriptsInstaller),
            typeof (FactoryDefaultWidgetProviderInstaller),
            typeof (MfaAuthenticatorExtension),
            typeof (VerifyEmailPlugin),
            typeof (VerifyEmailTokens),
            typeof (EmailVerifiedSocketMessage),
            typeof (DatePropertyTemplate)
        };

        public void LoadBindings(IContainer container)
        {
            container.Register<IMfaLogic, MfaLogic>(Reuse.Singleton);
            container.Register<IMfaDataProvider, MfaDataProvider>(Reuse.Singleton);
        }

        public int LoadOrder => 0;
        public void Update(IPluginConfiguration configuration)
        {
            _configuration = configuration;
        }

        public PropertyGroup[] ConfigurationOptions
        {
            get
            {
                PropertyGroup group = new PropertyGroup(){LabelResourceName = "GroupOptions" , Id="options"};

                group.Properties.Add(new Property(){Id="emailVerification" , LabelResourceName = "EmailVerification" , DataType = "bool" , DefaultValue = "true" });
                group.Properties.Add(new Property() { Id = "emailCutoffDate", LabelResourceName = "EmailCutoffDate", DescriptionResourceName = "EmailCutoffDateDescription", DataType = "Date", Template="mfadate", DefaultValue = "" });

                return new[] {group};
            }
        }

        public void SetController(ITranslatablePluginController controller)
        {
            _translations = controller;
        }

        public Translation[] DefaultTranslations
        {
            get
            {
                Translation tr = new Translation("en-us");

                tr.Set("EmailVerification", "Enable Email Verification");
                tr.Set("EmailCutoffDate", "Email Cutoff Date");
                tr.Set("EmailCutoffDateDescription", "When enabled on an existing community this date prevents users that have joined the site before this date being asked to authenticate email address"); 
                tr.Set("GroupOptions", "Options");

                return new[] {tr};
            }
        }
    }



    public class DatePropertyTemplate : IPlugin, IPropertyTemplate
    {
        public string Name => "MFA Date Property Template";

        public string Description => "Enables rendering of date configuration properties";

        public void Initialize()
        {
        }
        public string[] DataTypes => new string[] { "Date" };

        public string TemplateName => "mfadate";

        public bool SupportsReadOnly => true;

        public PropertyTemplateOption[] Options => (PropertyTemplateOption[])null;

        public void Render(TextWriter writer, IPropertyTemplateOptions options)
        {
            DateTime? nullable = options.Value == null ? new DateTime?() : (DateTime?)options.Value;
            if (options.Property.Editable)
            {
                writer.Write("<input type=\"text\" size=\"9\" id=\"");
                writer.Write(options.UniqueId);
                writer.Write("\"");
                if (nullable.HasValue)
                {
                    writer.Write(" value=\"");
                    writer.Write(DateTimeUtil.ToUtc(nullable.Value).ToString("o"));
                    writer.Write("\"");
                }
                writer.Write("/>");
                writer.Write($"<script type=\"text/javascript\">" +
                                $"$(function() {{\r\n    var api = {options.JsonApi};\r\n    var i = $('#{options.UniqueId}');\r\n    " +
                                $"i.glowDateTimeSelector($.extend({{}}, $.fn.glowDateTimeSelector.dateDefaults, " +
                                $"{{ showPopup: true, allowBlankValue: true }}));\r\n    api.register({{\r\n   " +
                                $"     val: function(val) {{ return i.val(); }},\r\n     " +
                                $"   hasValue: function() {{ return i.glowDateTimeSelector('val') != null; }}\r\n    }});\r\n  " +
                                $"  i.on('glowDateTimeSelectorChange', function() {{ \r\n   debugger;  " +
                                $"    api.changed(i.val()); \r\n " +
                                $"   }});\r\n}});\r\n</script>\r\n");
            }
            else
            {
                if (!nullable.HasValue)
                    return;
                writer.Write(HttpUtility.HtmlEncode(nullable.Value.ToString(options.DateFormatString)));
            }
        }

    }
}
