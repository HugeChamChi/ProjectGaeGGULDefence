// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("khEfECCSERoSkhEREL64p/m0gWiAP/FgRvq+qEdWTRTmNFSaVyLX5aQ0fYvBeiVppD2+MZIwVqzLTUWKCjh8scbyJOHPg5AvNZ0lGSF2XF288EH/rbqO6zp6OZgBx6jsbZfkdC4JomEgevWl8/RSPM4KAqyKt8j47+6ghbfEWwjuB45VnnKPrO+eEiQ7AyT1boaoiF8ews7uJm3MGBVWIB6od9vb6z5u6wBIgOucU/w0nw+fIfZPkapHjsvf5fPfUaILFYAgcXXJ4O1cQTquy8aptS8thM6WjlD40iCSETIgHRYZOpZYlucdERERFRATEbvkKhh0edYydo7ISxNSJe4l1Xw6hIkg6u3YObvC/5sj3ut/i8K5dykDBfVqRyxavxITERAR");
        private static int[] order = new int[] { 1,4,11,7,6,8,11,7,10,9,10,11,12,13,14 };
        private static int key = 16;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
