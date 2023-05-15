using System.IO;
using System.Reflection;
using System.Text;

namespace Cadmus.Graph.Sql.Test;

internal static class TestHelper
{
    public static Stream GetResourceStream(string name)
    {
        return Assembly.GetExecutingAssembly()!
            .GetManifestResourceStream($"Cadmus.Graph.Sql.Test.Assets.{name}")!;
    }

    public static string LoadResourceText(string name)
    {
        using StreamReader reader = new(GetResourceStream(name),
            Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
