using System.IO;
using IPA.Utilities;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SiraUtil.Zenject;

namespace SabersCore.Services;

internal class DirectoryManager : IAsyncInitializable
{
    private readonly string customSabersPath = Path.Combine(UnityGame.InstallPath, "CustomSabers");
    private readonly string userDataPath = Path.Combine(UnityGame.UserDataPath, Plugin.Metadata.Id);

    public DirectoryManager()
    {
        CustomSabers = new(customSabersPath);
        UserData = new(userDataPath);
        Ready = CreateDirectoriesAsync([customSabersPath, userDataPath], CancellationToken.None);
    }
    
    public DirectoryInfo CustomSabers { get; }
    public DirectoryInfo UserData { get; }
    internal Task Ready { get; }

    public async Task InitializeAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await Ready;
        token.ThrowIfCancellationRequested();
    }

    internal async Task EnsureDirectoriesAsync(string[] paths, CancellationToken token)
    {
        var capturedPaths = paths.Select(Path.GetFullPath).ToArray();
        await InitializeAsync(token);
        await CreateDirectoriesAsync(capturedPaths, token);
    }

    private static Task CreateDirectoriesAsync(string[] paths, CancellationToken token) =>
        Task.Factory.StartNew(CreateDirectories, (paths, token), token,
            TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

    private static void CreateDirectories(object state)
    {
        var (paths, token) = ((string[], CancellationToken))state;
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(path);
        }
    }
}
