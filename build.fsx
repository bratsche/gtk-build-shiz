#I "tools/FAKE/tools"
#I "tools/FSharp.Data/lib/net40"
#r "FakeLib.dll"
#r "FSharp.Data"

open System
open System.IO
open Fake
open Fake.FileUtils
open Fake.FileHelper
open Fake.Git
open FSharp.Data

// Some directories
// ------------------------------------------------------
let installDir = "C:\\gtk-build\\gtk\\Win32"
let buildDir = "C:\\gtk-build\\build\\Win32"
let binDir = Path.Combine(installDir, "bin")
let libDir = Path.Combine(installDir, "lib")
let patchDir = "C:\\gtk-build\\github\\fab\\patches"

// Some utility functions
// ------------------------------------------------------
let sh command args =
  let exitCode = ProcessHelper.ExecProcess (fun info ->
    info.FileName <- command
    info.Arguments <- args) TimeSpan.MaxValue

  if exitCode <> 0 then
    let errorMsg = sprintf "Executing %s failed with exit code %d." command exitCode
    raise (BuildException(errorMsg, []))

let filenameFromUrl (url:string) =
    url.Split('/')
    |> Array.toList
    |> List.rev
    |> List.head

let extract (path:string) =
  let file = Path.Combine("src", path)

  Path.Combine("patches", "stack.props") |> CopyFile buildDir

  let path = Path.Combine(buildDir, Path.GetFileNameWithoutExtension(file))
  DeleteDir path

  if not (Directory.Exists(path)) then
      printfn "extracting %s" file

      sprintf "x %s -o..\\..\\build\\win32" file
      |> sh "C:\Program Files\7-Zip\7z.exe"
      |> ignore

let install (path) =
    let inDi = new DirectoryInfo(Path.Combine(buildDir, path))
    let outDi = new DirectoryInfo(installDir)

    printfn "Installing from %s to %s..." inDi.FullName outDi.FullName
    copyRecursive inDi outDi true

let from (action: unit -> unit) (path: string) =
    pushd path
    action ()
    popd()

let patch filename =
  sprintf "-p1 -i %s" (Path.Combine(patchDir, filename))
  |> sh "C:\\msys32\\usr\\bin\\patch.exe"


// Targets
// --------------------------------------------------------
Target "prep" <| fun _ ->
    ensureDirectory buildDir
    ensureDirectory installDir

Target "freetype" <| fun _ ->
  trace "freetype"
  "freetype-2.5.5.7z" |> extract

  let srcvcpath = Path.Combine("slns", "freetype", "builds", "windows", "vc2013")
  let vcpath = Path.Combine(buildDir, "freetype-2.5.5", "builds", "windows", "vc2013")

  ensureDirectory vcpath
  CopyRecursive srcvcpath vcpath true
  |> Log "Copying files: "

  Path.Combine(vcpath, "freetype.vcxproj") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "Release"
                      ]
    }
  ) |> ignore

  let sourceDir = Path.Combine(buildDir, "freetype-2.5.5")
  let includeDir = Path.Combine(installDir, "include")
  ensureDirectory(includeDir)

  let includeSrc = Path.Combine(sourceDir, "include")
  let includeFiles = Directory.GetFiles(Path.Combine(sourceDir, "include"), "*.*", SearchOption.AllDirectories)

  CopyFiles includeDir includeFiles

  // XXX Not sure why this doesn't work.
  //CopyDir (Path.Combine(includeDir, "config")) (Path.Combine(includeSrc, "config"))
  ensureDirectory (Path.Combine(includeDir, "config"))
  CopyFiles (Path.Combine(includeDir, "config")) (Directory.GetFiles(Path.Combine(includeSrc, "config"), "*.*", SearchOption.AllDirectories))
  // </XXX>

  [ Path.Combine(sourceDir, "objs", "vc2013", "Win32", "freetype.lib")]
  |> Copy libDir

Target "libxml2" <| fun _ ->
  trace "libxml2"

  let checkoutDir = Path.Combine(buildDir, "libxml2")

  if not (Directory.Exists(checkoutDir)) then
      Git.Repository.clone (buildDir) "https://github.com/bratsche/libxml2.git" "libxml2"
      Git.Reset.hard (Path.Combine(pwd(), "build", "win32", "libxml2")) "726f67e2f140f8d936dfe993bf9ded3180d750d2" |> ignore

  Path.Combine(checkoutDir, "win32", "vc12") |> ensureDirectory

  (Directory.GetFiles(Path.Combine("slns", "libxml2", "win32", "vc12"), "*.*", SearchOption.AllDirectories))
  |> CopyFiles (Path.Combine(checkoutDir, "win32", "vc12"))

  Path.Combine("patches", "libxml2", "config.h") |> CopyFile checkoutDir
  Path.Combine("patches", "libxml2", "xmlversion.h") |> CopyFile (Path.Combine(checkoutDir, "include", "libxml"))

  Path.Combine(checkoutDir, "win32", "vc12", "libxml2.sln") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "Release"
                      ]
    }
  ) |> ignore


  ["libxml2.dll"; "libxml2.pdb"; "runsuite.exe"; "runsuite.pdb"]
  |> List.map (fun x -> Path.Combine(checkoutDir, "win32", "vc12", "Release", x))
  |> CopyFiles (Path.Combine(installDir, "bin"))

  Path.Combine(checkoutDir, "win32", "vc12", "Release", "libxml2.lib") |> CopyFile (Path.Combine(installDir, "lib"))

  (Directory.GetFiles(Path.Combine(checkoutDir, "include", "libxml"), "*.h") |> Array.toList) @ (["win32config.h"; "wsockcompat.h"] |> List.map (fun x -> Path.Combine(checkoutDir, "include", x)))
  |> CopyFiles (Path.Combine(installDir, "include", "libxml"))

Target "libffi" <| fun _ ->
  trace "libffi"
  "libffi-3.0.13.7z" |> extract

  CopyDir (Path.Combine(buildDir, "libffi-3.0.13", "build")) (Path.Combine("slns", "libffi", "build")) (fun _ -> true)
  CopyDir (Path.Combine(buildDir, "libffi-3.0.13", "i686-pc-mingw32")) (Path.Combine("slns", "libffi", "i686-pc-mingw32")) (fun _ -> true)

  Path.Combine(buildDir, "libffi-3.0.13", "build", "win32", "vs12", "libffi.sln") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "Release"
                      ]
    }
  ) |> ignore

  [Path.Combine(buildDir, "libffi-3.0.13", "i686-pc-mingw32", "include", "ffi.h"); Path.Combine(buildDir, "libffi-3.0.13", "src", "x86", "ffitarget.h")]
  |> CopyFiles (Path.Combine(installDir, "include"))

  Path.Combine(buildDir, "libffi-rel", "lib", "libffi.lib")
  |> CopyFile (Path.Combine(installDir, "lib"))

Target "openssl" <| fun _ ->
  trace "openssl"
  "openssl-1.0.1l.7z" |> extract

Target "gettext-runtime" <| fun _ ->
  trace "gettext-runtime"
  "gettext-runtime-0.18.7z" |> extract

  let iconvHeaders = Path.Combine(installDir, "..", "..", "..", "gtk", "Win32", "include")
  let iconvLib = Path.Combine(installDir, "..", "..", "..", "gtk", "Win32", "lib", "iconv.lib")

  Path.Combine(buildDir, "gettext-runtime-0.18")
  |> from (fun () ->
        patch "gettext-runtime\\gettext-runtime.patch"
        "-G \"NMake Makefiles\" \"-DCMAKE_INSTALL_PREFIX=..\..\..\gtk\Win32\" -DCMAKE_BUILD_TYPE=Debug"
        |> sh "cmake"
        |> ignore

        sh "nmake" "clean" |> ignore
        sh "nmake" |> ignore
        sh "nmake" "install" |> ignore
     )
  |> ignore

Target "fontconfig" <| fun _ ->
  trace "fontconfig"
  "fontconfig-2.8.0.7z" |> extract

Target "pixman" <| fun _ ->
  trace "pixman"
  "pixman-0.32.6.7z" |> extract

Target "glib" <| fun _ ->
  trace "glib"
  "glib-2.42.1.7z" |> extract

  Path.Combine(buildDir, "glib-2.42.1")
  |> from (fun () ->
    patch "glib\\glib-if_nametoindex.patch"
    patch "glib\\glib-package-installation-directory.patch"
  )

  [Path.Combine("slns", "glib", "build", "win32", "vs12", "glib-build-defines.props"); Path.Combine("slns", "glib", "build", "win32", "vs12", "glib-install.props")]
  |> CopyFiles (Path.Combine(buildDir, "glib-2.42.1", "build", "win32", "vs12"))

  Path.Combine(buildDir, "glib-2.42.1", "build", "win32", "vs12", "glib.sln")
  |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "Release"
                      ]
    }
  ) |> ignore

  install "glib-rel" |> ignore

Target "cairo" <| fun _ ->
  trace "cairo"
  "cairo-1.14.0.7z" |> extract

Target "harfbuzz" <| fun _ ->
  trace "harfbuzz"
  "harfbuzz-0.9.37.7z" |> extract
  CopyDir (Path.Combine(buildDir, "harfbuzz-0.9.37", "win32")) (Path.Combine("slns", "harfbuzz", "win32")) (fun _ -> true)
  [Path.Combine("slns", "harfbuzz", "src", "hb-gobject-enums.h"); Path.Combine("slns", "harfbuzz", "src", "rllist.txt")]
  |> CopyFiles (Path.Combine(buildDir, "harfbuzz-0.9.37", "src"))

  Path.Combine(buildDir, "harfbuzz-0.9.37", "win32", "harfbuzz.sln")
  |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "Release"
                      ]
    }
  ) |> ignore

  let releaseDir = Path.Combine(buildDir, "harfbuzz-0.9.37", "win32", "libs", "Release")
  [Path.Combine(releaseDir, "harfbuzz.dll"); Path.Combine(releaseDir, "harfbuzz.pdb")]
  |> CopyFiles (Path.Combine(installDir, "bin"))

  CopyFiles (Path.Combine(installDir, "include")) (Directory.GetFiles(Path.Combine(buildDir, "harfbuzz-0.9.37", "src"), "*.h"))


Target "atk" <| fun _ ->
  trace "atk"
  "atk-2.14.0.7z" |> extract

Target "gdk-pixbuf" <| fun _ ->
  trace "gdk-pixbuf"
  "gdk-pixbuf-2.30.8.7z" |> extract

Target "pango" <| fun _ ->
  trace "pango"
  "pango-1.36.8.7z" |> extract

Target "gtk" <| fun _ ->
  trace "gtk"
  "gtk-2.24.25.7z" |> extract

Target "zlib" <| fun _ ->
  trace "zlib"
  "zlib-1.2.8.7z" |> extract

  let slnDir = Path.Combine(buildDir, "zlib-1.2.8", "contrib", "vstudio", "vs12")
  CopyDir (slnDir) (Path.Combine("slns", "zlib", "contrib", "vstudio", "vc12")) (fun _ -> true)

  Path.Combine(slnDir, "zlibvc.sln") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "ReleaseWithoutAsm"
                      ]
    }
  ) |> ignore

  let sourceDir = Path.Combine(buildDir, "zlib-1.2.8")
  let includeDir = Path.Combine(installDir, "include")
  ensureDirectory(includeDir)
  [ Path.Combine(sourceDir, "zlib.h"); Path.Combine(sourceDir, "zconf.h") ] |> Copy includeDir

  [Path.Combine(slnDir, "x86", "ZlibDllReleaseWithoutAsm", "zlib1.dll"); Path.Combine(slnDir, "x86", "ZlibDllReleaseWithoutAsm", "zlib1.map"); Path.Combine(slnDir, "x86", "ZlibDllReleaseWithoutAsm", "zlib1.pdb")]
  |> Copy binDir

  [Path.Combine(slnDir, "x86", "ZlibDllReleaseWithoutAsm", "zlib1.lib"); Path.Combine(slnDir, "x86", "ZlibStatReleaseWithoutAsm", "zlibstat.lib")]
  |> Copy libDir

Target "win-iconv" <| fun _ ->
  trace "win-iconv"
  "win-iconv-0.0.6.7z" |> extract
  Path.Combine(buildDir, "win-iconv-0.0.6")
  |> from (fun () ->
        sprintf "-G \"NMake Makefiles\" \"-DCMAKE_INSTALL_PREFIX=%s\" -DCMAKE_BUILD_TYPE=Debug" installDir
        |> sh "cmake"
        |> ignore

        sh "nmake" "clean" |> ignore
        sh "nmake" "install" |> ignore
     )
  |> ignore

Target "libpng" <| fun _ ->
  trace "libpng"
  "libpng-1.6.16.7z" |> extract

Target "BuildAll" <| fun _ ->
  let config = getBuildParamOrDefault "config" "debug"
  trace("BuildAll " + config)

// Dependencies
// --------------------------------------------------------
"atk" <== ["glib"]
"cairo" <== ["fontconfig"; "glib"; "pixman"]
"fontconfig" <== ["freetype"; "libxml2"]
"gdk-pixbuf" <== ["glib"; "libpng"]
"gettext-runtime" <== ["win-iconv"]
"glib" <== ["libffi"; "gettext-runtime"; "zlib"]
"gtk" <== ["atk"; "gdk-pixbuf"; "pango"]
"harfbuzz" <== ["freetype"; "glib"]
"libpng" <== ["zlib"]
"libxml2" <== ["win-iconv"]
"openssl" <== ["zlib"]
"pango" <== ["cairo"; "harfbuzz"]
"pixman" <== ["libpng"]
"BuildAll" <== ["prep"; "harfbuzz"]

RunTargetOrDefault "BuildAll"