using MSCLoader;
using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace SnowyDiagnostics
{
    public class SnowyDiagnostics : Mod
    {
        public override string ID => "SnowyDiagnostics";
        public override string Name => "Snowy Diagnostics";
        public override string Author => "Awaken Developer";
        public override string Version => "1.0.0";
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
        private float gpuUsage;
        private TimeSpan lastCpu;
        private float cpuTimer;

        private string cpuName;
        private string gpuName;
        private string systemName;
        private string systemVersion;
        private string msclVersion;
        private int loadedMods;

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, OnLoad);
            SetupFunction(Setup.Update, OnUpdate);
            SetupFunction(Setup.OnGUI, OnGUI);
        }

        private void OnLoad()
        {

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
                cachedObjects = GameObject.FindObjectsOfType<GameObject>();
                loadedObjects = cachedObjects.Length;
            }

            objectTimer += Time.deltaTime;
            if (objectTimer >= 0.5f)
            {
                if (!mainCam) mainCam = Camera.main;
                visibleObjects = 0;

                if (mainCam)
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
            float height = showDetails ? 525f : 200f;
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
                GUI.DrawTexture(new Rect(10, y, windowRect.width - 20, 1), borderTexture);
                y += 10;

                DrawLine(ref y, "FRAMETIME", $"{frametimeMs:F2} ms");
                DrawLine(ref y, "CURRENT SCENE", sceneName);
                DrawLine(ref y, "UNITY VERSION", unityVersion);
                DrawLine(ref y, "OBJECTS LOADED", loadedObjects.ToString());
                DrawLine(ref y, "OBJECTS VISIBLE", visibleObjects.ToString());

                GUI.DrawTexture(new Rect(10, y + 4, windowRect.width - 20, 1), borderTexture);
                y += 14;

                DrawLine(ref y, "SYSTEM", systemName);
                DrawLine(ref y, "SYSTEM VERSION", systemVersion);
                DrawLine(ref y, "CPU", cpuName);
                DrawLine(ref y, "GPU", gpuName);
                DrawLine(ref y, "CPU USAGE", $"{cpuUsage:F1}%");

                GUI.DrawTexture(new Rect(10, y + 6, windowRect.width - 20, 1), borderTexture);
                y += 18;

                DrawLine(ref y, "MSCLOADER VERSION", msclVersion.ToString());
                DrawLine(ref y, "LOADED MODS", loadedMods.ToString());

                GUI.DrawTexture(new Rect(10, y + 8, windowRect.width - 20, 1), borderTexture);
            }

            GUI.Label(new Rect(0, height - 30, windowRect.width, 16),
                "<size=10><color=#8899AA>Press F6 to toggle the window</color></size>", labelStyle);

            GUI.DragWindow(new Rect(0, 0, windowRect.width, topBar));
        }

        void DrawLine(ref float y, string n, string v)
        {
            GUI.Label(new Rect(0, y, windowRect.width, 20),
                $"<size=12><b><color=#34d8eb>{n}:</color></b> <b>{v}</b></size>", labelStyle);
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
    }
}
