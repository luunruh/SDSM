using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using SDSM.PluginSdk;

namespace Plugins
{
    public class LoadedPlugin
    {
        public required PluginManifest Manifest { get; init; }
        public required string Directory { get; init; }
        public ISdsmPlugin? Instance { get; init; }
    }

    public partial class PluginLoader
    {
        private static readonly JsonSerializerOptions ManifestJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        [GeneratedRegex("^[a-z0-9-]+$")]
        private static partial Regex IdRegex();

        public IReadOnlyList<LoadedPlugin> Plugins { get; }
        public IReadOnlyList<string> Warnings { get; }

        private PluginLoader(List<LoadedPlugin> plugins, List<string> warnings)
        {
            Plugins = plugins;
            Warnings = warnings;
        }

        // Scans pluginsDir for plugin folders. Invalid plugins are skipped
        // with a warning; the server must still start without them.
        public static PluginLoader LoadFrom(string pluginsDir)
        {
            var plugins = new List<LoadedPlugin>();
            var warnings = new List<string>();

            if (!Directory.Exists(pluginsDir))
            {
                return new PluginLoader(plugins, warnings);
            }

            foreach (string dir in Directory.GetDirectories(pluginsDir).Order(StringComparer.Ordinal))
            {
                try
                {
                    plugins.Add(LoadPlugin(dir));
                }
                catch (Exception e)
                {
                    warnings.Add($"Skipping plugin '{Path.GetFileName(dir)}': {e.Message}");
                }
            }
            return new PluginLoader(plugins, warnings);
        }

        private static LoadedPlugin LoadPlugin(string dir)
        {
            string manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new InvalidDataException("no manifest.json");
            }
            PluginManifest manifest = LoadManifest(manifestPath);
            if (manifest.Id != Path.GetFileName(dir))
            {
                throw new InvalidDataException(
                    $"manifest id '{manifest.Id}' does not match folder name '{Path.GetFileName(dir)}'");
            }
            if (manifest.Ui != null && !File.Exists(Path.Combine(dir, manifest.Ui)))
            {
                throw new InvalidDataException($"ui entry '{manifest.Ui}' not found");
            }

            ISdsmPlugin? instance = null;
            if (manifest.Backend != null)
            {
                string assemblyPath = Path.Combine(dir, manifest.Backend);
                if (!File.Exists(assemblyPath))
                {
                    throw new InvalidDataException($"backend assembly '{manifest.Backend}' not found");
                }
                instance = LoadBackend(assemblyPath);
                if (instance.Id != manifest.Id)
                {
                    throw new InvalidDataException(
                        $"plugin class id '{instance.Id}' does not match manifest id '{manifest.Id}'");
                }
            }

            return new LoadedPlugin { Manifest = manifest, Directory = dir, Instance = instance };
        }

        public static PluginManifest LoadManifest(string path)
        {
            PluginManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(path), ManifestJsonOptions)
                    ?? throw new InvalidDataException("manifest.json is empty");
            }
            catch (JsonException e)
            {
                throw new InvalidDataException($"invalid manifest.json: {e.Message}");
            }
            if (!IdRegex().IsMatch(manifest.Id))
            {
                throw new InvalidDataException(
                    $"invalid plugin id '{manifest.Id}' (allowed: lowercase letters, digits, '-')");
            }
            return manifest;
        }

        private static ISdsmPlugin LoadBackend(string assemblyPath)
        {
            var context = new PluginAssemblyLoadContext(Path.GetFullPath(assemblyPath));
            Assembly assembly = context.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
            List<Type> pluginTypes = assembly.GetTypes()
                .Where(t => typeof(ISdsmPlugin).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();
            if (pluginTypes.Count != 1)
            {
                throw new InvalidDataException(
                    $"expected exactly one ISdsmPlugin implementation, found {pluginTypes.Count}");
            }
            return (ISdsmPlugin)Activator.CreateInstance(pluginTypes[0])!;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            foreach (LoadedPlugin plugin in Plugins)
            {
                plugin.Instance?.ConfigureServices(services);
            }
        }

        public void MapEndpoints(IEndpointRouteBuilder routes)
        {
            foreach (LoadedPlugin plugin in Plugins)
            {
                plugin.Instance?.MapEndpoints(routes.MapGroup($"/api/plugins/{plugin.Manifest.Id}"));
            }
        }
    }

    // Isolates each plugin's dependencies. The SDK assembly is delegated
    // to the default context so contract types stay identical.
    internal class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginAssemblyLoadContext(string pluginPath) : base(pluginPath)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == "SDSM.PluginSdk")
            {
                return null;
            }
            string? path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path != null ? LoadFromAssemblyPath(path) : null;
        }
    }
}
