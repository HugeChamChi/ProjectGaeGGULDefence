using System;
using UnityEngine;

namespace Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ButtonAttribute : PropertyAttribute
    {
        public string ButtonName { get; }

        public ButtonAttribute(string buttonName = null)
        {
            ButtonName = buttonName;
        }
    }
}
