using BepInEx;
using FortniteEmoteWheel.Classes.Admin;
using FortniteEmoteWheel.Classes.EzVersionChecking;
using FortniteEmoteWheel.Patches;
using Photon.Pun;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace FortniteEmoteWheel
{
    [BepInIncompatibility("org.hamburbur.menu")] // blame ZlothY for his rig manager :/
    [BepInPlugin(Constants.Guid, Constants.Name, Constants.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;

        public static Transform firstPersonCameraTransform;
        public static Transform thirdPersonCameraTransform;

        private static Transform kyleRoot;
        private static Transform kyleSpine2;
        private static Transform kyleLeftHand;
        private static Transform kyleRightHand;
        private static Transform kyleHead;

        private static Coroutine recorderCoroutine;

        public void Start() =>        
            GorillaTagger.OnPlayerSpawned(OnGameInit);

        private void OnGameInit()
        {
            HarmonyPatches.ApplyHarmonyPatches();
            Console.LoadConsole();

            gameObject.AddComponent<HamburburData>();
            gameObject.AddComponent<TrackerManager>();

            GameObject deezDataContainer = new("FEWHamburburData");
            deezDataContainer.AddComponent<HamburburData>();

            VersionCheckingInitializer.StartVersionChecking();

            if (VersionCheckingInitializer.VersionOutdated)
                StartCoroutine(CreateOutdatedCountdown());

            firstPersonCameraTransform = GorillaTagger.Instance.mainCamera.transform;
            thirdPersonCameraTransform = GorillaTagger.Instance.thirdPersonCamera.transform.GetChild(0);

            Hashtable properties = new()
            {
                    {
                            "Deez's FortniteEmoteWheel",
                            $"Made by Deez - Version {Constants.Hashkey}"
                    },
            };

            PhotonNetwork.LocalPlayer.SetCustomProperties(properties);
        }

        private IEnumerator CreateOutdatedCountdown()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/DeezVrOfficial/FortniteEmoteWheel/releases/latest",
                UseShellExecute = true,
            });

            GameObject stumpObj = new("FEWOutdatedCountdownObject");
            Canvas canvas = stumpObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            CanvasScaler scaler = stumpObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            stumpObj.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = stumpObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(9f, 9f);
            stumpObj.transform.position = new Vector3(-66.9419f, 12.35f, -82.6273f);
            stumpObj.transform.localScale = Vector3.one * 0.003f;
            stumpObj.transform.Rotate(0f, 180f, 0f);

            float timer = 20f;
            int lastSecond = Mathf.CeilToInt(timer);

            TextMeshProUGUI textObj = new GameObject("FEWOutdatedText").AddComponent<TextMeshProUGUI>();
            textObj.transform.SetParent(stumpObj.transform, false);
            textObj.fontSize = 30f;
            textObj.alignment = TextAlignmentOptions.Center;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchoredPosition = new Vector2(0f, -50f);
            textRect.sizeDelta = new Vector2(900f, 700f);

            textObj.text = VersionCheckingInitializer.OutdatedMessage +
                           $"<color=yellow> Game will close in</color> {lastSecond} <color=yellow>seconds</color>";

            Texture2D tex = LoadEmbeddedImage("FortniteEmoteWheel.Resources.error.png");
            if (tex != null)
            {
                GameObject imageObj = new("FEWWarningIcon");
                imageObj.transform.SetParent(stumpObj.transform, false);
                Image uiImage = imageObj.AddComponent<Image>();

                RectTransform imgRect = imageObj.GetComponent<RectTransform>();
                float targetHeight = 115f;
                float aspect = (float)tex.width / tex.height;
                float targetWidth = targetHeight * aspect;

                imgRect.sizeDelta = new Vector2(targetWidth, targetHeight);
                imgRect.anchoredPosition = new Vector2(0f, 100f);

                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                uiImage.sprite = sprite;
            }

            while (timer > 0f)
            {
                if (stumpObj != null && Camera.main != null)
                {
                    stumpObj.transform.LookAt(Camera.main.transform.position);
                    stumpObj.transform.Rotate(0f, 180f, 0f);
                }

                timer -= Time.deltaTime;
                int currentSecond = Mathf.CeilToInt(timer);

                if (currentSecond != lastSecond)
                {
                    lastSecond = currentSecond;
                    textObj.text = VersionCheckingInitializer.OutdatedMessage +
                                   $"<color=yellow>Game will close in</color> {lastSecond} <color=yellow>seconds</color>";
                }

                yield return null;
            }

            yield return new WaitForSeconds(1f);
            Application.Quit();
        }
        
        private static AssetBundle assetBundle;
        public static GameObject LoadAsset(string assetName)
        {
            GameObject gameObject = null;

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortniteEmoteWheel.Resources.fn");
            if (stream != null)
            {
                if (assetBundle == null)
                    assetBundle = AssetBundle.LoadFromStream(stream);
                gameObject = Instantiate<GameObject>(assetBundle.LoadAsset<GameObject>(assetName));
            }
            else
                Debug.LogError("Failed to load asset from resource: " + assetName);

            return gameObject;
        }

        public static GameObject audiomgr = null;
        public static void Play2DAudio(AudioClip sound, float volume, bool looping = false)
        {
            if (sound == null)
                return;

            if (audiomgr == null)
            {
                audiomgr = new GameObject("2DAudioMgr");
                AudioSource temp = audiomgr.AddComponent<AudioSource>();
                temp.spatialBlend = 0f;
            }

            AudioSource ausrc = audiomgr.GetComponent<AudioSource>();
            ausrc.volume = volume;
            ausrc.loop = looping;

            if (!looping)
                ausrc.PlayOneShot(sound);
            else
            {
                ausrc.clip = sound;
                ausrc.Play();
            }
        }

        public static Dictionary<string, AudioClip> audioPool = new Dictionary<string, AudioClip> { };
        public static AudioClip LoadSoundFromResource(string resourcePath)
        {
            AudioClip sound = null;

            if (!audioPool.ContainsKey(resourcePath))
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FortniteEmoteWheel.Resources.fn");
                if (stream != null)
                {
                    if (assetBundle == null)
                        assetBundle = AssetBundle.LoadFromStream(stream);

                    sound = assetBundle.LoadAsset(resourcePath) as AudioClip;
                    if (sound != null)
                    {
                        sound.LoadAudioData();
                        audioPool.Add(resourcePath, sound);
                    }
                    else
                    {
                        Debug.LogError("[FortniteEmoteWheel] Sound asset not found in bundle: " + resourcePath);
                    }
                }
                else
                {
                    Debug.LogError("Failed to load sound from resource: " + resourcePath);
                }
            }
            else
                sound = audioPool[resourcePath];

            return sound;
        }

        private static readonly List<GameObject> portedCosmetics = new List<GameObject> { };
        public static void DisableCosmetics()
        {
            try
            {
                VRRig.LocalRig.transform.Find("rig/head/gorillaface").gameObject.layer = LayerMask.NameToLayer("Default");
                foreach (GameObject Cosmetic in VRRig.LocalRig.cosmetics)
                {
                    if (Cosmetic.activeSelf && Cosmetic.transform.parent == VRRig.LocalRig.mainCamera.transform.Find("HeadCosmetics"))
                    {
                        portedCosmetics.Add(Cosmetic);
                        Cosmetic.transform.SetParent(VRRig.LocalRig.headMesh.transform, false);
                        Cosmetic.transform.localPosition += new Vector3(0f, 0.1333f, 0.1f);
                    }
                }
            }
            catch { }
        }

        public static void EnableCosmetics()
        {
            VRRig.LocalRig.transform.Find("rig/head/gorillaface").gameObject.layer = LayerMask.NameToLayer("MirrorOnly");
            foreach (GameObject Cosmetic in portedCosmetics)
            {
                Cosmetic.transform.SetParent(VRRig.LocalRig.mainCamera.transform.Find("HeadCosmetics"), false);
                Cosmetic.transform.localPosition -= new Vector3(0f, 0.1333f, 0.1f);
            }
            portedCosmetics.Clear();
        }

        public static GameObject Kyle;
        public static float emoteTime;

        public static Vector3 archivePosition;

        public static void Emote(string emoteName, string emoteSound, float animationTime = -1f, bool looping = false)
        {
            if (Kyle != null)
                Destroy(Kyle);

            if (recorderCoroutine != null)
            {
                Instance.StopCoroutine(recorderCoroutine);
                recorderCoroutine = null;
            }

            VRRig.LocalRig.enabled = false;
            DisableCosmetics();

            Play2DAudio(LoadSoundFromResource("play"), 0.5f);
    
            archivePosition = GorillaTagger.Instance.transform.position;
            GorillaLocomotion.GTPlayer.Instance.GetControllerTransform(false).parent.rotation *= Quaternion.Euler(0f, 180f, 0f);

            Kyle = LoadAsset("Rig");
            Kyle.transform.position = VRRig.LocalRig.transform.Find("rig/body_pivot").position - new Vector3(0f, 1.15f, 0f);
            Kyle.transform.rotation = VRRig.LocalRig.transform.Find("rig/body_pivot").rotation;

            kyleRoot = Kyle.transform.Find("KyleRobot/ROOT/Hips/Spine1");
            kyleSpine2 = kyleRoot.Find("Spine2");
            kyleLeftHand = kyleSpine2.Find("LeftShoulder/LeftUpperArm/LeftArm/LeftHand");
            kyleRightHand = kyleSpine2.Find("RightShoulder/RightUpperArm/RightArm/RightHand");
            kyleHead = kyleSpine2.Find("Neck/Head");

            Kyle.transform.Find("KyleRobot/RobotKile").gameObject.GetComponent<Renderer>().renderingLayerMask = 0;

            Animator KyleRobot = Kyle.transform.Find("KyleRobot").GetComponent<Animator>();
            KyleRobot.enabled = true;

            AnimationClip Animation = null;
            foreach (AnimationClip Clip in KyleRobot.runtimeAnimatorController.animationClips)
            {
                if (Clip.name == emoteName)
                {
                    Animation = Clip;
                    break;
                }
            }

            if (Animation == null)
            {
                Debug.LogError("[FortniteEmoteWheel] Emote animation not found: " + emoteName);
                emoteTime = Time.time;
                return;
            }

            Animation.wrapMode = looping ? WrapMode.Loop : WrapMode.Default;
            KyleRobot.Play(Animation.name);

            AudioClip Sound = LoadSoundFromResource(emoteSound);
            Play2DAudio(Sound, 0.5f, looping);

            if (Sound != null && GorillaTagger.Instance.myRecorder != null)
                recorderCoroutine = Instance.StartCoroutine(SetRecorderClipWhenReady(Sound));

            emoteTime = Time.time + (animationTime > 0f ? animationTime : Animation.length) + (looping ? 999999999999999f : 0);
        }

        private static IEnumerator SetRecorderClipWhenReady(AudioClip Sound)
        {
            while (Sound.loadState == AudioDataLoadState.Loading)
                yield return null;

            recorderCoroutine = null;

            if (Sound.loadState != AudioDataLoadState.Loaded)
                yield break;

            GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
            GorillaTagger.Instance.myRecorder.AudioClip  = Sound;
            GorillaTagger.Instance.myRecorder.RestartRecording(true);
        }

        public static Vector3 World2Player(Vector3 world) => world - GorillaTagger.Instance.bodyCollider.transform.position + GorillaTagger.Instance.transform.position;

        public void Update()
        {
            if (GorillaLocomotion.GTPlayer.Instance == null)
                return;

            if (Classes.Wheel.instance == null && VRRig.LocalRig != null)
            {
                GameObject Wheel = Plugin.LoadAsset("Wheel");
                Wheel.transform.SetParent(VRRig.LocalRig.transform.Find("rig/hand.R"), false);
                Wheel.AddComponent<Classes.Wheel>();
            }

            if (Time.time < emoteTime)
            {
                if (Kyle != null)
                {
                    VRRig.LocalRig.enabled = false;

                    GorillaTagger.Instance.transform.position = World2Player(Kyle.transform.position + (Kyle.transform.forward * 1.5f) + new Vector3(0f, 1.15f, 0f)) + new Vector3(0f, 0.5f, 0f);
                    GorillaTagger.Instance.leftHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position; 
                    GorillaTagger.Instance.rightHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;

                    GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;

                    VRRig.LocalRig.transform.position = kyleSpine2.position - (kyleSpine2.right / 2.5f);
                    VRRig.LocalRig.transform.rotation = Quaternion.Euler(new Vector3(0f, kyleSpine2.rotation.eulerAngles.y, 0f));

                    VRRig.LocalRig.leftHand.rigTarget.transform.position = kyleLeftHand.position;
                    VRRig.LocalRig.rightHand.rigTarget.transform.position = kyleRightHand.position;

                    VRRig.LocalRig.leftHand.rigTarget.transform.rotation = kyleLeftHand.rotation * Quaternion.Euler(0, 0, 75);
                    VRRig.LocalRig.rightHand.rigTarget.transform.rotation = kyleRightHand.rotation * Quaternion.Euler(180, 0, -75);

                    VRRig.LocalRig.head.rigTarget.transform.rotation = kyleHead.rotation * Quaternion.Euler(0f, 0f, 90f);
                }
            }
            else
            {
                if (Kyle != null)
                {
                    VRRig.LocalRig.enabled = true;
                    EnableCosmetics();

                    Destroy(Kyle);
                    kyleRoot = kyleSpine2 = kyleLeftHand = kyleRightHand = kyleHead = null;

                    if (recorderCoroutine != null)
                    {
                        StopCoroutine(recorderCoroutine);
                        recorderCoroutine = null;
                    }

                    if (GorillaTagger.Instance.myRecorder != null)
                    {
                        GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
                        GorillaTagger.Instance.myRecorder.AudioClip = null;
                        GorillaTagger.Instance.myRecorder.RestartRecording(true);
                    }

                    GorillaTagger.Instance.transform.position = archivePosition;
                    GorillaLocomotion.GTPlayer.Instance.GetControllerTransform(false).parent.rotation *= Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }

        private Texture2D LoadEmbeddedImage(string resourcePath)
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);

            if (stream == null)
                return null;

            byte[] imageData = new byte[stream.Length];
            stream.Read(imageData, 0, imageData.Length);
            Texture2D texture = new(2, 2);
            texture.LoadImage(imageData);

            return texture;
        }
    }
}