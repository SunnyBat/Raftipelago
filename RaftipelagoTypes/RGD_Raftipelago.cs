using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RaftipelagoTypes
{
    [Serializable]
    public class RGD_Raftipelago : RGD
    {
        public Dictionary<long, int> Raftipelago_PlayerCurrentItemIndeces;

        public RGD_Raftipelago() : base(RGDType.None, RGDSortingOrder.First)
        {
        }

        public RGD_Raftipelago(Dictionary<long, int> playerCurrentItemIndeces) : base(RGDType.None, RGDSortingOrder.TextWriterObjects)
        {
            Raftipelago_PlayerCurrentItemIndeces = playerCurrentItemIndeces;
        }

        public RGD_Raftipelago(SerializationInfo info, StreamingContext sc) : base(info, sc)
        {
            try
            {
                Raftipelago_PlayerCurrentItemIndeces = (Dictionary<long, int>)(info.GetValue("Raftipelago_PlayerCurrentItemIndeces", typeof(Dictionary<long, int>)) ?? new Dictionary<long, int>());
            }
            catch (Exception) { } // Raftipelago_PlayerCurrentItemIndeces will default to null, signaling that this is not a Raftipelago world (we could use a flag instead)
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext sc)
        {
            base.GetObjectData(info, sc);
            info.AddValue("Raftipelago_PlayerCurrentItemIndeces", Raftipelago_PlayerCurrentItemIndeces);
        }

        [OnDeserializing]
        protected override void SetDefaults(StreamingContext sc)
        {
            base.SetDefaults(sc);
            Raftipelago_PlayerCurrentItemIndeces = new Dictionary<long, int>();
        }
    }
}
