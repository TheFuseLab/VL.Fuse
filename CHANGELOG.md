# Changelog

All notable changes to the Fuse project will be documented in this file.

## 0.1.X - Unreleased

### Added

- Fresnel node and help patches
- New Texture object method nodes as per https://docs.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-to-type  
- New 3D SDF fractals
- Easing functions

### Changed

- Added more documentation on Noise and SDF
- Explicit GpuIn no longer required when working inside of regions

### Deprecated

### Removed

### Fixed

- Texture sampling will now work when used in Stride Matieral context
- VolumeSDF working with Raymarch (Material) 
