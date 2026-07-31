namespace Plugins
{
    public class PluginManifest
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required string Version { get; set; }
        public string? Backend { get; set; }
        public string? Ui { get; set; }
        public NavEntry? Nav { get; set; }
        /// Sidebar/default-plugin ordering; lower comes first.
        public int Order { get; set; } = 100;

        public class NavEntry
        {
            public required string Title { get; set; }
            public string? Icon { get; set; }
        }
    }
}
