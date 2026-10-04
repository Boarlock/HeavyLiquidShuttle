using RimUIText = RimUI.Elements.Text;
using RimUI.Adapter;
using RimUI.Components;
using RimUI.Elements;
using RimUI.Layout;
using RimWorld;
using Verse;

namespace HeavyLiquidShuttleMod
{
    public class ITab_HeavyLiquidShuttle : ITab
    {
        public HeavyLiquidShuttle Shuttle => SelThing.TryGetComp<HeavyLiquidShuttle>();
        private ShuttleExplosion Comp => Shuttle.parent.TryGetComp<ShuttleExplosion>();
        public override bool IsVisible => SelThing.TryGetComp<HeavyLiquidShuttle>() != null;

        public ITab_HeavyLiquidShuttle()
        {
            labelKey = "TabHeavyLiquidShuttle";

            TankA = Shuttle.TankA!;
            TankB = Shuttle.TankB!;
        }

        static ITab_HeavyLiquidShuttle()
        {
            headerText = new RimUIText("Heavy Liquid Shuttle");

            tankATitle = new RimUIText("Tank A");
            tankBTitle = new RimUIText("Tank B");
        }

        public override void TabTick()
        {
            if (!StillValid)
                return;

            if (!Shuttle.stateUpdated)
                return;

            int radius = Comp.ExplosionRadius;
            float tankAMassFloat;
            float tankBMassFloat;

            tankARatio = TankA.Storage / TankA.Capacity;
            tankBRatio = TankB.Storage / TankB.Capacity;

            tankAContent = new RimUIText($"{TankA.Storage} / {TankA.Capacity} Liters");
            tankBContent = new RimUIText($"{TankB.Storage} / {TankB.Capacity} Liters");

            if (TankA.Content != null)
                tankAMassFloat = TankA.Storage * TankA.Content.density;
            else
                tankAMassFloat = 0f;
            tankAMass = new RimUIText($"Mass: {tankAMassFloat} kg");

            if (TankB.Content != null)
                tankBMassFloat = TankB.Storage * TankB.Content.density;
            else
                tankBMassFloat = 0f;
            tankBMass = new RimUIText($"Mass: {tankBMassFloat} kg");

            if (TankA.isLocked)
                tankALocked = new RimUIText("State: Locked");
            else tankALocked = new RimUIText("State: Unlocked");

            if (TankB.isLocked)
                tankBLocked = new RimUIText("State: Locked");
            else tankBLocked = new RimUIText("State: Unlocked");

            tankANetSupply = (TankA.lastNetSupply == null) ? new RimUIText("N/A") : new RimUIText($"Egress: {TankA.lastNetSupply}");
            tankANetReceive = (TankA.lastNetReceive == null) ? new RimUIText("N/A") : new RimUIText($"Ingress: {TankA.lastNetReceive}");

            tankBNetSupply = (TankB.lastNetSupply == null) ? new RimUIText("N/A") : new RimUIText($"Egress: {TankB.lastNetSupply}");
            tankBNetReceive = (TankB.lastNetReceive == null) ? new RimUIText("N/A") : new RimUIText($"Ingress: {TankB.lastNetReceive}");

            switch (TankA.tankStatus)
            {
                case HeavyLiquidShuttle.CachedReceiving:
                    tankATransfer = new RimUIText("Flow: Receiving");
                    break;
                case HeavyLiquidShuttle.CachedDischarging:
                    tankATransfer = new RimUIText("Flow: Discharging");
                    break;
                default:
                    tankATransfer = new RimUIText("Flow: Holding");
                    break;
            }

            switch (TankB.tankStatus)
            {
                case HeavyLiquidShuttle.CachedReceiving:
                    tankBTransfer = new RimUIText("Flow: Receiving");
                    break;
                case HeavyLiquidShuttle.CachedDischarging:
                    tankBTransfer = new RimUIText("Flow: Discharging");
                    break;
                default:
                    tankBTransfer = new RimUIText("Flow: Holding");
                    break;
            }

            totalMass = new RimUIText($"Total Cargo Mass: {tankAMassFloat + tankBMassFloat}");

            if (radius > 15)
                explosiveRisk = new RimUIText($"[!] MASSIVE EXPLOSIVE RISK | Radius: {radius}");
            else if (radius >= 9)
                explosiveRisk = new RimUIText($"[!] EXTREME EXPLOSIVE RISK | Radius: {radius}");
            else if (radius >= 5)
                explosiveRisk = new RimUIText($"[!] HIGH EXPLOSIVE RISK | Radius: {radius}");
            else if (radius >= 2)
                explosiveRisk = new RimUIText($"Moderate Explosive Risk | Radius: {radius}");
            else if (radius > 0)
                explosiveRisk = new RimUIText($"Small Explosive Risk | Radius: {radius}");
            else
                explosiveRisk = new RimUIText("No Explosive Risk");

            Initialized = true;
            Shuttle.stateUpdated = false;
        }

        private bool Initialized = false;

        private TankState TankA;
        private TankState TankB;

        private static RimUIText? headerText;
        private static RimUIText? tankATitle;
        private static RimUIText? tankBTitle;
        private float tankARatio;
        private float tankBRatio;
        private RimUIText? tankAContent;
        private RimUIText? tankBContent;
        private RimUIText? tankAMass;
        private RimUIText? tankBMass;
        private RimUIText? tankALocked;
        private RimUIText? tankBLocked;
        private RimUIText? tankATransfer;
        private RimUIText? tankBTransfer;
        private RimUIText? tankANetSupply;
        private RimUIText? tankBNetSupply;
        private RimUIText? tankANetReceive;
        private RimUIText? tankBNetReceive;

        private RimUIText? totalMass;
        private RimUIText? explosiveRisk;

        protected override void FillTab()
        {
            if (!Initialized)
                return;

            var grid = new Grid();
            // Row 1
            grid.Cell(
                12, 
                new Field().Add(
                    new Field().Add(
                        headerText)).Add(
                    new Divider()));
            // Row 2
            grid.Cell(
                6,
                new Field().Add(
                    tankATitle).Add(
                    new ProgressBar(() => tankARatio)).Add(
                    tankAContent).Add(
                    tankAMass).Add(
                    tankALocked).Add(
                    tankANetSupply).Add(
                    tankANetReceive
                    ));
            grid.Cell(
                6,
                new Field().Add(
                    tankBTitle).Add(
                    new ProgressBar(() => tankBRatio)).Add(
                    tankBContent).Add(
                    tankBMass).Add(
                    tankBLocked).Add(
                    tankBNetSupply).Add(
                    tankBNetReceive
                    ));
            // Row 3
            grid.Cell(
                12, 
                new Divider());
            // Row 4
            grid.Cell(
                6, 
                new Field().Add(
                    totalMass));
            grid.Cell(
                6, 
                new Field().Add(
                    explosiveRisk));

            new UiWindow(grid).Show();
        }
    }
}
