using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using MaplesEcho.Services;

namespace MaplesEcho.Windows;

public sealed class WelcomeWindow : Window
{
    private static readonly Vector4 Plum = new(0.105f, 0.075f, 0.12f, 1f);
    private static readonly Vector4 Cream = new(0.98f, 0.94f, 0.87f, 1f);
    private static readonly Vector4 Rose = new(0.94f, 0.68f, 0.70f, 1f);
    private static readonly Vector4 Muted = new(0.80f, 0.75f, 0.79f, 1f);
    private static readonly Vector4 Card = new(0.18f, 0.135f, 0.20f, 1f);
    internal const string MascotResource = "MaplesEcho.Assets.maple-viera.png";
    private readonly ITextureProvider textures;
    private readonly Configuration config;
    private readonly Action save;
    private readonly Action openSettings;
    private readonly Func<bool> canShow;

    public WelcomeWindow(Configuration config, Action save, Action openSettings, Func<bool> canShow, ITextureProvider textures)
        : base("Welcome to Maple's Echo##MaplesEchoWelcome")
    {
        this.textures = textures;
        this.config = config;
        this.save = save;
        this.openSettings = openSettings;
        this.canShow = canShow;
        Flags = ImGuiWindowFlags.NoFocusOnAppearing;
        Size = new Vector2(560, 780);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 320),
            MaximumSize = new Vector2(1200, 1000),
        };
        IsOpen = ReleaseNotes.ShouldShow(config.LastSeenWelcomeVersion, ReleaseNotes.CurrentVersion);
    }

    // Defer the window until it can be read safely. Other plugin windows keep
    // their existing combat/cutscene behavior.
    public override bool DrawConditions()
        => canShow();

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Plum);
        ImGui.PushStyleColor(ImGuiCol.Text, Cream);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, Plum);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Plum);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.34f, 0.21f, 0.30f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.43f, 0.27f, 0.37f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.29f, 0.17f, 0.26f, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 16f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(24, 20));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(16, 10));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(12, 12));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(5);
        ImGui.PopStyleColor(7);
    }

    public override void Draw()
    {
        var scale = ImGui.GetFontSize() / 17f;
        DrawMascot(scale);
        ImGui.TextColored(Rose, $"MAPLE'S ECHO  /  {ReleaseNotes.DisplayVersion}");
        ImGui.TextWrapped("A little update from Maple");
        ImGui.PushStyleColor(ImGuiCol.Text, Muted);
        ImGui.TextWrapped("A few small comforts for your next adventure.");
        ImGui.PopStyleColor();
        ImGui.Spacing();
        foreach (var change in ReleaseNotes.Changes)
            DrawCard(change, scale);
        ImGui.Spacing();
        ImGui.TextWrapped("New here? Open settings to connect your Discord bot and choose a channel.");
        if (ImGui.Button("Lovely, let's go"))
            IsOpen = false;
        // Stack actions on narrower windows instead of clipping the second one.
        if (ImGui.GetContentRegionAvail().X > 380f * scale)
            ImGui.SameLine();
        if (ImGui.Button("Open settings"))
        {
            openSettings();
            IsOpen = false;
        }
        ImGui.PushStyleColor(ImGuiCol.Text, Muted);
        ImGui.TextWrapped("Always here: Settings > Welcome & changelog, or /mapleecho news.");
        ImGui.PopStyleColor();
    }

    private static void DrawCard(ReleaseNotes.Change change, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padding = 16f * scale;
        var textWidth = MathF.Max(1, width - padding * 2);
        var titleSize = ImGui.CalcTextSize(change.Title, false, textWidth);
        var bodySize = ImGui.CalcTextSize(change.Description, false, textWidth);
        var height = padding * 2 + titleSize.Y + 6f * scale + bodySize.Y;
        ImGui.GetWindowDrawList().AddRectFilled(origin, origin + new Vector2(width, height),
            ImGui.ColorConvertFloat4ToU32(Card), 12f * scale);
        var local = ImGui.GetCursorPos();
        ImGui.SetCursorPos(local + new Vector2(padding, padding));
        ImGui.PushTextWrapPos(local.X + width - padding);
        ImGui.PushStyleColor(ImGuiCol.Text, Rose);
        ImGui.TextWrapped(change.Title);
        ImGui.PopStyleColor();
        ImGui.SetCursorPosY(local.Y + padding + titleSize.Y + 6f * scale);
        ImGui.TextWrapped(change.Description);
        ImGui.PopTextWrapPos();
        ImGui.SetCursorPos(local);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawMascot(float scale)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var start = ImGui.GetCursorScreenPos();
        // Shared textures are loaded asynchronously and owned by Dalamud. Never
        // retain or dispose a frame's wrap; the rest of the welcome stays usable
        // while the image is loading (or if the texture cannot be decoded).
        var texture = textures.GetFromManifestResource(typeof(WelcomeWindow).Assembly, MascotResource);
        if (texture.TryGetWrap(out var wrap, out _))
        {
            var height = 120f * scale;
            var size = new Vector2(height * wrap.Width / wrap.Height, height);
            if (size.X > width)
                size *= width / size.X;
            var position = start + new Vector2((width - size.X) / 2f, 0);
            ImGui.GetWindowDrawList().AddImage(wrap.Handle, position, position + size);
        }
        ImGui.Dummy(new Vector2(width, 128f * scale));
    }

    public override void OnClose()
    {
        if (!ReleaseNotes.ShouldShow(config.LastSeenWelcomeVersion, ReleaseNotes.CurrentVersion))
            return;
        config.LastSeenWelcomeVersion = ReleaseNotes.CurrentVersion.ToString();
        save();
    }
}
