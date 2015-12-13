if (Test-Path tools\FAKE\tools\FAKE.exe) {
    "FAKE exists."
} else {
    "FAKE doesn't exist."
    .\tools\NuGet\nuget.exe install FAKE -OutputDirectory tools -ExcludeVersion
    .\tools\NuGet\nuget.exe install FSharp.Data -OutputDirectory tools -ExcludeVersion
}

$env:Path = "C:\msys32\usr\bin;$($env:Path)";

.\tools\FAKE\tools\FAKE.exe
