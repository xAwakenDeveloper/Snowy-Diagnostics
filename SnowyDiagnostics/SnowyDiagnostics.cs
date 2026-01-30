using MSCLoader;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SnowyDiagnostics
{
    public class SnowyDiagnostics : Mod
    {
        public override string ID => "SnowyDiagnostics";
        public override string Name => "Snowy Diagnostics";
        public override string Author => "Awaken Developer";
        public override string Version => "2.0.0";
        public override string Description => "Displays detailed performance statistics in a modern way!";
        public override byte[] Icon { get => base.Icon; set => base.Icon = value; }
        public override Game SupportedGames => Game.MyWinterCar;

        private bool showWindow;
        private bool showDetails;

        private Rect windowRect = new Rect(Screen.width - 350, 40, 320, 210);
        private bool guiInitialized;
        private Texture2D bgTexture;
        private Texture2D borderTexture;
        private Texture2D roundedBoxTexture;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle windowStyle;

        private readonly Color bgColor = new Color(0.08f, 0.09f, 0.12f, 0.85f);
        private readonly Color borderColor = new Color(0.12f, 0.13f, 0.16f, 1f);

        private float fps;
        private float minFPS = float.MaxValue;
        private float maxFPS;
        private int frames;
        private float fpsTimer;
        private float frametimeMs;

        private string sceneName = "Unknown";
        private string unityVersion;
        private int loadedObjects;
        private int visibleObjects;
        private GameObject[] cachedObjects;
        private Camera mainCam;
        private float objectTimer;

        private float cpuUsage;
        private TimeSpan lastCpu;
        private float cpuTimer;

        private string cpuName;
        private string gpuName;
        private string systemName;
        private string systemVersion;
        private string msclVersion;
        private int loadedMods;

        private Dictionary<string, string> graphicsSettings = new Dictionary<string, string>();
        private string qualityPresetName = "Unknown";
        private int qualityPresetIndex = -1;
        private float cachedFOV = 0f;
        private float cachedDrawDistance = 0f;

        private readonly string[] graphicsKeys = new string[]
        {
            "Antialiasing",
            "Bloom",
            "Sunshafts",
            "Contrast Enchance",
            "HDR",
            "Sun Shadows"
        };

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, OnLoad);
            SetupFunction(Setup.Update, OnUpdate);
            SetupFunction(Setup.OnGUI, OnGUI);
            SetupFunction(Setup.ModSettings, ModSettings);
        }

        private void ModSettings()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("SnowyDiagnostics.icon.png"))
            {
                if (stream != null)
                {
                    byte[] iconBytes = new byte[stream.Length];
                    stream.Read(iconBytes, 0, iconBytes.Length);
                    Icon = iconBytes;
                }
            }
        }

        private void OnLoad()
        {
            ModConsole.Log("[<color=#34d8eb>Snowy Diagnostics</color>] Mod loaded successfully! Press <color=#34d8eb>F6</color> to show menu.");

            unityVersion = Application.unityVersion;
            lastCpu = Process.GetCurrentProcess().TotalProcessorTime;

            cpuName = SystemInfo.processorType;
            gpuName = SystemInfo.graphicsDeviceName;

            string os = SystemInfo.operatingSystem;
            int s = os.IndexOf('(');
            int e = os.IndexOf(')');
            systemVersion = (s >= 0 && e > s) ? os.Substring(s + 1, e - s - 1) : "Unknown";

            int build = 0;
            string[] parts = systemVersion.Split('.');
            if (parts.Length >= 3) int.TryParse(parts[2], out build);
            systemName = build >= 22000 ? "Windows 11 64-bit" : "Windows 10 64-bit";

            msclVersion = ModLoader.MSCLoader_Ver;
            loadedMods = ModLoader.LoadedMods.Count - 2;

            try
            {
                cachedObjects = GameObject.FindObjectsOfType<GameObject>();
                loadedObjects = cachedObjects != null ? cachedObjects.Length : 0;
            }
            catch
            {
                cachedObjects = null;
                loadedObjects = 0;
            }

            PopulateBasicGraphics();

            try { RetrieveGraphicSettings(forceRefresh: true); } catch { }
        }

        private void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F6))
                showWindow = !showWindow;

            frametimeMs = Time.unscaledDeltaTime * 1000f;

            frames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fps = frames / fpsTimer;
                minFPS = Mathf.Min(minFPS, fps);
                maxFPS = Mathf.Max(maxFPS, fps);
                frames = 0;
                fpsTimer = 0f;
            }

            try { sceneName = Application.loadedLevelName; }
            catch { sceneName = "Unknown"; }

            if (cachedObjects == null)
            {
                try
                {
                    cachedObjects = GameObject.FindObjectsOfType<GameObject>();
                    loadedObjects = cachedObjects != null ? cachedObjects.Length : 0;
                }
                catch
                {
                    cachedObjects = null;
                    loadedObjects = 0;
                }
            }

            objectTimer += Time.deltaTime;
            if (objectTimer >= 0.5f)
            {
                if (!mainCam) mainCam = Camera.main;
                visibleObjects = 0;

                if (mainCam && cachedObjects != null)
                {
                    Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCam);
                    foreach (var o in cachedObjects)
                    {
                        if (!o || !o.activeInHierarchy) continue;
                        var r = o.GetComponent<Renderer>();
                        if (r && GeometryUtility.TestPlanesAABB(planes, r.bounds))
                            visibleObjects++;
                    }
                }
                objectTimer = 0f;
            }

            cpuTimer += Time.deltaTime;
            if (cpuTimer >= 1f)
            {
                var p = Process.GetCurrentProcess();
                var now = p.TotalProcessorTime;
                cpuUsage = (float)((now - lastCpu).TotalMilliseconds / (1000f * Environment.ProcessorCount)) * 100f;
                lastCpu = now;
                cpuTimer = 0f;
            }

            try
            {
                if (!mainCam) mainCam = Camera.main;
                if (mainCam)
                {
                    cachedFOV = mainCam.fieldOfView;
                    cachedDrawDistance = mainCam.farClipPlane;
                }
            }
            catch { }
        }

        void OnGUI()
        {
            if (!showWindow) return;

            if (!guiInitialized)
            {
                InitGUI();
                guiInitialized = true;
            }

            GUI.skin = null;
            windowRect = GUI.Window(1337, windowRect, DrawWindow, "");
        }

        void InitGUI()
        {
            bgTexture = CreateSolid(bgColor);
            borderTexture = CreateSolid(borderColor);
            roundedBoxTexture = CreateRounded(32);

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = Color.white }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white, background = CreateSolid(borderColor) },
                hover = { background = CreateSolid(borderColor + new Color(0.05f, 0.05f, 0.05f)) },
                active = { background = CreateSolid(borderColor + new Color(0.05f, 0.05f, 0.05f)) },
                padding = new RectOffset(12, 12, 6, 6)
            };

            var clear = CreateSolid(new Color(0, 0, 0, 0));
            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = clear;
            windowStyle.active.background = clear;
            windowStyle.hover.background = clear;
        }

        void DrawWindow(int id)
        {
            int topBar = 18;
            float height = showDetails ? 855f : 200f;
            windowRect.height = height;

            GUI.DrawTexture(new Rect(0, 0, windowRect.width, height), borderTexture);
            GUI.DrawTexture(new Rect(1, topBar, windowRect.width - 2, height - topBar - 1), bgTexture);

            GUI.Label(new Rect(0, topBar + 4, windowRect.width, 24),
                "<size=18><b><color=#34d8eb>Snowy Diagnostics</color></b></size>", labelStyle);

            float y = topBar + 34;
            float gap = 12;
            float boxW = 85;
            float boxH = 70;
            float startX = (windowRect.width - (boxW * 3 + gap * 2)) / 2;

            DrawBox("FPS", ((int)fps).ToString(), new Rect(startX, y, boxW, boxH));
            DrawBox("MIN", ((int)minFPS).ToString(), new Rect(startX + boxW + gap, y, boxW, boxH));
            DrawBox("MAX", ((int)maxFPS).ToString(), new Rect(startX + (boxW + gap) * 2, y, boxW, boxH));

            y += boxH + 10;

            float btnW = 120;
            if (GUI.Button(new Rect((windowRect.width - btnW) / 2, y, btnW, 26),
                showDetails ? "HIDE DETAILS" : "SHOW DETAILS", buttonStyle))
                showDetails = !showDetails;

            y += 34;

            if (showDetails)
            {
                GUI.Label(new Rect(0, y, windowRect.width, 22),
                    "<size=14><b><color=#34d8eb>GAME INFORMATIONS</color></b></size>", labelStyle);
                y += 26;

                DrawLine(ref y, "FRAMETIME", $"{frametimeMs:F2} ms");
                DrawLine(ref y, "CURRENT SCENE", sceneName);
                DrawLine(ref y, "UNITY VERSION", unityVersion);
                DrawLine(ref y, "OBJECTS LOADED", loadedObjects.ToString());
                DrawLine(ref y, "OBJECTS VISIBLE", visibleObjects.ToString());

                GUI.DrawTexture(new Rect(10, y + 4, windowRect.width - 20, 1), borderTexture);
                y += 14;

                GUI.Label(new Rect(0, y, windowRect.width, 22),
                    "<size=14><b><color=#34d8eb>GRAPHICS SETTINGS</color></b></size>", labelStyle);
                y += 26;

                float refreshW = 140;
                if (GUI.Button(new Rect((windowRect.width - refreshW) / 2, y, refreshW, 22), "REFRESH GRAPHICS", buttonStyle))
                {
                    try
                    {
                        cachedObjects = GameObject.FindObjectsOfType<GameObject>();
                        loadedObjects = cachedObjects != null ? cachedObjects.Length : 0;
                    }
                    catch
                    {
                        cachedObjects = null;
                        loadedObjects = 0;
                    }

                    ModConsole.Log("[<color=#34d8eb>Snowy Diagnostics</color>] Refreshed graphics settings!");

                    PopulateBasicGraphics();
                    RetrieveGraphicSettings(forceRefresh: true);
                }

                y += 28;
                y += 6;

                DrawLine(ref y, "QUALITY PRESET", $"{qualityPresetName} [{qualityPresetIndex}]");
                DrawLine(ref y, "FIELD OF VIEW", $"{cachedFOV:F1}");
                DrawLine(ref y, "DRAW DISTANCE", $"{cachedDrawDistance:F1}");

                foreach (var key in graphicsKeys)
                {
                    string val;
                    if (!graphicsSettings.TryGetValue(key, out val))
                        val = "Unknown";
                    DrawLine(ref y, key.ToUpper(), val);
                }

                GUI.DrawTexture(new Rect(10, y + 6, windowRect.width - 20, 1), borderTexture);
                y += 10;

                GUI.Label(new Rect(0, y, windowRect.width, 22),
                    "<size=14><b><color=#34d8eb>SYSTEM INFORMATIONS</color></b></size>", labelStyle);
                y += 26;

                DrawLine(ref y, "SYSTEM", systemName);
                DrawLine(ref y, "SYSTEM VERSION", systemVersion);
                DrawLine(ref y, "CPU", cpuName);
                DrawLine(ref y, "GPU", gpuName);
                DrawLine(ref y, "CPU USAGE (GAME)", $"{cpuUsage:F1}%");

                GUI.DrawTexture(new Rect(10, y + 6, windowRect.width - 20, 1), borderTexture);
                y += 18;

                GUI.Label(new Rect(0, y, windowRect.width, 22),
                    "<size=14><b><color=#34d8eb>MSCLOADER</color></b></size>", labelStyle);
                y += 26;

                DrawLine(ref y, "MSCLOADER VERSION", msclVersion.ToString());
                DrawLine(ref y, "LOADED MODS", loadedMods.ToString());

                GUI.DrawTexture(new Rect(10, y + 6, windowRect.width - 20, 1), borderTexture);
                y += 10;
            }

            GUI.Label(new Rect(0, height - 30, windowRect.width, 16),
                "<size=10><color=#8899AA>Press F6 to toggle the window</color></size>", labelStyle);

            GUI.DragWindow(new Rect(0, 0, windowRect.width, topBar));
        }

        void DrawLine(ref float y, string n, string v)
        {
            GUI.Label(new Rect(0, y, windowRect.width, 20),
                $"<size=12><b><color=#259ba8>{n}:</color></b> <b>{v}</b></size>", labelStyle);
            y += 22;
        }

        void DrawBox(string l, string v, Rect r)
        {
            GUI.color = new Color(0.2f, 0.85f, 0.9f, 0.2f);
            GUI.DrawTexture(r, roundedBoxTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(r.x, r.y + 6, r.width, 20),
                $"<size=14><b><color=#34d8eb>{l}</color></b></size>", labelStyle);
            GUI.Label(new Rect(r.x, r.y + 32, r.width, 26),
                $"<size=16><b>{v}</b></size>", labelStyle);
        }

        Texture2D CreateSolid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        Texture2D CreateRounded(int r)
        {
            int s = 256;
            var t = new Texture2D(s, s, TextureFormat.ARGB32, false);
            for (int x = 0; x < s; x++)
                for (int y = 0; y < s; y++)
                {
                    bool cut =
                        (x < r && y < r && Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) > r) ||
                        (x > s - r && y < r && Vector2.Distance(new Vector2(x, y), new Vector2(s - r, r)) > r) ||
                        (x < r && y > s - r && Vector2.Distance(new Vector2(x, y), new Vector2(r, s - r)) > r) ||
                        (x > s - r && y > s - r && Vector2.Distance(new Vector2(x, y), new Vector2(s - r, s - r)) > r);
                    t.SetPixel(x, y, cut ? Color.clear : Color.white);
                }
            t.Apply();
            return t;
        }

        private void PopulateBasicGraphics()
        {
            try
            {
                qualityPresetIndex = QualitySettings.GetQualityLevel();
                var qnames = QualitySettings.names;
                if (qnames != null && qualityPresetIndex >= 0 && qualityPresetIndex < qnames.Length)
                    qualityPresetName = qnames[qualityPresetIndex];
                else
                    qualityPresetName = "Unknown";
            }
            catch
            {
                qualityPresetName = "Unknown";
                qualityPresetIndex = -1;
            }
            try
            {
                if (!mainCam) mainCam = Camera.main;
                if (mainCam)
                {
                    cachedFOV = mainCam.fieldOfView;
                    cachedDrawDistance = mainCam.farClipPlane;
                }
            }
            catch { }
        }

        private void RetrieveGraphicSettings(bool forceRefresh = false)
        {
            graphicsSettings.Clear();
            string aaVal = "Unknown";
            try
            {
                bool foundAny = false;
                bool anyEnabled = false;
                string lastFoundName = null;

                var comps = GameObject.FindObjectsOfType<Component>();
                if (comps != null)
                {
                    foreach (var c in comps)
                    {
                        if (c == null) continue;
                        var tn = c.GetType().Name.ToLower();
                        if (tn.Contains("fxaa") || tn.Contains("taa") || tn.Contains("smaa") || tn.Contains("antialias") || tn.Contains("temporal") || tn.Contains("postprocess") || tn.Contains("postprocessing") || tn.Contains("msaa"))
                        {
                            foundAny = true;
                            lastFoundName = c.GetType().Name;
                            if (IsComponentEnabled(c, out string state) && state == "ON")
                            {
                                anyEnabled = true;
                                aaVal = $"ON ({c.GetType().Name})";
                                break;
                            }
                            else
                            {
                                aaVal = $"OFF ({c.GetType().Name})";
                            }
                        }
                    }
                }

                if (!foundAny)
                {
                    var ppInspect = TryInspectPostProcessFor("antialias");
                    if (!string.IsNullOrEmpty(ppInspect) && ppInspect != "Unknown")
                    {
                        if (ppInspect == "ON" || ppInspect == "OFF")
                            aaVal = ppInspect;
                        else
                            aaVal = $"ON ({ppInspect})";
                    }
                    else
                    {
                        try
                        {
                            if (!mainCam) mainCam = Camera.main;
                            if (mainCam != null)
                            {
                                var t = mainCam.GetType();
                                var prop = t.GetProperty("allowMSAA", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                                if (prop != null)
                                {
                                    var get = prop.GetGetMethod();
                                    if (get != null)
                                    {
                                        var v = get.Invoke(mainCam, null);
                                        if (v is bool vb)
                                        {
                                            aaVal = vb ? "ON (Camera MSAA)" : "OFF (Camera MSAA)";
                                        }
                                    }
                                }
                            }
                        }
                        catch { }

                        if (aaVal == "Unknown")
                        {
                            int qaa = QualitySettings.antiAliasing;
                            aaVal = (qaa > 0) ? $"ON ({qaa}x MSAA)" : "OFF";
                        }
                    }
                }
                else
                {

                }
            }
            catch { aaVal = "Unknown"; }
            graphicsSettings["Antialiasing"] = aaVal;

            string bloomVal = "Unknown";
            try
            {
                var bloomComp = FindAnyComponentByNameFragments(new[] { "bloom", "bloomcomponent", "postprocess", "postprocessvolume", "postprocessing" });
                if (bloomComp != null)
                {
                    if (IsComponentEnabled(bloomComp, out string state))
                    {
                        var intensity = TryGetFloatFieldOrProperty(bloomComp, new[] { "intensity", "threshold", "strength", "weight", "amount" }, float.NaN);
                        bloomVal = state;
                        if (!float.IsNaN(intensity)) bloomVal += $" (Strength: {intensity:F2})";
                    }
                    else
                    {
                        bloomVal = "OFF";
                    }
                }
                else
                {
                    bloomVal = TryInspectPostProcessFor("bloom");
                }
            }
            catch { bloomVal = "Unknown"; }
            graphicsSettings["Bloom"] = bloomVal;

            string sunshaftVal = "Unknown";
            try
            {
                var ssComp = FindAnyComponentByNameFragments(new[] { "sunshaft", "sunshafts", "godrays", "volumetric", "shafts" });
                if (ssComp != null)
                {
                    if (IsComponentEnabled(ssComp, out string state))
                        sunshaftVal = state;
                    else
                        sunshaftVal = "OFF";
                }
                else
                {
                    sunshaftVal = TryInspectPostProcessFor("sunshaft");
                }
            }
            catch { sunshaftVal = "Unknown"; }
            graphicsSettings["Sunshafts"] = sunshaftVal;

            string contrastVal = "Unknown";
            try
            {
                var contrastComp = FindAnyComponentByNameFragments(new[] { "contrastenhance", "contrast", "contrast_enhance" });
                if (contrastComp != null)
                {
                    if (IsComponentEnabled(contrastComp, out string state))
                    {
                        contrastVal = state;
                        var amount = TryGetFloatFieldOrProperty(contrastComp, new[] { "amount", "intensity", "strength", "contrast" }, float.NaN);
                        if (!float.IsNaN(amount)) contrastVal += $" ({amount:F2})";
                    }
                    else contrastVal = "OFF";
                }
                else
                {
                    contrastVal = TryInspectPostProcessFor("contrast");
                }
            }
            catch { contrastVal = "Unknown"; }
            graphicsSettings["Contrast Enchance"] = contrastVal;

            string hdrVal = "Unknown";
            try
            {
                if (!mainCam) mainCam = Camera.main;
                if (mainCam != null)
                {
                    var t = mainCam.GetType();
                    var prop = t.GetProperty("allowHDR", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop != null)
                    {
                        var get = prop.GetGetMethod();
                        if (get != null)
                        {
                            var v = get.Invoke(mainCam, null);
                            if (v is bool vb) hdrVal = vb ? "ON" : "OFF";
                            else if (v != null) hdrVal = v.ToString();
                        }
                    }
                    else
                    {
                        var prop2 = t.GetProperty("hdr", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (prop2 != null)
                        {
                            var get2 = prop2.GetGetMethod();
                            if (get2 != null)
                            {
                                var v2 = get2.Invoke(mainCam, null);
                                if (v2 is bool vb2) hdrVal = vb2 ? "ON" : "OFF";
                                else if (v2 != null) hdrVal = v2.ToString();
                            }
                        }
                        else
                        {
                            hdrVal = TryInspectPostProcessFor("hdr");
                        }
                    }
                }
            }
            catch { hdrVal = "Unknown"; }
            graphicsSettings["HDR"] = hdrVal;

            string sunShadowsVal = "Unknown";
            try
            {
                var suns = GameObject.FindObjectsOfType<Light>().Where(l => l != null && l.type == LightType.Directional).ToArray();
                if (suns.Length > 0)
                {
                    var sun = suns[0];
                    sunShadowsVal = (sun.shadows != LightShadows.None) ? "ON" : "OFF";
                }
                else sunShadowsVal = "Unknown";
            }
            catch { sunShadowsVal = "Unknown"; }
            graphicsSettings["Sun Shadows"] = sunShadowsVal;
        }

        private Component FindAnyComponentByNameFragments(string[] fragments)
        {
            if (cachedObjects == null) cachedObjects = GameObject.FindObjectsOfType<GameObject>();
            foreach (var go in cachedObjects)
            {
                if (go == null) continue;
                var comps = go.GetComponents<Component>();
                if (comps == null) continue;
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    string tn = c.GetType().Name.ToLower();
                    foreach (var f in fragments)
                    {
                        if (tn.Contains(f.ToLower()))
                            return c;
                    }
                }
            }
            return null;
        }

        private bool IsComponentEnabled(Component comp, out string state)
        {
            state = "Unknown";
            if (comp == null) return false;
            try
            {
                var t = comp.GetType();
                var enabledProp = t.GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (enabledProp != null)
                {
                    var get = enabledProp.GetGetMethod();
                    if (get != null)
                    {
                        var v = get.Invoke(comp, null);
                        if (v is bool b)
                        {
                            state = b ? "ON" : "OFF";
                            return b;
                        }
                    }
                }

                var go = comp as Component;
                if (go != null)
                {
                    bool active = go.gameObject.activeInHierarchy;
                    state = active ? "ON" : "OFF";
                    return active;
                }
            }
            catch { }
            return false;
        }

        private float TryGetFloatFieldOrProperty(object obj, string[] names, float fallback)
        {
            if (obj == null) return fallback;
            var t = obj.GetType();
            foreach (var n in names)
            {
                try
                {
                    var prop = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop != null)
                    {
                        var get = prop.GetGetMethod();
                        if (get != null)
                        {
                            var v = get.Invoke(obj, null);
                            if (v is float f) return f;
                            if (v is double d) return (float)d;
                            if (v is int i) return (float)i;
                            if (float.TryParse(v?.ToString(), out float parsed)) return parsed;
                        }
                    }
                    var field = t.GetField(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (field != null)
                    {
                        var v = field.GetValue(obj);
                        if (v is float f2) return f2;
                        if (v is double d2) return (float)d2;
                        if (v is int i2) return (float)i2;
                        if (float.TryParse(v?.ToString(), out float parsed2)) return parsed2;
                    }
                }
                catch { }
            }
            return fallback;
        }

        private string TryInspectPostProcessFor(string keyword)
        {
            try
            {
                var vols = GameObject.FindObjectsOfType<Component>().Where(c => c != null && c.GetType().Name.ToLower().Contains("postprocess")).ToArray();
                foreach (var v in vols)
                {
                    var t = v.GetType();
                    object profile = null;
                    var prop = t.GetProperty("profile", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop != null)
                    {
                        var get = prop.GetGetMethod();
                        if (get != null)
                        {
                            try { profile = get.Invoke(v, null); } catch { }
                        }
                    }
                    var field = t.GetField("profile", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (field != null)
                    {
                        try { profile = field.GetValue(v); } catch { }
                    }

                    if (profile != null)
                    {
                        var pt = profile.GetType();
                        foreach (var pf in pt.GetFields(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (pf.Name.ToLower().Contains(keyword.ToLower()))
                            {
                                var val = pf.GetValue(profile);
                                if (val is bool b) return b ? "ON" : "OFF";
                                if (val is float f) return $"{f:F2}";
                                if (val != null) return val.ToString();
                            }
                        }
                        foreach (var pp in pt.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (pp.Name.ToLower().Contains(keyword.ToLower()))
                            {
                                var get = pp.GetGetMethod();
                                if (get != null)
                                {
                                    var val = get.Invoke(profile, null);
                                    if (val is bool b) return b ? "ON" : "OFF";
                                    if (val is float f) return $"{f:F2}";
                                    if (val != null) return val.ToString();
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return "Unknown";
        }
        private string TryInspectAnyComponentForKeywords(string[] keywords)
        {
            try
            {
                var comps = GameObject.FindObjectsOfType<Component>();
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    var tn = c.GetType().Name.ToLower();

                    foreach (var k in keywords)
                    {
                        if (tn.Contains(k.ToLower()))
                        {
                            if (IsComponentEnabled(c, out string state))
                            {
                                var val = InspectObjectForKeywords(c, keywords);
                                if (!string.IsNullOrEmpty(val))
                                    return val.StartsWith("ON") || val.StartsWith("OFF") ? val : $"ON ({val})";
                                return state;
                            }
                            else
                            {
                                return "OFF";
                            }
                        }
                    }

                    var inspected = InspectObjectForKeywords(c, keywords);
                    if (!string.IsNullOrEmpty(inspected))
                    {
                        return inspected;
                    }
                }
            }
            catch { }
            return null;
        }

        private string InspectObjectForKeywords(object obj, string[] keywords)
        {
            if (obj == null) return null;
            var t = obj.GetType();

            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    string name = prop.Name.ToLower();
                    foreach (var k in keywords)
                    {
                        if (name.Contains(k.ToLower()))
                        {
                            var get = prop.GetGetMethod();
                            if (get == null) continue;
                            var v = get.Invoke(obj, null);
                            if (v is bool b) return b ? "ON" : "OFF";
                            if (v is Enum) return v.ToString();
                            if (v is float f) return $"{f:F2}";
                            if (v != null) return v.ToString();
                        }
                    }
                }
                catch { }
            }

            foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    string name = field.Name.ToLower();
                    foreach (var k in keywords)
                    {
                        if (name.Contains(k.ToLower()))
                        {
                            var v = field.GetValue(obj);
                            if (v is bool b) return b ? "ON" : "OFF";
                            if (v is Enum) return v.ToString();
                            if (v is float f) return $"{f:F2}";
                            if (v != null) return v.ToString();
                        }
                    }
                }
                catch { }
            }

            return null;
        }
    }
}