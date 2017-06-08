// <copyright file="ControllerConfig.cs" company="Ensage">
//    Copyright (c) 2017 Ensage.
// </copyright>

namespace ControllerSharp
{
    using System;
    using System.Text.RegularExpressions;

    using Ensage.Common.Menu;
    using Ensage.SDK.Menu;

    internal class ControllerConfig : IDisposable
    {
        private readonly MenuItem<Slider> deadzoneItem;

        private readonly MenuFactory factory;

        private readonly MenuItem<StringList> selectedControllerItem;

        private readonly MenuItem<Slider> vibrationItem;

        private bool disposed;

        public ControllerConfig()
        {
            this.factory = MenuFactory.Create("ControllerSharp");

            this.selectedControllerItem = this.factory.Item("Controller", new StringList("Controller 1", "Controller 2", "Controller 3", "Controller 4"));
            this.selectedControllerItem.Item.ValueChanged += this.ControllerChanged;

            this.vibrationItem = this.factory.Item("Vibration Intensity", new Slider(25));
            this.deadzoneItem = this.factory.Item("Deadzone", new Slider(33));
        }

        public event EventHandler<int> ControllerIndexChanged;

        public int ControllerIndex => this.GetIndexByName(this.selectedControllerItem);

        public float Deadzone => (float)this.deadzoneItem / this.deadzoneItem.Value.MaxValue;

        public float VibrationIntensity => (float)this.vibrationItem / this.vibrationItem.Value.MaxValue;

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.factory.Dispose();
            }

            this.disposed = true;
        }

        private void ControllerChanged(object sender, OnValueChangeEventArgs e)
        {
            var controllerName = e.GetNewValue<StringList>().SelectedValue;
            this.ControllerIndexChanged?.Invoke(this, this.GetIndexByName(controllerName));
        }

        private int GetIndexByName(string name)
        {
            return int.Parse(Regex.Match(name, @"\d+$").Value) - 1;
        }
    }
}