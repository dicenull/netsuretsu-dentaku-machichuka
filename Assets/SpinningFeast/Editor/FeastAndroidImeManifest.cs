#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace SpinningFeast.Editor
{
    /// <summary>Keep the Activity surface full-screen while Android overlays the IME.</summary>
    public sealed class FeastAndroidImeManifest : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace="http://schemas.android.com/apk/res/android";
        public int callbackOrder=>50;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // Unity regenerates unityLibrary for every build. Patch only the Unity
            // entry Activities and retain all other generated manifest attributes.
            var doc=new XmlDocument();
            string manifest=Path.Combine(path,"src","main","AndroidManifest.xml");
            doc.Load(manifest);
            int changed=0;
            foreach(XmlElement activity in doc.SelectNodes("/manifest/application/activity"))
            {
                string name=activity.GetAttribute("name",AndroidNamespace);
                if(name!="com.unity3d.player.UnityPlayerGameActivity" && name!="com.unity3d.player.UnityPlayerActivity")continue;
                string mode=activity.GetAttribute("windowSoftInputMode",AndroidNamespace);
                var modes=new System.Collections.Generic.List<string>();
                foreach(string value in mode.Split('|'))
                {if(value.StartsWith("state",System.StringComparison.Ordinal))modes.Add(value);}
                modes.Add("adjustNothing");
                activity.SetAttribute("windowSoftInputMode",AndroidNamespace,string.Join("|",modes));
                changed++;
            }
            if(changed==0)throw new System.InvalidOperationException("No Unity Android entry activity found for stable IME layout.");
            doc.Save(manifest);
            Debug.Log("FEAST_ANDROID_IME_MANIFEST_PASS adjustNothing");
        }
    }
}
#endif
