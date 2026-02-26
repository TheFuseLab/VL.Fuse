Place `msdf-atlas-gen.exe` and required runtime DLLs here to bundle with `Fuse.IO`.

Expected layout:

- `Font/Tools/msdf-atlas-gen/msdf-atlas-gen.exe`
- `Font/Tools/msdf-atlas-gen/*.dll`
- `Font/Tools/msdf-atlas-gen/LICENSE-msdf-atlas-gen.txt`

You can populate this folder via:

- `Font/Tools/Get-MsdfAtlasGen.ps1 -GitHubReleaseZipUrl "<zip-url>"`

At runtime, `GenerateFontAtlas` resolves the executable in this bundled folder when `MsdfAtlasGenExePath` is not set.
