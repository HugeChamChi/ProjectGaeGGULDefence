using System;

namespace GaeGGUL.IAP
{
    [Serializable]
    public sealed class DiamondPurchaseReceipt
    {
        public string Id;
        public string ProductId;
        public int Diamond;
        /// <summary>Package extras (0 for diamond-only products).</summary>
        public int Gold;
        public int Essence;
        /// <summary>Weekly period key at grant time; used for "주마다 1회" package limits.</summary>
        public string Period;
    }
}
