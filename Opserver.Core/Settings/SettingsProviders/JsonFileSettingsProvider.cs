using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StackExchange.Opserver.SettingsProviders
{
    public class JSONFileSettingsProvider : SettingsProvider
    {
        public override string ProviderType { get { return "JSON File"; } }

        public JSONFileSettingsProvider(SettingsSection settings) : base(settings)
        {
            if (Path.StartsWith("~\\"))
                Path = Path.Replace("~\\", AppDomain.CurrentDomain.BaseDirectory);
        }

        private readonly object _loadLock = new object();
        private readonly ConcurrentDictionary<Type, object> _settingsCache = new ConcurrentDictionary<Type, object>();

        public override T GetSettings<T>()
        {
            object cached;
            if (_settingsCache.TryGetValue(typeof (T), out cached))
                return (T) cached;

            lock (_loadLock)
            {
                if (_settingsCache.TryGetValue(typeof (T), out cached))
                    return (T) cached;

                var settings = GetFromFile<T>();
                if (settings == null)
                    return null;
                if (typeof (IAfterLoadActions).IsAssignableFrom(typeof (T)))
                    ((IAfterLoadActions) settings).AfterLoad();
                AddUpdateWatcher(settings);
                _settingsCache.TryAdd(typeof (T), settings);
                return settings;
            }
        }

        public override T SaveSettings<T>(T settings)
        {
            return settings;
        }

        private void AddUpdateWatcher<T>(T settings) where T : Settings<T>, new()
        {
            var settingsFileName = GetFullFileName<T>();
            if (!File.Exists(settingsFileName)) return;

            // NOTE: only watch changes in existing files
            var watcher = new FileSystemWatcher(System.IO.Path.GetDirectoryName(settingsFileName), System.IO.Path.GetFileName(settingsFileName))
                {
                    NotifyFilter = NotifyFilters.LastWrite
                };
            watcher.Changed += (s, args) =>
                {
                    var newSettings = GetFromFile<T>();
                    settings.UpdateSettings(newSettings);
                };
            watcher.EnableRaisingEvents = true;
        }

        private string GetFullFileName<T>()
        {   
            // NOTE: look for a specific configured file first (some settings files could be generic)
            string settingsFileName = System.IO.Path.Combine(Path, String.Format("{0}{1}.json", typeof (T).Name, ConfigurationManager.AppSettings["SettingsFileSuffix"]));
            if(!File.Exists(settingsFileName)) 
                settingsFileName = System.IO.Path.Combine(Path, String.Format("{0}.json", typeof (T).Name));
            return settingsFileName;
        }

        private T GetFromFile<T>() where T : new()
        {
            var path = GetFullFileName<T>();
            try
            {
                if (!File.Exists(path))
                {
                    return new T();
                }

                using (var sr = File.OpenText(path))
                {
                    var serializer = new JsonSerializer
                        {
                            ContractResolver = new CamelCasePropertyNamesContractResolver()
                        };
                    return (T) serializer.Deserialize(sr, typeof (T));
                }
            }
            catch (Exception e)
            {
                // A race on reloads can happen - ignore as this is during shutdown
                if (!e.Message.Contains("The process cannot access the file"))
                    Opserver.Current.LogException("Error loading settings from " + path, e);
                return default(T);
            }
        }
    }
}
