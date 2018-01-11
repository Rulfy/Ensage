// <copyright file="ControllerConfig.cs" company="Ensage">
//    Copyright (c) 2018 Ensage.
// </copyright>

namespace ControllerSharp
{
    using Ensage.SDK.Menu;
    using Ensage.SDK.Menu.Items;

    [Menu("ControllerSharp")]
    public class ControllerConfig
    {
        [Item]
        public Selection<string> Controller { get; set; } = new Selection<string>("Controller 1", "Controller 2", "Controller 3", "Controller 4");

        [Item("Vibration intensity")]
        public Slider<float> Vibration { get; set; } = new Slider<float>(25, 0, 100);

        public float VibrationPercentage
        {
            get
            {
                return this.Vibration.Value / this.Vibration.MaxValue;
            }
        }

        [Item]
        public Slider<float> Deadzone { get; set; } = new Slider<float>(25, 0, 100);

        public float DeadzonePercentage
        {
            get
            {
                return this.Deadzone.Value / this.Deadzone.MaxValue;
            }
        }
    }
}