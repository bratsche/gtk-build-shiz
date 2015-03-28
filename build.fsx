// Author: Cody Russell <cody@jhu.edu>

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

type OS = Mac | Windows
type Arch = X86 | X64

let sh command args =
  ProcessHelper.ExecProcessAndReturnMessages (fun info ->
    info.FileName <- command
    info.Arguments <- args
  ) TimeSpan.MaxValue

let filenameFromUrl (url:string) =
    url.Split('/')
    |> Array.toList
    |> List.rev
    |> List.head

let installDir = "C:\\gtk-build\\gtk"
let buildDir = "C:\\gtk-build\\build\\Win32"

let extract (path:string) =
  let file = Path.Combine("src", path)

  Path.Combine("patches", "stack.props") |> CopyFile buildDir

  let path = Path.Combine(buildDir, Path.GetFileNameWithoutExtension(file))
  if not (Directory.Exists(path)) then
      printfn "extracting %s" file

      sprintf "x %s -o..\\..\\build\\win32" file
      |> sh "C:\Program Files\7-Zip\7z.exe"
      |> ignore

let from (action: unit -> unit) (path: string) =
    pushd path
    action ()
    popd()

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

  let file = Path.Combine(vcpath, "freetype.vcxproj")
  sprintf "%s /p:Platform=%s /p:Configuration=Release /maxcpucount /nodeReuse:True" file "Win32"
  |> sh "msbuild"
  |> ignore

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

  let libDir = Path.Combine(installDir, "lib")
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

  let file = Path.Combine(checkoutDir, "win32", "vc12", "libxml2.sln")
  sprintf "%s /p:Platform=%s /p:Configuration=Release /maxcpucount /nodeReuse:True" file "Win32"
  |> sh "msbuild"
  |> ignore

Target "libffi" <| fun _ ->
  trace "libffi"
  "libffi-3.0.13.7z" |> extract

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
        sprintf "-G \"NMake Makefiles\" \"-DCMAKE_INSTALL_PREFIX=%s\" -DCMAKE_BUILD_TYPE=Debug -DICONV_INCLUDE_DIR=%s -DICONV_LIBRARIES=%s" installDir iconvHeaders iconvLib
        |> sh "cmake"
        |> ignore

        sh "nmake" "clean" |> ignore
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

Target "cairo" <| fun _ ->
  trace "cairo"
  "cairo-1.14.0.7z" |> extract

Target "harfbuzz" <| fun _ ->
  trace "harfbuzz"
  "harfbuzz-0.9.37.7z" |> extract

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

  let vcpath = Path.Combine("slns", "zlib", "contrib", "vstudio", "vc12")
  let file = Path.Combine(vcpath, "zlibvc.sln")
  sprintf "%s /p:Platform=%s /p:Configuration=ReleaseWithoutAsm /maxcpucount /nodeReuse:True" file "Win32"
  |> sh "msbuild"
  |> ignore

  let sourceDir = Path.Combine(buildDir, "zlib-1.2.8")
  let includeDir = Path.Combine(installDir, "include")
  ensureDirectory(includeDir)
  [ Path.Combine(sourceDir, "zlib.h") ] |> Copy includeDir

  (*
  Path.Combine(pwd(), "slns", "zlib", "contrib", "vstudio", "vc12")
  |> from (fun () ->
        let binDir = Path.Combine(installDir, "bin")
        ensureDirectory binDir

        [
            Path.Combine("TestZlibDllRelease", "testzlibdll.exe");
            Path.Combine("TestZlibDllRelease", "testzlibdll.pdb");
            Path.Combine("TestZlibReleaseWithoutAsm", "testzlib.exe");
            Path.Combine("TestZlibReleaseWithoutAsm", "testzlib.pdb");
            Path.Combine("ZlibDllReleaseWithoutAsm", "zlib1.dll");
            Path.Combine("ZlibDllReleaseWithoutAsm", "zlib1.map");
            Path.Combine("ZlibDllReleaseWithoutAsm", "zlib1.pdb")
        ] |> Copy binDir
     )
  |> ignore
  *)

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
"glib" <== ["gettext-runtime"; "libffi"; "zlib"]
"gtk" <== ["atk"; "gdk-pixbuf"; "pango"]
"harfbuzz" <== ["freetype"; "glib"]
"libpng" <== ["zlib"]
"libxml2" <== ["win-iconv"]
"openssl" <== ["zlib"]
"pango" <== ["cairo"; "harfbuzz"]
"pixman" <== ["libpng"]
"BuildAll" <== ["prep"; "gtk"]

RunTargetOrDefault "BuildAll"