@echo off

if not exist tools\FAKE\tools\Fake.exe (
	tools\NuGet\nuget.exe install FAKE -OutputDirectory tools -ExcludeVersion
)