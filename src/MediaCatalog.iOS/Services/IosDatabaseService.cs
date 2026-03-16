using System;
using System.IO;
using System.Threading.Tasks;
using MediaCatalog.Services;

namespace MediaCatalog.iOS.Services;

public class IosDatabaseService : IDatabaseService
{
    public string GetDatabasePath()
    {
        string docFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string libFolder = Path.Combine(docFolder, "..", "Library");

        if (!Directory.Exists(libFolder))
        {
            Directory.CreateDirectory(libFolder);
        }

        return Path.Combine(libFolder, "todo.db");
    }

    public Task SaveAsync() => Task.CompletedTask;
}