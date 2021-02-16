﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web;
using System.Xml.Linq;
using FourRoads.TelligentCommunity.Installer.Components.Interfaces;
using Telligent.Evolution.Extensibility;
using Telligent.Evolution.Extensibility.Api.Version1;
using Telligent.Evolution.Extensibility.Automation.Version1;
using Telligent.Evolution.Extensibility.Configuration.Version1;
using Telligent.Evolution.Extensibility.Jobs.Version1;
using Telligent.Evolution.Extensibility.Storage.Version1;
using Telligent.Evolution.Extensibility.UI.Version1;
using Telligent.Evolution.Extensibility.Version1;
using PluginManager = Telligent.Evolution.Extensibility.Version1.PluginManager;
using File = System.IO.File;
using IConfigurablePlugin = Telligent.Evolution.Extensibility.Version2.IConfigurablePlugin;
using IPluginConfiguration = Telligent.Evolution.Extensibility.Version2.IPluginConfiguration;
using FourRoads.TelligentCommunity.Installer.Components.Utility;

namespace FourRoads.TelligentCommunity.Installer
{
    public abstract class FactoryDefaultAutomationInstallerBase<TAutomationFactoryDefaultProvider> : IHttpCallback, IInstallablePlugin, IConfigurablePlugin, IEvolutionJob where TAutomationFactoryDefaultProvider : class, IAutomationFactoryDefaultProvider
    {
        public abstract Guid AutomationFactoryDefaultIdentifier { get; }
        protected abstract string ProjectName { get; }
        protected abstract string BaseResourcePath { get; }
        protected abstract EmbeddedResourcesBase EmbeddedResources { get; }
        private TAutomationFactoryDefaultProvider _sourceScriptedFragment;
        private IHttpCallbackController _callbackController;

        #region IPlugin Members

        public string Name => ProjectName + " - Automations";

        public string Description => "Defines the default automations set for " + ProjectName + ".";

        public void Initialize()
        {
            _sourceScriptedFragment = PluginManager.Get<TAutomationFactoryDefaultProvider>().FirstOrDefault();

            if (IsDebugBuild)
            {
                if (_enableFilewatcher)
                {
                    InitializeFilewatcher();
                }

                ScheduleInstall();
            }
        }

        #endregion
        /// <summary>
        /// Set this to false to prevent the installer from installing when version numbers are lower
        /// </summary>
        protected virtual bool SupportAutoInstall => true;

        #region IInstallablePlugin Members

        public virtual void Install(Version lastInstalledVersion)
        {
            if (SupportAutoInstall)
            {
                if (lastInstalledVersion < Version)
                {
                    ScheduleInstall();
                }
            }
        }

        public void Execute(JobData jobData)
        {
            InstallNow();
        }

        protected void ScheduleInstall()
        {
            Apis.Get<IJobService>().Schedule(GetType(), DateTime.UtcNow.AddSeconds(5));
        }

        protected void InstallNow()
        {

            Uninstall();

            string basePath = BaseResourcePath + "Automations.";

            EmbeddedResources.EnumerateResources(basePath, "automation.xml", resourceName =>
            {
                try
                {
                    // Resource path to all files relating to this widget:
                    string widgetPath = resourceName.Replace(".automation.xml", ".");

                    Guid instanceId;
                    Guid providerId;
                    var widgetXml = EmbeddedResources.GetString(resourceName);

                    if (!GetInstanceIdFromWidgetXml(widgetXml, out instanceId, out providerId))
                        return;

                    Apis.Get<IEventLog>().Write($"Installing automation '{resourceName}'", new EventLogEntryWriteOptions() { Category = "Installer" });

                    // If this widget's provider ID is not the one we're installing, then ignore it:
                    if (providerId != AutomationFactoryDefaultIdentifier)
                    {
                        return;
                    }

                    //no extensible installer available so need to copy it manually
                    var automations = CentralizedFileStorage.GetFileStore("defaultautomations");

                    automations.AddFile(CentralizedFileStorage.MakePath(providerId.ToString("N")), $"{instanceId:N}.xml", EmbeddedResources.GetStream(resourceName), false);

                    IEnumerable<string> supplementaryResources = GetType().Assembly.GetManifestResourceNames()
                                                .Where(r => r.StartsWith(widgetPath) && !r.EndsWith(".automation.xml")).ToArray();

                    if (!supplementaryResources.Any())
                        return;

                    foreach (string supplementPath in supplementaryResources)
                    {
                        string supplementName = supplementPath.Substring(widgetPath.Length);

                        automations.AddFile(CentralizedFileStorage.MakePath(providerId.ToString("N"), instanceId.ToString("N")), supplementName, EmbeddedResources.GetStream(supplementPath), false);
                    }
                }
                catch (Exception exception)
                {
                    Apis.Get<IExceptions>().Log(new Exception($"Couldn't load widget from '{resourceName}' embedded resource.", exception));
                }
            });

        }

        private bool GetInstanceIdFromWidgetXml(string widhgetXml, out Guid instanceId, out Guid providerId)
        {
            instanceId = Guid.Empty;
            providerId = Guid.Empty;
            // GetInstanceIdFromWidgetXml widget identifier
            XDocument xdoc = XDocument.Parse(widhgetXml);
            XElement root = xdoc.Root;

            if (root == null)
                return false;

            XElement element = root.Element("automation");

            if (element == null)
                return false;

            XAttribute attribute = element.Attribute("id");

            if (attribute == null)
                return false;

            instanceId = new Guid(attribute.Value);

            XAttribute providerAttr = element.Attribute("provider");

            if (providerAttr == null)
                return false;

            providerId = new Guid(providerAttr.Value);

            return true;
        }

        public virtual void Uninstall()
        {
            if (!IsDebugBuild)
            {
                //Only in release do we want to uninstall widgets, when in development we don't want this to happen
                try
                {
                    //FactoryDefaultScriptedContentFragmentProviderFiles.DeleteAllFiles(_sourceScriptedFragment);
                }
                catch (Exception exception)
                {
                    Apis.Get<IExceptions>().Log(new Exception("Couldn't delete factory default widgets from provider ID: '{ScriptedContentFragmentFactoryDefaultIdentifier}'.", exception));
                }
            }
        }
        private static bool IsDebug()
        {
            object[] customAttributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(DebuggableAttribute), false);
            if ((customAttributes != null) && (customAttributes.Length == 1))
            {
                DebuggableAttribute attribute = customAttributes[0] as DebuggableAttribute;
                return (attribute.IsJITOptimizerDisabled && attribute.IsJITTrackingEnabled);
            }
            return false;
        }

        private bool IsDebugBuild => IsDebug();

        public Version Version => GetType().Assembly.GetName().Version;

        #endregion

        public void Update(IPluginConfiguration configuration)
        {
            if (IsDebugBuild)
            {
                _enableFilewatcher = configuration.GetBool("filewatcher").HasValue ? configuration.GetBool("filewatcher").Value : false;
            }
        }

        public PropertyGroup[] ConfigurationOptions
        {
            get
            {
                PropertyGroup pg = new PropertyGroup() { Id = "options", LabelText = "Options" };

                var updateStatusProp = new Property()
                {
                    Id = "widgetRefresh",
                    LabelText = "Update Custom Automations",
                    DescriptionResourceName = "Request a background job to refresh the custom automations",
                    DataType = "custom",
                    Template = "fourroads_triggerAction",
                    DefaultValue = ""
                };
                if (_callbackController != null)
                {
                    updateStatusProp.Options.Add("callback", _callbackController.GetUrl());
                }
                updateStatusProp.Options.Add("resturl", "");
                updateStatusProp.Options.Add("data", "refresh:true");
                updateStatusProp.Options.Add("label", "Trigger Refresh");
                pg.Properties.Add(updateStatusProp);

                if (IsDebugBuild)
                {
                    var statusMappingProp = new Property()
                    {
                        Id = "filewatcher",
                        LabelText = "Resource Watcher for Development",
                        DescriptionText = "During development automatically refresh automations when they are edited",
                        DataType = "Bool",
                        Template = "bool",
                        DefaultValue = bool.TrueString
                    };
                    pg.Properties.Add(statusMappingProp);
                }

                return new[] { pg };
            }
        }

        public Stream TextAsStream(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        private byte[] ReadFileBytes(string path)
        {
            byte[] result = null;

            while (result == null)
            {
                try
                {
                    result = File.ReadAllBytes(path);
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
            }

            return result;
        }

        public static byte[] ReadStream(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private string BytesToText(byte[] data)
        {
            // UTF8 file without BOM
            return Encoding.UTF8.GetString(data).Trim('\uFEFF', '\u200B'); ;
        }

        public static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
        /// <summary>
        /// Builds a single widget held in the given file path.
        /// </summary>
        private Guid BuildWidget(string pathToWidget)
        {

            // WaitForFile(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            var widgetXml = BytesToText(ReadFileBytes(pathToWidget + "/automation.xml"));

            // Get the widget ID:
            Guid instanceId;
            Guid providerId;

            if (!GetInstanceIdFromWidgetXml(widgetXml, out instanceId, out providerId))
            {
                return Guid.Empty;
            }

            // If this widget's provider ID is not the one we're installing, then ignore it:
            if (providerId != AutomationFactoryDefaultIdentifier)
            {
                return Guid.Empty;
            }

            var automations = CentralizedFileStorage.GetFileStore("defaultautomations");

            automations.AddFile(CentralizedFileStorage.MakePath(providerId.ToString("N")), $"{instanceId:N}.xml", GenerateStreamFromString(widgetXml), false);

            // Copy in any files which are siblings of widget.xml:
            foreach (var supFile in Directory.EnumerateFiles(pathToWidget))
            {
                var fileName = Path.GetFileName(supFile);

                if (fileName == "automation.xml")
                {
                    continue;
                }

                automations.AddFile(CentralizedFileStorage.MakePath(providerId.ToString("N"), instanceId.ToString("N")), fileName, File.Open(supFile, FileMode.Open), false);
            }

            return instanceId;
        }

        /// <summary>
        /// Builds all widgets that belong to this installer.
        /// </summary>

        protected abstract ICallerPathVistor CallerPath();

        //Becuase this is ont API safe and also relies on file paths this should never go into a release build
        private bool _enableFilewatcher;
        private FileSystemWatcher _fileSystemWatcher;


        private void OnChanged(object source, FileSystemEventArgs e)
        {
            // 1. Which widget is this file for?
            //    Can be identified from the file called 'widget.xml' alongside the file that changed.
            var widgetPath = Path.GetDirectoryName(e.FullPath);

            if (File.Exists(widgetPath + "/automation.xml"))
            {
                // Build just this widget.
                BuildWidget(widgetPath);
            }
        }

        private void InitializeFilewatcher()
        {
            _fileSystemWatcher?.Dispose();
            string path = CallerPath().GetPath();

            if (!string.IsNullOrWhiteSpace(path))
            {
                path = Path.GetDirectoryName(path).Replace("\\", "/");
                var directoryParts = path.Split('/').ToList();
                var pathToFind = "/Resources/Automations";

                // Go up the directory tree and check for a nearby Resources/Widgets dir.
                for (var i = 0; i < directoryParts.Count; i++)
                {
                    var widgetsPath = string.Join("/", directoryParts) + pathToFind;

                    if (Directory.Exists(widgetsPath))
                    {
                        _fileSystemWatcher = new FileSystemWatcher();
                        _fileSystemWatcher.Path = widgetsPath;
                        _fileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite;
                        _fileSystemWatcher.Filter = "*.*";
                        _fileSystemWatcher.IncludeSubdirectories = true;
                        _fileSystemWatcher.EnableRaisingEvents = true;

                        _fileSystemWatcher.Changed += OnChanged;
                        _fileSystemWatcher.Created += OnChanged;
                        _fileSystemWatcher.Deleted += OnChanged;
                        return;
                    }

                    // Pop the last one and go around again:
                    directoryParts.RemoveAt(directoryParts.Count - 1);
                }
            }
        }

        public void ProcessRequest(HttpContextBase httpContext)
        {
            ScheduleInstall();
        }

        public void SetController(IHttpCallbackController controller)
        {
            this._callbackController = controller;
        }
    }
    public class TriggerActionPropertyTemplate : IPropertyTemplate
    {
        public string[] DataTypes => new string[] { "custom", "string" };

        public string TemplateName => "installer_triggerAction";

        public bool SupportsReadOnly => true;

        public PropertyTemplateOption[] Options
        {
            get
            {
                return new PropertyTemplateOption[4]
                {
                    new PropertyTemplateOption("resturl", "")
                    {
                        Description = "The rest url to call via the button."
                    },
                    new PropertyTemplateOption("callback", "")
                    {
                        Description = "The callback to make via the button."
                    },
                    new PropertyTemplateOption("data", "")
                    {
                        Description = "The data payload for the call."
                    }
                    ,
                    new PropertyTemplateOption("label", "")
                    {
                        Description = "The label for the button."
                    }

                };
            }
        }

        public string Name => "Trigger Action Property Template";

        public string Description => "Allows an action to be trigger via a button";

        public void Initialize()
        {
        }

        public void Render(TextWriter writer, IPropertyTemplateOptions options)
        {
            string value = options.Value == null ? string.Empty : options.Value.ToString();
            string resturl = options.Property.Options["resturl"] ?? "";
            string callback = options.Property.Options["callback"] ?? "";
            string data = options.Property.Options["data"] ?? "";
            string label = options.Property.Options["label"] ?? "Click";

            if (!string.IsNullOrWhiteSpace(callback))
            {
                resturl = $"'{callback}'";
            }
            else
            {
                resturl = $"$.telligent.evolution.site.getBaseUrl() + '{resturl}'";
            }

            if (options.Property.Editable)
            {
                writer.Write("<div style='display:inline-block'>");
                writer.Write($"<a href ='#' class='button' id='{options.UniqueId}'>{label}</a>");
                writer.Write("</br>");
                writer.Write("</div>");

                string action = string.Empty;
                if (!string.IsNullOrWhiteSpace(resturl))
                {
                    action = @"
                    $.telligent.evolution.get({
                	url : " + resturl + @",
                	data : {"
                            + data +
                            @"},
                	success : function (response) {
                           $.telligent.evolution.notifications.show('Action requested', { type: 'success' });
                    },
                    error : function(xhr, desc, ex) {
                           $.telligent.evolution.notifications.show('Action request failed ' + desc, { type: 'error' });
                    }
                    });";
                }

                writer.Write(
                    $"\r\n<script type=\"text/javascript\">\r\n$(document).ready(function() {{\r\n " +
                    $"var api = {(object)options.JsonApi};\r\n    var i = $('#{(object)options.UniqueId}');\r\n       api.register({{\r\n        val: function(val) {{ return (typeof val == 'undefined') ? i.val() : i.val(val); }},\r\n        hasValue: function() {{ return i.val() != null; }}\r\n    }});\r\n  i.on('click', function(e) {{ e.preventDefault(); {action} }});\r\n i.on('change', function() {{ api.changed(i.val()); }});\r\n}});\r\n</script>\r\n");

            }
        }
    }
}
