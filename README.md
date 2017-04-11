gtk+ build scripts for Windows
==============================

Prerequisites
-------------

### Visual C compiler paths ###

You need to have the Visual C compiler in your path. Either run the appropriate
.bat script that comes with Visual Studio or set the variables in Powershell.
Here's what I'm doing:

```powershell
pushd "C:\Program Files (x86)\Microsoft Visual
Studio\2017\Enterprise\Common7\Tools"
cmd /c "VsDevCmd.bat&set" |
foreach {
  if ($_ -match "=") {
    $v = $_.split("=");
    Set-Item -force -path "ENV:\$($v[0])" -value "$($v[1])"
  }
}
popd
```

### 7-Zip ###

You also need to install 7-Zip. Get it here: http://www.7-zip.org/download.html

### CMake ###

Install CMake. It's available here: https://cmake.org/download/


Run the build
-------------

.\build.ps1
