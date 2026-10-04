using HarmonyLib;
using RimUI.Adapter;
using RimUI.Components;
using RimUI.Core;
using RimUI.Elements;
using RimUI.Layout;
using RimUI.Rendering;
using RimUI.Styling;
using RimWorld;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using RimUIText = RimUI.Elements.Text;

namespace HeavyLiquidShuttleMod
{
    [DefOf]
    public class InfoCardTankStatus
    {
        public static StatDef? TankStatus;
    }

    public class StatWorker_TankStatus : StatWorker
    {
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
        {
            HeavyLiquidShuttle comp = req.Thing.TryGetComp<HeavyLiquidShuttle>();

            return comp.TankA!.Storage + comp.TankB!.Storage;
        }

        public override bool ShouldShowFor(StatRequest req)
        {
            return req.Thing?.TryGetComp<HeavyLiquidShuttle>() != null;
        }
    }

    [HarmonyPatch(typeof(StatsReportUtility), "DrawStatsWorker")]
    public class DrawStatsWorkerPatch
    {
        private static readonly FieldInfo selectedEntryField = typeof(StatsReportUtility).GetField("selectedEntry", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo mouseOverEntryField = typeof(StatsReportUtility).GetField("mousedOverEntry", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo cachedDrawEntriesField = typeof(StatsReportUtility).GetField("cachedDrawEntries", BindingFlags.Static | BindingFlags.NonPublic);
        private static bool GetSelectedEntryForTankStatus()
        {
            StatDrawEntry? selectedEntry = selectedEntryField?.GetValue(null) as StatDrawEntry;

            StatDrawEntry? mousedOverEntry = mouseOverEntryField?.GetValue(null) as StatDrawEntry;

            List<StatDrawEntry>? cachedDrawEntries = cachedDrawEntriesField?.GetValue(null) as List<StatDrawEntry>;

            StatDrawEntry? statDrawEntry = selectedEntry ?? mousedOverEntry ?? cachedDrawEntries?.FirstOrDefault();

            return statDrawEntry?.stat == InfoCardTankStatus.TankStatus;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction>instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                // stloc.s 18 = i (rect6)
                if (codes[i].opcode == OpCodes.Stloc_S &&
                    codes[i].operand is LocalBuilder local &&
                    local.LocalIndex == 18)
                {
                    // call void Verse.Widgets
                    codes[i + 9] = new CodeInstruction(OpCodes.Call, typeof(DrawStatsWorkerPatch).GetMethod("Helper", BindingFlags.NonPublic | BindingFlags.Static));

                    CodeInstruction ldarg = CodeInstruction.LoadArgument(1);

                    // Stack already has ldloc.s 18, and ldloc.s 16
                    codes.Insert(i + 9, ldarg);
                }
            }

            return codes;
        }

        private static void Helper(Rect originalRect, string explanationText, Thing thing)
        {
            if (GetSelectedEntryForTankStatus() && thing != null)
            {
                HeavyLiquidShuttle thingComp = thing.TryGetComp<HeavyLiquidShuttle>();

                if (thingComp != null)
                {
                    RimUIRenderer.RenderUI(thingComp, originalRect);
                    return;
                }
            }

            Widgets.Label(originalRect, explanationText);
        }
    }

    public class RimUIRenderer
    {
        public RimUIRenderer(HeavyLiquidShuttle thingComp)
        {
            Shuttle = thingComp;
            ExplosionComp = thingComp.parent.TryGetComp<ShuttleExplosion>();
            TransporterComp = thingComp.parent.TryGetComp<CompTransporter>();

            ctx = new LayoutContext(metrics, metrics, uiState);
        }

        static RimUIRenderer()
        {
            headerText = new RimUIText("Heavy Liquid Shuttle");
            headerText.Style.Margin = new Thickness(8f);
            headerText.Ts.Size = Medium;
            headerText.Ts.Weight = Bold;
            headerText.Ts.Align = centerAlign;
            headerText.Ts.VAlign = middleAlign;
            headerText.Ts.Wrap = false;

            tankATitle = new RimUIText("Tank A");
            headerText.Style.Margin = new Thickness(4f);
            headerText.Ts.Size = Medium;
            tankATitle.Ts.Weight = Bold;
            tankATitle.Ts.Align = centerAlign;
            tankATitle.Ts.VAlign = middleAlign;
            tankATitle.Ts.Wrap = false;


            tankBTitle = new RimUIText("Tank B");
            headerText.Style.Margin = new Thickness(4f);
            headerText.Ts.Size = Medium;
            tankBTitle.Ts.Weight = Bold;
            tankBTitle.Ts.Align = centerAlign;
            tankBTitle.Ts.VAlign = middleAlign;
            tankBTitle.Ts.Wrap = false;
        }

        private static readonly FontSize Medium = FontSize.Medium;
        private static readonly FontWeight Bold = FontWeight.Bold;
        private static readonly TextAlign centerAlign = TextAlign.Center;
        private static readonly VerticalAlign middleAlign = VerticalAlign.Middle;

        private static RimUIRenderer? renderer;
        public HeavyLiquidShuttle? Shuttle;
        private ShuttleExplosion ExplosionComp;
        private CompTransporter TransporterComp;

        private readonly RimWorldMetrics metrics = new RimWorldMetrics();
        private readonly UiState uiState = new UiState();
        private readonly DrawList drawList = new DrawList();
        private readonly LayoutContext? ctx;
        private RimWorldRenderer? uiRenderer;

        private TankState TankA => Shuttle!.TankA!;
        private TankState TankB => Shuttle!.TankB!;


        private static RimUIText? headerText;
        private static RimUIText? tankATitle;
        private static RimUIText? tankBTitle;

        private float tankARatio;
        private float tankBRatio;

        private RimUIText? tankARatioText;
        private RimUIText? tankBRatioText;

        private const string content = "Content: ";
        private RimUIText? tankAContent;
        private RimUIText? tankBContent;

        private const string mass = "Mass: ";
        private RimUIText? tankAMass;
        private RimUIText? tankBMass;

        private const string state = "State: ";
        private RimUIText? tankALocked;
        private RimUIText? tankBLocked;

        private const string contam = "Contaminated: ";
        private RimUIText? tankAContaminated;
        private RimUIText? tankBContaminated;

        private const string flow = "Flow: ";
        private RimUIText? tankATransfer;
        private RimUIText? tankBTransfer;

        private const string egress = "Egress: ";
        private RimUIText? tankANetSupply;
        private RimUIText? tankBNetSupply;

        private const string ingress = "Ingress: ";
        private RimUIText? tankANetReceive;
        private RimUIText? tankBNetReceive;

        private const string totalCargoMass = "Total cargo mass: ";
        private RimUIText? totalMass;

        private RimUIText? explosiveRisk;

        private void RenderTick()
        {
            int radius = ExplosionComp.ExplosionRadius;
            float tankAMassFloat;
            float tankBMassFloat;

            tankARatio = TankA.Storage / TankA.Capacity;
            tankBRatio = TankB.Storage / TankB.Capacity;

            tankARatioText = new RimUIText($"{TankA.Storage} / {TankA.Capacity} L");
            tankBRatioText = new RimUIText($"{TankB.Storage} / {TankB.Capacity} L");

            tankARatioText.Ts.Align = centerAlign;
            tankBRatioText.Ts.Align = centerAlign;
            tankARatioText.Style.Margin = new Thickness(4f);
            tankBRatioText.Style.Margin = new Thickness(4f);


            if (TankA.Content == null)
                tankAContent = new RimUIText(content + "Empty");
            else if (TankA.Content == CachedDefs.Water)
                tankAContent = new RimUIText(content + $"{TankA.Content} ({TankA.waterQuality})");
            else
                tankAContent = new RimUIText(content + $"{TankA.Content}");
            if (TankB.Content == null)
                tankBContent = new RimUIText(content + "Empty");
            else if (TankB.Content == CachedDefs.Water)
                tankBContent = new RimUIText(content + $"{TankB.Content} ({TankB.waterQuality})");
            else
                tankBContent = new RimUIText(content + $"{TankB.Content}");

            tankAContent.Ts.VAlign = middleAlign;
            tankBContent.Ts.VAlign = middleAlign;
            tankAContent.Style.Margin = new Thickness(2f);
            tankBContent.Style.Margin = new Thickness(2f);


            tankAMassFloat = (TankA.Content != null) ? TankA.Storage * TankA.Content.density : 0f;
            tankAMass = new RimUIText(mass + $"{tankAMassFloat} kg");
            tankBMassFloat = (TankB.Content != null) ? TankB.Storage * TankB.Content.density : 0f;
            tankBMass = new RimUIText(mass + $"{tankBMassFloat} kg");

            tankAMass.Ts.VAlign = middleAlign;
            tankBMass.Ts.VAlign = middleAlign;
            tankAMass.Style.Margin = new Thickness(2f);
            tankBMass.Style.Margin = new Thickness(2f);


            tankALocked = (TankA.isLocked) ? new RimUIText(state + "Locked") : new RimUIText(state + "Unlocked");
            tankBLocked = (TankB.isLocked) ? new RimUIText(state + "Locked") : new RimUIText(state + "Unlocked");

            tankALocked.Ts.VAlign = middleAlign;
            tankBLocked.Ts.VAlign = middleAlign;
            tankALocked.Style.Margin = new Thickness(2f);
            tankBLocked.Style.Margin = new Thickness(2f);


            tankAContaminated = (TankA.isContaminated) ? new RimUIText(contam + "True") : new RimUIText(contam + "False");
            tankBContaminated = (TankB.isContaminated) ? new RimUIText(contam + "True") : new RimUIText(contam + "False");

            tankAContaminated.Ts.VAlign = middleAlign;
            tankBContaminated.Ts.VAlign = middleAlign;
            tankAContaminated.Style.Margin = new Thickness(2f);
            tankBContaminated.Style.Margin = new Thickness(2f);


            switch (TankA.tankStatus)
            {
                case HeavyLiquidShuttle.CachedReceiving:
                    tankATransfer = new RimUIText(flow + "Receiving");
                    break;
                case HeavyLiquidShuttle.CachedDischarging:
                    tankATransfer = new RimUIText(flow + "Discharging");
                    break;
                default:
                    tankATransfer = new RimUIText(flow + "Holding");
                    break;
            }

            switch (TankB.tankStatus)
            {
                case HeavyLiquidShuttle.CachedReceiving:
                    tankBTransfer = new RimUIText(flow + "Receiving");
                    break;
                case HeavyLiquidShuttle.CachedDischarging:
                    tankBTransfer = new RimUIText(flow + "Discharging");
                    break;
                default:
                    tankBTransfer = new RimUIText(flow + "Holding");
                    break;
            }

            tankATransfer.Ts.VAlign = middleAlign;
            tankBTransfer.Ts.VAlign = middleAlign;
            tankATransfer.Style.Margin = new Thickness(2f);
            tankBTransfer.Style.Margin = new Thickness(2f);


            tankANetSupply = (TankA.lastNetSupply == null) ? new RimUIText(egress + "N/A") : new RimUIText(egress + $"{TankA.lastNetSupply} Network");
            tankANetReceive = (TankA.lastNetReceive == null) ? new RimUIText(ingress + "N/A") : new RimUIText(ingress + $"{TankA.lastNetReceive} Network");

            tankBNetSupply = (TankB.lastNetSupply == null) ? new RimUIText(egress + "N/A") : new RimUIText(egress + $"{TankB.lastNetSupply} Network");
            tankBNetReceive = (TankB.lastNetReceive == null) ? new RimUIText(ingress + "N/A") : new RimUIText(ingress + $"{TankB.lastNetReceive} Network");

            tankANetSupply.Ts.VAlign = middleAlign;
            tankANetReceive.Ts.VAlign = middleAlign;
            tankBNetSupply.Ts.VAlign = middleAlign;
            tankBNetReceive.Ts.VAlign = middleAlign;
            tankANetSupply.Style.Margin = new Thickness(2f);
            tankANetReceive.Style.Margin = new Thickness(2f);
            tankBNetSupply.Style.Margin = new Thickness(2f);
            tankBNetReceive.Style.Margin = new Thickness(2f);


            MassPatch.NotifyLiquidMassChanged(Shuttle!);
            totalMass = new RimUIText(totalCargoMass + $"{tankAMassFloat + tankBMassFloat + TransporterComp.MassUsage}");

            totalMass.Ts.VAlign = middleAlign;
            totalMass.Style.Margin = new Thickness(2f);


            if (radius > 15)
            {
                explosiveRisk = new RimUIText($"[!] MASSIVE EXPLOSIVE RISK | Radius: {radius}");
                explosiveRisk.Ts.Weight = Bold;
            }
            else if (radius >= 9)
            {
                explosiveRisk = new RimUIText($"[!] EXTREME EXPLOSIVE RISK | Radius: {radius}");
                explosiveRisk.Ts.Weight = Bold;
            }
            else if (radius >= 5)
            {
                explosiveRisk = new RimUIText($"[!] HIGH EXPLOSIVE RISK | Radius: {radius}");
                explosiveRisk.Ts.Weight = Bold;
            }
            else if (radius >= 2)
                explosiveRisk = new RimUIText($"Moderate explosive risk | Radius: {radius}");
            else if (radius > 0)
                explosiveRisk = new RimUIText($"Small explosive risk | Radius: {radius}");
            else
                explosiveRisk = new RimUIText("No explosive risk");

            explosiveRisk.Ts.VAlign = middleAlign;
            explosiveRisk.Style.Margin = new Thickness(2f);
        }

        public static void RenderUI(HeavyLiquidShuttle thingComp, Rect originalRect)
        {
            if (renderer == null || renderer.Shuttle != thingComp)
            {
                renderer = new RimUIRenderer(thingComp);
            }

            renderer.RenderTick();

            renderer.uiRenderer ??= new RimWorldRenderer();

            var grid = new Grid();

            ColorRGBA progressBarColor = new ColorRGBA(0.04f, 0.05f, 0.07f, 1f);

            var tankAProgressBar = new ProgressBar(() => renderer.tankARatio) { BarHeight = 24f };
            var tankBProgressBar = new ProgressBar(() => renderer.tankBRatio) { BarHeight = 24f };
            tankAProgressBar.Style.Background = Fill.Solid(progressBarColor);
            tankBProgressBar.Style.Background = Fill.Solid(progressBarColor);

            BorderWidth width = BorderWidth.Middle;
            BorderRadius radius = BorderRadius.Middle;
            ColorRGBA fieldColor = new ColorRGBA(0.16f, 0.25f, 0.33f, 1f);
            ColorRGBA borderColor = new ColorRGBA(0.35f, 0.40f, 0.46f, 1f);

            var headerField = new Field(); 
            headerField.Style.Background = Fill.Solid(fieldColor);
            headerField.Style.Opacity = 0.85f;
            headerField.Style.BorderWidth = width;
            headerField.Style.BorderFill = Fill.Solid(borderColor);
            headerField.Style.Radius = radius;

            var tankAField = new Field();
            tankAField.Style.Background = Fill.Solid(fieldColor);
            tankAField.Style.Opacity = 0.85f;
            tankAField.Style.BorderWidth = width;
            tankAField.Style.BorderFill = Fill.Solid(borderColor);
            tankAField.Style.Radius = radius;

            var tankBField = new Field();
            tankBField.Style.Background = Fill.Solid(fieldColor);
            tankBField.Style.Opacity = 0.85f;
            tankBField.Style.BorderWidth = width;
            tankBField.Style.BorderFill = Fill.Solid(borderColor);
            tankBField.Style.Radius = radius;

            var massField = new Field();
            massField.Style.Background = Fill.Solid(fieldColor);
            massField.Style.Opacity = 0.85f;
            massField.Style.BorderWidth = width;
            massField.Style.BorderFill = Fill.Solid(borderColor);
            massField.Style.Radius = radius;

            var explosiveField = new Field();
            explosiveField.Style.Background = Fill.Solid(fieldColor);
            explosiveField.Style.Opacity = 0.85f;
            explosiveField.Style.BorderWidth = width;
            explosiveField.Style.BorderFill = Fill.Solid(borderColor);
            explosiveField.Style.Radius = radius;

            // Row 1
            grid!.Cell(
                12,
                new Field().Add(
                    headerField.Add(
                        headerText)).Add(
                    new Divider()));
            // Row 2
            grid.Cell(
                6,
                tankAField.Add(
                    tankATitle).Add(
                    tankAProgressBar).Add(
                    renderer.tankARatioText).Add(
                    renderer.tankAContent).Add(
                    renderer.tankAMass).Add(
                    renderer.tankALocked).Add(
                    renderer.tankAContaminated).Add(
                    renderer.tankATransfer).Add(
                    renderer.tankANetSupply).Add(
                    renderer.tankANetReceive).Add(
                    renderer.totalMass
                    ));

            grid.Cell(
                6,
                tankBField.Add(
                    tankBTitle).Add(
                    tankBProgressBar).Add(
                    renderer.tankBRatioText).Add(
                    renderer.tankBContent).Add(
                    renderer.tankBMass).Add(
                    renderer.tankBLocked).Add(
                    renderer.tankBContaminated).Add(
                    renderer.tankBTransfer).Add(
                    renderer.tankBNetSupply).Add(
                    renderer.tankBNetReceive).Add(
                    renderer.explosiveRisk
                    ));

            RectF rectF = UnitConv.ToRectF(originalRect);
            renderer.ctx!.Scale = Prefs.UIScale;
            renderer.ctx.WindowArea = rectF;
            grid.Measure(new RimUI.Core.SizeF(originalRect.width, float.MaxValue), renderer.ctx); // Marked RimUI.Core.SizeF
            grid.Arrange(rectF, renderer.ctx);
            renderer.drawList.Clear();
            grid.EmitStyled(renderer.drawList, renderer.ctx);
            renderer.uiRenderer.Execute(renderer.drawList);
        }
    }
}
