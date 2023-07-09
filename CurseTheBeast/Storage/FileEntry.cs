﻿using System.Security.Cryptography;

namespace CurseTheBeast.Storage;


public class FileEntry
{
    public string LocalPath { get; private init; }
    public string LocalTempPath { get; private init; }
    public string? DisplayName { get; private set; }
    public string? ArchiveEntryName { get; private set; }
    public string? Url { get; private set; }
    public bool Required { get; private set; } = true;
    public bool UnixExecutable { get; private set; } = false;
    public bool Unreachable { get; private set; } = false;
    public string? Sha1 { get; private set; }
    public int? Size { get; private set; }

    string _sha1FilePath;
    bool _validated = false;
    bool _isSha1FileRequired = false;

    public FileEntry(RepoType repo, params string[] path)
        : this(LocalStorage.Persistent, repo, path)
    {

    }

    public FileEntry(RepoType repo, string path)
        : this(LocalStorage.Persistent, repo, path)
    {

    }

    public FileEntry(LocalStorage storage, RepoType repo, params string[] pathSegments)
        : this(storage.GetFilePath(repo, pathSegments))
    {

    }

    public FileEntry(LocalStorage storage, RepoType repo, string path)
        : this(storage.GetFilePath(repo, path))
    {

    }

    public FileEntry(string localPath)
    {
        LocalPath = localPath;
        LocalTempPath = localPath + ".tmp";
        _sha1FilePath = localPath + ".sha1";
    }

    public FileEntry SetUnixExecutable()
    {
        UnixExecutable = true;
        return this;
    }

    public FileEntry SetUnreachable()
    {
        Unreachable = true;
        return this;
    }

    public FileEntry SetUnrequired()
    {
        Required = false;
        return this;
    }

    public FileEntry WithSha1(string? sha1)
    {
        Sha1 = sha1;
        return this;
    }

    public FileEntry SetSha1FileRequired()
    {
        _isSha1FileRequired = true;
        return this;
    }

    public FileEntry WithSize(int size)
    {
        Size = size;
        return this;
    }

    public FileEntry WithArchiveEntryName(IEnumerable<string?> entryName)
    {
        ArchiveEntryName = string.Join('/', entryName
            .Where(entryName => !string.IsNullOrWhiteSpace(entryName))
            .Select(entryName => entryName!
                .Replace(Path.DirectorySeparatorChar, '/')
                .TrimStart('.')
                .Trim('/')));
        return this;
    }

    public FileEntry WithArchiveEntryName(params string?[] entryName)
    {
        return WithArchiveEntryName(entryName as IEnumerable<string?>);
    }

    public FileEntry SetDownloadable(string displayName, string url)
    {
        DisplayName = displayName;
        Url = url;
        Unreachable = false;
        return this;
    }

    public bool ValidateTempAndApply()
    {
        if(validateInternal(LocalTempPath, false, true))
        {
            File.Move(LocalTempPath, LocalPath, true);
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool Validate(bool deleteOnFailed = true)
    {
        return _validated || validateInternal(LocalPath, true, deleteOnFailed);
    }

    public void Delete()
    {
        if (File.Exists(LocalPath))
            File.Delete(LocalPath);
    }

    public void DeleteTemp()
    {
        if (File.Exists(LocalTempPath))
            File.Delete(LocalTempPath);
    }

    bool validateInternal(string filePath, bool strict, bool deleteOnFailed)
    {
        var file = new FileInfo(filePath);
        var sha1File = new FileInfo(_sha1FilePath);

        if (!file.Exists)
            return false;

        // 已提供文件大小且不匹配
        if (Size != null && file.Length != Size)
        {
            if(deleteOnFailed)
                file.Delete();
            return false;
        }

        // 获取哈希
        byte[]? providedSha1 = null;
        if (Sha1 != null)
        {
            providedSha1 = Convert.FromHexString(Sha1);
        }
        else if (sha1File.Exists)
        {
            providedSha1 = File.ReadAllBytes(sha1File.FullName);
        }
        else if(strict)
        {
            if (deleteOnFailed)
                file.Delete();
            return false;
        }

        var computedSha1 = calculateSha1(file);

        // 有哈希且不匹配
        if (providedSha1 != null && !computedSha1.SequenceEqual(providedSha1))
        {
            file.Delete();
            if (sha1File.Exists)
                sha1File.Delete();
            return false;
        }

        if(!sha1File.Exists && (_isSha1FileRequired || providedSha1 == null))
            File.WriteAllBytes(sha1File.FullName, computedSha1);

        _validated = true;
        return true;
    }

    static byte[] calculateSha1(FileInfo file)
    {
        using var fs = file.OpenRead();
        return SHA1.HashData(fs);
    }
}
