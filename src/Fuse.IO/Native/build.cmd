@echo off
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat" >nul
cl /nologo /O2 /MD /c ufbx\ufbx.c /Foufbx.obj
if errorlevel 1 exit /b 1
cl /nologo /O2 /MD /EHsc /std:c++17 /LD bridge.cpp ufbx.obj /FeFuse.Ufbx.dll
