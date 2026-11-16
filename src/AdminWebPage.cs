namespace MaydayHostControl
{
    internal static class AdminWebPage
    {
        internal static readonly string Html = Load();

        private static string Load()
        {
            using var stream = typeof(AdminWebPage).Assembly.GetManifestResourceStream("MaydayHostControl.admin.html");
            if (stream == null) throw new System.InvalidOperationException("Embedded admin.html resource is missing");
            using var reader = new System.IO.StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
