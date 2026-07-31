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

        public class NavEntry
        {
            public required string Title { get; set; }
            public string? Icon { get; set; }
        }
    }
}
