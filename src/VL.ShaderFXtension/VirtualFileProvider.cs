using Stride.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VL.ShaderFXtension
{
    public class InMemoryFileProvider : VirtualFileProviderBase, IDisposable
        {
            private readonly Dictionary<string, byte[]> _inMemory = new Dictionary<string, byte[]>();
            private readonly HashSet<string> _tempFiles = new HashSet<string>();
            private readonly IVirtualFileProvider _virtualFileProvider;

            public InMemoryFileProvider(IVirtualFileProvider baseFileProvider) : base(baseFileProvider.RootPath)
            {
                _virtualFileProvider = baseFileProvider;
            }

            public void Register(string url, string content)
            {
                _inMemory[url] = Encoding.Default.GetBytes(content);

                // The effect system assumes there is a /path - doesn't do a FileExists check first :/
                var path = GetTempFileName();
                _tempFiles.Add(path);
                File.WriteAllText(path, content);
                _inMemory[$"{url}/path"] = Encoding.Default.GetBytes(path);
            }

            // Don't use Path.GetTempFileName() where we can run into overflow issue (had it during development)
            // https://stackoverflow.com/questions/18350699/system-io-ioexception-the-file-exists-when-using-system-io-path-gettempfilena
            private static string GetTempFileName()
            {
                return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            }

            public new void Dispose()
            {
                try
                {
                    foreach (var f in _tempFiles)
                        File.Delete(f);
                }
                catch (Exception)
                {
                    // Ignore
                }
                base.Dispose();
            }

            public override bool FileExists(string url)
            {
                if (_inMemory.ContainsKey(url))
                    return true;

                return _virtualFileProvider.FileExists(url);
            }

            public override Stream OpenStream(string url, VirtualFileMode mode, VirtualFileAccess access, VirtualFileShare share = VirtualFileShare.Read, StreamFlags streamFlags = StreamFlags.None)
            {
                if (_inMemory.TryGetValue(url, out var bytes))
                {
                    return new MemoryStream(bytes);
                }

                return _virtualFileProvider.OpenStream(url, mode, access, share, streamFlags);
            }
        }
}