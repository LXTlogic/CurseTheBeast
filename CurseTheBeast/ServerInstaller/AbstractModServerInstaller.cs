﻿using CurseTheBeast.Storage;

namespace CurseTheBeast.ServerInstaller;


public abstract class AbstractModServerInstaller : IDisposable
{
    public string GameVersion { get; set; } = null!;
    public string LoaderVersion { get; set; } = null!;
    public string? ServerName { get; set; }
    public int? Ram { get; set; }

    public virtual Task<IReadOnlyCollection<FileEntry>> ResolveStandaloneLoaderJarAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Array.Empty<FileEntry>() as IReadOnlyCollection<FileEntry>);
    }

    public virtual bool IsPreinstallationSupported()
    {
        return false;
    }

    /// <summary>
    /// 不需要时才返回空，获取失败直接throw
    /// </summary>
    public virtual Task<IReadOnlyCollection<FileEntry>> ResolveInstallerAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task<IReadOnlyCollection<FileEntry>> ResolveInstallerDependenciesAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task<IReadOnlyCollection<FileEntry>> PreInstallAsync(JavaRuntime jre, FileEntry serverJar, CancellationToken ct = default) 
    {
        throw new NotImplementedException();
    }

    protected async Task<FileEntry> GenerateEulaAgreeFileAsync(string dir, CancellationToken ct = default)
    {
        var file = new FileEntry(Path.Combine(dir, "eula.txt"))
            .WithArchiveEntryName("eula.txt");
        await File.WriteAllTextAsync(file.LocalPath, $"""
            # Minecraft EULA https://aka.ms/MinecraftEULA.
            # {DateTime.UtcNow} UTC
            eula=true
            """, ct);
        return file;
    }

    public virtual void Dispose() 
    {

    }
}
