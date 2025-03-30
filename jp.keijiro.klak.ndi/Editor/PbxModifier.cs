#if UNITY_IOS

using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

namespace Klak.Ndi.Editor {

// Xcode project file modifier for iOS support
public class PbxModifier
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget buildTarget, string path)
    {
        if (buildTarget == BuildTarget.iOS)
        {
            ModifyProjectFile(path);
            ModifyPlistFile(path);
        }
    }

    static void ModifyProjectFile(string basePath)
    {
        var path = PBXProject.GetPBXProjectPath(basePath);

        var proj = new PBXProject();
        proj.ReadFromFile(path);

        var target = proj.GetUnityFrameworkTargetGuid();
        var libPath = "/Library/NDI\\ SDK\\ for\\ Apple/lib/iOS";
        proj.AddBuildProperty(target, "LIBRARY_SEARCH_PATHS", libPath);
        proj.AddFrameworkToProject(target, "Accelerate.framework", false);
        proj.AddFrameworkToProject(target, "VideoToolbox.framework", false);
        proj.AddFrameworkToProject(target, "libndi_ios.a", false);

        proj.WriteToFile(path);
    }

    static void ModifyPlistFile(string basePath)
    {
        var path = Path.Combine(basePath, "Info.plist");

        var plist = new PlistDocument();
        plist.ReadFromFile(path);

        var root = plist.root;

        // Bonjour service list
        {
            var key = "NSBonjourServices";
            if (root.values.ContainsKey(key))
                root.values[key].AsArray().AddString("_ndi._tcp");
            else
                root.CreateArray(key).AddString("_ndi._tcp");
        }

        // LAN usage description
        {
            var key = "NSLocalNetworkUsageDescription";
            var desc = "FingerWorks uses the local network to discover and receive NDI video feeds from other devices on the same network.";
            if (!root.values.ContainsKey(key)) root.SetString(key, desc);
        }

        plist.WriteToFile(path);
    }
}

} // namespace Klak.Ndi.Editor

#endif
