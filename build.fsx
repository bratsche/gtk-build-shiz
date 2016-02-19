#I "tools/FAKE/tools"
#I "tools/FSharp.Data/lib/net40"
#r "FakeLib.dll"
#r "FSharp.Data"

open System
open System.IO
open System.Net
open System.Text.RegularExpressions
open Fake
open Fake.FileUtils
open Fake.FileHelper
open Fake.StringHelper
open Fake.Git
open FSharp.Data

let mutable originDir = "C:\\gtk-build-shiz"

// Some directories
// ------------------------------------------------------
let installDir () = Path.Combine(originDir, "install", "gtk", "Win32")
let buildDir () = Path.Combine(originDir, "build", "Win32")
let binDir () = Path.Combine(installDir(), "bin")
let libDir () = Path.Combine(installDir(), "lib")
let patchDir () = Path.Combine(originDir, "patches")
let srcDir () = Path.Combine(originDir, "src")

// Some utility functions
// ------------------------------------------------------
let mingwify (str: string) =
    str.Replace('\\', '/')
    |> replaceFirst "C:" "/C"

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

let from (action: unit -> unit) (path: string) =
    pushd path
    action ()
    popd()

let download (url: string) =
    let client = new WebClient()
    let file = Path.Combine(srcDir(), Path.GetFileName url)

    if not (File.Exists (file)) then
        printfn "Downloading %s" file
        client.DownloadFile(url, file)

    file

let extract (path:string) =
  let file = Path.Combine(srcDir(), path)

  Path.Combine("patches", "stack.props") |> CopyFile (buildDir())

  let path = Path.Combine(buildDir(), Path.GetFileNameWithoutExtension(file))
  DeleteDir path

  if not (Directory.Exists(path)) then
      printfn "extracting %s" file

      match file with
      | x when Regex.Match(x, "\.7z$").Success ->
          sprintf "x %s -o.\\build\\win32" file
          |> sh "C:\Program Files\7-Zip\7z.exe"
          |> ignore
      | x when Regex.Match(x, "\.tar\.[gx]z$").Success || Regex.Match(x, "\.tar\.bz2$").Success ->
          buildDir()
          |> from (fun () ->
              sprintf "xf %s" (mingwify(file))
              |> sh "tar"
              |> ignore
          )
      | _ ->
        printfn "Unknown file type to extract for %s" file

let install (path) =
    let inDi = new DirectoryInfo(Path.Combine(buildDir(), path))
    let outDi = new DirectoryInfo(installDir())

    printfn "Installing from %s to %s..." inDi.FullName outDi.FullName
    copyRecursive inDi outDi true

let patch filename =
  sprintf "-p1 -i %s" (Path.Combine(patchDir(), filename))
  |> sh "C:\\msys32\\usr\\bin\\patch.exe"


// Targets
// --------------------------------------------------------
Target "prep" <| fun _ ->
    originDir <- pwd()
    ensureDirectory (buildDir())
    ensureDirectory (installDir())
    ensureDirectory (binDir())
    ensureDirectory (libDir())

Target "freetype" <| fun _ ->
  "http://download.savannah.gnu.org/releases/freetype/freetype-2.5.5.tar.bz2"
  |> download
  |> extract

  let srcvcpath = Path.Combine("slns", "freetype", "builds", "windows", "vc2013")
  let vcpath = Path.Combine(buildDir(), "freetype-2.5.5", "builds", "windows", "vc2013")

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

  let sourceDir = Path.Combine(buildDir(), "freetype-2.5.5")
  let includeDir = Path.Combine(installDir(), "include")
  ensureDirectory(includeDir)

  let includeSrc = Path.Combine(sourceDir, "include")
  let includeFiles = Directory.GetFiles(Path.Combine(sourceDir, "include"), "*.*", SearchOption.AllDirectories)

  CopyFiles includeDir includeFiles

  ensureDirectory (Path.Combine(includeDir, "config"))
  CopyFiles (Path.Combine(includeDir, "config")) (Directory.GetFiles(Path.Combine(includeSrc, "config"), "*.*", SearchOption.AllDirectories))

  [ Path.Combine(sourceDir, "objs", "vc2013", "Win32", "freetype.lib")]
  |> Copy (libDir())

Target "libxml2" <| fun _ ->
  let checkoutDir = Path.Combine(buildDir(), "libxml2")

  if not (Directory.Exists(checkoutDir)) then
      Git.Repository.clone (buildDir()) "https://github.com/bratsche/libxml2.git" "libxml2"
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


  ["libxml2-2.dll"; "libxml2-2.pdb"; "runsuite.exe"; "runsuite.pdb"]
  |> List.map (fun x -> Path.Combine(checkoutDir, "win32", "vc12", "Release", x))
  |> CopyFiles (Path.Combine(installDir(), "bin"))

  Path.Combine(checkoutDir, "win32", "vc12", "Release", "libxml2-2.lib") |> CopyFile (Path.Combine(installDir(), "lib"))

  (Directory.GetFiles(Path.Combine(checkoutDir, "include", "libxml"), "*.h") |> Array.toList) @ (["win32config.h"; "wsockcompat.h"] |> List.map (fun x -> Path.Combine(checkoutDir, "include", x)))
  |> CopyFiles (Path.Combine(installDir(), "include", "libxml"))

Target "libffi" <| fun _ ->
  "libffi-3.0.13.7z" |> extract

  let ffiRootDir = Path.Combine(buildDir(), "libffi-3.0.13")
  ensureDirectory (Path.Combine(ffiRootDir, "build", "Win32", "vs12"))
  CopyDir (Path.Combine(ffiRootDir, "build")) (Path.Combine("slns", "libffi", "build")) (fun _ -> true)

  ensureDirectory (Path.Combine(ffiRootDir, "i686-pc-mingw32"))
  CopyDir (Path.Combine(ffiRootDir, "i686-pc-mingw32")) (Path.Combine("slns", "libffi", "i686-pc-mingw32")) (fun _ -> true)

  Path.Combine(ffiRootDir, "build", "win32", "vs12", "libffi.sln") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "Release"
                      ]
    }
  ) |> ignore

  [Path.Combine(ffiRootDir, "i686-pc-mingw32", "include", "ffi.h"); Path.Combine(buildDir(), "libffi-3.0.13", "src", "x86", "ffitarget.h")]
  |> CopyFiles (Path.Combine(installDir(), "include"))

  Path.Combine(buildDir(), "libffi-rel", "lib", "libffi.lib")
  |> CopyFile (Path.Combine(installDir(), "lib"))

Target "gettext-runtime" <| fun _ ->
  "gettext-runtime-0.18.7z" |> extract

  Path.Combine(buildDir(), "gettext-runtime-0.18")
  |> from (fun () ->
        patch "gettext-runtime\\gettext-runtime.patch"
        patch "gettext-runtime\\libtool-style-libintl-dll.patch"

        "-G \"NMake Makefiles\" \"-DCMAKE_INSTALL_PREFIX=..\..\..\install\gtk\Win32\" -DCMAKE_BUILD_TYPE=Debug"
        |> sh "cmake"
        |> ignore

        sh "nmake" "clean" |> ignore
        sh "nmake" |> ignore
        sh "nmake" "install" |> ignore
     )
  |> ignore

Target "glib" <| fun _ ->
  "http://ftp.gnome.org/pub/gnome/sources/glib/2.42/glib-2.42.1.tar.xz"
  |> download
  |> extract

  Path.Combine(buildDir(), "glib-2.42.1")
  |> from (fun () ->
    patch "glib\\glib-if_nametoindex.patch"
    patch "glib\\glib-package-installation-directory.patch"
  )

  Directory.GetFiles(Path.Combine("slns", "glib", "build", "win32", "vs12"), "*.*")
  |> CopyFiles (Path.Combine(buildDir(), "glib-2.42.1", "build", "win32", "vs12"))

  Path.Combine(buildDir(), "glib-2.42.1", "build", "win32", "vs12", "glib.sln")
  |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "Release"
                      ]
    }
  ) |> ignore

  install "glib-rel" |> ignore

Target "harfbuzz" <| fun _ ->
  "http://www.freedesktop.org/software/harfbuzz/release/harfbuzz-1.1.3.tar.bz2"
  |> download
  |> extract

  CopyDir (Path.Combine(buildDir(), "harfbuzz-1.1.3", "win32")) (Path.Combine("slns", "harfbuzz", "win32")) (fun _ -> true)
  [Path.Combine("slns", "harfbuzz", "src", "hb-gobject-enums.h"); Path.Combine("slns", "harfbuzz", "src", "rllist.txt")]
  |> CopyFiles (Path.Combine(buildDir(), "harfbuzz-1.1.3", "src"))

  Path.Combine(buildDir(), "harfbuzz-1.1.3", "win32", "harfbuzz.sln")
  |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "Release"
                      ]
    }
  ) |> ignore

  let releaseDir = Path.Combine(buildDir(), "harfbuzz-1.1.3", "win32", "libs", "Release")
  [Path.Combine(releaseDir, "libharfbuzz-0.dll"); Path.Combine(releaseDir, "harfbuzz.pdb")]
  |> CopyFiles (Path.Combine(installDir(), "bin"))

  CopyFiles (Path.Combine(installDir(), "include")) (Directory.GetFiles(Path.Combine(buildDir(), "harfbuzz-1.1.3", "src"), "*.h"))

  Path.Combine(buildDir(), "harfbuzz-1.1.3", "win32", "libs", "harfbuzz", "Release", "harfbuzz.lib")
  |> CopyFile (Path.Combine(installDir(), "lib"))

Target "atk" <| fun _ ->
  "http://ftp.gnome.org/pub/gnome/sources/atk/2.14/atk-2.14.0.tar.xz"
  |> download
  |> extract

  Directory.GetFiles(Path.Combine("slns", "atk", "build", "win32", "vs12"), "*.*")
  |> CopyFiles (Path.Combine(buildDir(), "atk-2.14.0", "build", "win32", "vs12"))

  Path.Combine(buildDir(), "atk-2.14.0", "build", "win32", "vs12", "atk.sln")
  |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "Release"
                      ]
    }
  )

  install "atk-2.14.0-rel" |> ignore

Target "gdk-pixbuf" <| fun _ ->
  "http://ftp.gnome.org/pub/gnome/sources/gdk-pixbuf/2.30/gdk-pixbuf-2.30.8.tar.xz"
  |> download
  |> extract

  let slnDir = Path.Combine(buildDir(), "gdk-pixbuf-2.30.8", "build", "win32", "vc12")
  CopyDir (slnDir) (Path.Combine("slns", "gdk-pixbuf", "build", "win32", "vc12")) (fun _ -> true)

  Path.Combine(slnDir, "gdk-pixbuf.sln") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "Release"]
    }
  )

  install "gdk-pixbuf-2.30.8-rel" |> ignore

Target "fontconfig" <| fun _ ->
  "http://www.freedesktop.org/software/fontconfig/release/fontconfig-2.8.0.tar.gz"
  |> download
  |> extract

  Path.Combine(buildDir(), "fontconfig-2.8.0")
  |> from (fun () ->
      patch "fontconfig\\fontconfig.patch"
  )

  CopyFiles (Path.Combine(buildDir(), "fontconfig-2.8.0")) (Directory.GetFiles(Path.Combine("slns", "fontconfig"), "*.*", SearchOption.AllDirectories))
  CopyFiles (Path.Combine(buildDir(), "fontconfig-2.8.0", "src")) (Directory.GetFiles(Path.Combine("slns", "fontconfig", "src"), "*.*", SearchOption.AllDirectories))

  Path.Combine(buildDir(), "fontconfig-2.8.0", "fontconfig.sln")
  |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = ["Platform", "Win32"
                                    "Configuration", "Release"
                                    "SolutionDir", Path.Combine(buildDir(), "fontconfig-2.8.0")
                      ]
    }
  )

  // Install the .exe and .pdb files to bin
  ["fc-cache"; "fc-cat"; "fc-match"; "fc-query"; "fc-scan"]
  |> List.map (fun x -> [x + ".exe"; x + ".pdb"])
  |> Seq.fold (fun lst i -> List.append lst i) []
  |> List.append ["libfontconfig-1.dll"; "fontconfig.pdb"]  // And the .dll
  |> List.map (fun x -> Path.Combine(buildDir(), "fontconfig-2.8.0", "Release", x))
  |> CopyFiles (Path.Combine(installDir(), "bin"))

  ensureDirectory (Path.Combine(installDir(), "etc", "fonts"))
  ["fonts.conf"; "fonts.dtd"]
  |> List.map (fun x -> Path.Combine(buildDir(), "fontconfig-2.8.0"))
  |> CopyFiles (Path.Combine(installDir(), "etc", "fonts"))

  ["fcfreetype.h"; "fcprivate.h"; "fontconfig.h"]
  |> List.map (fun x -> Path.Combine(buildDir(), "fontconfig-2.8.0", "fontconfig", x))
  |> CopyFiles (Path.Combine(installDir(), "include", "fontconfig"))

  Path.Combine(buildDir(), "fontconfig-2.8.0", "Release", "fontconfig.lib")
  |> CopyFile (Path.Combine(installDir(), "lib"))


Target "pixman" <| fun _ ->
  "http://cairographics.org/releases/pixman-0.32.8.tar.gz"
  |> download
  |> extract

  CopyDir (Path.Combine(buildDir(), "pixman-0.32.8", "build")) (Path.Combine("slns", "pixman", "build")) (fun _ -> true)

  Path.Combine("slns", "pixman", "pixman.symbols")
  |> CopyFile (Path.Combine(buildDir(), "pixman-0.32.8", "pixman"))

  let slnDir = Path.Combine(buildDir(), "pixman-0.32.8", "build", "win32", "vc12")
  Path.Combine(slnDir, "pixman.vcxproj") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = ["Configuration", "Release"
                                    "SolutionDir", slnDir
                      ]
    }
  )

  Path.Combine(slnDir, "install.vcxproj") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = ["Configuration", "Release"
                                    "SolutionDir", slnDir
                      ]
    }
  )

  install "pixman-0.32.6-rel" |> ignore

Target "cairo" <| fun _ ->
  "http://cairographics.org/releases/cairo-1.14.6.tar.xz"
  |> download
  |> extract

  Path.Combine(buildDir(), "cairo-1.14.6")
  |> from (fun () ->
    printfn "No Cairo patches"
  )

  let slnDir = Path.Combine(buildDir(), "cairo-1.14.6", "msvc")
  CopyDir (slnDir) (Path.Combine("slns", "cairo", "msvc")) (fun _ -> true)

  Path.Combine(slnDir, "vc12", "cairo.sln") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = ["Platform", "Win32"
                                    "Configuration", "Release_FC"
                      ]
    }
  )

  install "cairo-1.14.0-rel" |> ignore

Target "pango" <| fun _ ->
  "http://ftp.gnome.org/pub/gnome/sources/pango/1.36/pango-1.36.8.tar.xz"
  |> download
  |> extract

  Path.Combine(buildDir(), "pango-1.36.8")
  |> from (fun () ->
    patch "pango\\pango-synthesize-fonts-properly.patch"
    patch "pango\\pango_win32_device_scale.patch"
    //patch "pango\\absolute_size.patch"
    patch "pango\\win32_markup_font_size.patch"
  ) |> ignore

  let slnDir = Path.Combine(buildDir(), "pango-1.36.8", "build", "win32", "vs12")
  CopyDir (slnDir) (Path.Combine("slns", "pango", "build", "win32", "vs12")) (fun _ -> true)

  Path.Combine(slnDir, "pango.sln") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "Release_FC"
                      ]
    }
  )

  install "pango-1.36.8-rel" |> ignore

Target "gtk" <| fun _ ->
  "http://ftp.gnome.org/pub/gnome/sources/gtk+/2.24/gtk+-2.24.26.tar.xz"
  |> download
  |> extract

  Path.Combine(buildDir(), "gtk+-2.24.26")
  |> from (fun () ->
    //patch "gtk\\gtk-revert-scrolldc-commit.patch"
    //patch "gtk\\gtk-bgimg.patch"
    //patch "gtk\\gtk-accel.patch"
    //patch "gtk\\gtk-multimonitor.patch"

    patch "gtk\\0001-aborted-drag-should-leave.patch"
    patch "gtk\\0002-fix-dnd-in-autohide-pads.patch"
    patch "gtk\\0003-choose-ime-based-on-locale.patch"
    patch "gtk\\0004-fix-ime-candidate-location.patch"
    patch "gtk\\0005-set-gdkscreen-resolution.patch"
    patch "gtk\\0006-never-restack-below-temp.patch"
    patch "gtk\\0007-disable-combobox-scrolling.patch"
    patch "gtk\\0008-remove-window-pos-changing-stacking.patch"
    patch "gtk\\0009-dont-override-icon-size-in-mswindows-theme.patch"
    patch "gtk\\0010-treeview-combobox-dont-appear-as-list.patch"
    patch "gtk\\0011-retina-icons.patch"
    patch "gtk\\0012-win32-scale-factor.patch"
    patch "gtk\\0013-win32-dpi-awareness.patch"
    patch "gtk\\0014-fix-win32-exports.patch"
    patch "gtk\\0015-scaled-image-win32.patch"
    patch "gtk\\0016-round-scale-up-to-2-0.patch"
    patch "gtk\\0017-combobox-rendering.patch"
    patch "gtk\\0018-dead-key-fixes.patch"
    patch "gtk\\0019-fix-keyboard-input.patch"
    patch "gtk\\0020-dont-affect-zorder-of-window-stack.patch"
    patch "gtk\\0021-register-classw.patch"
    patch "gtk\\0022-include-math-h.patch"
    patch "gtk\\0022-gtk-draw-child-bg-2.patch"
    patch "gtk\\0023-gtk-highdpi-8.patch"
  )

  let slnDir = Path.Combine(buildDir(), "gtk+-2.24.26", "build", "win32", "vs12")
  Directory.GetFiles(Path.Combine("slns", "gtk", "build", "win32", "vs12"), "*.*")
  |> CopyFiles slnDir

  Path.Combine(slnDir, "gtk+.sln") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = ["Platform", "Win32"
                                    "Configuration", "Release"
                      ]
    }
  )

  install "gtk-2.24.25-rel" |> ignore

Target "zlib" <| fun _ ->
  "zlib-1.2.8.7z" |> extract

  let slnDir = Path.Combine(buildDir(), "zlib-1.2.8", "contrib", "vstudio", "vs12")
  CopyDir (slnDir) (Path.Combine("slns", "zlib", "contrib", "vstudio", "vc12")) (fun _ -> true)

  Path.Combine(slnDir, "zlibvc.sln") |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = [ "Platform", "Win32"
                                     "Configuration", "ReleaseWithoutAsm"
                      ]
    }
  ) |> ignore

  let sourceDir = Path.Combine(buildDir(), "zlib-1.2.8")
  let includeDir = Path.Combine(installDir(), "include")
  ensureDirectory(includeDir)
  [ Path.Combine(sourceDir, "zlib.h"); Path.Combine(sourceDir, "zconf.h") ] |> Copy includeDir

  [Path.Combine(slnDir, "x86", "ZlibDllReleaseWithoutAsm", "zlib1.dll"); Path.Combine(slnDir, "x86", "ZlibDllReleaseWithoutAsm", "zlib1.map"); Path.Combine(slnDir, "x86", "ZlibDllReleaseWithoutAsm", "zlib1.pdb")]
  |> Copy (binDir())

  [Path.Combine(slnDir, "x86", "ZlibDllReleaseWithoutAsm", "zlib1.lib"); Path.Combine(slnDir, "x86", "ZlibStatReleaseWithoutAsm", "zlibstat.lib")]
  |> Copy (libDir())

Target "win-iconv" <| fun _ ->
  "win-iconv-0.0.6.7z" |> extract
  Path.Combine(buildDir(), "win-iconv-0.0.6")
  |> from (fun () ->
        sprintf "-G \"NMake Makefiles\" \"-DCMAKE_INSTALL_PREFIX=%s\" -DCMAKE_BUILD_TYPE=Debug" (installDir())
        |> sh "cmake"
        |> ignore

        sh "nmake" "clean" |> ignore
        sh "nmake" "install" |> ignore
     )
  |> ignore

Target "libpng" <| fun _ ->
  "libpng-1.6.16.7z" |> extract

  let slnDir = Path.Combine(buildDir(), "libpng-1.6.16", "projects", "vc12")
  CopyDir (slnDir) (Path.Combine("slns", "libpng", "projects", "vc12")) (fun _ -> true)

  Path.Combine(slnDir, "pnglibconf", "pnglibconf.vcxproj")
  |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = ["Platform", "Win32"
                                    "Configuration", "Release"
                                    "SolutionDir", slnDir
                      ]
    }
  )

  Path.Combine(slnDir, "libpng", "libpng.vcxproj")
  |> MSBuildHelper.build (fun parameters ->
    { parameters with Targets = ["Build"]
                      Properties = ["Platform", "Win32"
                                    "Configuration", "Release"
                                    "SolutionDir", slnDir
                      ]
    }
  )

  let releaseDir = Path.Combine(buildDir(), "libpng-1.6.16", "projects", "vc12Release")

  [Path.Combine(releaseDir, "libpng16.dll"); Path.Combine(releaseDir, "libpng16.pdb")] |> Copy (binDir())
  Path.Combine(releaseDir, "libpng16.lib") |> CopyFile (libDir())

  Path.Combine(buildDir(), "libpng-1.6.16") |> from (fun () ->
    ["png.h"; "pngconf.h"; "pnglibconf.h"; "pngpriv.h"] |> Copy (Path.Combine(installDir(), "include"))
  )


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
"pango" <== ["cairo"; "harfbuzz"]
"pixman" <== ["libpng"]
"BuildAll" <== ["prep"; "gtk"]

RunTargetOrDefault "BuildAll"
