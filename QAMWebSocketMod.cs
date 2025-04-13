using Elements.Core;
using FrooxEngine;
using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QAMWebSocketMod
{
    public class QAMWebSocketMod : ResoniteMod
    {
        public override string Name => "QAMWebSocketMod";
        public override string Author => "Rucio";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/bontebok/QAMWebSocketMod";

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> ENABLED = new("enabled", "Enable mod?", () => true);
        
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<string> ALLOWEDURIS = new("allowedUris", "Allowed URI(s) for WebSockets, separate multiple entries with a comma");

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<dummy> DUMMY = new("dummy", "URI examples: localhost,localhost:8080,ws://127.0.0.1/ws,wss://remotehost.com:12345");

        public static UrlAllowListValidator urlAllowListValidator;
        public static ModConfiguration Config;

        public override void OnEngineInit()
        {
            try
            {
                Config = GetConfiguration();
                Harmony harmony = new($"com.{Author}.{Name}");
                var cacheList = Config.GetValue(ALLOWEDURIS);

                if (String.IsNullOrEmpty(cacheList)) {
                    Msg("allowedUris is empty! Please ensure allowedUris is in the mod's config file.");
                    return;
                }

                urlAllowListValidator = new(cacheList); // Build allow list
                harmony.PatchAll();
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        [HarmonyPatch(typeof(DynamicVariableSpace))]
        public class DynamicVariableSpacePatch
        {
            static readonly Dictionary<string, WebSocketManager> WebSockets = new();

            public class Color
            {
                public byte R { get; set; }
                public byte G { get; set; }
                public byte B { get; set; }
            }

            public abstract class QAMJson
            {
                [JsonPropertyName("type")]
                public string Type { get; set; }
            }
            public class LineUpdate : QAMJson
            {
                [JsonPropertyName("y")]
                public int Line { get; set; }

                [JsonPropertyName("colors")]
                [JsonConverter(typeof(Base64ColorsConverter))]
                public List<Color> Colors { get; set; }
            }

            public class ResolutionUpdate : QAMJson
            {
                [JsonPropertyName("width")]
                public int Width { get; set; }
                [JsonPropertyName("height")]
                public int Height { get; set; }
            }

            // Custom JsonConverter to handle Base64 encoded color
            public class Base64ColorsConverter : JsonConverter<List<Color>>
            {
                public override List<Color> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                {
                    string base64String = reader.GetString();
                    byte[] bytes = Convert.FromBase64String(base64String);
                    var colors = new List<Color>();

                    if (bytes.Length % 3 != 0)
                    {
                        return colors;
                    }

                    for (int i = 0; i < bytes.Length; i += 3)
                    {
                        colors.Add(new Color { R = bytes[i], G = bytes[i + 1], B = bytes[i + 2] });
                    }

                    return colors;
                }

                public override void Write(Utf8JsonWriter writer, List<Color> value, JsonSerializerOptions options)
                {
                }
            }

            private static QuadArrayMesh InitQAM(Slot QAMSlot, int Width, int Height, MeshRenderer QAMMeshRenderer)
            {
                Msg($"Creating new QuadArrayMesh - {Width}, {Height}");
                QAMSlot.GetComponent<QuadArrayMesh>()?.Destroy(); // Destroy last

                var QAM = QAMSlot.AttachComponent<QuadArrayMesh>();

                var size = new float2(1, 1);

                var pixels = Width * Height;

                int offsetX = Width / 2;
                int offsetY = Height / 2;

                QAM.Profile.Value = ColorProfile.sRGB;
                QAM.ColorsProfile.Value = ColorProfile.sRGB;

                QAM.Points.SetSize(pixels);
                QAM.Sizes.SetSize(pixels);
                QAM.Colors.SetSize(pixels);
                int c = 0;

                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int transformedX = x - offsetX;
                        int transformedY = (y - offsetY) * -1; // flip Y

                        QAM.Points[c] = new float3(transformedX, transformedY, 0);
                        QAM.Sizes[c] = size;
                        QAM.Colors[c] = new color(0.5f, 0.5f, 0.5f);
                        c++;
                    }
                }

                QAMMeshRenderer.Mesh.Target = QAM;

                return QAM;

            }

            private static void UpdateQAM(int LineNumber, int Width, List<Color> Colors, QuadArrayMesh QAM)
            {
                var lineoffset = LineNumber * Width;

                foreach (var color in Colors)
                {
                    QAM.Colors[lineoffset] = new color(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f);
                    //QAM.Colors[lineoffset].SetR(color.R / 255.0f);
                    //QAM.Colors[lineoffset].SetG(color.G / 255.0f);
                    //QAM.Colors[lineoffset].SetB(color.B / 255.0f);
                    lineoffset++;
                }
            }

            private static async Task QAMUpdates(Task WebSocketTask, WebSocketManager WSM, Slot QAMSlot, MeshRenderer QAMMeshRenderer)
            {
                QuadArrayMesh QAM = null;
                int Width = 0;
                int Height = 0;
                while (!WebSocketTask.IsCompleted)
                {
                    var messages = WSM.GetAllMessages();
                    if (messages.Count > 0)
                    {
                        foreach (var message in messages)
                        {
                            using (JsonDocument doc = JsonDocument.Parse(message))
                            {
                                JsonElement root = doc.RootElement;
                                string jsontype = root.GetProperty("type").GetString();

                                switch (jsontype)
                                {
                                    case "init":
                                        {
                                            var init = JsonSerializer.Deserialize<ResolutionUpdate>(message);
                                            if (init.Width != Width || init.Height != Height)
                                            {
                                                QAM = InitQAM(QAMSlot, init.Width, init.Height, QAMMeshRenderer);
                                                Width = init.Width;
                                                Height = init.Height;
                                            }
                                            break;
                                        }
                                    case "line":
                                        {
                                            var line = JsonSerializer.Deserialize<LineUpdate>(message);
                                            if (QAM != null) UpdateQAM(line.Line, Width, line.Colors, QAM);
                                            break;
                                        }
                                }

                                //Msg(message);
                            }
                            await new NextUpdate();
                        }
                    }
                    await new NextUpdate();
                }
                Msg("Done");
            }

            [HarmonyPostfix]
            [HarmonyPatch("OnStart")]
            public static void OnAwake_Postfix(DynamicVariableSpace __instance)
            {
                if (__instance.SpaceName == "QAMWebSocket")
                {
                    if (!Config.GetValue(ENABLED)) return; // Return if disabled. Check is probably slower than the string check above?

                    var RootSlot = __instance.Slot.GetObjectRoot();

                    if (RootSlot == null) return;

                    __instance.World.RunInUpdates(3, () =>
                    {
                        // Pull the values for all three (required)
                        if (!__instance.TryReadValue("Uri", out Uri QAMUri)) return;
                        if (!__instance.TryReadValue("Slot", out Slot QAMSlot)) return;
                        if (!__instance.TryReadValue("MeshRenderer", out MeshRenderer QAMMeshRenderer)) return;

                        Msg($"Checking: {QAMUri.ToString()}");

                        if (!urlAllowListValidator.IsUrlAllowed(QAMUri.ToString()))
                        {
                            Msg($"URI not allowed per allowedUris list: {QAMUri}");
                            return;
                        }

                        Msg($"Allowed: {QAMUri.ToString()}");

                        var WSM = new WebSocketManager();
                        WebSockets.Add(RootSlot.ReferenceID.ToString(), WSM);

                        RootSlot.Destroyed += new Action<IDestroyable>(target =>
                        {
                            var destroyingId = target.ReferenceID.ToString();
                            if (WebSockets.ContainsKey(destroyingId))
                            {
                                var WSM = WebSockets[destroyingId];
                                WSM.StopWebSocket();
                                WebSockets.Remove(destroyingId);
                            }
                        });

                        // Start the WebSocket in a separate task
                        var webSocketTask = WSM.StartWebSocketAsync(QAMUri);
                        __instance.StartTask(async () =>
                        {
                            await QAMUpdates(webSocketTask, WSM, QAMSlot, QAMMeshRenderer);
                        });
                    });
                }
            }
        }
    }
}