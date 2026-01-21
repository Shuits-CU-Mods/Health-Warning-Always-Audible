using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static HealthWarningAlwaysAudible.HealthWarningAlwaysAudible;
using static HealthWarningAlwaysAudible.SharedState;

namespace HealthWarningAlwaysAudible
{
    public static class SharedState
    {
        public static AudioSource woundAudio;
    }

    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class HealthWarningAlwaysAudible : BaseUnityPlugin
    {
        public static ManualLogSource logger;
        public const string pluginGuid = "shushu.casualtiesunknown.healthwarningalwaysaudible";
        public const string pluginName = "Health Warning Always Audible";

        // Year.Month.Version.Bugfix
        public const string pluginVersion = "26.1.1.1";

        public static HealthWarningAlwaysAudible Instance;

        public static int isOkayToPatch = 0;
        public static string activeVersion = "";

        public void Awake()
        {
            Instance = this;
            logger = Logger;

            logger.LogInfo("Awake() ran - mod loaded!");

            Harmony harmony = new Harmony(pluginGuid);

            StartCoroutine(CheckGameVersion(harmony));
        }

        public static void Log(string message)
        {
            logger.LogInfo(message);
        }

        public static IEnumerator CheckGameVersion(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(PreRunScript), "Awake"), prefix: new HarmonyMethod(typeof(HealthWarningAlwaysAudible).GetMethod("VersionCheck")));

            while (true)
            {
                if (isOkayToPatch == 1)
                {
                    break;
                }
                if (isOkayToPatch == -1)
                {
                    harmony.Unpatch(AccessTools.Method(typeof(PreRunScript), "Awake"), HarmonyPatchType.Prefix);
                    logger.LogError($"Game version is not {activeVersion}, mod exiting...");
                    yield break;
                }
                yield return null;
            }

            harmony.Unpatch(AccessTools.Method(typeof(PreRunScript), "Awake"), HarmonyPatchType.Prefix);

            List<MethodInfo> patches = typeof(MyPatches).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).ToList();
            foreach (MethodInfo patch in patches)
            {
                try
                {
                    string[] splitName = patch.Name.Replace("__", "$").Split('_');
                    for (int i = 0; i < splitName.Length; i++)
                        splitName[i] = splitName[i].Replace("$", "_");
                    if (splitName.Length < 3)
                        throw new Exception($"Patch method is named incorrectly\nPlease make sure the Patch method is named in the following pattern:\n\tTargetClass_TargetMethod_PatchType[_Version]");

                    if (splitName.Length >= 4)
                        if (splitName[3] != activeVersion)
                        {
                            Log($"{patch.Name} is not supported by version {activeVersion}");
                            continue;
                        }

                    string targetType = splitName[0];
                    MethodType targetMethodType;
                    if (splitName[1].Contains("get_"))
                        targetMethodType = MethodType.Getter;
                    else if (splitName[1].Contains("set_"))
                        targetMethodType = MethodType.Setter;
                    else
                        targetMethodType = MethodType.Normal;
                    string ogTargetMethod = splitName[1];
                    string targetMethod = splitName[1].Replace("get_", "").Replace("set_", "");
                    string patchType = splitName[2];

                    MethodInfo ogScript = null;
                    switch (targetMethodType)
                    {
                        case MethodType.Enumerator:
                        case MethodType.Normal:
                            ogScript = AccessTools.Method(AccessTools.TypeByName(targetType), targetMethod);
                            break;

                        case MethodType.Getter:
                            ogScript = AccessTools.PropertyGetter(AccessTools.TypeByName(targetType), targetMethod);
                            break;

                        case MethodType.Setter:
                        case MethodType.Constructor:
                        case MethodType.StaticConstructor:
                        default:
                            throw new Exception($"Unknown patch method\nPatch method type \"{targetMethodType}\" currently has no handling");
                    }

                    MethodInfo patchScript = typeof(MyPatches).GetMethod(patch.Name);
                    List<string> validPatchTypes = new List<string>
                    {
                        "Prefix",
                        "Postfix",
                        "Transpiler"
                    };
                    if (ogScript == null || patchScript == null || !validPatchTypes.Contains(patchType))
                    {
                        throw new Exception("Patch method is named incorrectly\nPlease make sure the Patch method is named in the following pattern:\n\tTargetClass_TargetMethod_PatchType[_Version]");
                    }
                    HarmonyMethod harmonyMethod = new HarmonyMethod(patchScript)
                    {
                        methodType = targetMethodType
                    };

                    HarmonyMethod postfix = null;
                    HarmonyMethod prefix = null;
                    HarmonyMethod transpiler = null;
                    switch (patchType)
                    {
                        case "Prefix":
                            prefix = harmonyMethod;
                            break;

                        case "Postfix":
                            postfix = harmonyMethod;
                            break;

                        case "Transpiler":
                            transpiler = harmonyMethod;
                            break;
                    }
                    harmony.Patch(ogScript, prefix: prefix, postfix: postfix, transpiler: transpiler);
                    Log("Patched " + targetType + "." + targetMethod + " as a " + patchType);
                }
                catch (Exception exception)
                {
                    logger.LogError($"Failed to patch {patch.Name}");
                    logger.LogError(exception);
                }
            }

            // If you have any PreRunScript Awake/Start patches, uncomment next line
            //SceneManager.LoadScene("PreGen");
        }

        public static void VersionCheck()
        {
            Dictionary<string, string[]> supportedVersions = new Dictionary<string, string[]>
            {
                ["Text (TMP) (18)"] = new string[] { "V5 Pre-testing 5", "5_5" },
                ["Text (TMP) (17)"] = new string[] { "V5 Pre-testing 4", "5_4" }
            };
            foreach (var supportedVersion in supportedVersions)
            {
                if (isOkayToPatch == 0)
                {
                    GameObject obj = GameObject.Find(supportedVersion.Key);
                    if (obj == null)
                        continue;
                    if (obj.GetComponent<TextMeshProUGUI>().text.Contains(supportedVersion.Value[0]))
                    {
                        activeVersion = supportedVersion.Value[1];
                        isOkayToPatch = 1;
                        break;
                    }
                }
            }
            if (isOkayToPatch == 0)
                isOkayToPatch = -1;
        }
    }

    public class MyPatches
    {
        [HarmonyPatch(typeof(WoundView), "Update")]
        [HarmonyPrefix]
        public static bool WoundView_Update_Prefix(WoundView __instance)
        {
            __instance.checkTime += Time.unscaledDeltaTime;
            if (__instance.gameObject.activeInHierarchy)
            {
                __instance.UpdateView();
            }
            return false;
        }

        [HarmonyPatch(typeof(WoundView), "Awake")]
        [HarmonyPostfix]
        public static void WoundView_Awake_Postfix(WoundView __instance)
        {
            __instance.gameObject.GetComponent<AudioSource>().clip = null;
            //__instance.gameObject.GetComponent<AudioSource>().enabled = false;
        }

        [HarmonyPatch(typeof(PlayerCamera), "Awake")]
        [HarmonyPrefix]
        public static void PlayerCamera_Awake_Prefix(PlayerCamera __instance)
        {
            woundAudio = __instance.gameObject.AddComponent<AudioSource>();
            woundAudio.bypassReverbZones = true;
            woundAudio.dopplerLevel = 0;
            woundAudio.loop = true;
            woundAudio.playOnAwake = false;
            woundAudio.velocityUpdateMode = AudioVelocityUpdateMode.Auto;
            AudioClip clip = Resources.FindObjectsOfTypeAll<AudioClip>().FirstOrDefault(c => c.name == "healthpanel-alert");
            if (clip != null)
            {
                woundAudio.clip = clip;
            }
        }

        [HarmonyPatch(typeof(Body), "Update")]
        [HarmonyPrefix]
        public static void Body_Update_Prefix(Body __instance)
        {
            if (__instance == null || woundAudio == null)
                return;
            bool flag = !WorldGeneration.unchipped && __instance.isCriticallyDying && __instance.alive;
            if (flag && !woundAudio.isPlaying)
            {
                woundAudio.Play();
                return;
            }
            if (!flag && woundAudio.isPlaying)
            {
                woundAudio.Stop();
            }
        }
    }
}