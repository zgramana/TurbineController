using System;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.Threading;
using GHIElectronics.NETMF.FEZ;
using GHIElectronics.NETMF.Hardware;
using GHIElectronics.NETMF.USBClient;
using System.Text;

namespace TurbineController
{
    public class Program
    {
        private static readonly Pulse leftEngine = new Pulse();
        private static readonly Pulse rightEngine = new Pulse();

        private static Pulse currentEngine;
        private static USBC_Keyboard keyboardDevice;
        private static OutputPort led;
        private static USBC_CDC serialPort;
        private static USBClientController.State state;

        public static void Main()
        {
            Debug.Print(Resources.GetString(Resources.StringResources.String1));
            Debug.Print(Resources.GetString(Resources.StringResources.String1));

            Cpu.GlitchFilterTime = TimeSpan.FromTicks(10000 * 50);

            //serialPort = USBClientController.StandardDevices.StartCDC_WithDebugging();

            //Write("Started CDC");

            state = USBClientController.GetState();

            if (state == USBClientController.State.Running)
            {
                // We're currently debugging.
                keyboardDevice = null;
            }
            else
            {
                keyboardDevice = USBClientController.StandardDevices.StartKeyboard();
            }

            var leftAnalogEngine = new AnalogIn((AnalogIn.Pin)GHIElectronics.NETMF.FEZ.FEZ_Pin.AnalogIn.An5);

            var leftEngine = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di11, false, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeBoth);
            leftEngine.OnInterrupt += collector_OnInterrupt;
            
            var rightEngine = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di12, false, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeBoth);
            rightEngine.OnInterrupt += collector_OnInterrupt;

            var stopButton = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di37, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
            stopButton.OnInterrupt += stopButton_OnInterrupt;
            
            var singlePlayerButton = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di36, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
            singlePlayerButton.OnInterrupt += stopButton_OnInterrupt;

            led = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.LED, false);
            led.Write(false);

            while (true)
            {
                // We can do other work here, like updating
                // an LCD display or something.
                Thread.Sleep(5000);
                //Debug.Print(leftAnalogEngine.Read().ToString());
            }
        }

        private static void Write(string p)
        {
            var bytes = Encoding.UTF8.GetBytes(p);
            //serialPort.Write(bytes, 0, bytes.Length);
        }

        static void stopButton_OnInterrupt(uint data1, uint data2, DateTime time)
        {
            Debug.Print(data1.ToString() + " " + data2.ToString());
            led.Write(!led.Read());

            if (keyboardDevice == null)
            {
                return;
            }

            state = USBClientController.GetState();

            if (state != USBClientController.State.Stopped && state != USBClientController.State.Suspended)
            {
                //Write("Attempting to write a B.");
                if (data1 == 3)
                {
                    keyboardDevice.KeyTap(USBC_Key.B);
                }
                else if (data1 == 8)
                {
                    keyboardDevice.KeyTap(USBC_Key.S);
                }
            }
        }

        static void collector_OnInterrupt(uint data1, uint data2, DateTime time)
        {
            // Right Engine: data1 == 41
            // Left Engine: data1 == 40

            if (data1 == 41)
            {
                currentEngine = leftEngine;
            }
            else if (data1 == 40)
            {
                currentEngine = rightEngine;
            }
            else
            {
                Debug.Print("Unknown engine: " + data1);
                return;
            }

            if (data2 == 0) // Edge going low, start of pulse.
            {
                currentEngine.sample = time;
            }
            else            // Edge going high, end of pulse.
            {
                currentEngine.lastSample = currentEngine.sample;
                currentEngine.sample = time;
                currentEngine.pulseCodeComplete = !currentEngine.pulseCodeComplete;

                if (!currentEngine.pulseCodeComplete)
                {
                    currentEngine.interval = (currentEngine.sample - currentEngine.lastSample).Ticks / 10000;
                }
                else
                {
                    currentEngine.lastInterval = currentEngine.interval;
                    currentEngine.interval = (currentEngine.sample - currentEngine.lastSample).Ticks / 10000;

                    if (USBClientController.GetState() == USBClientController.State.Running)
                    {
                        if (currentEngine == leftEngine)
                        {
                            Debug.Print("x");
                            keyboardDevice.KeyTap(USBC_Key.X);
                        }
                        else
                        {
                            Debug.Print(".");
                            keyboardDevice.KeyTap(USBC_Key.Period);
                        }
                    }

                    Debug.Print(currentEngine.lastInterval + "/" + currentEngine.interval + ", rotation direction: " + (currentEngine.IsForward ? "+" : "-"));

                    // Reset timers.
                    currentEngine.lastInterval = 0;
                    currentEngine.interval = 0;
                    currentEngine.sample = DateTime.MinValue;
                    currentEngine.lastSample = DateTime.MinValue;
                }
            }
        }

        class Pulse
        {
            public DateTime sample;
            public DateTime lastSample;
            public long interval;
            public long lastInterval;
            public bool pulseCodeComplete;

            public bool IsForward { get { return currentEngine.interval > currentEngine.lastInterval; } }

        }
    }
}
