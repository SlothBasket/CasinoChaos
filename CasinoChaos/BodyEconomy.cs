namespace GWYF_CasinoChaos
{
    internal enum MachinePart : byte { Eye, Ear, Mouth, Dong, Leg, Body, Butt }
    internal sealed class BodyPartDefinition
    {
        internal readonly MachinePart Id;
        internal readonly string DisplayName, VanillaButton;
        internal readonly int SellValue, BuyValue, MachineOrder;
        internal BodyPartDefinition(MachinePart id, string name, int value, int order, string vanillaButton = null)
        { Id=id; DisplayName=name; SellValue=BuyValue=value; MachineOrder=order; VanillaButton=vanillaButton; }
        internal int Price(bool buying) => buying ? BuyValue : SellValue;
    }
    internal static class BodyEconomy
    {
        // Vanilla uses the same ticket value for buying and selling.
        internal static readonly BodyPartDefinition[] Entries = {
            new BodyPartDefinition(MachinePart.Eye,"EYE",3,0,"EyeButton"),
            new BodyPartDefinition(MachinePart.Ear,"EAR",2,1),
            new BodyPartDefinition(MachinePart.Mouth,"MOUTH",2,2,"MouthButton"),
            new BodyPartDefinition(MachinePart.Dong,"DONG",2,3),
            new BodyPartDefinition(MachinePart.Leg,"LEG",3,4),
            new BodyPartDefinition(MachinePart.Body,"BODY",5,5,"BodyButton"),
            new BodyPartDefinition(MachinePart.Butt,"BUTT",2,6)
        };
        internal static BodyPartDefinition Get(MachinePart id)
        { foreach(var entry in Entries) if(entry.Id==id)return entry; return null; }
        internal static bool TryTransact(BodyState state, MachinePart id, bool buying, bool randomRight,
            System.Func<long,bool> tickets, System.Action<CustomBodyPart,bool> toggle, out CustomBodyPart chosen)
        {
            if(!Choose(state,id,buying,randomRight,out chosen))return false;
            int price=Get(id).Price(buying);
            if(!tickets(buying?-(long)price:price))return false;
            toggle(chosen,buying);return true;
        }
        internal static bool CustomPair(MachinePart id, out CustomBodyPart left, out CustomBodyPart right)
        {
            left=right=CustomBodyPart.Dong;
            if(id==MachinePart.Butt){left=right=CustomBodyPart.Butt;return true;}
            if(id==MachinePart.Ear){left=CustomBodyPart.LeftEar;right=CustomBodyPart.RightEar;return true;}
            if(id==MachinePart.Leg){left=CustomBodyPart.LeftLeg;right=CustomBodyPart.RightLeg;return true;}
            return id==MachinePart.Dong;
        }
        internal static bool Choose(BodyState state, MachinePart id, bool buying, bool randomRight, out CustomBodyPart chosen)
        {
            chosen=default;
            if(!CustomPair(id,out var left,out var right))return false;
            bool l=buying?!state.Has(left):state.Has(left),r=buying?!state.Has(right):state.Has(right);
            chosen=l&&r?(randomRight?right:left):l?left:right;
            return l||r;
        }
    }
}
